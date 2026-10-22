using LeviathanText.Imaging;
using LeviathanText.Recognition;
using SkiaSharp;

namespace LeviathanText.Training;

/// <summary>
/// Deterministic line-image input transform: resize (preserving aspect unless a width is
/// fixed), optional Otsu binarization, and explicit channel layout. The output is a
/// channel-major float tensor in [0,1], so callers cannot accidentally mix layouts.
/// </summary>
public static class LineInputTransform
{
    public sealed record Options
    {
        public int Height { get; init; } = CrnnModel.InputHeight;
        public int Width { get; init; } = 0;
        public int Channels { get; init; } = 1;
        public bool ForceBinarization { get; init; }
    }

    public sealed record Result(float[] Pixels, int Channels, int Width, int Height, bool Binarized)
    {
        public LineImage ToLineImage()
        {
            if (Channels != 1)
                throw new InvalidOperationException("LineImage requires a single-channel transform result.");
            return new LineImage(Height, Width, Pixels);
        }
    }

    public static Result Apply(SKBitmap source, Options? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        options ??= new Options();
        if (source.Width <= 0 || source.Height <= 0)
            throw new ArgumentException("Cannot transform an empty image.", nameof(source));
        if (options.Height < 1) throw new ArgumentOutOfRangeException(nameof(options), "Height must be positive.");
        if (options.Width < 0) throw new ArgumentOutOfRangeException(nameof(options), "Width must be non-negative.");
        if (options.Channels is not (1 or 3)) throw new ArgumentOutOfRangeException(nameof(options), "Channels must be 1 or 3.");

        int width = options.Width > 0
            ? options.Width
            : Math.Max(8, (int)Math.Round((float)source.Width * options.Height / source.Height));
        using var resized = source.Resize(new SKImageInfo(width, options.Height), new SKSamplingOptions(SKFilterMode.Linear));
        if (resized is null) throw new InvalidOperationException("Image resize failed.");

        bool binarize = options.ForceBinarization || Binarization.IsBlackWhite(source);
        float threshold = 0.5f;
        if (binarize && !Binarization.IsBlackWhite(source))
        {
            var histogram = new int[256];
            foreach (var pixel in resized.Pixels)
                histogram[(int)Math.Round(Luminance(pixel) * 255)]++;
            threshold = Binarization.Otsu(histogram, resized.Width * resized.Height) / 255f;
        }

        int plane = width * options.Height;
        var pixels = new float[options.Channels * plane];
        int p = 0;
        foreach (var pixel in resized.Pixels)
        {
            float value = Luminance(pixel);
            if (binarize) value = value <= threshold ? 0 : 1;
            if (options.Channels == 1)
            {
                pixels[p++] = value;
            }
            else
            {
                pixels[p] = pixel.Red / 255f;
                pixels[plane + p] = pixel.Green / 255f;
                pixels[(2 * plane) + p] = pixel.Blue / 255f;
                p++;
            }
        }
        return new Result(pixels, options.Channels, width, options.Height, binarize);
    }

    private static float Luminance(SKColor pixel) =>
        (0.299f * pixel.Red + 0.587f * pixel.Green + 0.114f * pixel.Blue) / 255f;
}
