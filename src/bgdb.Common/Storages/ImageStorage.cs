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
    private const string SearchThumbnailContentType = "image/webp";
    
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
         => $"{SearchThumbnailPrefix}{searchId}.webp";
    
    public async Task UploadBackgroundImageAsync(int mapsetId, string fileName, byte[] content)
    {
        using var activity = Telemetry.ActivitySource.StartActivity();
        
        using var image = new MagickImage(content);
        image.Format = MagickFormat.Jxl;
        image.Quality = 75;
            
        var key = GetBackgroundImageKey(mapsetId, fileName);
        await _fileStorage.PutFileAsync(key, BackgroundImageContentType, image.ToByteArray());
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

    public async Task GenerateBackgroundThumbnailAsync(int mapsetId, string fileName)
    {
        var sourceImage = await GetBackgroundImageAsync(mapsetId, fileName);
        if (sourceImage is null)
            return;
        
        using var magickImage = new MagickImage(sourceImage.Content);
        magickImage.Format = MagickFormat.Avif;
        magickImage.Quality = 65;
        
        magickImage.Resize(new MagickGeometry(320, 320)
        {
            IgnoreAspectRatio = false
        });
        
        var key = GetBackgroundThumbnailKey(mapsetId, fileName);
        await _fileStorage.PutFileAsync(key, BackgroundThumbnailContentType, magickImage.ToByteArray());
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
        magickImage.Quality = 65;
        
        magickImage.Resize(new MagickGeometry(320, 320)
        {
            IgnoreAspectRatio = false
        });
        
        var imageBytes = magickImage.ToByteArray();
        await _fileStorage.PutFileAsync(key, BackgroundThumbnailContentType, imageBytes);
        
        return new StorageFile(imageBytes, BackgroundThumbnailContentType);
    }
    
    public async Task<IReadOnlyCollection<StorageImage>> GetAllBackgroundThumbnails()
    {
        using var activity = Telemetry.ActivitySource.StartActivity();
        
        return (await _fileStorage.ListFilesAsync(BackgroundThumbnailPrefix))
            .Select(key =>
            {
                var parts = key[BackgroundThumbnailPrefix.Length..].Split('/', 2);
                return new StorageImage
                {
                    MapsetId = int.Parse(parts[0]),
                    FileName = parts[1]
                };
            })
            .ToArray();
    }

    public async Task UploadSearchImageAsync(Guid searchId, string fileName, byte[] content)
    {
        using var activity = Telemetry.ActivitySource.StartActivity();
        
        var key = GetSearchImageKey(searchId, fileName);
        var contentType = MimeTypeMap.GetMimeType(fileName);
        await _fileStorage.PutFileAsync(key, contentType, content);
    }

    public async Task GenerateSearchThumbnailAsync(Guid searchId, byte[] content)
    {
        using var activity = Telemetry.ActivitySource.StartActivity();
        
        using var image = new MagickImage(content);
        image.Format = MagickFormat.WebP;
        image.Quality = 75;
                
        image.Resize(new MagickGeometry(160, 160)
        {
            IgnoreAspectRatio = false
        });
        
        var key = GetSearchThumbnailKey(searchId);
        await _fileStorage.PutFileAsync(key, SearchThumbnailContentType, image.ToByteArray());
    }

    public async Task<StorageFile?> GetSearchThumbnailAsync(Guid searchId)
    {
        using var activity = Telemetry.ActivitySource.StartActivity();
        
        var key = GetSearchThumbnailKey(searchId);
        var imageBytes = await _fileStorage.GetFileAsync(key);
        if (imageBytes is null)
            return null;
        
        return new StorageFile(imageBytes, SearchThumbnailContentType);
    }
}