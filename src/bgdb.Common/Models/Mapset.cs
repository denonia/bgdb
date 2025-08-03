namespace bgdb.Common.Models;

public class Mapset
{
    public Mapset(int mapsetId, string artist, string title, string creator)
    {
        MapsetId = mapsetId;
        Artist = artist;
        Title = title;
        Creator = creator;
    }
    
    public int MapsetId { get; set; }
    public string Artist { get; set; }
    public string Title { get; set; }
    public string Creator { get; set; }
}