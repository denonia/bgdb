using bgdb.Common.Models;
using Npgsql;
using Pgvector;

namespace bgdb.Common.Repositories;

public class ImageRepository : IImageRepository
{
    private readonly IDbSession _dbSession;

    public ImageRepository(IDbSession dbSession)
    {
        _dbSession = dbSession;
    }

    public async Task<IList<int>> GetCompletedMapsetsAsync()
    {
        await _dbSession.EnsureOpenedAsync();
        await using var cmd = new NpgsqlCommand("SELECT DISTINCT mapset_id FROM images", _dbSession.Connection);

        var result = new List<int>();

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(reader.GetInt32(0));
        }

        return result;
    }

    public async Task<IList<ImageRecord>> GetImageRecordsAsync()
    {
        await _dbSession.EnsureOpenedAsync();
        await using var cmd = new NpgsqlCommand("SELECT mapset_id, filename FROM images", _dbSession.Connection);

        var result = new List<ImageRecord>();

        await using var reader = await cmd.ExecuteReaderAsync();
        
        // TODO: we don't need to load embeddings every time
        while (await reader.ReadAsync())
            result.Add(new ImageRecord(reader.GetInt32(0), reader.GetString(1), null));

        return result;
    }

    public async Task<IList<string>> GetMapsetImageFileNamesAsync(int mapsetId)
    {
        await _dbSession.EnsureOpenedAsync();
        await using var cmd = new NpgsqlCommand("SELECT filename FROM images WHERE mapset_id = @mapset_id", _dbSession.Connection);
        cmd.Parameters.AddWithValue("mapset_id", mapsetId);

        var result = new List<string>();

        await using var reader = await cmd.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
            result.Add(reader.GetString(0));

        return result;
    }
        
    public async Task InsertImageRecord(ImageRecord image)
    {
        await _dbSession.EnsureOpenedAsync();
        var cmd = new NpgsqlCommand("INSERT INTO images (mapset_id, filename, embedding) VALUES (@mapset_id, @filename, @embedding)", _dbSession.Connection);
        cmd.Parameters.AddWithValue("mapset_id", image.MapsetId);
        cmd.Parameters.AddWithValue("filename", image.FileName);
        cmd.Parameters.AddWithValue("embedding", new Vector(image.Embedding));
        await cmd.ExecuteNonQueryAsync(); 
    }

    public async Task InsertMapset(Mapset mapset)
    {
        await _dbSession.EnsureOpenedAsync();
        var cmd = new NpgsqlCommand("INSERT INTO mapsets (mapset_id, artist, title, creator) VALUES (@mapset_id, @artist, @title, @creator)", _dbSession.Connection);
        cmd.Parameters.AddWithValue("mapset_id", mapset.MapsetId);
        cmd.Parameters.AddWithValue("artist", mapset.Artist);
        cmd.Parameters.AddWithValue("title", mapset.Title);
        cmd.Parameters.AddWithValue("creator", mapset.Creator);
        await cmd.ExecuteNonQueryAsync(); 
    }

    public async Task<IList<MatchResult>> GetClosestMatches(float[] embedding)
    {
        var query = """
                    SELECT
                      m.mapset_id,
                      m.artist,
                      m.title,
                      m.creator,
                      i.filename,
                      1 - (i.embedding <=> @embedding) AS similarity
                    FROM images i
                    JOIN mapsets m on i.mapset_id = m.mapset_id
                    ORDER BY i.embedding <=> @embedding
                    LIMIT 20;
                    """;
        
        await _dbSession.EnsureOpenedAsync();
        await using var cmd = new NpgsqlCommand(query, _dbSession.Connection);
        cmd.Parameters.AddWithValue("embedding", new Vector(embedding));
        
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
}