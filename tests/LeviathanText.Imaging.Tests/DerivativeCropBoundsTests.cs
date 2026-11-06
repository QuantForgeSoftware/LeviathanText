using LeviathanText.Contracts;
using LeviathanText.Imaging;
using SkiaSharp;
using Xunit;

namespace LeviathanText.Imaging.Tests;

// Real public SDK path with only2x2/1px native buffers; no models or large input.
// Proposed source only. Both baseline/candidate run receipts remain pending.
public sealed class DerivativeCropBoundsTests
{
    private static SKBitmap Source()
    {
        var bitmap = new SKBitmap(2, 2, SKColorType.Bgra8888, SKAlphaType.Premul);
        bitmap.SetPixel(0, 0, SKColors.Red); bitmap.SetPixel(1, 0, SKColors.Green);
        bitmap.SetPixel(0, 1, SKColors.Blue); bitmap.SetPixel(1, 1, SKColors.White);
        return bitmap;
    }

    [Theory]
    [InlineData(int.MaxValue, 0, 1, 1)]
    [InlineData(0, int.MaxValue, 1, 1)]
    [InlineData(int.MaxValue - 1, 0, 2, 1)]
    [InlineData(0, int.MaxValue - 1, 1, 2)]
    public void Overflowing_crop_end_is_refused_before_creating_a_blank_derivative(int left, int top, int width, int height)
    {
        using var source = Source(); var before = source.GetPixelSpan().ToArray();
        var error = Assert.Throws<LeviathanException>(() =>
        {
            // Clean up even the unexpected old successful1px result: the
            // fail-before experiment must not itself leak a native bitmap.
            var result = DerivativeFactory.Apply(source, [new ImageTransform.Crop(left, top, width, height)]);
            if (!ReferenceEquals(result.Bitmap, source)) result.Bitmap.Dispose();
        });
        Assert.Equal(ErrorCode.StageContractViolation, error.Error.Code);
        Assert.Equal(before, source.GetPixelSpan().ToArray());
        Assert.NotEqual(IntPtr.Zero, source.Handle);
    }

    [Theory]
    [InlineData(-1, 0, 1, 1)]
    [InlineData(0, -1, 1, 1)]
    [InlineData(0, 0, 0, 1)]
    [InlineData(0, 0, 1, 0)]
    [InlineData(1, 0, 2, 1)]
    [InlineData(0, 1, 1, 2)]
    public void Existing_invalid_bounds_still_have_the_typed_contract_refusal(int left, int top, int width, int height)
    {
        using var source = Source();
        var error = Assert.Throws<LeviathanException>(() => DerivativeFactory.Apply(source,
            [new ImageTransform.Crop(left, top, width, height)]));
        Assert.Equal(ErrorCode.StageContractViolation, error.Error.Code);
        Assert.NotEqual(IntPtr.Zero, source.Handle);
    }

    [Theory]
    [InlineData(0, 0, 2, 2)]
    [InlineData(1, 1, 1, 1)]
    [InlineData(0, 1, 2, 1)]
    public void Exact_edge_crops_keep_pixels_metadata_and_caller_owned_source(int left, int top, int width, int height)
    {
        using var source = Source(); var before = source.GetPixelSpan().ToArray();
        var crop = new ImageTransform.Crop(left, top, width, height);
        var result = DerivativeFactory.Apply(source, [crop]);
        using var bitmap = result.Bitmap;
        Assert.NotSame(source, bitmap); Assert.Equal(width, bitmap.Width); Assert.Equal(height, bitmap.Height);
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            Assert.Equal(source.GetPixel(left + x, top + y), bitmap.GetPixel(x, y));
        Assert.Single(result.AppliedTransforms); Assert.Same(crop, result.AppliedTransforms[0]);
        Assert.Equal(crop.Description, result.TransformChain);
        Assert.Equal(before, source.GetPixelSpan().ToArray()); Assert.NotEqual(IntPtr.Zero, source.Handle);
    }
}
