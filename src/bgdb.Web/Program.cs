using Amazon.Runtime;
using Amazon.S3;
using bgdb.Common;
using bgdb.Common.Import;
using bgdb.Common.Repositories;
using bgdb.Common.Services;
using bgdb.Common.Storages;
using bgdb.Web.Endpoints;
using bgdb.Web.Middlewares;
using Microsoft.AspNetCore.DataProtection;
using Npgsql;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace bgdb.Web;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddRazorPages();
        builder.Services.AddControllers();
        
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(Settings.ConnectionString);
        dataSourceBuilder.UseVector();
        var dataSource = dataSourceBuilder.Build();
        builder.Services.AddSingleton(dataSource);
        
        builder.Services.AddSingleton(sp => 
            new ImageEmbedder(Settings.ModelPath, sp.GetRequiredService<ILogger<ImageEmbedder>>()));
        
        builder.Services.AddSingleton<ImportWorker>();
        
        var storageKind = Enum.Parse<StorageKind>(Settings.StorageKind);
        if (storageKind == StorageKind.S3)
        {
            builder.Services.AddSingleton<IAmazonS3>(_ =>
            {
                var credentials = new BasicAWSCredentials(
                    Settings.S3AccessKey,
                    Settings.S3SecretKey);

                var config = new AmazonS3Config
                {
                    ServiceURL = Settings.S3ServiceUrl,
                    ForcePathStyle = true
                };

                return new AmazonS3Client(credentials, config);
            });
        
            builder.Services.AddSingleton<IFileStorage, S3FileStorage>();
        }
        else if (storageKind == StorageKind.Files)
            builder.Services.AddSingleton<IFileStorage, FileStorage>();
        
        builder.Services.AddSingleton<ImageStorage>();
        
        builder.Services.AddSingleton<ImageRepository>();
        builder.Services.AddSingleton<SearchRepository>();
        
        builder.Services.AddSingleton<ImageSearchService>();
        builder.Services.AddSingleton<StatsService>();
        
        builder.Services.AddRateLimiter();
        builder.Services.AddResponseCaching();
        
        builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(Settings.KeysPath))
            .SetApplicationName("bgdb");

        builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(resource =>
                resource.AddService("bgdb"))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(Telemetry.SourceName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter();
            });
        
        builder.Logging.AddOpenTelemetry(options =>
        {
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;

            options.AddOtlpExporter();
        });

        builder.WebHost.ConfigureKestrel(opt =>
        {
            opt.Limits.MaxRequestBodySize = 5 * 1024 * 1024;
        });

        builder.Services.AddCors();

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        app.UseExceptionHandler("/Error");
        app.UseHttpsRedirection();
        app.UseResponseCaching();

        app.UseRouting();
        
        app.UseMiddleware<AdminMiddleware>();
        
        app.UseAuthorization();

        app.MapImageEndpoints();
        app.MapSearchEndpoints();

        app.MapStaticAssets();
        app.MapRazorPages()
            .WithStaticAssets();

        app.MapControllers();

        var worker = app.Services.GetRequiredService<ImportWorker>();
        Task.Run(async () => await worker.RunAsync());

        app.Run();
    }
}