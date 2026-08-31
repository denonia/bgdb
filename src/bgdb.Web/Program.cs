using bgdb.Common;
using bgdb.Common.Hashing;
using bgdb.Common.Repositories;
using bgdb.Common.Services;
using bgdb.Web.Middlewares;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.FileProviders;
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
        
        var analyzer = new ImageAnalyzer(Settings.ModelPath);
        builder.Services.AddSingleton<IImageAnalyzer>(analyzer);
        
        builder.Services.AddSingleton<Worker>();
        
        builder.Services.AddScoped<IDbSession, DbSession>();
        builder.Services.AddTransient<IImageRepository, ImageRepository>();
        builder.Services.AddTransient<ISearchRepository, SearchRepository>();
        
        builder.Services.AddTransient<IImageConversionService, ImageConversionService>();
        builder.Services.AddTransient<IImageSearchService, ImageSearchService>();
        builder.Services.AddTransient<IStatsService, StatsService>();
        
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

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(Settings.ImagePath),
            RequestPath = "/img/raw",
            ServeUnknownFileTypes = true
        });
        
        app.UseCors(opt => opt.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());

        app.UseRouting();
        
        app.UseMiddleware<AdminMiddleware>();
        
        app.UseAuthorization();

        app.MapStaticAssets();
        app.MapRazorPages()
            .WithStaticAssets();

        app.MapControllers();

        var worker = app.Services.GetRequiredService<Worker>();
        Task.Run(async () => await worker.RunAsync());

        app.Run();
    }
}