using System.Net;

namespace bgdb.Common.Models;

public class SearchRecord
{
    public Guid SearchId { get; set; }
    public IPAddress IpAddress { get; set; }
    public DateTime Timestamp { get; set; }
}