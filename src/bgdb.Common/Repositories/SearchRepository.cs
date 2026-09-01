using System.Net;
using bgdb.Common.Models;
using Npgsql;

namespace bgdb.Common.Repositories;

public class SearchRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public SearchRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task CreateSearchAsync(Guid searchId, IPAddress ipAddress)
    {
        await using var command = _dataSource.CreateCommand(
            "INSERT INTO searches (search_id, ip_addr) VALUES (@search_id, @ip_addr)");
        command.Parameters.AddWithValue("search_id", searchId);
        command.Parameters.AddWithValue("ip_addr", ipAddress);
        await command.ExecuteNonQueryAsync(); 
    }

    public async Task InsertSearchResultsAsync(Guid searchId, IList<MatchResult> results)
    {
        await using var batch = _dataSource.CreateBatch();

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
        
        await using var command = _dataSource.CreateCommand(query);
        command.Parameters.AddWithValue("search_id", searchId);
        
        var result = new List<MatchResult>();
        
        await using var reader = await command.ExecuteReaderAsync();
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
        await using var command = _dataSource.CreateCommand(
            "SELECT * FROM searches ORDER BY timestamp DESC LIMIT 100");
        
        var result = new List<SearchRecord>();
        
        await using var reader = await command.ExecuteReaderAsync();
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