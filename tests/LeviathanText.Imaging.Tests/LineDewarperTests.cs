using SkiaSharp;

namespace LeviathanText.Imaging.Tests;

public class LineDewarperTests
{
    [Fact]
    public void Linear_vertical_drift_is_reduced_and_target_height_is_used()
    {
        using var source = DriftingLine(SKColors.Black);
        double before = CenterVariance(source);
        using var result = LineDewarper.Dewarp(source, new LineDewarper.Options { TargetHeight = 64 });
        double after = CenterVariance(result);

        Assert.Equal(64, result.Height);
        Assert.Equal(source.Width, result.Width);
        Assert.True(before > 1, $"test line should drift, variance={before}");
        Assert.True(after < before * 0.5, $"expected drift reduction, before={before}, after={after}");
    }

    [Fact]
    public void Grayscale_input_is_accepted()
    {
        using var source = DriftingLine(new SKColor(128, 128, 128));
        using var result = LineDewarper.Dewarp(source, new LineDewarper.Options { TargetHeight = 48 });
        Assert.Equal(48, result.Height);
        Assert.Contains(result.Pixels, p => p.Red < 255);
    }

    [Fact]
    public void Color_input_is_rejected()
    {
        using var source = new SKBitmap(20, 20);
        source.Erase(new SKColor(180, 20, 20));

        var ex = Assert.Throws<LeviathanText.Contracts.LeviathanException>(() => LineDewarper.Dewarp(source));
        Assert.Equal(LeviathanText.Contracts.ErrorCode.StageContractViolation, ex.Error.Code);
    }

    [Fact]
    public void Blank_undewarpable_line_does_not_crash()
    {
        using var source = new SKBitmap(20, 20);
        source.Erase(SKColors.White);

        using var result = LineDewarper.Dewarp(source, new LineDewarper.Options { TargetHeight = 32 });
        Assert.Equal(32, result.Height);
        Assert.All(result.Pixels, p => Assert.Equal(SKColors.White, p));
    }

    private static SKBitmap DriftingLine(SKColor ink)
    {
        var bitmap = new SKBitmap(160, 60);
        bitmap.Erase(SKColors.White);
        for (int x = 0; x < bitmap.Width; x++)
        {
            int center = 15 + x * 30 / (bitmap.Width - 1);
            for (int y = center - 2; y <= center + 2; y++)
                bitmap.SetPixel(x, y, ink);
        }
        return bitmap;
    }

    private static double CenterVariance(SKBitmap bitmap)
    {
        var centers = LineDewarper.InkCenters(bitmap).Where(v => !double.IsNaN(v)).ToArray();
        double mean = centers.Average();
        return centers.Select(v => (v - mean) * (v - mean)).Average();
    }
}
