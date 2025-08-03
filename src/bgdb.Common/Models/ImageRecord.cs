namespace bgdb.Common.Models;

public class ImageRecord
{
    public ImageRecord(int mapsetId, string fileName, float[] embedding)
    {
        MapsetId = mapsetId;
        FileName = fileName;
        Embedding = embedding;
    }
    
    public int MapsetId { get; set; }
    public string FileName { get; set; }
    public float[] Embedding { get; set; }
}