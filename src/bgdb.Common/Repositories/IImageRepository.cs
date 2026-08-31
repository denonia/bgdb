using bgdb.Common.Models;

namespace bgdb.Common.Repositories;

public interface IImageRepository
{
    Task<IList<int>> GetCompletedMapsetsAsync();
    Task<IList<ImageRecord>> GetImageRecordsAsync();
    Task<IList<string>> GetMapsetImageFileNamesAsync(int mapsetId);
    Task InsertImageRecordAsync(ImageRecord image);
    Task InsertMapsetAsync(Mapset mapset);
    Task<IList<MatchResult>> GetClosestMatchesAsync(float[] embedding);
}