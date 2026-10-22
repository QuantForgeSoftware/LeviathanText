using SkiaSharp;

namespace LeviathanText.Imaging;

/// <summary>
/// Deterministic Otsu binarization for page/line derivatives. Images that are already
/// strictly black/white are returned unchanged; empty images are rejected. This is a
/// preprocessing aid, not an evidence transformation: originals are never overwritten.
/// </summary>
public static class Binarization
{
    public static SKBitmap ToBlackWhite(SKBitmap source)
    {
        if (source.Width == 0 || source.Height == 0)
            throw new ArgumentException("Cannot binarize an empty image.", nameof(source));

        var histogram = new int[256];
        int pixels = source.Width * source.Height;
        bool onlyBlackWhite = IsBlackWhite(source);
        foreach (var pixel in source.Pixels)
            histogram[Luminance(pixel)]++;
        if (onlyBlackWhite) return source.Copy();

        int threshold = Otsu(histogram, pixels);
        var result = new SKBitmap(source.Width, source.Height);
        for (int y = 0; y < source.Height; y++)
            for (int x = 0; x < source.Width; x++)
            {
                var pixel = source.GetPixel(x, y);
                var color = Luminance(pixel) <= threshold ? SKColors.Black : SKColors.White;
                result.SetPixel(x, y, color);
            }
        return result;
    }

    /// <summary>True when every pixel is pure black or pure white on all channels.</summary>
    public static bool IsBlackWhite(SKBitmap source)
    {
        if (source.Width == 0 || source.Height == 0)
            throw new ArgumentException("Cannot inspect an empty image.", nameof(source));
        foreach (var pixel in source.Pixels)
            if (pixel.Red is not (0 or 255) || pixel.Green is not (0 or 255) || pixel.Blue is not (0 or 255))
                return false;
        return true;
    }

    public static int Otsu(IReadOnlyList<int> histogram, int total)
    {
        double sum = 0;
        for (int i = 0; i < 256; i++) sum += (double)i * histogram[i];
        double sumB = 0, weightB = 0, maxVariance = -1;
        int best = 127;
        for (int t = 1; t < 255; t++)
        {
            weightB += histogram[t];
            if (weightB == 0) continue;
            double weightF = total - weightB;
            if (weightF == 0) break;
            sumB += (double)t * histogram[t];
            double meanB = sumB / weightB;
            double meanF = (sum - sumB) / weightF;
            double variance = weightB * weightF * (meanB - meanF) * (meanB - meanF);
            if (variance > maxVariance)
            {
                maxVariance = variance;
                best = t;
            }
        }
        return best;
    }

    private static int Luminance(SKColor color) =>
        (int)Math.Round(0.299 * color.Red + 0.587 * color.Green + 0.114 * color.Blue);
}
