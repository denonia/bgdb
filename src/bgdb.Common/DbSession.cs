using System.Data;
using Npgsql;

namespace bgdb.Common;

public class DbSession : IDbSession
{
    private readonly NpgsqlConnection _conn;
    
    public DbSession(NpgsqlDataSource dataSource)
    {
        _conn = dataSource.CreateConnection();
    }

    public NpgsqlConnection Connection => _conn;

    public async Task OpenAsync()
    {
        await _conn.OpenAsync();
    }

    public async Task EnsureOpenedAsync()
    {
        if (_conn.State == ConnectionState.Closed)
            await OpenAsync();
    }

    public async Task EnsureCreatedAsync()
    {
        var initSql = await File.ReadAllTextAsync(Settings.SqlInitScriptPath);

        var cmd = new NpgsqlCommand(initSql, _conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public void Dispose()
    {
        _conn.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _conn.DisposeAsync();
    }
}