namespace bgdb.Common.Storages;

public interface IFileStorage
{
    Task PutFileAsync(string key, string contentType, byte[] content);
    Task<IReadOnlyCollection<string>> ListFilesAsync(string prefix);
    Task<byte[]?> GetFileAsync(string key);
}
