using SkiaSharp;

namespace LeviathanText.Imaging.Tests;

public class BandedExtractionTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"leviathan-band-{Guid.NewGuid():N}");

    public BandedExtractionTests() => Directory.CreateDirectory(_dir);

    private string MakeJpeg()
    {
        var path = Path.Combine(_dir, "page.jpg");
        using var surface = SKSurface.Create(new SKImageInfo(1200, 900));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        using var paint = new SKPaint { Color = SKColors.Black, StrokeWidth = 3 };
        canvas.DrawRect(new SKRect(200, 300, 1000, 360), paint);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        using var stream = File.Create(path);
        data.SaveTo(stream);
        return path;
    }

    [Fact]
    public void Band_decode_matches_full_decode_for_the_same_rectangle()
    {
        var path = MakeJpeg();
        using var full = SKBitmap.Decode(path);
        using var expected = new SKBitmap();
        full.ExtractSubset(expected, new SKRectI(0, 250, 1200, 420));
        using var band = ImageDecoder.DecodeBand(path, 250, 170);

        Assert.Equal(expected.Width, band.Width);
        Assert.Equal(expected.Height, band.Height);
        Assert.True(expected.Bytes.AsSpan().SequenceEqual(band.Bytes));
    }

    [Fact]
    public void Band_decode_matches_full_decode_for_png()
    {
        var path = Path.Combine(_dir, "page.png");
        TestImage.RenderPage(path, 400, 200);
        using var full = SKBitmap.Decode(path);
        using var expected = new SKBitmap();
        full.ExtractSubset(expected, new SKRectI(0, 80, 400, 160));
        using var band = ImageDecoder.DecodeBand(path, 80, 80);

        Assert.Equal(expected.Width, band.Width);
        Assert.Equal(expected.Height, band.Height);
        Assert.True(expected.Bytes.AsSpan().SequenceEqual(band.Bytes));
    }

    [Fact]
    public void Banded_line_extraction_matches_full_page_extraction_for_png()
    {
        var path = Path.Combine(_dir, "line.png");
        TestImage.RenderPage(path, 400, 200);
        var points = new[]
        {
            new SKPoint(20, 80), new SKPoint(300, 80),
            new SKPoint(300, 130), new SKPoint(20, 130),
        };
        using var full = SKBitmap.Decode(path);
        var expected = LineCropper.Extract(full, points);
        var banded = LineCropper.ExtractBanded(path, points);
        try
        {
            Assert.Equal(expected.AppliedCrop, banded.AppliedCrop);
            Assert.Equal(expected.DerivativeChecksum, banded.DerivativeChecksum);
            Assert.True(expected.Bitmap.Bytes.AsSpan().SequenceEqual(banded.Bitmap.Bytes));
        }
        finally
        {
            expected.Bitmap.Dispose();
            banded.Bitmap.Dispose();
        }
    }

    [Fact]
    public void Banded_line_extraction_matches_full_page_extraction()
    {
        var path = MakeJpeg();
        var points = new[]
        {
            new SKPoint(200, 300), new SKPoint(1000, 300),
            new SKPoint(1000, 360), new SKPoint(200, 360),
        };
        using var full = SKBitmap.Decode(path);
        var expected = LineCropper.Extract(full, points);
        var banded = LineCropper.ExtractBanded(path, points);
        try
        {
            Assert.Equal(expected.AppliedCrop, banded.AppliedCrop);
            Assert.Equal(expected.DerivativeChecksum, banded.DerivativeChecksum);
            Assert.True(expected.Bitmap.Bytes.AsSpan().SequenceEqual(banded.Bitmap.Bytes));
        }
        finally
        {
            expected.Bitmap.Dispose();
            banded.Bitmap.Dispose();
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
