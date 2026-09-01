namespace bgdb.Common.Storages;

public class FileStorage : IFileStorage
{
    private readonly string _storagePath;
    
    public FileStorage()
    {
        _storagePath = Settings.StoragePath;
    }
    
    public async Task PutFileAsync(string key, string contentType, Stream content)
    {
        var path = Path.Combine(_storagePath, key);
        
        var directory = Path.GetDirectoryName(path);
        if (directory != null)
            Directory.CreateDirectory(directory);
        
        await using var fs = File.OpenWrite(path);
        await content.CopyToAsync(fs);
    }

    public async Task<IReadOnlyCollection<string>> ListFilesAsync(string prefix)
    {
        var directory = Path.Combine(_storagePath, prefix);
        return Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories)
            .Select(dir => dir[(_storagePath.Length + 1)..])
            .ToArray();
    }

    public async Task<Stream?> GetFileAsync(string key)
    {
        var path = Path.Combine(_storagePath, key);

        if (!File.Exists(path))
            return null;

        return File.OpenRead(path);
    }
}