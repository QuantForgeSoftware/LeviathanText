using LeviathanText.Contracts;
using SkiaSharp;

namespace LeviathanText.Imaging;

/// <summary>
/// A bounded center-normalization baseline for one text line. It estimates a vertical
/// ink centroid per column, smooths that centerline, and shifts each column so the line
/// is centered in a fixed-height output. This removes low-frequency vertical drift; it
/// does not estimate a full 2-D page dewarp and it is not enabled in recognition by
/// default.
/// </summary>
public static class LineDewarper
{
    public sealed record Options
    {
        public int TargetHeight { get; init; } = 64;

        /// <summary>Moving-average radius in columns; 0 means no smoothing.</summary>
        public int SmoothingRadius { get; init; } = 4;
    }

    public static SKBitmap Dewarp(SKBitmap source, Options? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        options ??= new Options();
        if (source.Width <= 0 || source.Height <= 0)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = "dewarp",
                Message = "Cannot dewarp an empty line image.",
            });
        if (options.TargetHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Target height must be positive.");

        for (int y = 0; y < source.Height; y++)
            for (int x = 0; x < source.Width; x++)
            {
                var pixel = source.GetPixel(x, y);
                if (pixel.Red != pixel.Green || pixel.Green != pixel.Blue)
                    throw new LeviathanException(new LeviathanError
                    {
                        Code = ErrorCode.StageContractViolation,
                        Scope = "dewarp",
                        Message = "Line dewarping supports black/white or grayscale input, not color.",
                    });
            }

        var centers = InkCenters(source);
        var filled = FillAndSmooth(centers, options.SmoothingRadius);
        double targetCenter = (options.TargetHeight - 1) / 2.0;

        var output = new SKBitmap(source.Width, options.TargetHeight);
        output.Erase(SKColors.White);
        for (int x = 0; x < source.Width; x++)
        {
            int shift = (int)Math.Round(targetCenter - filled[x]);
            for (int y = 0; y < options.TargetHeight; y++)
            {
                int sourceY = y - shift;
                if (sourceY < 0 || sourceY >= source.Height) continue;
                output.SetPixel(x, y, source.GetPixel(x, sourceY));
            }
        }
        return output;
    }

    /// <summary>Per-column dark-pixel centroid, or NaN for a column with no ink.</summary>
    public static double[] InkCenters(SKBitmap source)
    {
        var centers = new double[source.Width];
        for (int x = 0; x < source.Width; x++)
        {
            double weighted = 0, total = 0;
            for (int y = 0; y < source.Height; y++)
            {
                var pixel = source.GetPixel(x, y);
                double darkness = 1.0 - (0.299 * pixel.Red + 0.587 * pixel.Green + 0.114 * pixel.Blue) / 255.0;
                if (darkness <= 0.01) continue;
                weighted += y * darkness;
                total += darkness;
            }
            centers[x] = total > 0 ? weighted / total : double.NaN;
        }
        return centers;
    }

    private static double[] FillAndSmooth(double[] centers, int radius)
    {
        var result = (double[])centers.Clone();
        int first = Array.FindIndex(result, value => !double.IsNaN(value));
        if (first < 0)
        {
            Array.Fill(result, 0);
            return result;
        }

        for (int x = 0; x < first; x++) result[x] = result[first];
        for (int x = first + 1; x < result.Length; x++)
        {
            if (!double.IsNaN(result[x])) continue;
            int next = x + 1;
            while (next < result.Length && double.IsNaN(result[next])) next++;
            if (next >= result.Length)
            {
                result[x] = result[x - 1];
                continue;
            }
            double left = result[x - 1], right = result[next];
            int gap = next - (x - 1);
            result[x] = left + (right - left) * (x - (x - 1)) / gap;
        }

        if (radius <= 0) return result;
        var smoothed = new double[result.Length];
        for (int x = 0; x < result.Length; x++)
        {
            double sum = 0;
            int count = 0;
            for (int k = -radius; k <= radius; k++)
            {
                int index = x + k;
                if (index < 0 || index >= result.Length) continue;
                sum += result[index];
                count++;
            }
            smoothed[x] = sum / count;
        }
        return smoothed;
    }
}
