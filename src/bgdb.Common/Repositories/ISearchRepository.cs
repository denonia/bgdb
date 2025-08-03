using System.Net;
using bgdb.Common.Models;

namespace bgdb.Common.Repositories;

public interface ISearchRepository
{
    Task CreateSearchAsync(Guid searchId, IPAddress ipAddress);
    Task InsertSearchResultsAsync(Guid searchId, IList<MatchResult> results);
    Task<IList<MatchResult>> GetSearchResultsAsync(Guid searchId);
    Task<IList<SearchRecord>> GetLatestSearches();
}