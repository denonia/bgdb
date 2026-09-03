using bgdb.Common.Models;

namespace bgdb.Web.Endpoints.Responses;

public class SearchResponse
{
    public required Guid SearchId { get; init; }
    public required IEnumerable<MatchResultResponse> Results { get; init; }
}

public class GetSearchResponse
{
    public required IEnumerable<MatchResultResponse> Results { get; init; }
}

public class MatchResultResponse
{
    public required int MapsetId { get; init; }
    public required string Artist { get; init; }
    public required string Title { get; init; }
    public required string Creator { get; init; }
    public required string FileName { get; init; }
    public required float Similarity { get; init; }

    public static MatchResultResponse FromEntity(MatchResult result)
    {
        return new MatchResultResponse
        {
            Artist = result.Artist,
            Creator = result.Creator,
            FileName = result.FileName,
            MapsetId = result.MapsetId,
            Similarity = result.Similarity,
            Title = result.Title
        };
    }
}
