using bgdb.Common;
using bgdb.Common.Hashing;
using Npgsql;
using Serilog;

namespace bgdb.Hasher;

class Program
{
    static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.File(Settings.LogPath, rollingInterval: RollingInterval.Day)
            .WriteTo.Console()
            .CreateLogger();
        
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(Settings.ConnectionString);
        dataSourceBuilder.UseVector();
        await using var dataSource = dataSourceBuilder.Build();

        var analyzer = new ImageAnalyzer(Settings.ModelPath);
        var worker = new Worker(analyzer, dataSource);
        await worker.RunAsync();
    }
}