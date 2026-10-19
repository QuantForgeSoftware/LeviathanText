using SkiaSharp;

namespace LeviathanText.Layout;

/// <summary>
/// Per-row features from a page's ink profile, shared by training and inference so the
/// learned detector sees exactly the same representation in both phases.
/// </summary>
public static class RowFeatureExtractor
{
    public sealed record PageProfile(
        float[] RowDensity,
        int Width,
        int Height,
        float InkThreshold,
        int[] InkRuns = null!,
        int[] MaxRun = null!);

    public const int FeatureCount = 13;

    /// <summary>
    /// Bounding box of the bright page area (parchment/paper), excluding the dark
    /// surround common in photographic scans. Falls back to the full image when no
    /// clear content region exists.
    /// </summary>
    public static (int Left, int Top, int Right, int Bottom) ContentBounds(SKBitmap page)
    {
        int width = page.Width, height = page.Height;
        var info = page.Info;
        var bytes = page.Bytes;
        int stride = Math.Max(1, Math.Min(width, height) / 400); // subsample rows/cols

        var luminance = new float[height, width];
        double total = 0;
        for (int y = 0; y < height; y += stride)
            for (int x = 0; x < width; x += stride)
            {
                int b = (y * width + x) * info.BytesPerPixel;
                byte blue = bytes[b], green = bytes[b + 1], red = bytes[b + 2];
                float l = (0.299f * red + 0.587f * green + 0.114f * blue) / 255f;
                luminance[y, x] = l;
                total += l;
            }
        int samples = ((height + stride - 1) / stride) * ((width + stride - 1) / stride);
        float mean = (float)(total / Math.Max(1, samples));

        int top = height, bottom = 0, left = width, right = 0;
        for (int y = 0; y < height; y += stride)
            for (int x = 0; x < width; x += stride)
            {
                if (luminance[y, x] <= mean) continue;
                // only count bright pixels well inside a bright neighborhood (avoid glare specks)
                if (y < top) top = y;
                if (y > bottom) bottom = y;
                if (x < left) left = x;
                if (x > right) right = x;
            }

        // Require a plausible page region; otherwise keep the full frame.
        if (bottom - top < height * 0.2 || right - left < width * 0.2)
            return (0, 0, width, height);

        // Small inward margin so the black surround does not leak into the profile.
        int marginY = Math.Max(2, (bottom - top) / 100);
        int marginX = Math.Max(2, (right - left) / 100);
        return (Math.Max(0, left + marginX), Math.Max(0, top + marginY),
                Math.Min(width, right - marginX), Math.Min(height, bottom - marginY));
    }

    public static PageProfile Profile(SKBitmap page)
    {
        int width = page.Width, height = page.Height;
        var info = page.Info;
        var bytes = page.Bytes;
        var (left, top, right, bottom) = ContentBounds(page);

        // Otsu within the content region (the surround would otherwise dominate).
        int sampleStride = Math.Max(1, ((right - left) * (bottom - top)) / 200_000);
        var histogram = new int[256];
        long sampled = 0;
        for (int y = top; y < bottom; y++)
            for (int x = left; x < right; x += sampleStride)
            {
                int b = (y * width + x) * info.BytesPerPixel;
                byte blue = bytes[b], green = bytes[b + 1], red = bytes[b + 2];
                float l = (0.299f * red + 0.587f * green + 0.114f * blue) / 255f;
                histogram[(int)(Math.Clamp(l, 0f, 1f) * 255f)]++;
                sampled++;
            }
        int otsu = ProjectionProfileLayout.Otsu(histogram, (int)sampled);
        // Paper-relative threshold: faint manuscript letters sit ~0.1-0.25 below the
        // parchment tone, while global Otsu lands between parchment and dark borders
        // (measured: letters at 0.60-0.75 vs paper 0.90 on real pages).
        float paper = Percentile(histogram, (int)sampled, 0.95);
        float paperRelative = Math.Clamp(paper - 0.12f, 0.05f, 0.90f);
        float inkThreshold = paperRelative;

        var density = new float[height];
        var runs = new int[height];
        var maxRun = new int[height];
        for (int y = top; y < bottom; y++)
        {
            int ink = 0;
            int run = 0;
            for (int x = left; x < right; x++)
            {
                int b = (y * width + x) * info.BytesPerPixel;
                byte blue = bytes[b], green = bytes[b + 1], red = bytes[b + 2];
                float l = (0.299f * red + 0.587f * green + 0.114f * blue) / 255f;
                bool isInk = l < inkThreshold;
                if (isInk)
                {
                    ink++;
                    run++;
                    if (run > maxRun[y]) maxRun[y] = run;
                }
                else
                {
                    if (run > 0) runs[y]++;
                    run = 0;
                }
            }
            if (run > 0) runs[y]++;
            density[y] = (float)ink / Math.Max(1, right - left);
        }

        return new PageProfile(density, width, height, inkThreshold, runs, maxRun);
    }

    /// <summary>
    /// Ink bounds and density for a horizontal band, restricted to [minX, maxX). Uses the
    /// given ink threshold (paper-relative) so callers can reuse one page calibration.
    /// </summary>
    public static (int Left, int Right, double Density) InkBounds(
        SKBitmap page, int top, int bottom, float inkThreshold, int minX, int maxX)
    {
        int width = page.Width;
        var info = page.Info;
        var bytes = page.Bytes;
        int left = maxX, right = minX;
        long inkPixels = 0;

        for (int row = Math.Max(0, top); row < Math.Min(page.Height, bottom); row++)
        {
            for (int col = Math.Max(0, minX); col < Math.Min(width, maxX); col++)
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
        if (right < left) return (minX, maxX, density);
        return (left, right, density);
    }

    /// <summary>Luminance percentile from a 256-bin histogram (bin center / 255).</summary>
    public static float Percentile(int[] histogram, int total, double percentile)
    {
        if (total <= 0) return 0f;
        long target = (long)Math.Ceiling(total * percentile);
        long seen = 0;
        for (int i = 0; i < 256; i++)
        {
            seen += histogram[i];
            if (seen >= target) return i / 255f;
        }
        return 1f;
    }

    /// <summary>Feature vector for one row: local densities, scale-relative windows, gradient and contrast.</summary>
    public static float[] Features(PageProfile profile, int row)
    {
        var d = profile.RowDensity;
        int n = d.Length;

        // Window radii scale with page height: text lines are a few percent of the page,
        // so fixed pixel windows (~3-8 px) cannot see line structure on large scans.
        // Radii stay BELOW one line height (lines ≈ 3–6% of page height): the wide
        // window must see the gap on both sides, not neighboring lines.
        int narrow = Math.Max(1, n / 400);
        int bandWidth = Math.Max(2, n / 100);
        int wide = Math.Max(4, n / 60);
        int context = Math.Max(4, n / 30);

        float At(int delta) => row + delta >= 0 && row + delta < n ? d[row + delta] : 0f;

        float meanWindow(int radius)
        {
            double sum = 0;
            int count = 0;
            for (int i = Math.Max(0, row - radius); i <= Math.Min(n - 1, row + radius); i++)
            {
                sum += d[i];
                count++;
            }
            return (float)(sum / Math.Max(1, count));
        }

        float maxWindow(int radius)
        {
            float max = 0;
            for (int i = Math.Max(0, row - radius); i <= Math.Min(n - 1, row + radius); i++)
                max = Math.Max(max, d[i]);
            return max;
        }

        float minWindow(int radius)
        {
            float min = float.MaxValue;
            for (int i = Math.Max(0, row - radius); i <= Math.Min(n - 1, row + radius); i++)
                min = Math.Min(min, d[i]);
            return min == float.MaxValue ? 0f : min;
        }

        float runMean = 0;
        if (profile.InkRuns is not null)
        {
            double sum = 0;
            int count = 0;
            for (int i = Math.Max(0, row - bandWidth); i <= Math.Min(n - 1, row + bandWidth); i++)
            {
                sum += profile.InkRuns[i];
                count++;
            }
            runMean = (float)(sum / Math.Max(1, count));
        }

        return
        [
            At(0),
            At(-narrow), At(narrow),
            At(-bandWidth), At(bandWidth),
            meanWindow(bandWidth),
            meanWindow(wide),
            maxWindow(context),
            minWindow(context),
            At(0) - meanWindow(wide), // contrast to the local background
            // Column texture (stroke structure): many short runs inside letter rows,
            // few long runs in gaps. Normalized by page width to be scale-free.
            profile.InkRuns is null ? 0f : profile.InkRuns[row] * 1000f / Math.Max(1, profile.Width),
            profile.MaxRun is null ? 0f : profile.MaxRun[row] / (float)Math.Max(1, profile.Width),
            runMean * 1000f / Math.Max(1, profile.Width),
        ];
    }

    /// <summary>Ground-truth row labels from line boxes (1 = inside a text band).</summary>
    public static bool[] Labels(int height, IReadOnlyList<(int Top, int Bottom)> bands)
    {
        var labels = new bool[height];
        foreach (var (top, bottom) in bands)
            for (int row = Math.Max(0, top); row < Math.Min(height, bottom); row++)
                labels[row] = true;
        return labels;
    }
}
