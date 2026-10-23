using LeviathanText.Contracts;
using SkiaSharp;

namespace LeviathanText.Imaging.Tests;

/// <summary>Generates synthetic page images (invented fixtures; no real corpus).</summary>
public static class TestImage
{
    public static string RenderPage(string path, int width = 400, int height = 200, double deskewDegrees = 0)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        using var paint = new SKPaint { Color = SKColors.Black, StrokeWidth = 3, IsAntialias = true };
        using var font = new SKFont(SKTypeface.FromFamilyName("serif"), 24);

        canvas.DrawText("ΑΒΓΔΕΖΗΘ", 20, 60, SKTextAlign.Left, font, paint);
        canvas.DrawText("α\u0305λ\u0305α\u0305νθρωπ", 20, 120, SKTextAlign.Left, font, paint); // overbarred abbreviation
        if (deskewDegrees != 0)
        {
            canvas.Translate(width / 2f, height / 2f);
            canvas.RotateDegrees((float)deskewDegrees);
            canvas.Translate(-width / 2f, -height / 2f);
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);
        return path;
    }
}

public class ImageDecoderTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"leviathan-img-{Guid.NewGuid():N}.png");

    [Fact]
    public void Decode_returns_bitmap_with_checksum()
    {
        TestImage.RenderPage(_path);
        using var decoded = ImageDecoder.Decode(_path);

        Assert.Equal(400, decoded.Bitmap.Width);
        Assert.Equal(200, decoded.Bitmap.Height);
        Assert.Equal("sha256", decoded.SourceChecksum.Algorithm);
        Assert.Equal(64, decoded.SourceChecksum.Digest.Length);
    }

    [Fact]
    public void Checksum_is_stable_across_decodes()
    {
        TestImage.RenderPage(_path);
        using var first = ImageDecoder.Decode(_path);
        using var second = ImageDecoder.Decode(_path);
        Assert.Equal(first.SourceChecksum, second.SourceChecksum);
    }

    [Fact]
    public void Missing_file_is_input_missing()
    {
        var ex = Assert.Throws<LeviathanException>(() => ImageDecoder.Decode(_path));
        Assert.Equal(ErrorCode.InputMissing, ex.Error.Code);
    }

    [Fact]
    public void DecodeBounded_scales_large_images_and_maps_coordinates()
    {
        using var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(3000, 2000));
        surface.Canvas.Clear(SkiaSharp.SKColors.White);
        using var image = surface.Snapshot();
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 90);
        var path = Path.Combine(Path.GetTempPath(), $"lt-big-{Guid.NewGuid():N}.png");
        using (var stream = File.Create(path)) data.SaveTo(stream);

        try
        {
            using var bounded = ImageDecoder.DecodeBounded(path, maxPixels: 1_000_000);
            Assert.True(bounded.IsScaled);
            Assert.Equal(3000, bounded.OriginalWidth);
            Assert.Equal(2000, bounded.OriginalHeight);
            Assert.True((long)bounded.Bitmap.Width * bounded.Bitmap.Height <= 1_000_000);
            // Aspect ratio preserved within one pixel of scaling.
            Assert.Equal(1.5, (double)bounded.Bitmap.Width / bounded.Bitmap.Height, 1);
            Assert.Equal(3000.0 * bounded.ScaleX, bounded.Bitmap.Width, 0);

            using var full = ImageDecoder.DecodeBounded(path, maxPixels: 100_000_000);
            Assert.False(full.IsScaled);
            Assert.Equal(3000, full.Bitmap.Width);
            Assert.Equal(1.0, full.ScaleX);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Tiff_is_decoded_through_the_bounded_fallback()
    {
        var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample.tif");
        if (!File.Exists(fixture)) return; // fixture not copied: skip silently in tests

        using var decoded = ImageDecoder.Decode(fixture);
        Assert.Equal(16, decoded.Bitmap.Width);
        Assert.Equal(12, decoded.Bitmap.Height);
        using var bounded = ImageDecoder.DecodeBounded(fixture, maxPixels: 100);
        Assert.Equal(16, bounded.OriginalWidth);
        Assert.Equal(12, bounded.OriginalHeight);
    }

    public void Dispose() => File.Delete(_path);
}

public class LineCropperTests
{
    private static SKBitmap Page(int width = 400, int height = 200)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        using var paint = new SKPaint { Color = SKColors.Black };
        canvas.DrawCircle(100, 100, 30, paint); // visible content marker
        using var image = surface.Snapshot();
        return SKBitmap.FromImage(image);
    }

    [Fact]
    public void Crop_extends_above_and_below_the_line_band()
    {
        using var page = Page();
        // A tight band y=[90,110] (height 20); padding 0.25 → 5px above/below (under the 12px cap); margins 8px.
        var extracted = LineCropper.Extract(page, [new SKPoint(50, 90), new SKPoint(200, 110)]);

        Assert.Equal(new ImageTransform.Crop(42, 85, 166, 30), extracted.AppliedCrop);
        Assert.Equal(30, extracted.Bitmap.Height);
        Assert.Equal("", extracted.Losses);
    }

    [Fact]
    public void Crop_at_page_edge_truncates_and_reports_loss()
    {
        using var page = Page(width: 100, height: 200);
        var extracted = LineCropper.Extract(page, [new SKPoint(50, 90), new SKPoint(200, 110)]);
        Assert.NotEqual("", extracted.Losses);
        Assert.Contains("truncated", extracted.Losses);
    }

    [Fact]
    public void Empty_geometry_is_refused_explicitly()
    {
        using var page = Page();
        var ex = Assert.Throws<LeviathanException>(() => LineCropper.Extract(page, []));
        Assert.Equal(ErrorCode.StageContractViolation, ex.Error.Code);
        Assert.Contains("explicitly", ex.Error.Message);
    }
}

public class DerivativeFactoryTests
{
    [Fact]
    public void Crop_records_its_chain_and_maps_coordinates()
    {
        using var page = new SKBitmap(200, 100);
        var crop = new ImageTransform.Crop(10, 20, 100, 50);
        var result = DerivativeFactory.Apply(page, [crop]);

        Assert.Equal("crop:l10,t20,w100,h50", result.TransformChain);
        Assert.Single(result.AppliedTransforms);
        var mapped = crop.Map(new SKPoint(50, 60));
        Assert.Equal(new SKPoint(40, 40), mapped);
    }

    [Fact]
    public void Nonlinear_dewarp_is_recorded_but_refused_and_unmappable()
    {
        var dewarp = new ImageTransform.NonlinearDewarp("control-v1");
        Assert.Null(dewarp.Map(new SKPoint(3, 4))); // explicitly unmappable

        using var page = new SKBitmap(10, 10);
        var ex = Assert.Throws<LeviathanException>(() => DerivativeFactory.Apply(page, [dewarp]));
        Assert.Equal(ErrorCode.StageContractViolation, ex.Error.Code);
        Assert.Contains("refusing to fake it", ex.Error.Message);
    }

    [Fact]
    public void Out_of_bounds_crop_fails_loudly()
    {
        using var page = new SKBitmap(100, 100);
        var ex = Assert.Throws<LeviathanException>(() =>
            DerivativeFactory.Apply(page, [new ImageTransform.Crop(50, 50, 100, 100)]));
        Assert.Equal(ErrorCode.StageContractViolation, ex.Error.Code);
    }

    [Fact]
    public void Deskew_maps_coordinates_around_the_center_pivot()
    {
        // Same matrix DerivativeFactory uses: device = T(pivot)·R(θ)·T(-pivot)·source.
        var deskew = new ImageTransform.Deskew(90, SourceWidth: 100, SourceHeight: 100);
        var mapped = deskew.Map(new SKPoint(60, 50)); // pivot (50,50) + (10,0)
        Assert.Equal(new SKPoint(50, 60), mapped!.Value); // R90·(10,0) = (0,10)
    }
}
