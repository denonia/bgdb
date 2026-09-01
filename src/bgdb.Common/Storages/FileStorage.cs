namespace bgdb.Common.Storages;

public class FileStorage : IFileStorage
{
    private readonly string _storagePath;
    
    public FileStorage()
    {
        _storagePath = Settings.StoragePath;
    }
    
    public async Task PutFileAsync(string key, string contentType, byte[] content)
    {
        var path = Path.Combine(_storagePath, key);
        
        var directory = Path.GetDirectoryName(path);
        if (directory != null)
            Directory.CreateDirectory(directory);
        
        await File.WriteAllBytesAsync(path, content);
    }

    public async Task<IReadOnlyCollection<string>> ListFilesAsync(string prefix)
    {
        var directory = Path.Combine(_storagePath, prefix);
        return Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories)
            .Select(dir => dir[(_storagePath.Length + 1)..])
            .ToArray();
    }

    public async Task<byte[]?> GetFileAsync(string key)
    {
        var path = Path.Combine(_storagePath, key);

        if (!File.Exists(path))
            return null;

        return await File.ReadAllBytesAsync(path);
    }
}