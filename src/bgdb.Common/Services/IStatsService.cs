using bgdb.Common.Models;

namespace bgdb.Common.Services;

public interface IStatsService
{
    Task<DatabaseStats> GetDatabaseStats();
}