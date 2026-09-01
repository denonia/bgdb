using bgdb.Common.Storages;
using Microsoft.AspNetCore.Mvc;

namespace bgdb.Web.Controllers;

[ApiController]
[Route("/img")]
public class ImageController : ControllerBase
{
    private readonly ImageStorage _imageStorage;

    public ImageController(ImageStorage imageStorage)
    {
        _imageStorage = imageStorage;
    }
    
    [HttpGet("thumb/{mapsetId}/{fileName}")]
    public async Task<IActionResult> GetThumbnail(int mapsetId, string fileName)
    {
        var image = await _imageStorage.GetBackgroundThumbnailAsync(mapsetId, fileName);
        if (image is null)
            return NotFound();
        
        return File(image.Content, image.ContentType);
    }

    [HttpGet("search/{searchId}")]
    public async Task<IActionResult> GetSearchThumbnail(Guid searchId)
    {
        var image = await _imageStorage.GetSearchThumbnailAsync(searchId);
        if (image is null)
            return NotFound();

        return File(image.Content, image.ContentType);
    }
}