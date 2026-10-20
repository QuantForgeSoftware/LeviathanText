using LeviathanText.Contracts;
using SkiaSharp;

namespace LeviathanText.Layout;

/// <summary>
/// A detected layout element in one coordinate frame (project-plan §4: regions, baselines,
/// reading order). Reading order is a validated linear order for this module; the graph
/// model with alternatives arrives with the trainable order module.
/// </summary>
public sealed record DetectedLine
{
    public required EntityId Id { get; init; }
    public required int Top { get; init; }
    public required int Bottom { get; init; }
    public required int Left { get; init; }
    public required int Right { get; init; }
    public int OrderIndex { get; init; }

    /// <summary>Mean ink density inside the band (0..1) — a bounded, defined heuristic signal, not scholarly certainty.</summary>
    public double InkDensity { get; init; }

    public int Height => Bottom - Top;

    public override string ToString() => $"line[{Top}..{Bottom}] x[{Left}..{Right}] density={InkDensity:0.###}";
}

public sealed record LayoutResult(IReadOnlyList<DetectedLine> Lines, string Method, string ParametersHash);

/// <summary>
/// Automatic layout detection, first module: horizontal projection-profile line
/// segmentation on a binarized page (todo Phase 1: baseline/layout inference — the
/// smallest honest baseline for single-column documentary pages).
///
/// Contract notes:
/// - Confidence is <see cref="Confidence.None"/> for now: the projection heuristic has
///   no probability semantics; fabricating one is forbidden (domain-and-pipeline §4).
/// - Multi-column, curved and marginal layouts are NOT handled and are recorded as
///   unsupported conditions, not silently mis-segmented.
/// </summary>
public static class ProjectionProfileLayout
{
    public sealed record Options
    {
        /// <summary>Binarization threshold (luminance); ink is darker.</summary>
        public float InkThreshold { get; init; } = 0.55f;

        /// <summary>A band is a line candidate only when its peak row density exceeds this.</summary>
        public float MinRowDensity { get; init; } = 0.02f;

        /// <summary>Minimum band height in pixels (rejects specks).</summary>
        public int MinLineHeight { get; init; } = 6;

        /// <summary>Rows below this density split bands (valley threshold as fraction of peak).</summary>
        public float ValleyFraction { get; init; } = 0.15f;

        /// <summary>
        /// Drop implausibly faint bands: density below half the page's median band density.
        /// Dev-selected on M02a dev pages (later reported on held-out pages: precision
        /// 0.335→0.381, recall unchanged). Disable to reproduce the unfiltered baseline.
        /// </summary>
        public bool FilterImplausibleLines { get; init; } = true;
    }

    public static LayoutResult Detect(SKBitmap page, Options? options = null)
    {
        options ??= new Options();
        int width = page.Width, height = page.Height;
        var info = page.Info;
        var bytes = page.Bytes;

        // 1) Per-page Otsu binarization (fixed thresholds fail across captures).
        int sampleStride = Math.Max(1, (width * height) / 200_000);
        var histogram = new int[256];
        long sampled = 0;
        for (int p = 0; p < width * height; p += sampleStride)
        {
            int b = p * info.BytesPerPixel;
            byte blue = bytes[b], green = bytes[b + 1], red = bytes[b + 2];
            float luminance = (0.299f * red + 0.587f * green + 0.114f * blue) / 255f;
            histogram[(int)(luminance * 255f)]++;
            sampled++;
        }
        int otsu = Otsu(histogram, (int)sampled);
        // Paper-relative threshold (see RowFeatureExtractor): faint letters are missed by
        // global Otsu on real scans, where the dark surround dominates the histogram.
        float paper = RowFeatureExtractor.Percentile(histogram, (int)sampled, 0.95);
        float inkThreshold = Math.Clamp(paper - 0.12f, 0.05f, 0.90f);

        // 2) Restrict to the bright page area first (photographic scans often have a dark
        //    surround that produces spurious bands), then trim a small inner border.
        var (contentLeft, contentTop, contentRight, contentBottom) = RowFeatureExtractor.ContentBounds(page);
        int trimX = (int)((contentRight - contentLeft) * 0.02), trimY = (int)((contentBottom - contentTop) * 0.02);
        int areaLeft = contentLeft + trimX, areaRight = contentRight - trimX;
        int areaTop = contentTop + trimY, areaBottom = contentBottom - trimY;

        // 3) Row ink densities inside the trimmed area.
        var rowDensity = new double[height];
        for (int row = areaTop; row < areaBottom; row++)
        {
            int ink = 0;
            for (int col = areaLeft; col < areaRight; col++)
            {
                int b = (row * width + col) * info.BytesPerPixel;
                byte blue = bytes[b], green = bytes[b + 1], red = bytes[b + 2];
                float luminance = (0.299f * red + 0.587f * green + 0.114f * blue) / 255f;
                if (luminance < inkThreshold) ink++;
            }
            rowDensity[row] = (double)ink / Math.Max(1, areaRight - areaLeft);
        }

        // 4) Smooth (moving average) to bridge intraline gaps and suppress speck noise.
        int smoothWindow = Math.Max(1, height / 150);
        var smoothed = MovingAverage(rowDensity, smoothWindow);

        // 5) Bands split at rows below an adaptive threshold between the LOCAL BACKGROUND
        //    and the local peak. (Peak-only fractions fail on real scans: the parchment
        //    texture floor is ~1/3 of the letter peaks, so peak*0.15 < floor and every
        //    row looks like text.)
        int localWindow = Math.Max(20, height / 12);
        var lines = new List<DetectedLine>();
        int start = -1;
        for (int row = areaTop; row <= areaBottom; row++)
        {
            double localPeak = LocalPeak(smoothed, row, localWindow, areaTop, areaBottom);
            double localFloor = LocalFloor(smoothed, row, localWindow, areaTop, areaBottom);
            double splitAt = Math.Max(options.MinRowDensity,
                localFloor + (localPeak - localFloor) * options.ValleyFraction);
            bool inBand = row < areaBottom && smoothed[row] >= splitAt;
            if (inBand && start < 0) start = row;
            if (!inBand && start >= 0)
            {
                int end = row; // exclusive
                if (end - start >= options.MinLineHeight && localPeak > 0)
                {
                    var (left, right, density) = BandExtent(page, start, end, inkThreshold, areaLeft, areaRight);
                    if (right > left)
                        lines.Add(new DetectedLine
                        {
                            Id = new EntityId(EntityKind.Region, $"lay{lines.Count:x4}"),
                            Top = start,
                            Bottom = end,
                            Left = left,
                            Right = right,
                            OrderIndex = lines.Count,
                            InkDensity = density,
                        });
                }
                start = -1;
            }
        }

        if (options.FilterImplausibleLines && lines.Count > 2)
        {
            // Post-filter: drop bands far below this page's own median ink density. A page-
            // relative rule (not an absolute threshold) keeps it usable across materials.
            var medianDensity = Median(lines.Select(l => l.InkDensity));
            if (medianDensity > 0)
            {
                lines = [.. lines
                    .Where(l => l.InkDensity >= 0.5 * medianDensity)
                    .Select((l, index) => l with { OrderIndex = index })];
            }
        }

        var parametersHash = HashOf(options, inkThreshold);
        return new LayoutResult(lines, nameof(ProjectionProfileLayout), parametersHash);
    }

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        if (sorted.Count == 0) return 0;
        return sorted.Count % 2 == 1
            ? sorted[sorted.Count / 2]
            : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2;
    }

    internal static int Otsu(int[] histogram, int total)
    {
        double sum = 0;
        for (int i = 0; i < 256; i++) sum += (double)i * histogram[i];

        double sumB = 0, wB = 0, maxVariance = -1;
        int best = 127;
        // Search interior thresholds only: t=0/t=255 are degenerate for perfectly
        // bimodal histograms (they make one class the pure black/white peak and
        // maximize variance at threshold 0, which binarizes nothing as ink).
        for (int t = 1; t < 255; t++)
        {
            wB += histogram[t];
            if (wB == 0) continue;
            double wF = total - wB;
            if (wF == 0) break;
            sumB += (double)t * histogram[t];
            double meanB = sumB / wB;
            double meanF = (sum - sumB) / wF;
            double variance = wB * wF * (meanB - meanF) * (meanB - meanF);
            if (variance > maxVariance) { maxVariance = variance; best = t; }
        }
        return best;
    }

    private static double[] MovingAverage(double[] values, int window)
    {
        var result = new double[values.Length];
        double sum = 0;
        int count = 0;
        for (int i = 0; i < values.Length; i++)
        {
            sum += values[i]; count++;
            if (i >= window) { sum -= values[i - window]; count--; }
            result[i] = sum / Math.Max(1, count);
        }
        return result;
    }

    private static double LocalPeak(double[] values, int center, int window, int low, int high)
    {
        double peak = 0;
        int from = Math.Max(low, center - window / 2);
        int to = Math.Min(high, center + window / 2);
        for (int i = from; i < to; i++) peak = Math.Max(peak, values[i]);
        return peak;
    }

    private static double LocalFloor(double[] values, int center, int window, int low, int high)
    {
        double floor = double.MaxValue;
        int from = Math.Max(low, center - window / 2);
        int to = Math.Min(high, center + window / 2);
        for (int i = from; i < to; i++) floor = Math.Min(floor, values[i]);
        return floor == double.MaxValue ? 0 : floor;
    }

    private static (int Left, int Right, double Density) BandExtent(
        SKBitmap page, int top, int bottom, float inkThreshold, int minX, int maxX)
    {
        int width = page.Width;
        var info = page.Info;
        var bytes = page.Bytes;
        int left = maxX, right = minX;
        long inkPixels = 0;

        for (int row = top; row < bottom; row++)
        {
            for (int col = minX; col < maxX; col++)
            {
                int b = (row * width + col) * info.BytesPerPixel;
                byte blue = bytes[b], green = bytes[b + 1], red = bytes[b + 2];
                float luminance = (0.299f * red + 0.587f * green + 0.114f * blue) / 255f;
                if (luminance < inkThreshold)
                {
                    if (col < left) left = col;
                    if (col > right) right = col;
                    inkPixels++;
                }
            }
        }

        double density = (double)inkPixels / Math.Max(1, (bottom - top) * Math.Max(1, maxX - minX));
        if (right < left) return (0, 0, density);
        return (left, right, density);
    }

    internal static string HashOf(Options options, float resolvedInkThreshold) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(
                $"{options.InkThreshold:R}|{options.MinRowDensity:R}|{options.MinLineHeight}|{options.ValleyFraction:R}|otsu:{resolvedInkThreshold:R}"))).ToLowerInvariant();
}
