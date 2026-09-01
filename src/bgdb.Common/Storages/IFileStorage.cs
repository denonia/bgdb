namespace bgdb.Common.Storages;

public interface IFileStorage
{
    Task PutFileAsync(string key, string contentType, Stream content);
    Task<IReadOnlyCollection<string>> ListFilesAsync(string prefix);
    Task<Stream?> GetFileAsync(string key);
}