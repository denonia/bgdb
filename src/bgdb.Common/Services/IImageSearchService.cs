using System.Net;

namespace bgdb.Common.Services;

public interface IImageSearchService
{
    Task<Guid> CreateSearchAsync(Stream stream, IPAddress requesterIp);
}