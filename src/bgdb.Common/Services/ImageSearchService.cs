using System.Net;
using bgdb.Common.Repositories;
using bgdb.Common.Storages;

namespace bgdb.Common.Services;

public class ImageSearchService
{
    private readonly ImageEmbedder _embedder;
    private readonly ImageRepository _imageRepository;
    private readonly SearchRepository _searchRepository;
    private readonly ImageStorage _imageStorage;

    public ImageSearchService(ImageEmbedder embedder, 
        ImageRepository imageRepository,
        SearchRepository searchRepository,
        ImageStorage imageStorage)
    {
        _embedder = embedder;
        _imageRepository = imageRepository;
        _searchRepository = searchRepository;
        _imageStorage = imageStorage;
    }

    public async Task<Guid> CreateSearchAsync(byte[] imageBytes, string fileName, IPAddress requesterIp)
    {
        using var activity = Telemetry.ActivitySource.StartActivity();
        
        var searchId = Guid.NewGuid();
        
        _ = Task.Run(async () =>
        {
            await _imageStorage.GenerateSearchThumbnailAsync(searchId, imageBytes);
            await _imageStorage.UploadSearchImageAsync(searchId, fileName, imageBytes);
        });
        
        await PerformSearchAsync(searchId, imageBytes, requesterIp);

        return searchId;
    }

    private async Task PerformSearchAsync(Guid searchId, byte[] imageBytes, IPAddress requesterIp)
    {
        using var activity = Telemetry.ActivitySource.StartActivity();

        var embedding = _embedder.CreateEmbeddingVector(imageBytes);
        var results = (await _imageRepository.GetClosestMatchesAsync(embedding)).ToList();

        await _searchRepository.CreateSearchAsync(searchId, requesterIp);
        await _searchRepository.InsertSearchResultsAsync(searchId, results);
    }
}