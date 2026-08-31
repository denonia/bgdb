using System.Diagnostics;
using System.Net;
using bgdb.Common.Repositories;
using Microsoft.Extensions.Logging;

namespace bgdb.Common.Services;

public class ImageSearchService : IImageSearchService
{
    private readonly IImageAnalyzer _analyzer;
    private readonly IImageRepository _imageRepository;
    private readonly ISearchRepository _searchRepository;
    private readonly IImageConversionService _conversionService;
    private readonly ILogger<ImageSearchService> _logger;

    public ImageSearchService(IImageAnalyzer analyzer, IImageRepository imageRepository,
        ISearchRepository searchRepository,
        IImageConversionService conversionService, ILogger<ImageSearchService> logger)
    {
        _analyzer = analyzer;
        _imageRepository = imageRepository;
        _searchRepository = searchRepository;
        _conversionService = conversionService;
        _logger = logger;
    }

    public async Task<Guid> CreateSearchAsync(Stream stream, IPAddress requesterIp)
    {
        using var activity = Telemetry.ActivitySource.StartActivity();
        
        var searchId = Guid.NewGuid();

        var ms = new MemoryStream();
        await stream.CopyToAsync(ms);

        var genThumbnailTask = _conversionService.GenerateSearchThumbnailAsync(searchId.ToString(), ms.GetBuffer());
        
        await Task.WhenAll(genThumbnailTask, PerformSearchAsync(searchId, ms, requesterIp));

        _ = Task.Run(async () =>
        {
            await _conversionService.ConvertSearchImageAsync(searchId.ToString(), ms.GetBuffer());
        });

        return searchId;
    }

    private async Task PerformSearchAsync(Guid searchId, Stream stream, IPAddress requesterIp)
    {
        using var activity = Telemetry.ActivitySource.StartActivity();

        stream.Seek(0, SeekOrigin.Begin);
        var embedding = _analyzer.CreateEmbeddingVector(stream);
        var results = (await _imageRepository.GetClosestMatchesAsync(embedding)).ToList();

        await _searchRepository.CreateSearchAsync(searchId, requesterIp);
        await _searchRepository.InsertSearchResultsAsync(searchId, results);
    }
}