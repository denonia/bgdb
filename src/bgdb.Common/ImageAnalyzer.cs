using ImageMagick;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

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
        using var activity = Telemetry.ActivitySource.StartActivity();
        
        var inputTensor = PreprocessImage(imageStream, 224, 224);
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("pixel_values", inputTensor)
        };

        using var results = _session.Run(inputs);
        var output = results[0].AsEnumerable<float>().ToArray();

        var norm = MathF.Sqrt(output.Sum(x => x * x));
        for (var i = 0; i < output.Length; i++)
            output[i] /= norm;
        return output;
    }

    private static DenseTensor<float> PreprocessImage(Stream imageStream, int width, int height)
    {
        using var activity = Telemetry.ActivitySource.StartActivity();

        var magickReadSettings = new MagickReadSettings();
        magickReadSettings.SetDefine("profile:skip", "*");
        magickReadSettings.SetDefine(MagickFormat.Png, "ignore-crc", true);
        magickReadSettings.SetDefine(MagickFormat.Jpeg, "size", $"{width}x{height}");

        using var image = new MagickImage(imageStream, magickReadSettings);
        
        if (image.HasAlpha)
            image.Alpha(AlphaOption.Off);
        if (image.ColorSpace != ColorSpace.sRGB)
            image.ColorSpace = ColorSpace.sRGB;

        image.FilterType = FilterType.Triangle;
        image.Resize(new MagickGeometry((uint)width, (uint)height)
        {
            IgnoreAspectRatio = true
        });

        var tensor = new DenseTensor<float>([1, 3, height, width]);

        unsafe
        {
            using var pixels = image.GetPixelsUnsafe();

            var channels = image.ChannelCount;
            var data = (byte*)pixels.GetAreaPointer(0, 0, (uint)width, (uint)height);

            for (var y = 0; y < height; y++)
            {
                var rowOffset = y * height * channels;
                for (var x = 0; x < width; x++)
                {
                    var offset = rowOffset + x * channels;

                    var r = data[offset + 0];
                    var g = data[offset + 1];
                    var b = data[offset + 2];

                    tensor[0, 0, y, x] = r / 255.0f;
                    tensor[0, 1, y, x] = g / 255.0f;
                    tensor[0, 2, y, x] = b / 255.0f;
                }
            }
        }

        return tensor;
    }
}