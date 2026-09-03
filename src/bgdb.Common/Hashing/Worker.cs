using bgdb.Common.Models;
using bgdb.Common.Repositories;
using bgdb.Common.Storages;
using Microsoft.Extensions.Logging;

namespace bgdb.Common.Hashing;

public class Worker
{
    private readonly ImageEmbedder _embedder;
    private readonly ImageStorage _imageStorage;
    private readonly ImageRepository _imageRepository;
    private readonly ILogger<Worker> _logger;

    public Worker(ImageEmbedder embedder, 
        ImageStorage imageStorage,
        ImageRepository imageRepository,
        ILogger<Worker> logger)
    {
        _embedder = embedder;
        _imageStorage = imageStorage;
        _imageRepository = imageRepository;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        //await ProcessLocalMapsAsync();
        //await ConvertMissingBackgroundsAsync();
        //await GenerateMissingThumbnailsAsync();

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ProcessNewMapsetsAsync();
            }
            catch (Exception e)
            {
                _logger.LogError("Failed to get latest mapsets: {exception}", e);
            }
            finally
            {
                await Task.Delay(TimeSpan.FromMinutes(10), ct);
            }
        }
    }

    private async Task ProcessNewMapsetsAsync()
    {
        _logger.LogInformation("Checking for new mapsets...");
        
        var completedMapsetIds = await _imageRepository.GetCompletedMapsetsAsync();
        
        using var apiClient = new OsuApiClient();
        var latestMapsets = await apiClient.GetLatestMapsets();
        
        var mapsetIds = latestMapsets!.Beatmapsets.Select(s => s.Id).ToList();
        var newMapsets = mapsetIds.Except(completedMapsetIds).ToList();
        
        _logger.LogInformation("{newMapsetCount} new mapsets found!", newMapsets.Count);

        foreach (var mapsetId in newMapsets)
            await ProcessRemoteMapsetAsync(mapsetId);
    }

    private async Task ProcessLocalMapsAsync()
    {
        var completedMapsetIds = await _imageRepository.GetCompletedMapsetsAsync();
        
        _logger.LogInformation("{processedMapsetCount} processed mapsets in database", completedMapsetIds.Count);

        if (!Directory.Exists(Settings.LocalSongsPath))
        {
            _logger.LogWarning("Local songs folder not found. Skipping local map processing...");
            return;
        }
        
        var localOszs = Directory.GetFiles(Settings.LocalSongsPath).Where(f => f.EndsWith(".osz"));
        var localMapsetIds = localOszs.Select(o => int.Parse(Path.GetFileNameWithoutExtension(o))).ToList();
        var mapsetsToProcess = localMapsetIds.Except(completedMapsetIds).Order().ToList();
        
        _logger.LogInformation("{localMapsetCount} mapsets found on disk", localMapsetIds.Count);
        _logger.LogInformation("{mapsetsToProcessCount} mapsets left to process", mapsetsToProcess.Count);

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
                _logger.LogError("Invalid archive: {mapsetId}. Trying to redownload from mirror...", mapsetId);
                await ProcessRemoteMapsetAsync(mapsetId);
            }
            catch (Exception e)
            {
                _logger.LogError("Error processing mapset {mapsetId}: {exception}", mapsetId, e.ToString());
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
            _logger.LogInformation("No backgrounds found in {mapsetId}", mapsetId);
            return;
        }

        var meta = await oszFile.GetMetadataAsync();
        var mapset = new Mapset(mapsetId, meta.Artist, meta.Title, meta.Creator);
        await _imageRepository.InsertMapsetAsync(mapset);

        foreach (var bg in backgrounds)
        {
            using var ms = new MemoryStream();
            await bg.Content.CopyToAsync(ms);
            var vector = _embedder.CreateEmbeddingVector(ms.GetBuffer());

            var imageRecord = new ImageRecord(mapsetId, bg.FileName, vector);
            await _imageRepository.InsertImageRecordAsync(imageRecord);
             
            _logger.LogInformation("Processed {mapsetId}: {fileName}", mapsetId, bg.FileName);
        }
    }

    private async Task ConvertMissingBackgroundsAsync()
    {
        _logger.LogInformation("Checking for missing backgrounds in storage...");

        var mapsetsInDb = await _imageRepository.GetCompletedMapsetsAsync();
        var mapsetsInStorage = (await _imageStorage.GetAllBackgroundImages()).Select(x => x.MapsetId);
        var remainingMapsets = mapsetsInDb.Except(mapsetsInStorage).ToArray();
        
        _logger.LogInformation("Converting backgrounds for {remainingMapsets} mapsets...", remainingMapsets.Length);

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
                    _logger.LogInformation("{mapsetId} not found locally. Downloading from mirror...", mapsetId);
                    await using var oszStream = await GetMirrorOszStreamAsync(mapsetId);
                    await ConvertImageAsync(mapsetId, oszStream);
                }
            }
            catch (InvalidDataException e)
            {
                _logger.LogError("Invalid archive: {mapsetId}. Trying to redownload from mirror...", mapsetId);
                await using var oszStream = await GetMirrorOszStreamAsync(mapsetId);
                await ConvertImageAsync(mapsetId, oszStream);
            }
            catch (Exception e)
            {
                _logger.LogError("Error converting background for {mapsetId}: {exception}", mapsetId, e.ToString());
            }
        });
    }

    private async Task GenerateMissingThumbnailsAsync()
    {
        var backgrounds = await _imageRepository.GetImageRecordsAsync();
        var mapsetsWithThumbnails = (await _imageStorage.GetAllBackgroundThumbnails()).Select(x => x.MapsetId);
        var backgroundsToProcess = backgrounds
            .Where(x => !mapsetsWithThumbnails.Contains(x.MapsetId))
            .ToArray();
        
        _logger.LogInformation("Generating thumbnails for {remainingMapsets} mapsets...", backgroundsToProcess.Length);
        
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = 8
        };

        await Parallel.ForEachAsync(backgroundsToProcess, parallelOptions, async (bg, token) =>
        {
            try
            {
                await _imageStorage.GenerateBackgroundThumbnailAsync(bg.MapsetId, bg.FileName);
                _logger.LogInformation("Generated thumbnail for {fileName} ({mapsetId})", 
                    bg.FileName, bg.MapsetId);
            }
            catch (Exception e)
            {
                _logger.LogError("Error generating thumbnail for {fileName} ({mapsetId}): {exception}", 
                    bg.FileName, bg.MapsetId, e.ToString());
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
            _logger.LogInformation("No backgrounds found in {mapsetId}", mapsetId);
            return;
        }

        foreach (var bg in backgrounds)
        {
            using var ms = new MemoryStream();
            await bg.Content.CopyToAsync(ms);
            
            await _imageStorage.UploadBackgroundImageAsync(mapsetId, bg.FileName, ms.GetBuffer());
            await _imageStorage.GenerateBackgroundThumbnailAsync(mapsetId, bg.FileName);
            _logger.LogInformation("Uploaded background for {mapsetId} ({fileName})", mapsetId, bg.FileName);
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
}