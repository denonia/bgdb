using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace bgdb.Common;

public class ImageAnalyzer : IImageAnalyzer
{
    private readonly InferenceSession _session;

    public ImageAnalyzer(string modelPath)
    {
        _session = new InferenceSession(modelPath);
    }

    public IEnumerable<string> MetadataKeys => _session.InputMetadata.Select(m => m.Key);

    public float[] CreateEmbeddingVector(Stream imageStream)
    {
        var inputTensor = PreprocessImage(imageStream, 224, 224);
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("pixel_values", inputTensor)
        };

        using var results = _session.Run(inputs);
        var output = results[0].AsEnumerable<float>().ToArray();

        var norm = (float)Math.Sqrt(output.Sum(x => x * x));
        return output.Select(x => x / norm).ToArray();
    }

    private static DenseTensor<float> PreprocessImage(Stream imageStream, int width, int height)
    {
        using var image = Image.Load(imageStream);
        image.Mutate(x => x.Resize(width, height));
        
        using var rgbImage = image.CloneAs<Rgb24>();

        var tensor = new DenseTensor<float>(new[] { 1, 3, height, width });

        for (var y = 0; y < height; y++)
        {
            var pixelRowSpan = rgbImage.DangerousGetPixelRowMemory(y).Span;

            for (var x = 0; x < width; x++)
            {
                var pixel = pixelRowSpan[x];
                tensor[0, 0, y, x] = pixel.R / 255f;
                tensor[0, 1, y, x] = pixel.G / 255f;
                tensor[0, 2, y, x] = pixel.B / 255f;
            }
        }

        return tensor;
    }
}