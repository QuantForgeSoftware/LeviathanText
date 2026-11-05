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

        /// <summary>
        /// Opt-in two-column split on a clear vertical gutter. The page is banded per column
        /// and ordered left-to-right; without a credible gutter (or with fewer than two lines
        /// on either side) the result is the unchanged single-column baseline. This is a
        /// geometric helper, not a learned region model or a rotated-layout claim.
        /// </summary>
        public bool DetectColumns { get; init; }

        /// <summary>When columns are detected, order the right column before the left (RTL).</summary>
        public bool RightToLeftColumns { get; init; }
    }

    public static LayoutResult Detect(SKBitmap page, Options? options = null)
    {
        options ??= new Options();
        var raster = new LayoutRaster(page);
        int width = raster.Width, height = raster.Height;
        int pixelSize = raster.BytesPerPixel, rowBytes = raster.RowBytes;
        int rowPadding = rowBytes - width * pixelSize;

        // 1) Per-page Otsu binarization (fixed thresholds fail across captures).
        int sampleStride = Math.Max(1, (width * height) / 200_000);
        var histogram = new int[256];
        long sampled = 0;
        for (int p = 0; p < width * height; p += sampleStride)
        {
            int offset = p * pixelSize;
            if (rowPadding != 0)
                offset += (p / width) * rowPadding;
            float luminance = raster.Luminance(offset);
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
        var (contentLeft, contentTop, contentRight, contentBottom) = RowFeatureExtractor.ContentBounds(raster);
        int trimX = (int)((contentRight - contentLeft) * 0.02), trimY = (int)((contentBottom - contentTop) * 0.02);
        int areaLeft = contentLeft + trimX, areaRight = contentRight - trimX;
        int areaTop = contentTop + trimY, areaBottom = contentBottom - trimY;

        // 3)–5) Row bands. Banding is a function of an x-range so the same measured
        // procedure runs over the whole page or over each side of an opt-in column gutter.
        int nextLineId = 0;
        List<DetectedLine> ExtractBands(int xLeft, int xRight)
        {
            var rowDensity = new double[height];
            for (int row = areaTop; row < areaBottom; row++)
            {
                int ink = 0;
                int offset = row * rowBytes + xLeft * pixelSize;
                for (int col = xLeft; col < xRight; col++, offset += pixelSize)
                    if (raster.Luminance(offset) < inkThreshold) ink++;
                rowDensity[row] = (double)ink / Math.Max(1, xRight - xLeft);
            }

            // Smooth (moving average) to bridge intraline gaps and suppress speck noise.
            int smoothWindow = Math.Max(1, height / 150);
            var smoothed = MovingAverage(rowDensity, smoothWindow);

            // Bands split at rows below an adaptive threshold between the LOCAL BACKGROUND
            // and the local peak. (Peak-only fractions fail on real scans: the parchment
            // texture floor is ~1/3 of the letter peaks, so peak*0.15 < floor and every
            // row looks like text.)
            int localWindow = Math.Max(20, height / 12);
            var bands = new List<DetectedLine>();
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
                        var (left, right, density) = BandExtent(raster, start, end, inkThreshold, xLeft, xRight);
                        if (right > left)
                            bands.Add(new DetectedLine
                            {
                                Id = new EntityId(EntityKind.Region, $"lay{nextLineId++:x4}"),
                                Top = start,
                                Bottom = end,
                                Left = left,
                                Right = right,
                                OrderIndex = bands.Count,
                                InkDensity = density,
                            });
                    }
                    start = -1;
                }
            }
            return bands;
        }

        List<DetectedLine> lines;
        int? columnBoundary = options.DetectColumns
            ? FindColumnBoundary(raster, areaLeft, areaRight, areaTop, areaBottom, inkThreshold)
            : null;
        if (columnBoundary is { } boundary)
        {
            var left = ExtractBands(areaLeft, boundary);
            var right = ExtractBands(boundary, areaRight);
            lines = left.Count >= 2 && right.Count >= 2
                ? options.RightToLeftColumns ? [.. right, .. left] : [.. left, .. right]
                : ExtractBands(areaLeft, areaRight);
        }
        else
        {
            lines = ExtractBands(areaLeft, areaRight);
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
        var method = columnBoundary is null ? nameof(ProjectionProfileLayout) : $"{nameof(ProjectionProfileLayout)}-columns";
        return new LayoutResult(lines, method, parametersHash);
    }

    /// <summary>
    /// Find a credible two-column gutter from column-ink density. Returns the gutter midpoint
    /// only when a low-ink run is wide enough, lies away from the margins, and both sides carry
    /// ink; otherwise null (the single-column path is unchanged).
    /// </summary>
    private static int? FindColumnBoundary(LayoutRaster raster,
        int areaLeft, int areaRight, int areaTop, int areaBottom, float inkThreshold)
    {
        int width = areaRight - areaLeft;
        if (width < 80) return null;
        var density = new double[width];
        int rowBytes = raster.RowBytes, pixelSize = raster.BytesPerPixel;
        for (int x = 0; x < width; x++)
        {
            int ink = 0;
            int offset = areaTop * rowBytes + (areaLeft + x) * pixelSize;
            for (int row = areaTop; row < areaBottom; row++, offset += rowBytes)
                if (raster.Luminance(offset) < inkThreshold) ink++;
            density[x] = (double)ink / Math.Max(1, areaBottom - areaTop);
        }

        var positive = density.Where(v => v > 0).OrderBy(v => v).ToList();
        if (positive.Count == 0) return null;
        double reference = positive[positive.Count * 3 / 4];
        double gutterThreshold = Math.Max(0.002, reference * 0.10);
        int minRun = Math.Max(4, (int)(width * 0.025));
        int leftInk = 0, rightInk = 0, totalInk = 0;
        foreach (var value in density) if (value > gutterThreshold) totalInk++;

        int bestStart = -1, bestLength = 0;
        int runStart = -1;
        for (int x = 0; x <= width; x++)
        {
            bool clear = x < width && density[x] <= gutterThreshold;
            if (clear && runStart < 0) runStart = x;
            if ((!clear || x == width) && runStart >= 0)
            {
                int length = x - runStart;
                double center = (runStart + x) / 2.0 / width;
                if (length >= minRun && length > bestLength && center >= 0.30 && center <= 0.70)
                {
                    bestLength = length;
                    bestStart = runStart;
                }
                runStart = -1;
            }
        }
        if (bestStart < 0) return null;

        // Both sides must carry real ink, not just scan noise or a single speck.
        for (int x = 0; x < bestStart; x++) if (density[x] > gutterThreshold) leftInk++;
        for (int x = bestStart + bestLength; x < width; x++) if (density[x] > gutterThreshold) rightInk++;
        if (totalInk == 0 || leftInk < totalInk * 0.15 || rightInk < totalInk * 0.15) return null;
        return areaLeft + bestStart + bestLength / 2;
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
        LayoutRaster raster, int top, int bottom, float inkThreshold, int minX, int maxX)
    {
        int rowBytes = raster.RowBytes, pixelSize = raster.BytesPerPixel;
        int left = maxX, right = minX;
        long inkPixels = 0;

        for (int row = top; row < bottom; row++)
        {
            int offset = row * rowBytes + minX * pixelSize;
            for (int col = minX; col < maxX; col++, offset += pixelSize)
            {
                if (raster.Luminance(offset) < inkThreshold)
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
                $"{options.InkThreshold:R}|{options.MinRowDensity:R}|{options.MinLineHeight}|{options.ValleyFraction:R}|filter:{options.FilterImplausibleLines}|columns:{options.DetectColumns}|rtl:{options.RightToLeftColumns}|otsu:{resolvedInkThreshold:R}"))).ToLowerInvariant();
}
