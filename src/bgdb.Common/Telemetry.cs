using System.Diagnostics;

namespace bgdb.Common;

public static class Telemetry
{
    public const string SourceName = "bgdb";
    public static readonly ActivitySource ActivitySource = new(SourceName);
}