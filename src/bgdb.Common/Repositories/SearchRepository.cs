using System.Net;
using bgdb.Common.Models;
using Npgsql;

namespace bgdb.Common.Repositories;

public class SearchRepository : ISearchRepository
{
    private readonly IDbSession _dbSession;

    public SearchRepository(IDbSession dbSession)
    {
        _dbSession = dbSession;
    }

    public async Task CreateSearchAsync(Guid searchId, IPAddress ipAddress)
    {
        await _dbSession.EnsureOpenedAsync();
        var cmd = new NpgsqlCommand("INSERT INTO searches (search_id, ip_addr) VALUES (@search_id, @ip_addr)", _dbSession.Connection);
        cmd.Parameters.AddWithValue("search_id", searchId);
        cmd.Parameters.AddWithValue("ip_addr", ipAddress);
        await cmd.ExecuteNonQueryAsync(); 
    }

    public async Task InsertSearchResultsAsync(Guid searchId, IList<MatchResult> results)
    {
        await _dbSession.EnsureOpenedAsync();
        var batch = new NpgsqlBatch(_dbSession.Connection);

        foreach (var result in results)
        {
            batch.BatchCommands.Add(
                new NpgsqlBatchCommand(
                    "INSERT INTO search_results (search_id, mapset_id, filename, similarity) VALUES (@search_id, @mapset_id, @filename, @similarity)")
                {
                    Parameters =
                    {
                        new NpgsqlParameter("search_id", searchId),
                        new NpgsqlParameter("mapset_id", result.MapsetId),
                        new NpgsqlParameter("filename", result.FileName),
                        new NpgsqlParameter("similarity", result.Similarity),
                    }
                });
        }

        await batch.ExecuteNonQueryAsync(); 
    }
    
    public async Task<IList<MatchResult>> GetSearchResultsAsync(Guid searchId)
    {
        var query = """
                    SELECT
                      r.mapset_id,
                      m.artist,
                      m.title,
                      m.creator,
                      r.filename,
                      r.similarity
                    FROM search_results r
                    JOIN mapsets m on r.mapset_id = m.mapset_id
                    WHERE r.search_id = @search_id
                    ORDER BY r.similarity DESC
                    LIMIT 20;
                    """;
        
        await _dbSession.EnsureOpenedAsync();
        await using var cmd = new NpgsqlCommand(query, _dbSession.Connection);
        cmd.Parameters.AddWithValue("search_id", searchId);
        
        var result = new List<MatchResult>();
        
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var match = new MatchResult
            {
                MapsetId = reader.GetInt32(0),
                Artist = reader.GetString(1),
                Title = reader.GetString(2),
                Creator = reader.GetString(3),
                FileName = reader.GetString(4),
                Similarity = reader.GetFloat(5)
            };
            result.Add(match);
        }

        return result;
    }

    public async Task<IList<SearchRecord>> GetLatestSearches()
    {
        await _dbSession.EnsureOpenedAsync();
        await using var cmd = new NpgsqlCommand("SELECT * FROM searches ORDER BY timestamp DESC LIMIT 100", _dbSession.Connection);
        
        var result = new List<SearchRecord>();
        
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var search = new SearchRecord
            {
                SearchId = reader.GetGuid(0),
                IpAddress = reader.GetFieldValue<IPAddress>(1),
                Timestamp = reader.GetDateTime(2)
            };
            result.Add(search);
        }

        return result;
    }
}