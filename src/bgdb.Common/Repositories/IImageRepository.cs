using bgdb.Common.Models;

namespace bgdb.Common.Repositories;

public interface IImageRepository
{
    Task<IList<int>> GetCompletedMapsetsAsync();
    Task<IList<ImageRecord>> GetImageRecordsAsync();
    Task<IList<string>> GetMapsetImageFileNamesAsync(int mapsetId);
    Task InsertImageRecord(ImageRecord image);
    Task InsertMapset(Mapset mapset);
    Task<IList<MatchResult>> GetClosestMatches(float[] embedding);
}