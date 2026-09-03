using System.Runtime.InteropServices;
using ImageMagick;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace bgdb.Common;

public class ImageEmbedder
{
    private readonly ILogger<ImageEmbedder> _logger;
    private readonly InferenceSession _session;

    private static readonly long[] InputShape = [1, 3, 224, 224];

    public ImageEmbedder(string modelPath, ILogger<ImageEmbedder> logger)
    {
        _logger = logger;

        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
        };

        AddExecutionProvider(options);

        _session = new InferenceSession(modelPath, options);
    }

    public float[] CreateEmbeddingVector(ReadOnlySpan<byte> imageBytes)
    {
        using var activity = Telemetry.ActivitySource.StartActivity();

        var inputTensor = PreprocessImage(imageBytes, 224, 224);

        using var inputValue = OrtValue.CreateTensorValueFromMemory(
            OrtMemoryInfo.DefaultInstance,
            inputTensor.Buffer,
            InputShape);

        using var results = _session.Run(
            new RunOptions(),
            ["pixel_values"],
            [inputValue],
            [_session.OutputNames[0]]);

        var output = results[0].GetTensorDataAsSpan<float>().ToArray();

        var norm = MathF.Sqrt(output.Sum(x => x * x));
        for (var i = 0; i < output.Length; i++)
            output[i] /= norm;
        return output;
    }

    private static DenseTensor<float> PreprocessImage(ReadOnlySpan<byte> imageBytes, int width, int height)
    {
        using var activity = Telemetry.ActivitySource.StartActivity();

        var magickReadSettings = new MagickReadSettings();
        magickReadSettings.SetDefine("profile:skip", "*");
        magickReadSettings.SetDefine(MagickFormat.Png, "ignore-crc", true);
        magickReadSettings.SetDefine(MagickFormat.Jpeg, "size", $"{width}x{height}");

        using var image = new MagickImage(imageBytes, magickReadSettings);

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

    private void AddExecutionProvider(SessionOptions options)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            if (TryAddProvider(() => options.AppendExecutionProvider_CoreML()))
            {
                _logger.LogInformation("Using CoreML execution provider.");
                return;
            }
        }

        var xnnpackOptions = new Dictionary<string, string>
        {
            ["intra_op_num_threads"] = Environment.ProcessorCount.ToString()
        };
        
        if (TryAddProvider(() => options.AppendExecutionProvider("XNNPACK", xnnpackOptions)))
        {
            options.IntraOpNumThreads = 1;
            options.AddSessionConfigEntry("session.intra_op.allow_spinning", "0");
            
            _logger.LogInformation("Using XNNPACK execution provider.");
            return;
        }

        _logger.LogInformation("Using CPU execution provider.");
    }

    private bool TryAddProvider(Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex) when (ex is OnnxRuntimeException or NotSupportedException)
        {
            _logger.LogWarning("Provider unavailable: {exception}", ex.Message);
        }

        return false;
    }
}