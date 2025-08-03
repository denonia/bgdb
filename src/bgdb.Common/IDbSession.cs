using Npgsql;

namespace bgdb.Common;

public interface IDbSession : IDisposable, IAsyncDisposable
{
    NpgsqlConnection Connection { get; }
    
    Task OpenAsync();
    Task EnsureOpenedAsync();
    Task EnsureCreatedAsync();
}