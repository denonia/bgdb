using System.Diagnostics;
using ImageMagick;
using Microsoft.Extensions.Logging;

namespace bgdb.Common.Services;

public class ImageConversionService : IImageConversionService
{
    private readonly ILogger<ImageConversionService> _logger;

    public ImageConversionService(ILogger<ImageConversionService> logger)
    {
        _logger = logger;
    }

    public async Task GenerateSearchThumbnailAsync(string searchId, byte[] source)
    {
        var sw = new Stopwatch();
        sw.Start();
        
        var image = new MagickImage(source);
        image.Format = MagickFormat.Jxl;
        image.Quality = 75;
                
        image.Resize(new MagickGeometry(160, 160)
        {
            IgnoreAspectRatio = false
        });
                
        using var resultMs = new MemoryStream();
        await image.WriteAsync(resultMs);
        await File.WriteAllBytesAsync(Path.Combine(Settings.SearchPath, $"{searchId}.jxl"), resultMs.GetBuffer());
        
        _logger.LogInformation("[{searchId}] Search thumbnail generated - {elapsedMs} ms", searchId, sw.ElapsedMilliseconds);
    }

    public async Task ConvertSearchImageAsync(string searchId, byte[] source)
    {
        var sw = new Stopwatch();
        sw.Start();
        
        var image = new MagickImage(source);
        image.Format = MagickFormat.Jxl;
        image.Quality = 75;
                
        using var resultMs = new MemoryStream();
        await image.WriteAsync(resultMs);
        await File.WriteAllBytesAsync(Path.Combine(Settings.SearchPathRaw, $"{searchId}.jxl"),
            resultMs.GetBuffer());
        _logger.LogInformation("[{searchId}] Full search image converted - {elapsedMs} ms", searchId, sw.ElapsedMilliseconds);
    }
}