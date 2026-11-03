using LeviathanText.Recognition;

namespace LeviathanText.Training;

/// <summary>
/// Deterministic on-the-fly augmentation for line images. It changes only geometry and
/// noise and luminance, never the label, and is applied to training batches only: validation/test data
/// must stay untouched for comparable measurements.
/// </summary>
public static class LineAugmenter
{
    // Keep the original compiled signature; identity gamma also preserves its RNG stream.
    public static LineImage Apply(LineImage image, Random random, double maxHorizontalStretch,
        int maxVerticalShiftPx, double saltPepperFraction) =>
        Apply(image, random, maxHorizontalStretch, maxVerticalShiftPx, saltPepperFraction, 1, 1);

    public static LineImage Apply(
        LineImage image,
        Random random,
        double maxHorizontalStretch = 0.1,
        int maxVerticalShiftPx = 1,
        double saltPepperFraction = 0.01,
        double gammaMin = 1,
        double gammaMax = 1)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(random);
        if (maxHorizontalStretch is < 0 or >= 1) throw new ArgumentOutOfRangeException(nameof(maxHorizontalStretch), "Stretch must be in [0,1).");
        if (maxVerticalShiftPx < 0 || maxVerticalShiftPx >= image.Height) throw new ArgumentOutOfRangeException(nameof(maxVerticalShiftPx));
        if (saltPepperFraction is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(saltPepperFraction));
        ValidateGammaRange(gammaMin, gammaMax);

        var stretch = 1 + (random.NextDouble() * 2 - 1) * maxHorizontalStretch;
        var width = Math.Max(1, (int)Math.Round(image.Width * stretch));
        var shift = maxVerticalShiftPx == 0 ? 0 : random.Next(-maxVerticalShiftPx, maxVerticalShiftPx + 1);

        var pixels = new float[image.Height * width];
        for (var y = 0; y < image.Height; y++)
        {
            var sourceY = y - shift;
            if (sourceY < 0 || sourceY >= image.Height)
            {
                if (image.BackgroundValue != 0)
                    Array.Fill(pixels, image.BackgroundValue, y * width, width);
                continue;
            }
            for (var x = 0; x < width; x++)
            {
                var sourceX = (int)((long)x * image.Width / width);
                pixels[y * width + x] = image.Pixels[sourceY * image.Width + sourceX];
            }
        }

        if (saltPepperFraction > 0)
        {
            for (var i = 0; i < pixels.Length; i++)
            {
                if (random.NextDouble() >= saltPepperFraction) continue;
                pixels[i] = random.Next(2); // black/white endpoints; either polarity is supported
            }
        }

        float background = image.BackgroundValue;
        if (gammaMin != 1 || gammaMax != 1)
        {
            double gamma = gammaMin == gammaMax ? gammaMin : gammaMin + random.NextDouble() * (gammaMax - gammaMin);
            static float Transform(float value, double exponent) =>
                Math.Clamp((float)Math.Pow(Math.Clamp(value, 0f, 1f), exponent), 0f, 1f);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Transform(pixels[i], gamma);
            background = Transform(background, gamma);
        }

        return new LineImage(image.Height, width, pixels, background);
    }

    internal static void ValidateGammaRange(double gammaMin, double gammaMax)
    {
        if (!double.IsFinite(gammaMin) || gammaMin <= 0)
            throw new ArgumentOutOfRangeException(nameof(gammaMin), "Gamma must be finite and positive.");
        if (!double.IsFinite(gammaMax) || gammaMax < gammaMin)
            throw new ArgumentOutOfRangeException(nameof(gammaMax), "Maximum gamma must be finite and at least the minimum.");
    }
}
