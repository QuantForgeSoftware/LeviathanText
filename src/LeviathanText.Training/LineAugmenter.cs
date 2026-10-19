using LeviathanText.Recognition;

namespace LeviathanText.Training;

/// <summary>
/// Deterministic on-the-fly augmentation for line images. It changes only geometry and
/// noise, never the label, and is applied to training batches only: validation/test data
/// must stay untouched for comparable measurements.
/// </summary>
public static class LineAugmenter
{
    public static LineImage Apply(
        LineImage image,
        Random random,
        double maxHorizontalStretch = 0.1,
        int maxVerticalShiftPx = 1,
        double saltPepperFraction = 0.01)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(random);
        if (maxHorizontalStretch is < 0 or >= 1) throw new ArgumentOutOfRangeException(nameof(maxHorizontalStretch), "Stretch must be in [0,1).");
        if (maxVerticalShiftPx < 0 || maxVerticalShiftPx >= image.Height) throw new ArgumentOutOfRangeException(nameof(maxVerticalShiftPx));
        if (saltPepperFraction is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(saltPepperFraction));

        var stretch = 1 + (random.NextDouble() * 2 - 1) * maxHorizontalStretch;
        var width = Math.Max(1, (int)Math.Round(image.Width * stretch));
        var shift = maxVerticalShiftPx == 0 ? 0 : random.Next(-maxVerticalShiftPx, maxVerticalShiftPx + 1);

        var pixels = new float[image.Height * width];
        for (var y = 0; y < image.Height; y++)
        {
            var sourceY = y - shift;
            for (var x = 0; x < width; x++)
            {
                if (sourceY < 0 || sourceY >= image.Height) continue; // background outside the shifted image
                var sourceX = (int)((long)x * image.Width / width);
                pixels[y * width + x] = image.Pixels[sourceY * image.Width + sourceX];
            }
        }

        if (saltPepperFraction > 0)
        {
            for (var i = 0; i < pixels.Length; i++)
            {
                if (random.NextDouble() >= saltPepperFraction) continue;
                pixels[i] = random.Next(2); // 0 = background, 1 = ink
            }
        }

        return new LineImage(image.Height, width, pixels);
    }
}
