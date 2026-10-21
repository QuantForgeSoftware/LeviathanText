using SkiaSharp;

namespace LeviathanText.Imaging.Tests;

public class BinarizationTests
{
    [Fact]
    public void Empty_image_is_rejected()
    {
        using var empty = new SKBitmap(0, 0);
        Assert.Throws<ArgumentException>(() => Binarization.ToBlackWhite(empty));
    }

    [Fact]
    public void Black_white_image_is_returned_unchanged()
    {
        using var source = new SKBitmap(3, 1);
        source.SetPixel(0, 0, SKColors.Black);
        source.SetPixel(1, 0, SKColors.White);
        source.SetPixel(2, 0, SKColors.Black);
        using var result = Binarization.ToBlackWhite(source);
        Assert.Equal(source.Bytes, result.Bytes);
    }

    [Fact]
    public void Gray_image_is_thresholded_to_black_and_white()
    {
        using var source = new SKBitmap(4, 1);
        source.SetPixel(0, 0, new SKColor(20, 20, 20));
        source.SetPixel(1, 0, new SKColor(80, 80, 80));
        source.SetPixel(2, 0, new SKColor(200, 200, 200));
        source.SetPixel(3, 0, new SKColor(250, 250, 250));
        using var result = Binarization.ToBlackWhite(source);
        Assert.Equal(SKColors.Black, result.GetPixel(0, 0));
        Assert.Equal(SKColors.White, result.GetPixel(3, 0));
        Assert.All(result.Pixels, p => Assert.True(p.Red is 0 or 255));
    }
}
