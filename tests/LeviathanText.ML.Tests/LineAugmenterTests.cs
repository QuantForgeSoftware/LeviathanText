using LeviathanText.Recognition;
using LeviathanText.Training;
using Xunit;

namespace LeviathanText.ML.Tests;

/// <summary>Deterministic, label-preserving training augmentation.</summary>
public class LineAugmenterTests
{
    private static LineImage Image()
    {
        var pixels = new float[16 * 20];
        for (var i = 0; i < pixels.Length; i++) pixels[i] = i % 3 == 0 ? 1f : 0f;
        return new LineImage(16, 20, pixels);
    }

    [Fact]
    public void Same_seed_produces_identical_augmentation()
    {
        var image = Image();
        var first = LineAugmenter.Apply(image, new Random(42), 0.1, 1, 0.01);
        var second = LineAugmenter.Apply(image, new Random(42), 0.1, 1, 0.01);

        Assert.Equal(first.Width, second.Width);
        Assert.Equal(first.Pixels, second.Pixels);
    }

    [Fact]
    public void Zero_transforms_is_the_identity()
    {
        var image = Image();
        var augmented = LineAugmenter.Apply(image, new Random(1), maxHorizontalStretch: 0, maxVerticalShiftPx: 0, saltPepperFraction: 0);

        Assert.Equal(image.Width, augmented.Width);
        Assert.Equal(image.Height, augmented.Height);
        Assert.Equal(image.Pixels, augmented.Pixels);
    }

    [Fact]
    public void Horizontal_stretch_stays_within_the_configured_bound()
    {
        var image = Image();
        for (var seed = 0; seed < 50; seed++)
        {
            var augmented = LineAugmenter.Apply(image, new Random(seed), maxHorizontalStretch: 0.1, maxVerticalShiftPx: 0, saltPepperFraction: 0);
            Assert.InRange(augmented.Width, (int)Math.Round(image.Width * 0.9), (int)Math.Round(image.Width * 1.1));
        }
    }

    [Fact]
    public void Vertical_shift_fills_exposed_rows_with_background()
    {
        var image = Image();
        var augmented = LineAugmenter.Apply(image, new Random(7), maxHorizontalStretch: 0, maxVerticalShiftPx: 3, saltPepperFraction: 0);

        Assert.Equal(image.Width, augmented.Width);
        Assert.Equal(image.Height, augmented.Height);
        var anyBackground = Enumerable.Range(0, augmented.Height * augmented.Width).Any(i => augmented.Pixels[i] == 0f);
        Assert.True(anyBackground);
    }

    [Fact]
    public void Full_salt_and_pepper_binarizes_every_pixel()
    {
        var augmented = LineAugmenter.Apply(Image(), new Random(3), maxHorizontalStretch: 0, maxVerticalShiftPx: 0, saltPepperFraction: 1.0);

        Assert.All(augmented.Pixels, value => Assert.True(value is 0f or 1f));
        Assert.Contains(augmented.Pixels, value => value == 0f);
        Assert.Contains(augmented.Pixels, value => value == 1f);
    }

    [Fact]
    public void Invalid_parameters_are_rejected()
    {
        var image = Image();
        Assert.Throws<ArgumentOutOfRangeException>(() => LineAugmenter.Apply(image, new Random(1), maxHorizontalStretch: 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => LineAugmenter.Apply(image, new Random(1), maxVerticalShiftPx: 16));
        Assert.Throws<ArgumentOutOfRangeException>(() => LineAugmenter.Apply(image, new Random(1), saltPepperFraction: 1.5));
    }
}
