using bgdb.Common.Models;
using ImageMagick;
using MimeTypes;

namespace bgdb.Common.Storages;

public class ImageStorage
{
    private const string BackgroundImagePrefix = "img/";
    private const string BackgroundThumbnailPrefix = "thumb/";
    private const string SearchImagePrefix = "search/";
    private const string SearchThumbnailPrefix = "search-thumb/";
    
    private const string BackgroundImageContentType = "image/jxl";
    private const string BackgroundThumbnailContentType = "image/avif";
    private const string SearchThumbnailContentType = "image/avif";
    
    private readonly IFileStorage _fileStorage;

    public ImageStorage(IFileStorage fileStorage)
    {
        _fileStorage = fileStorage;
    }
    
    private string GetBackgroundImageKey(int mapsetId, string fileName) 
        => $"{BackgroundImagePrefix}{mapsetId}_{Path.GetFileNameWithoutExtension(fileName)}.jxl";
    
    private string GetBackgroundThumbnailKey(int mapsetId, string fileName) 
        => $"{BackgroundThumbnailPrefix}{mapsetId}/{Path.GetFileNameWithoutExtension(fileName)}.avif";

    private string GetSearchImageKey(Guid searchId, string fileName)
        => $"{SearchImagePrefix}{searchId}/{fileName}";
    
    private string GetSearchThumbnailKey(Guid searchId) 
         => $"{SearchThumbnailPrefix}{searchId}.avif";
    
    public async Task UploadBackgroundImageAsync(int mapsetId, string fileName, Stream stream)
    {
        using var activity = Telemetry.ActivitySource.StartActivity();
        
        var image = new MagickImage(stream);
        image.Format = MagickFormat.Jxl;
            
        using var ms = new MemoryStream(image.ToByteArray());
        var key = GetBackgroundImageKey(mapsetId, fileName);
        await _fileStorage.PutFileAsync(key, BackgroundImageContentType, ms);
    }

    public async Task<IReadOnlyCollection<StorageImage>> GetAllBackgroundImages()
    {
        using var activity = Telemetry.ActivitySource.StartActivity();
        
        return (await _fileStorage.ListFilesAsync(BackgroundImagePrefix))
            .Select(key =>
            {
                var parts = key[BackgroundImagePrefix.Length..].Split('_', 2);
                return new StorageImage
                {
                    MapsetId = int.Parse(parts[0]),
                    FileName = parts[1]
                };
            })
            .ToArray();
    }

    public async Task<StorageFile?> GetBackgroundImageAsync(int mapsetId, string fileName)
    {
        using var activity = Telemetry.ActivitySource.StartActivity();
        
        var key = GetBackgroundImageKey(mapsetId, fileName);
        var content = await _fileStorage.GetFileAsync(key);
        if (content is null)
            return null;

        return new StorageFile(content, BackgroundImageContentType);
    }

    public async Task<StorageFile?> GetBackgroundThumbnailAsync(int mapsetId, string fileName)
    {
        using var activity = Telemetry.ActivitySource.StartActivity();
        
        var key = GetBackgroundThumbnailKey(mapsetId, fileName);
        var content = await _fileStorage.GetFileAsync(key);

        if (content is not null)
            return new StorageFile(content, BackgroundThumbnailContentType);
        
        var sourceImage = await GetBackgroundImageAsync(mapsetId, fileName);
        if (sourceImage is null)
            return null;
        
        using var magickImage = new MagickImage(sourceImage.Content);
        magickImage.Format = MagickFormat.Avif;
        
        magickImage.Resize(new MagickGeometry(320, 320)
        {
            IgnoreAspectRatio = false
        });
        
        var imageBytes = magickImage.ToByteArray();
        using var ms = new MemoryStream(imageBytes);
        await _fileStorage.PutFileAsync(key, BackgroundThumbnailContentType, ms);
        
        return new StorageFile(new MemoryStream(imageBytes), BackgroundThumbnailContentType);
    }

    public async Task UploadSearchImageAsync(Guid searchId, string fileName, Stream stream)
    {
        using var activity = Telemetry.ActivitySource.StartActivity();
        
        var key = GetSearchImageKey(searchId, fileName);
        var contentType = MimeTypeMap.GetMimeType(fileName);
        await _fileStorage.PutFileAsync(key, contentType, stream);
    }

    public async Task GenerateSearchThumbnailAsync(Guid searchId, Stream stream)
    {
        using var activity = Telemetry.ActivitySource.StartActivity();
        
        var image = new MagickImage(stream);
        image.Format = MagickFormat.Avif;
                
        image.Resize(new MagickGeometry(160, 160)
        {
            IgnoreAspectRatio = false
        });
                
        using var ms = new MemoryStream();
        await image.WriteAsync(ms);
        ms.Seek(0, SeekOrigin.Begin);
        
        var key = GetSearchThumbnailKey(searchId);
        await _fileStorage.PutFileAsync(key, SearchThumbnailContentType, ms);
    }

    public async Task<StorageFile?> GetSearchThumbnailAsync(Guid searchId)
    {
        using var activity = Telemetry.ActivitySource.StartActivity();
        
        var key = GetSearchThumbnailKey(searchId);
        var imageStream = await _fileStorage.GetFileAsync(key);
        if (imageStream is null)
            return null;
        
        return new StorageFile(imageStream, SearchThumbnailContentType);
    }
}