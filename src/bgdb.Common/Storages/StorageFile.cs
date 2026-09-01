namespace bgdb.Common.Storages;

public class StorageFile
{
    public StorageFile(Stream content, string contentType)
    {
        Content = content;
        ContentType = contentType;
    }
    
    public Stream Content { get; init; }
    public string ContentType { get; init; }
}