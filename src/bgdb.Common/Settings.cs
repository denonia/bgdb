using DotNetEnv;

namespace bgdb.Common;

public static class Settings
{
    static Settings()
    {
        Env.Load();
    }
    
    public static string BaseUrl => GetEnv("BASE_URL");
    public static string AdminToken => GetEnv("ADMIN_TOKEN");
    
    public static string KeysPath => GetEnv("KEYS_PATH");
    public static string LocalSongsPath => GetEnv("LOCAL_SONGS_PATH");
    public static string SqlInitScriptPath => GetEnv("SQL_INIT_SCRIPT_PATH");
    
    public static string StorageKind => GetEnv("STORAGE_KIND");
    
    // File storage
    public static string StoragePath => GetEnv("STORAGE_PATH");
    
    // S3 storage
    public static string S3ServiceUrl => GetEnv("S3_SERVICE_URL");
    public static string S3AccessKey => GetEnv("S3_ACCESS_KEY");
    public static string S3SecretKey => GetEnv("S3_SECRET_KEY");
    public static string S3BucketName => GetEnv("S3_BUCKET_NAME");
    
    public static string ModelPath => GetEnv("MODEL_PATH");
    public static string MirrorUrl => GetEnv("MIRROR_URL");
    public static string ConnectionString => GetEnv("CONNECTION_STRING");

    public const int ImageCacheDuration = 300;
    
    public static string GetLocalOszPath(int mapsetId) => Path.Combine(LocalSongsPath, $"{mapsetId}.osz");

    private static string GetEnv(string key) => Environment.GetEnvironmentVariable(key) 
                                                ?? throw new InvalidOperationException($"{key} environment variable not set.");
}