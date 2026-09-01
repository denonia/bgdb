using bgdb.Common.Models;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;

namespace bgdb.Common.Services;

public class StatsService
{
    private const string StatsCacheKey = "database-stats";
    private readonly TimeSpan _statsCacheDuration = TimeSpan.FromMinutes(1);

    private readonly NpgsqlDataSource _dataSource;
    private readonly IMemoryCache _memoryCache;

    public StatsService(NpgsqlDataSource dataSource, IMemoryCache memoryCache)
    {
        _dataSource = dataSource;
        _memoryCache = memoryCache;
    }

    public async Task<DatabaseStats> GetDatabaseStats()
    {
        if (!_memoryCache.TryGetValue(StatsCacheKey, out DatabaseStats? databaseStats))
        {
            var query = """
                        SELECT 
                            (SELECT COUNT(*) FROM images) AS image_count,
                            (SELECT COUNT(*) FROM mapsets) AS mapset_count,
                            (SELECT MAX(processed_at) FROM images) AS last_update;
                        """;
            await using var command = _dataSource.CreateCommand(query);
        
            await using var reader = await command.ExecuteReaderAsync();
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