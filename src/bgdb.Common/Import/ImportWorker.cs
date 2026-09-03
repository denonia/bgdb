using bgdb.Common.Models;
using bgdb.Common.Repositories;
using bgdb.Common.Storages;
using ICSharpCode.SharpZipLib.BZip2;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace bgdb.Common.Import;

public class ImportWorker
{
    private const string OnlineDatabaseUrl = "https://assets.ppy.sh/client-resources/online.db.bz2";
    
    private readonly ImageEmbedder _embedder;
    private readonly ImageStorage _imageStorage;
    private readonly ImageRepository _imageRepository;
    private readonly ILogger<ImportWorker> _logger;

    public ImportWorker(ImageEmbedder embedder, 
        ImageStorage imageStorage,
        ImageRepository imageRepository,
        ILogger<ImportWorker> logger)
    {
        _embedder = embedder;
        _imageStorage = imageStorage;
        _imageRepository = imageRepository;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        if (Settings.ProcessLocalMaps)
            await ProcessLocalMapsAsync();
        if (Settings.VerifyFromOnlineDatabase)
            await VerifyFromOnlineDatabaseAsync();
        if (Settings.FetchMissingBackgrounds)
            await FetchMissingBackgroundsAsync();
        if (Settings.GenerateMissingThumbnails)
            await GenerateMissingThumbnailsAsync();

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
        {
            await using var oszStream = await GetOszStreamAsync(mapsetId);
            await using var oszFile = new OszFile(oszStream);
            await ProcessMapsetAsync(mapsetId, oszFile);
            await FetchMapsetBackgroundsAsync(mapsetId, oszFile);
        }
    }

    private async Task ProcessLocalMapsAsync()
    {
        var completedMapsetIds = await _imageRepository.GetCompletedMapsetsAsync();

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

        await Parallel.ForEachAsync(mapsetsToProcess, async (mapsetId, token) =>
        {
            await using var oszStream = await GetOszStreamAsync(mapsetId);
            await using var oszFile = new OszFile(oszStream);
            await ProcessMapsetAsync(mapsetId, oszFile);
            await FetchMapsetBackgroundsAsync(mapsetId, oszFile);
        });
    }

    private async Task VerifyFromOnlineDatabaseAsync()
    {
        _logger.LogInformation("Fetching the online database...");
        
        var path = Path.GetTempFileName();
        using (var httpClient = new HttpClient())
        await using (var httpStream = await httpClient.GetStreamAsync(OnlineDatabaseUrl))
        await using (var file = File.Create(path))
        await using (var bz2 = new BZip2InputStream(httpStream))
        {
            await bz2.CopyToAsync(file);
        }

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM osu_beatmapsets";

        var onlineMapsetIds = new List<int>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            onlineMapsetIds.Add(reader.GetInt32(0));
        
        var completedMapsetIds = await _imageRepository.GetCompletedMapsetsAsync();
        var mapsetsToProcess = onlineMapsetIds.Except(completedMapsetIds).ToList();
        
        _logger.LogInformation("Found {mapsetsToProcess} mapsets from online database to process", mapsetsToProcess.Count);

        foreach (var mapsetId in mapsetsToProcess)
        {
            await using var oszStream = await GetOszStreamAsync(mapsetId);
            await using var oszFile = new OszFile(oszStream);
            await ProcessMapsetAsync(mapsetId, oszFile);
            await FetchMapsetBackgroundsAsync(mapsetId, oszFile);
        }
    }

    private async Task FetchMissingBackgroundsAsync()
    {
        _logger.LogInformation("Checking for missing backgrounds in storage...");
    
        var mapsetsInDb = await _imageRepository.GetCompletedMapsetsAsync();
        var mapsetsInStorage = (await _imageStorage.GetAllBackgroundImages()).Select(x => x.MapsetId);
        var remainingMapsets = mapsetsInDb.Except(mapsetsInStorage).ToArray();
        
        _logger.LogInformation("Fetching backgrounds for {remainingMapsets} mapsets...", remainingMapsets.Length);

        foreach (var mapsetId in remainingMapsets)
        {
            await using var oszStream = await GetOszStreamAsync(mapsetId);
            await using var oszFile = new OszFile(oszStream);
            await FetchMapsetBackgroundsAsync(mapsetId, oszFile);
        }
    }

    private async Task GenerateMissingThumbnailsAsync()
    {
        var backgrounds = await _imageRepository.GetImageRecordsAsync();
        var mapsetsWithThumbnails = (await _imageStorage.GetAllBackgroundThumbnails()).Select(x => x.MapsetId);
        var backgroundsToProcess = backgrounds
            .Where(x => !mapsetsWithThumbnails.Contains(x.MapsetId))
            .ToArray();
        
        _logger.LogInformation("Generating thumbnails for {remainingMapsets} mapsets...", backgroundsToProcess.Length);

        await Parallel.ForEachAsync(backgroundsToProcess, async (bg, token) =>
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

    private async Task ProcessMapsetAsync(int mapsetId, OszFile oszFile)
    {
        try
        {
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
        catch (Exception e)
        {
            _logger.LogError("Error processing mapset {mapsetId}: {exception}", mapsetId, e.ToString());
        }
    }

    private async Task FetchMapsetBackgroundsAsync(int mapsetId, OszFile oszFile)
    {
        try
        {
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
                _logger.LogInformation("Uploaded background for {mapsetId}: {fileName}", mapsetId, bg.FileName);
            }
        }
        catch (Exception e)
        {
            _logger.LogError("Error fetching mapset backgrounds {mapsetId}: {exception}", mapsetId, e.ToString());
        }
    }

    private async Task<Stream> GetOszStreamAsync(int mapsetId)
    {
        var localOszPath = Settings.GetLocalOszPath(mapsetId);
        if (File.Exists(localOszPath))
        {
            _logger.LogInformation("Found mapset {mapsetId} locally.", mapsetId);
            return File.OpenRead(localOszPath);
        }
        
        _logger.LogInformation("Downloading mapset {mapsetId} from mirror...", mapsetId);
        return await GetMirrorOszStreamAsync(mapsetId);
    }

    private static async Task<Stream> GetMirrorOszStreamAsync(int mapsetId)
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:141.0) Gecko/20100101 Firefox/141.0");
        await using var oszStream = await httpClient.GetStreamAsync(Settings.MirrorUrl + mapsetId);

        var ms = new MemoryStream();
        await oszStream.CopyToAsync(ms);
        ms.Seek(0, SeekOrigin.Begin);
        return ms;
    }
}