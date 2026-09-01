using System.Net;
using Amazon.S3;
using Amazon.S3.Model;

namespace bgdb.Common.Storages;

public class S3FileStorage : IFileStorage
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;

    public S3FileStorage(IAmazonS3 s3Client)
    {
        _s3Client = s3Client;
        _bucketName = Settings.S3BucketName;
    }
    
    public async Task PutFileAsync(string key, string contentType, Stream content)
    {
        await _s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = content,
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

    public async Task<Stream?> GetFileAsync(string key)
    {
        try
        {
            return await _s3Client.GetObjectStreamAsync(_bucketName, key,
                new Dictionary<string, object>());
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}