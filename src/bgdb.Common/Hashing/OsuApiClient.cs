using System.Net.Http.Json;

namespace bgdb.Common.Hashing;

public class OsuApiClient : IDisposable
{
    private const string BeatmapSearchUrl =
        "https://osu.ppy.sh/beatmapsets/search?e=&c=&g=&l=&m=&nsfw=&played=&q=&r=&sort=&s=";
    
    private readonly HttpClient _httpClient = new();
    
    public Task<BeatmapSearchResponse?> GetLatestMapsets()
    {
        return _httpClient.GetFromJsonAsync<BeatmapSearchResponse>(BeatmapSearchUrl);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}