using LeviathanText.Recognition;
using LeviathanText.Training;
using System.Text.Json;
using Xunit;

namespace LeviathanText.ML.Tests;

public class SafePaddingTests
{
    [Fact]
    public void Json_round_trip_preserves_white_background_and_pixels()
    {
        var image = new LineImage(8, 8, Enumerable.Repeat(0.4f, 64).ToArray(), backgroundValue: 1);
        var restored = JsonSerializer.Deserialize<LineImage>(JsonSerializer.Serialize(image))!;

        Assert.Equal(image.Height, restored.Height);
        Assert.Equal(image.Width, restored.Width);
        Assert.Equal(1f, restored.BackgroundValue);
        Assert.Equal(image.Pixels, restored.Pixels);
    }

    [Fact]
    public void Legacy_json_without_background_uses_zero()
    {
        const string json = "{\"Height\":1,\"Width\":2,\"Pixels\":[0.25,0.75]}";
        var restored = JsonSerializer.Deserialize<LineImage>(json)!;

        Assert.Equal(0f, restored.BackgroundValue);
        Assert.Equal(new[] { 0.25f, 0.75f }, restored.Pixels);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1f)]
    [InlineData(0.75f)]
    public void Variable_width_batch_uses_declared_background_without_changing_valid_pixels_or_lengths(float background)
    {
        var narrow = new LineImage(8, 8, Enumerable.Repeat(0.4f, 64).ToArray(), background);
        var wide = new LineImage(8, 12, Enumerable.Repeat(0.6f, 96).ToArray(), background);

        var (tensor, lengths) = LineBatcher.ToTensor([narrow, wide], widthDownsampleFactor: 4, inputHeight: 8);
        using (tensor)
        {
            Assert.Equal(new long[] { 2, 1, 8, 12 }, tensor.shape);
            Assert.Equal(new[] { 2, 3 }, lengths);
            var pixels = tensor.data<float>().ToArray();
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 12; x++)
            {
                Assert.Equal(x < 8 ? 0.4f : background, pixels[y * 12 + x]);
                Assert.Equal(0.6f, pixels[96 + y * 12 + x]);
            }
        }
        Assert.All(narrow.Pixels, p => Assert.Equal(0.4f, p));
        Assert.All(wide.Pixels, p => Assert.Equal(0.6f, p));
    }

    [Fact]
    public void Batch_padding_is_declared_per_image_and_legacy_default_remains_zero()
    {
        var legacy = new LineImage(8, 8, new float[64]);
        var luminance = new LineImage(8, 4, new float[32], backgroundValue: 1);
        var gray = new LineImage(8, 4, new float[32], backgroundValue: 0.75f);
        var (tensor, lengths) = LineBatcher.ToTensor([legacy, luminance, gray], inputHeight: 8);
        using (tensor)
        {
            var pixels = tensor.data<float>().ToArray();
            Assert.Equal(0f, legacy.BackgroundValue);
            Assert.Equal(new[] { 2, 1, 1 }, lengths);
            for (int y = 0; y < 8; y++)
            for (int x = 4; x < 8; x++)
            {
                Assert.Equal(1f, pixels[64 + y * 8 + x]);
                Assert.Equal(0.75f, pixels[128 + y * 8 + x]);
            }
        }
    }

    [Theory]
    [InlineData(0f, -2)]
    [InlineData(0f, 2)]
    [InlineData(1f, -2)]
    [InlineData(1f, 2)]
    [InlineData(0.75f, 2)]
    public void Shift_fills_every_exposed_pixel_with_declared_background_and_preserves_metadata(float background, int shift)
    {
        var pixels = Enumerable.Range(0, 8 * 8).Select(i => (i + 1) / 100f).ToArray();
        var originalPixels = pixels.ToArray();
        var image = new LineImage(8, 8, pixels, background);
        var augmented = LineAugmenter.Apply(image, new FixedShiftRandom(shift),
            maxHorizontalStretch: 0, maxVerticalShiftPx: 2, saltPepperFraction: 0);

        Assert.Equal(background, augmented.BackgroundValue);
        Assert.Equal(image.Width, augmented.Width);
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
        {
            int sourceY = y - shift;
            Assert.Equal(sourceY < 0 || sourceY >= 8 ? background : pixels[sourceY * 8 + x], augmented[y, x]);
        }
        Assert.Equal(originalPixels, image.Pixels);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1f)]
    public void Salt_pepper_uses_both_pixel_extremes_and_preserves_background_metadata(float background)
    {
        var image = new LineImage(8, 8, Enumerable.Repeat(0.5f, 64).ToArray(), background);
        var augmented = LineAugmenter.Apply(image, new Random(3),
            maxHorizontalStretch: 0, maxVerticalShiftPx: 0, saltPepperFraction: 1);
        Assert.Equal(background, augmented.BackgroundValue);
        Assert.All(augmented.Pixels, value => Assert.True(value is 0f or 1f));
        Assert.Contains(0f, augmented.Pixels);
        Assert.Contains(1f, augmented.Pixels);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(-0.01f)]
    [InlineData(1.01f)]
    public void Invalid_background_values_are_rejected(float background) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new LineImage(8, 8, new float[64], background));

    private sealed class FixedShiftRandom(int shift) : Random
    {
        public override double NextDouble() => 0.5;
        public override int Next(int minValue, int maxValue) => shift;
    }
}
