namespace bgdb.Common;

public interface IImageAnalyzer
{
    IEnumerable<string> MetadataKeys { get; }

    float[] CreateEmbeddingVector(Stream imageStream);
}