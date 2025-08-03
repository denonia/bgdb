namespace bgdb.Common.Services;

public interface IImageConversionService
{
    Task GenerateSearchThumbnailAsync(string searchId, byte[] source);
    Task ConvertSearchImageAsync(string searchId, byte[] source);
}