using System.Net;
using bgdb.Common.Repositories;
using bgdb.Common.Storages;

namespace bgdb.Common.Services;

public class ImageSearchService : IImageSearchService
{
    private readonly IImageAnalyzer _analyzer;
    private readonly IImageRepository _imageRepository;
    private readonly ISearchRepository _searchRepository;
    private readonly ImageStorage _imageStorage;

    public ImageSearchService(IImageAnalyzer analyzer, 
        IImageRepository imageRepository,
        ISearchRepository searchRepository,
        ImageStorage imageStorage)
    {
        _analyzer = analyzer;
        _imageRepository = imageRepository;
        _searchRepository = searchRepository;
        _imageStorage = imageStorage;
    }

    public async Task<Guid> CreateSearchAsync(Stream stream, string fileName, IPAddress requesterIp)
    {
        using var activity = Telemetry.ActivitySource.StartActivity();
        
        var searchId = Guid.NewGuid();
        
        using var genStream = new MemoryStream();
        await stream.CopyToAsync(genStream);
        genStream.Seek(0, SeekOrigin.Begin);
        
        using var uploadStream = new MemoryStream(genStream.ToArray());

        await Task.WhenAll(
            _imageStorage.GenerateSearchThumbnailAsync(searchId, genStream),
            _imageStorage.UploadSearchImageAsync(searchId, fileName, uploadStream),
            PerformSearchAsync(searchId, stream, requesterIp));

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