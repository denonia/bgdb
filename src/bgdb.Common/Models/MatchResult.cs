namespace bgdb.Common.Models;

public class MatchResult
{
    public MatchResult()
    {
    }
    
    public MatchResult(int mapsetId, string artist, string title, string creator, string fileName, float similarity)
    {
        MapsetId = mapsetId;
        Artist = artist;
        Title = title;
        Creator = creator;
        FileName = fileName;
        Similarity = similarity;
    }
    
    public int MapsetId { get; set; }
    public string Artist { get; set; }
    public string Title { get; set; }
    public string Creator { get; set; }
    public string FileName { get; set; }
    public float Similarity { get; set; }

    public bool IsMatch => Similarity >= 0.98f;

    public string GetThumbnailUrl(string baseUrl)
    {
        return $"{baseUrl}/{MapsetId}/{FileName}";
    }
}