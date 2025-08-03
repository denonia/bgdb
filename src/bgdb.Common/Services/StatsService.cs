using bgdb.Common.Models;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;

namespace bgdb.Common.Services;

public class StatsService : IStatsService
{
    private const string StatsCacheKey = "database-stats";
    private readonly TimeSpan _statsCacheDuration = TimeSpan.FromMinutes(1);
    
    private readonly IDbSession _dbSession;
    private readonly IMemoryCache _memoryCache;

    public StatsService(IDbSession dbSession, IMemoryCache memoryCache)
    {
        _dbSession = dbSession;
        _memoryCache = memoryCache;
    }

    public async Task<DatabaseStats> GetDatabaseStats()
    {
        if (!_memoryCache.TryGetValue(StatsCacheKey, out DatabaseStats? databaseStats))
        {
            await _dbSession.EnsureOpenedAsync();
        
            var query = """
                        SELECT 
                            (SELECT COUNT(*) FROM images) AS image_count,
                            (SELECT COUNT(*) FROM mapsets) AS mapset_count,
                            (SELECT MAX(processed_at) FROM images) AS last_update;
                        """;
            await using var cmd = new NpgsqlCommand(query, _dbSession.Connection);
        
            await using var reader = await cmd.ExecuteReaderAsync();
            await reader.ReadAsync();

            databaseStats = new DatabaseStats
            {
                ImageCount = reader.GetInt32(0),
                MapsetCount = reader.GetInt32(1),
                LastUpdate = reader.GetDateTime(2)
            };
            
            var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(_statsCacheDuration);
            _memoryCache.Set(StatsCacheKey, databaseStats, cacheEntryOptions);
        }

        return databaseStats!;
    }
}