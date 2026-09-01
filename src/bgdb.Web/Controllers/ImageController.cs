using bgdb.Common;
using bgdb.Common.Repositories;
using bgdb.Common.Storages;
using Microsoft.AspNetCore.Mvc;

namespace bgdb.Web.Controllers;

[ApiController]
[Route("/img")]
public class ImageController : ControllerBase
{
    private readonly IImageRepository _imageRepository;
    private readonly ImageStorage _imageStorage;

    public ImageController(IImageRepository imageRepository, ImageStorage imageStorage)
    {
        _imageRepository = imageRepository;
        _imageStorage = imageStorage;
    }
    
    [HttpGet("thumb/{mapsetId}/{fileName?}")]
    [ResponseCache(Duration = Settings.ImageCacheDuration, Location = ResponseCacheLocation.Any, NoStore = false)]
    public async Task<IActionResult> GetThumbnail(int mapsetId, string? fileName)
    {
        var fileNames = await _imageRepository.GetMapsetImageFileNamesAsync(mapsetId);

        if (fileNames.Count == 0 || (fileName is not null && !fileNames.Contains(fileName)))
            return NotFound();

        var imageName = fileName ?? fileNames[0];
        var image = await _imageStorage.GetBackgroundThumbnailAsync(mapsetId, imageName);
        if (image is null)
            return NotFound();
        
        return File(image.Content, image.ContentType);
    }

    [HttpGet("search/{searchId}")]
     [ResponseCache(Duration = Settings.ImageCacheDuration, Location = ResponseCacheLocation.Any, NoStore = false)]
    public async Task<IActionResult> GetSearchThumbnail(Guid searchId)
    {
        var image = await _imageStorage.GetSearchThumbnailAsync(searchId);
        
        if (image is null)
            return NotFound();

        return File(image.Content, image.ContentType);
    }
}