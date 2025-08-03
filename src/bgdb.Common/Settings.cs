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
    public static string LogPath => GetEnv("LOG_PATH");
    public static string LocalSongsPath => GetEnv("LOCAL_SONGS_PATH");
    public static string ImagePath => GetEnv("IMAGE_PATH");
    public static string SearchPath => GetEnv("SEARCH_PATH");
    public static string SearchPathRaw => GetEnv("SEARCH_PATH_RAW");
    public static string SqlInitScriptPath => GetEnv("SQL_INIT_SCRIPT_PATH");
    
    public static string ModelPath => GetEnv("MODEL_PATH");
    public static string MirrorUrl => GetEnv("MIRROR_URL");
    public static string ConnectionString => GetEnv("CONNECTION_STRING");

    public const int ImageCacheDuration = 300;
    
    
    public static string GetLocalOszPath(int mapsetId) => Path.Combine(LocalSongsPath, $"{mapsetId}.osz");

    public static string GetImagePath(int mapsetId, string fileName) => Path.Combine(ImagePath,
        $"{mapsetId}_{Path.GetFileNameWithoutExtension(fileName)}.jxl");

    public static string GetSearchImagePath(string searchId) => Path.Combine(SearchPath, $"{searchId}.jxl");

    private static string GetEnv(string key) => Environment.GetEnvironmentVariable(key) 
                                                ?? throw new InvalidOperationException($"{key} environment variable not set.");
}