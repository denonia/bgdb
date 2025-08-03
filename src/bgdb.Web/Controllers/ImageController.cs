using bgdb.Common;
using bgdb.Common.Repositories;
using ImageMagick;
using Microsoft.AspNetCore.Mvc;

namespace bgdb.Web.Controllers;

[ApiController]
[Route("/img")]
public class ImageController : ControllerBase
{
    private readonly IImageRepository _imageRepository;

    public ImageController(IImageRepository imageRepository)
    {
        _imageRepository = imageRepository;
    }

    [HttpGet("full/{mapsetId}/{fileName?}")]
    [ResponseCache(Duration = Settings.ImageCacheDuration, Location = ResponseCacheLocation.Any, NoStore = false)]
    public async Task<IActionResult> GetFullImage(int mapsetId, string? fileName)
    {
        var fileNames = await _imageRepository.GetMapsetImageFileNamesAsync(mapsetId);

        if (fileNames.Count == 0 || (fileName is not null && !fileNames.Contains(fileName)))
            return NotFound();

        var imageName = fileName ?? fileNames[0];
        
        var imagePath = Path.Combine(Settings.ImagePath, $"{mapsetId}_{Path.GetFileNameWithoutExtension(imageName)}.jxl");
        var image = new MagickImage(imagePath);
            
        image.Format = MagickFormat.Jpg;
        image.Quality = 90;

        return File(image.ToByteArray(), "image/jpeg");
    }
    
    [HttpGet("thumb/{mapsetId}/{fileName?}")]
    [ResponseCache(Duration = Settings.ImageCacheDuration, Location = ResponseCacheLocation.Any, NoStore = false)]
    public async Task<IActionResult> GetThumbnail(int mapsetId, string? fileName)
    {
        var fileNames = await _imageRepository.GetMapsetImageFileNamesAsync(mapsetId);

        if (fileNames.Count == 0 || (fileName is not null && !fileNames.Contains(fileName)))
            return NotFound();

        var imageName = fileName ?? fileNames[0];
        
        var image = new MagickImage(Settings.GetImagePath(mapsetId, imageName));
            
        image.Resize(new MagickGeometry(380, 380)
        {
            IgnoreAspectRatio = false
        });
            
        image.Format = MagickFormat.Jpg;
        image.Quality = 90;

        return File(image.ToByteArray(), "image/jpeg");
    }

    [HttpGet("search/{searchId}")]
    [ResponseCache(Duration = Settings.ImageCacheDuration, Location = ResponseCacheLocation.Any, NoStore = false)]
    public async Task<IActionResult> GetSearchImage(string searchId)
    {
        var imagePath = Settings.GetSearchImagePath(searchId);

        if (!System.IO.File.Exists(imagePath))
            return NotFound();
        
        var image = new MagickImage(imagePath);
            
        image.Format = MagickFormat.Jpg;
        image.Quality = 90;

        return File(image.ToByteArray(), "image/jpeg");
    }
}