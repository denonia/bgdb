using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Caching.Memory;

namespace bgdb.Common.Storages;

public class S3FileStorage : IFileStorage
{
    private readonly IAmazonS3 _s3Client;
    private readonly IMemoryCache _memoryCache;
    private readonly string _bucketName;
    
    private readonly TimeSpan _entryCacheDuration = TimeSpan.FromMinutes(5);

    public S3FileStorage(IAmazonS3 s3Client, IMemoryCache memoryCache)
    {
        _s3Client = s3Client;
        _memoryCache = memoryCache;
        _bucketName = Settings.S3BucketName;
    }
    
    public async Task PutFileAsync(string key, string contentType, byte[] content)
    {
        _memoryCache.Set(key, content, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _entryCacheDuration
        });

        using var ms = new MemoryStream(content);
        await _s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = ms,
            ContentType = contentType,
            DisablePayloadSigning = true,
            DisableDefaultChecksumValidation = true
        });
    }
    
    public async Task<IReadOnlyCollection<string>> ListFilesAsync(string prefix)
    {
        var paginator = _s3Client.Paginators.ListObjectsV2(new ListObjectsV2Request
        {
            BucketName = _bucketName,
            Prefix = prefix
        });

        return await paginator.S3Objects
            .Select(x => x.Key)
            .ToArrayAsync();
    }

    public async Task<byte[]?> GetFileAsync(string key)
    {
        if (_memoryCache.TryGetValue(key, out byte[]? result))
            return result;
        
        try
        {
            var stream = await _s3Client.GetObjectStreamAsync(_bucketName, key,
                new Dictionary<string, object>());
            
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            return ms.ToArray();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}