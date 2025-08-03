using bgdb.Common;
using bgdb.Common.Models;
using bgdb.Common.Repositories;
using ImageMagick;
using Npgsql;
using Serilog;

namespace bgdb.Hasher;

public class Worker : IDisposable, IAsyncDisposable
{
    private readonly ImageAnalyzer _analyzer;
    private readonly NpgsqlDataSource _dataSource;

    public Worker(ImageAnalyzer analyzer, NpgsqlDataSource dataSource)
    {
        _analyzer = analyzer;
        _dataSource = dataSource;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        await ProcessLocalMapsAsync();
        await ConvertMissingBackgroundsAsync();

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ProcessNewMapsetsAsync();
                await Task.Delay(TimeSpan.FromMinutes(10), ct);
            }
            catch (Exception e)
            {
                Log.Error("Failed to get latest mapsets: {exception}", e);
            }
        }
    }

    private async Task ProcessNewMapsetsAsync()
    {
        Log.Information("Checking for new mapsets...");
        
        await using var conn = new DbSession(_dataSource);
        var imageRepository = new ImageRepository(conn);
        await conn.OpenAsync();
        await conn.EnsureCreatedAsync();
        var completedMapsetIds = await imageRepository.GetCompletedMapsetsAsync();
        
        using var apiClient = new OsuApiClient();
        var latestMapsets = await apiClient.GetLatestMapsets();
        
        var mapsetIds = latestMapsets!.Beatmapsets.Select(s => s.Id).ToList();
        var newMapsets = mapsetIds.Except(completedMapsetIds).ToList();
        
        Log.Information("{newMapsetCount} new mapsets found!", newMapsets.Count);

        foreach (var mapsetId in newMapsets)
            await ProcessRemoteMapsetAsync(mapsetId);
    }

    private async Task ProcessLocalMapsAsync()
    {
        await using var conn = new DbSession(_dataSource);
        var imageRepository = new ImageRepository(conn);
        await conn.OpenAsync();
        await conn.EnsureCreatedAsync();
        
        var completedMapsetIds = await imageRepository.GetCompletedMapsetsAsync();
        
        Log.Information("{processedMapsetCount} processed mapsets in database", completedMapsetIds.Count);

        if (!Directory.Exists(Settings.LocalSongsPath))
        {
            Log.Warning("Local songs folder not found. Skipping local map processing...");
            return;
        }
        
        var localOszs = Directory.GetFiles(Settings.LocalSongsPath).Where(f => f.EndsWith(".osz"));
        var localMapsetIds = localOszs.Select(o => int.Parse(Path.GetFileNameWithoutExtension(o))).ToList();
        var mapsetsToProcess = localMapsetIds.Except(completedMapsetIds).Order().ToList();
        
        Log.Information("{localMapsetCount} mapsets found on disk", localMapsetIds.Count);
        Log.Information("{mapsetsToProcessCount} mapsets left to process", mapsetsToProcess.Count);

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = 8
        };

        await Parallel.ForEachAsync(mapsetsToProcess, parallelOptions, async (mapsetId, token) =>
        {
            try
            {
                await ProcessLocalMapsetAsync(mapsetId);
            }
            catch (InvalidDataException e)
            {
                Log.Error("Invalid archive: {mapsetId}. Trying to redownload from mirror...", mapsetId);
                await ProcessRemoteMapsetAsync(mapsetId);
            }
            catch (Exception e)
            {
                Log.Error("Error processing mapset {mapsetId}: {exception}", mapsetId, e.ToString());
            }
        });
    }

    private async Task ProcessRemoteMapsetAsync(int mapsetId)
    {
        await using var oszStream = await GetMirrorOszStreamAsync(mapsetId);
        await ProcessMapsetAsync(mapsetId, oszStream);
        await ConvertImageAsync(mapsetId, oszStream);
    }

    private async Task ProcessLocalMapsetAsync(int mapsetId)
    {
        var oszPath = Settings.GetLocalOszPath(mapsetId);
        await using var fileStream = File.OpenRead(oszPath);
        
        await ProcessMapsetAsync(mapsetId, fileStream);
    }

    private async Task ProcessMapsetAsync(int mapsetId, Stream oszStream)
    {
        await using var oszFile = new OszFile(oszStream);

        var backgrounds = (await oszFile.GetBackgroundsAsync(mapsetId)).ToList();

        if (backgrounds.Count == 0)
        {
            Log.Information("No backgrounds found in {mapsetId}", mapsetId);
            return;
        }

        await using var conn = new DbSession(_dataSource);
        var imageRepository = new ImageRepository(conn);
        await conn.OpenAsync();
            
        foreach (var bg in backgrounds)
        {
            var vector = _analyzer.CreateEmbeddingVector(bg.Content);

            var imageRecord = new ImageRecord(mapsetId, bg.FileName, vector);
            await imageRepository.InsertImageRecord(imageRecord);
             
            Log.Information("Processed {mapsetId}: {fileName}", mapsetId, bg.FileName);
        }

        var meta = await oszFile.GetMetadataAsync();
        var mapset = new Mapset(mapsetId, meta.Artist, meta.Title, meta.Creator);
        await imageRepository.InsertMapset(mapset);
    }

    private async Task ConvertMissingBackgroundsAsync()
    {
        Log.Information("Checking for missing backgrounds...");
        
        await using var conn = new DbSession(_dataSource);
        var imageRepository = new ImageRepository(conn);
        await conn.OpenAsync();
        await conn.EnsureCreatedAsync();
        var imageRecords = await imageRepository.GetImageRecordsAsync();
        var mapsetImgFileNames = imageRecords.Select(r => 
            (r.MapsetId, Settings.GetImagePath(r.MapsetId, r.FileName))).ToList();
        
        var remainingMapsets = mapsetImgFileNames
            .Where(mf => !File.Exists(mf.Item2))
            .Select(mf => mf.MapsetId)
            .Distinct()
            .ToList();
        
        Log.Information("Converting backgrounds for {remainingMapsets} mapsets...", remainingMapsets.Count);

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = 8
        };

        await Parallel.ForEachAsync(remainingMapsets, parallelOptions, async (mapsetId, token) =>
        {
            try
            {
                var oszPath = Settings.GetLocalOszPath(mapsetId);

                if (File.Exists(oszPath))
                {
                    await using var fileStream = File.OpenRead(oszPath);
                    await ConvertImageAsync(mapsetId, fileStream);
                }
                else
                {
                    Log.Information("{mapsetId} not found locally. Downloading from mirror...", mapsetId);
                    await using var oszStream = await GetMirrorOszStreamAsync(mapsetId);
                    await ConvertImageAsync(mapsetId, oszStream);
                }
            }
            catch (InvalidDataException e)
            {
                Log.Error("Invalid archive: {mapsetId}. Trying to redownload from mirror...", mapsetId);
                await using var oszStream = await GetMirrorOszStreamAsync(mapsetId);
                await ConvertImageAsync(mapsetId, oszStream);
            }
            catch (Exception e)
            {
                Log.Error("Error converting background for {mapsetId}: {exception}", mapsetId, e.ToString());
            }
        });
    }

    private async Task ConvertImageAsync(int mapsetId, Stream oszStream)
    {
        oszStream.Seek(0, SeekOrigin.Begin);
        await using var oszFile = new OszFile(oszStream);

        var backgrounds = (await oszFile.GetBackgroundsAsync(mapsetId)).ToList();

        if (backgrounds.Count == 0)
        {
            Log.Information("No backgrounds found in {mapsetId}", mapsetId);
            return;
        }

        foreach (var bg in backgrounds)
        {
            var resultPath = Settings.GetImagePath(mapsetId, bg.FileName);
            
            var image = new MagickImage(bg.Content);
            image.Format = MagickFormat.Jxl;
            image.Quality = 75;
            await image.WriteAsync(resultPath);
            
            Log.Information("Converted background for {mapsetId} ({fileName})", mapsetId, bg.FileName);
        }
    }

    private async Task<Stream> GetMirrorOszStreamAsync(int mapsetId)
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:141.0) Gecko/20100101 Firefox/141.0");
        var oszStream = await httpClient.GetStreamAsync(Settings.MirrorUrl + mapsetId);

        var ms = new MemoryStream();
        await oszStream.CopyToAsync(ms);
        ms.Seek(0, SeekOrigin.Begin);
        return ms;
    }

    public void Dispose()
    {
        _dataSource.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _dataSource.DisposeAsync();
    }
}