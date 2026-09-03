using System.Net;

namespace bgdb.Web.Extensions;

public static class HttpRequestExtensions
{
    public static IPAddress GetRemoteIpAddress(this HttpRequest request)
    {
        var forwardedHeader = request.Headers["X-Forwarded-For"].FirstOrDefault();
        var ipStr = forwardedHeader?.Split(',').FirstOrDefault()?.Trim();
        return (ipStr is null ? request.HttpContext.Connection.RemoteIpAddress : IPAddress.Parse(ipStr))!;
    }
}