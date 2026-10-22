using LeviathanText.Recognition;
using LeviathanText.Training;
using SkiaSharp;
using Xunit;

namespace LeviathanText.ML.Tests;

public class LineInputTransformTests
{
    [Fact]
    public void Aspect_preserving_transform_has_expected_shape()
    {
        using var source = new SKBitmap(20, 10);
        source.Erase(SKColors.White);
        var result = LineInputTransform.Apply(source, new LineInputTransform.Options { Height = 16 });

        Assert.Equal(1, result.Channels);
        Assert.Equal(16, result.Height);
        Assert.Equal(32, result.Width);
        Assert.Equal(32 * 16, result.Pixels.Length);
        Assert.Equal(result.Pixels.Length, result.ToLineImage().Pixels.Length);
    }

    [Fact]
    public void Fixed_width_and_three_channels_use_channel_major_layout()
    {
        using var source = new SKBitmap(20, 10);
        source.Erase(new SKColor(10, 20, 30));
        var result = LineInputTransform.Apply(source, new LineInputTransform.Options
        {
            Height = 8, Width = 24, Channels = 3,
        });

        Assert.Equal(3, result.Channels);
        Assert.Equal(24, result.Width);
        Assert.Equal(8, result.Height);
        Assert.Equal(3 * 24 * 8, result.Pixels.Length);
        Assert.All(result.Pixels.Take(24 * 8), value => Assert.InRange(value, 0.03f, 0.05f)); // red plane
        Assert.Throws<InvalidOperationException>(() => result.ToLineImage());
    }

    [Fact]
    public void Forced_binarization_produces_only_zero_and_one()
    {
        using var source = new SKBitmap(8, 4);
        source.Erase(new SKColor(128, 128, 128));
        source.SetPixel(2, 2, SKColors.Black);
        var result = LineInputTransform.Apply(source, new LineInputTransform.Options
        {
            Height = 4, Width = 8, ForceBinarization = true,
        });

        Assert.True(result.Binarized);
        Assert.All(result.Pixels, value => Assert.True(value is 0 or 1));
    }

    [Fact]
    public void Invalid_options_are_rejected()
    {
        using var source = new SKBitmap(4, 4);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LineInputTransform.Apply(source, new LineInputTransform.Options { Channels = 2 }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LineInputTransform.Apply(source, new LineInputTransform.Options { Height = 0 }));
    }
}
