namespace bgdb.Common.Storages;

public class StorageFile
{
    public StorageFile(byte[] content, string contentType)
    {
        Content = content;
        ContentType = contentType;
    }
    
    public byte[] Content { get; init; }
    public string ContentType { get; init; }
}