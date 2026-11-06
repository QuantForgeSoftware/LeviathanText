using System.Security.Cryptography;
using System.Text;
using LeviathanText.Contracts;
using SkiaSharp;

namespace LeviathanText.Imaging.Tests;

public sealed class DecodedPixelBudgetTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lt-pixel-budget-" + Guid.NewGuid().ToString("N"));
    public DecodedPixelBudgetTests() => Directory.CreateDirectory(_root);

    [Theory]
    [InlineData("png")]
    [InlineData("jpeg")]
    public void Thin_images_fit_the_literal_budget_and_retain_source_identity(string format)
    {
        var bytes = Encode(12000, 1, format, constant: true);
        var path = Save(bytes, format);
        using var result = ImageDecoder.DecodeBounded(path, 128);
        Assert.Equal(128, result.Bitmap.Width);
        Assert.Equal(1, result.Bitmap.Height);
        AssertIdentity(result, bytes, 12000, 1);
        Assert.Equal(128d / 12000, result.ScaleX);
        Assert.Equal(1, result.ScaleY);
        for (int x = 0; x < 128; x++) Assert.Equal(new SKColor(200, 200, 200), result.Bitmap.GetPixel(x, 0));
    }

    [Fact]
    public void Native_minimum_scale_is_resampled_once_to_one_pixel()
    {
        var bytes = Encode(16, 12, "jpeg");
        using var input = new MemoryStream(bytes, writable: false);
        using var codec = SKCodec.Create(input)!;
        var minimum = codec.GetScaledDimensions(0.01f);
        Assert.True((long)minimum.Width * minimum.Height > 1);
        using var intermediate = new SKBitmap(minimum.Width, minimum.Height, codec.Info.ColorType, codec.Info.AlphaType);
        Assert.Equal(SKCodecResult.Success, codec.GetPixels(intermediate.Info, intermediate.GetPixels()));
        using var expected = intermediate.Resize(new SKImageInfo(1, 1, intermediate.ColorType, intermediate.AlphaType), new SKSamplingOptions(SKFilterMode.Linear));
        Assert.NotNull(expected);
        using var actual = ImageDecoder.DecodeBounded(bytes, "generated", 1);
        Assert.Equal(1, actual.Bitmap.Width);
        Assert.Equal(1, actual.Bitmap.Height);
        Assert.Equal(expected.GetPixel(0, 0), actual.Bitmap.GetPixel(0, 0));
        AssertIdentity(actual, bytes, 16, 12);
    }

    [Fact]
    public void Fallback_resamples_the_original_raster_once_to_one_pixel()
    {
        var bytes = Pgm();
        using var result = ImageDecoder.DecodeBounded(bytes, "generated.pgm", 1);
        Assert.Equal(1, result.Bitmap.Width);
        Assert.Equal(1, result.Bitmap.Height);
        Assert.Equal(new SKColor(17, 17, 17), result.Bitmap.GetPixel(0, 0));
        AssertIdentity(result, bytes, 8, 2);
    }

    [Fact]
    public void In_budget_legacy_rounded_fallback_keeps_its_six_by_one_shape()
    {
        var bytes = Pgm();
        using var result = ImageDecoder.DecodeBounded(bytes, "generated.pgm", 8);
        Assert.Equal(6, result.Bitmap.Width);
        Assert.Equal(1, result.Bitmap.Height);
        for (int x = 0; x < 6; x++) Assert.Equal(new SKColor(17, 17, 17), result.Bitmap.GetPixel(x, 0));
        AssertIdentity(result, bytes, 8, 2);
    }

    [Fact]
    public void In_budget_native_output_retains_exact_codec_pixels()
    {
        var bytes = Encode(16, 12, "jpeg");
        using var input = new MemoryStream(bytes, writable: false);
        using var codec = SKCodec.Create(input)!;
        var dimensions = codec.GetScaledDimensions(0.25f);
        Assert.Equal(new SKSizeI(4, 3), dimensions);
        using var expected = new SKBitmap(4, 3, codec.Info.ColorType, codec.Info.AlphaType);
        Assert.Equal(SKCodecResult.Success, codec.GetPixels(expected.Info, expected.GetPixels()));
        using var actual = ImageDecoder.DecodeBounded(bytes, "generated", 12);
        AssertPixels(expected, actual.Bitmap);
        AssertIdentity(actual, bytes, 16, 12);
    }

    [Fact]
    public void In_budget_png_resample_keeps_exact_existing_canvas_pixels()
    {
        var bytes = Encode(16, 12, "png");
        using var source = SKBitmap.Decode(bytes);
        using var input = new MemoryStream(bytes, writable: false);
        using var codec = SKCodec.Create(input)!;
        using var expected = new SKBitmap(8, 6, codec.Info.ColorType, codec.Info.AlphaType);
        using (var canvas = new SKCanvas(expected))
            canvas.DrawBitmap(source, new SKRect(0, 0, 16, 12), new SKRect(0, 0, 8, 6), new SKSamplingOptions(SKFilterMode.Linear));
        using var actual = ImageDecoder.DecodeBounded(bytes, "generated", 48);
        AssertPixels(expected, actual.Bitmap);
        AssertIdentity(actual, bytes, 16, 12);
    }

    [Fact]
    public void Source_already_within_budget_keeps_exact_full_pixels()
    {
        var bytes = Encode(16, 12, "png");
        using var expected = SKBitmap.Decode(bytes);
        using var actual = ImageDecoder.DecodeBounded(bytes, "generated", 192);
        AssertPixels(expected, actual.Bitmap);
        AssertIdentity(actual, bytes, 16, 12);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(long.MinValue)]
    public void Invalid_byte_budget_is_a_contract_error_before_format_decoding(long budget)
    {
        var error = Assert.Throws<LeviathanException>(() => ImageDecoder.DecodeBounded([], "generated", budget));
        Assert.Equal(ErrorCode.StageContractViolation, error.Error.Code);
        Assert.Equal("invalid-pixel-budget", error.Error.Details!["reason"]);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void Existing_path_with_invalid_budget_is_rejected_before_image_decoding(long budget)
    {
        var path = Save(Encoding.ASCII.GetBytes("generated non-image"), "bin");
        var error = Assert.Throws<LeviathanException>(() => ImageDecoder.DecodeBounded(path, budget));
        Assert.Equal(ErrorCode.StageContractViolation, error.Error.Code);
        Assert.Equal("invalid-pixel-budget", error.Error.Details!["reason"]);
    }

    [Fact]
    public void Missing_path_retains_priority_over_invalid_budget()
    {
        var error = Assert.Throws<LeviathanException>(() => ImageDecoder.DecodeBounded(Path.Combine(_root, "missing.png"), 0));
        Assert.Equal(ErrorCode.InputMissing, error.Error.Code);
    }

    private static byte[] Pgm() => Encoding.ASCII.GetBytes("P2\n8 2\n255\n" + string.Join(' ', Enumerable.Repeat("17", 16)) + "\n");
    private static byte[] Encode(int width, int height, string format, bool constant = false)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        if (constant) bitmap.Erase(new SKColor(200, 200, 200));
        else for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            bitmap.SetPixel(x, y, new SKColor((byte)(x * 13 + y * 5), (byte)(x * 7 + y * 11), (byte)(x * 3 + y * 17)));
        using var encoded = bitmap.Encode(format == "png" ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg, 92);
        return encoded.ToArray();
    }
    private string Save(byte[] bytes, string extension)
    {
        var path = Path.Combine(_root, Guid.NewGuid().ToString("N") + "." + extension);
        File.WriteAllBytes(path, bytes); return path;
    }
    private static void AssertIdentity(ImageDecoder.ScaledImage result, byte[] bytes, int width, int height)
    {
        Assert.Equal(width, result.OriginalWidth); Assert.Equal(height, result.OriginalHeight);
        Assert.Equal("sha256", result.SourceChecksum.Algorithm);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), result.SourceChecksum.Digest);
    }
    private static void AssertPixels(SKBitmap expected, SKBitmap actual)
    {
        Assert.Equal(expected.Width, actual.Width); Assert.Equal(expected.Height, actual.Height);
        for (int y = 0; y < expected.Height; y++) for (int x = 0; x < expected.Width; x++)
            Assert.Equal(expected.GetPixel(x, y), actual.GetPixel(x, y));
    }
    public void Dispose() { Directory.Delete(_root, recursive: true); }
}
