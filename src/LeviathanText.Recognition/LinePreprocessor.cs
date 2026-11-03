using LeviathanText.Imaging;
using SkiaSharp;

namespace LeviathanText.Recognition;

/// <summary>
/// The shared pixel transform for training and inference. Geometry is supplied by the
/// caller; this class neither invents line bounds nor rectifies manuscript photographs.
/// </summary>
public static class LinePreprocessor
{
    public static LineImage Apply(SKBitmap crop, LinePreprocessingProfile profile)
    {
        ArgumentNullException.ThrowIfNull(crop);
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();
        if (crop.Width <= 0 || crop.Height <= 0)
            throw new ArgumentException("Cannot preprocess an empty line image.", nameof(crop));

        int width = Math.Max(8, (int)Math.Round((float)crop.Width * profile.Height / crop.Height));
        if (profile.Recipe == "luminance-local-background-v1")
            return NormalizeLocalBackground(crop, width, profile.Height);

        using var resized = crop.Resize(new SKImageInfo(width, profile.Height), new SKSamplingOptions(SKFilterMode.Linear));
        if (resized is null) throw new InvalidOperationException("Line image resize failed.");

        var pixels = new float[profile.Height * width];
        if (profile.Recipe == "luminance-grayscale")
        {
            int index = 0;
            foreach (var color in resized.Pixels)
                pixels[index++] = (0.299f * color.Red + 0.587f * color.Green + 0.114f * color.Blue) / 255f;
            return new LineImage(profile.Height, width, pixels, backgroundValue: 1f);
        }

        // Preserve the byte-based channel/luminance math of the P01 loader and the
        // original application path, including their BGRA assumption about resized
        // bytes and float rounding at threshold boundaries. The luminance recipe above
        // uses SKColor channels; changing these legacy byte reads would reinterpret old models.
        var info = resized.Info;
        var bytes = resized.Bytes;
        int[]? histogram = profile.Recipe == "p01-otsu" ? new int[256] : null;
        for (int index = 0, offset = 0; index < pixels.Length; index++, offset += info.BytesPerPixel)
        {
            byte blue = bytes[offset], green = bytes[offset + 1], red = bytes[offset + 2];
            float luminance = (0.299f * red + 0.587f * green + 0.114f * blue) / 255f;
            pixels[index] = histogram is null ? 1f - luminance : luminance;
            if (histogram is not null) histogram[(int)(luminance * 255f)]++;
        }

        if (histogram is not null)
        {
            // Deliberately retain legacy P01 floor bins, the existing Otsu algorithm
            // and strict '<'. No already-black/white shortcut is used for this recipe.
            float threshold = Binarization.Otsu(histogram, pixels.Length) / 255f;
            for (int i = 0; i < pixels.Length; i++) pixels[i] = pixels[i] < threshold ? 1f : 0f;
        }

        return new LineImage(profile.Height, width, pixels, backgroundValue: 0f);
    }

    private static LineImage NormalizeLocalBackground(SKBitmap crop, int width, int height)
    {
        const int tileWidth = 64, tileHeight = 32;
        const float denominatorFloor = .05f;
        int columns = (crop.Width + tileWidth - 1) / tileWidth;
        int rows = (crop.Height + tileHeight - 1) / tileHeight;
        var luminance = new float[crop.Width * crop.Height];
        int p = 0;
        foreach (var color in crop.Pixels)
            luminance[p++] = (0.299f * color.Red + 0.587f * color.Green + 0.114f * color.Blue) / 255f;

        var backgrounds = new float[columns * rows];
        var values = new float[tileWidth * tileHeight];
        var xCenters = new double[columns];
        var yCenters = new double[rows];
        for (int column = 0; column < columns; column++)
        {
            int left = column * tileWidth, right = Math.Min(crop.Width, left + tileWidth);
            xCenters[column] = (left + right - 1) / 2.0;
        }
        for (int row = 0; row < rows; row++)
        {
            int top = row * tileHeight, bottom = Math.Min(crop.Height, top + tileHeight);
            yCenters[row] = (top + bottom - 1) / 2.0;
            for (int column = 0; column < columns; column++)
            {
                int left = column * tileWidth, right = Math.Min(crop.Width, left + tileWidth);
                int index = 0;
                for (int y = top; y < bottom; y++)
                for (int x = left; x < right; x++) values[index++] = luminance[y * crop.Width + x];
                // Nearest-rank p90. Partial edge tiles use their actual pixels/centers.
                backgrounds[row * columns + column] = SelectNth(values.AsSpan(0, index), (int)Math.Ceiling(.9 * index) - 1);
            }
        }

        var normalized = new float[luminance.Length];
        // The horizontal interpolation brackets do not depend on the row.
        var xBrackets = new (int Lower, int Upper, double Weight)[crop.Width];
        for (int x = 0; x < crop.Width; x++) xBrackets[x] = Bracket(xCenters, x);
        for (int y = 0; y < crop.Height; y++)
        {
            var (y0, y1, ty) = Bracket(yCenters, y);
            for (int x = 0; x < crop.Width; x++)
            {
                var (x0, x1, tx) = xBrackets[x];
                double top = backgrounds[y0 * columns + x0] * (1 - tx) + backgrounds[y0 * columns + x1] * tx;
                double bottom = backgrounds[y1 * columns + x0] * (1 - tx) + backgrounds[y1 * columns + x1] * tx;
                double background = top * (1 - ty) + bottom * ty;
                normalized[y * crop.Width + x] = Math.Clamp(luminance[y * crop.Width + x] / (float)Math.Max(background, denominatorFloor), 0f, 1f);
            }
        }

        // Work in float throughout, so faint-stroke values are not requantized to 8 bits.
        // Half-pixel center mapping and clamped source edges define this recipe's resize.
        var pixels = new float[width * height];
        for (int y = 0; y < height; y++)
        {
            double sy = Math.Clamp((y + .5) * crop.Height / height - .5, 0, crop.Height - 1);
            int y0 = (int)Math.Floor(sy), y1 = Math.Min(y0 + 1, crop.Height - 1);
            double ty = sy - y0;
            for (int x = 0; x < width; x++)
            {
                double sx = Math.Clamp((x + .5) * crop.Width / width - .5, 0, crop.Width - 1);
                int x0 = (int)Math.Floor(sx), x1 = Math.Min(x0 + 1, crop.Width - 1);
                double tx = sx - x0;
                double top = normalized[y0 * crop.Width + x0] * (1 - tx) + normalized[y0 * crop.Width + x1] * tx;
                double bottom = normalized[y1 * crop.Width + x0] * (1 - tx) + normalized[y1 * crop.Width + x1] * tx;
                pixels[y * width + x] = (float)(top * (1 - ty) + bottom * ty);
            }
        }
        return new LineImage(height, width, pixels, backgroundValue: 1f);
    }

    // Exact order-statistic selection over finite RGB luminances. Three-way
    // partitioning avoids repeatedly sorting large runs of equal paper-tone values.
    private static float SelectNth(Span<float> values, int rank)
    {
        int left = 0, right = values.Length - 1;
        int partitionsRemaining = 2 * (int)Math.Log2(values.Length) + 2;
        while (left < right)
        {
            if (partitionsRemaining-- == 0)
            {
                values.Slice(left, right - left + 1).Sort();
                return values[rank];
            }
            float first = values[left], middle = values[left + (right - left) / 2], last = values[right];
            if (first > middle) (first, middle) = (middle, first);
            if (middle > last) (middle, last) = (last, middle);
            if (first > middle) middle = first;
            float pivot = middle;
            int lower = left, scan = left, upper = right;
            while (scan <= upper)
            {
                if (values[scan] < pivot)
                {
                    (values[lower], values[scan]) = (values[scan], values[lower]);
                    lower++; scan++;
                }
                else if (values[scan] > pivot)
                {
                    (values[scan], values[upper]) = (values[upper], values[scan]);
                    upper--;
                }
                else scan++;
            }
            if (rank < lower) right = lower - 1;
            else if (rank > upper) left = upper + 1;
            else return pivot;
        }
        return values[left];
    }

    private static (int Lower, int Upper, double Weight) Bracket(double[] centers, double position)
    {
        if (position <= centers[0]) return (0, 0, 0);
        int last = centers.Length - 1;
        if (position >= centers[last]) return (last, last, 0);
        int upper = Array.BinarySearch(centers, position);
        if (upper >= 0) return (upper, upper, 0);
        upper = ~upper;
        int lower = upper - 1;
        return (lower, upper, (position - centers[lower]) / (centers[upper] - centers[lower]));
    }
}
