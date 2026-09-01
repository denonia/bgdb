using bgdb.Common.Models;
using Npgsql;
using Pgvector;

namespace bgdb.Common.Repositories;

public class ImageRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public ImageRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<IList<int>> GetCompletedMapsetsAsync()
    {
        await using var command = _dataSource.CreateCommand("SELECT DISTINCT mapset_id FROM images");

        var result = new List<int>();

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(reader.GetInt32(0));
        }

        return result;
    }

    public async Task<IList<ImageRecord>> GetImageRecordsAsync()
    {
        await using var command = _dataSource.CreateCommand("SELECT mapset_id, filename FROM images");

        var result = new List<ImageRecord>();

        await using var reader = await command.ExecuteReaderAsync();
        
        // TODO: we don't need to load embeddings every time
        while (await reader.ReadAsync())
            result.Add(new ImageRecord(reader.GetInt32(0), reader.GetString(1), null));

        return result;
    }

    public async Task<IList<string>> GetMapsetImageFileNamesAsync(int mapsetId)
    {
        await using var command = _dataSource.CreateCommand(
            "SELECT filename FROM images WHERE mapset_id = @mapset_id");
        command.Parameters.AddWithValue("mapset_id", mapsetId);

        var result = new List<string>();

        await using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
            result.Add(reader.GetString(0));

        return result;
    }
        
    public async Task InsertImageRecordAsync(ImageRecord image)
    {
        await using var command = _dataSource.CreateCommand(
            "INSERT INTO images (mapset_id, filename, embedding) VALUES (@mapset_id, @filename, @embedding)");
        command.Parameters.AddWithValue("mapset_id", image.MapsetId);
        command.Parameters.AddWithValue("filename", image.FileName);
        command.Parameters.AddWithValue("embedding", new Vector(image.Embedding));
        await command.ExecuteNonQueryAsync(); 
    }

    public async Task InsertMapsetAsync(Mapset mapset)
    {
        await using var command = _dataSource.CreateCommand(
            "INSERT INTO mapsets (mapset_id, artist, title, creator) VALUES (@mapset_id, @artist, @title, @creator)");
        command.Parameters.AddWithValue("mapset_id", mapset.MapsetId);
        command.Parameters.AddWithValue("artist", mapset.Artist);
        command.Parameters.AddWithValue("title", mapset.Title);
        command.Parameters.AddWithValue("creator", mapset.Creator);
        await command.ExecuteNonQueryAsync(); 
    }

    public async Task<IList<MatchResult>> GetClosestMatchesAsync(float[] embedding)
    {
        using var activity = Telemetry.ActivitySource.StartActivity();

        await using var command = _dataSource.CreateCommand(
            """
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
            """);
        command.Parameters.AddWithValue("embedding", new Vector(embedding));
        
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
}