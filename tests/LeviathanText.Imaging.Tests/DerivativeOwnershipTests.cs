using System.Reflection;
using System.Runtime.ExceptionServices;
using LeviathanText.Contracts;
using LeviathanText.Imaging;
using SkiaSharp;
using Xunit;

namespace LeviathanText.Imaging.Tests;

// Run against BOTH the explicitly labeled extraction-baseline and candidate.
// A missing new helper in an original binary is not evidence of the old bug.
public sealed class DerivativeOwnershipTests
{
    private static SKBitmap Source()
    {
        var source = new SKBitmap(2, 2, SKColorType.Bgra8888, SKAlphaType.Premul);
        source.SetPixel(0, 0, new SKColor(180, 40, 90, 128)); source.SetPixel(1, 0, SKColors.Green);
        source.SetPixel(0, 1, SKColors.Blue); source.SetPixel(1, 1, SKColors.White); return source;
    }
    private static T Invoke<T>(string method, params object[] args)
    {
        var info = typeof(DerivativeFactory).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Use the extraction-baseline/candidate: a missing helper is not fail-before proof.");
        try { return (T)info.Invoke(null, args)!; }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
    private static DerivativeFactory.DerivativeResult Core(SKBitmap source, IReadOnlyList<ImageTransform> transforms,
        Func<SKBitmap, ImageTransform, SKBitmap> step) => Invoke<DerivativeFactory.DerivativeResult>("ApplyCore", source, transforms, step);
    private static SKBitmap Default(SKBitmap source, ImageTransform transform) => Invoke<SKBitmap>("ApplyTransform", source, transform);
    private static void Cleanup(IEnumerable<SKBitmap> created, SKBitmap source)
    { foreach (var bitmap in created.Distinct()) if (!ReferenceEquals(bitmap, source)) bitmap.Dispose(); }
    private static ImageTransform[] Steps() =>
        [new ImageTransform.Crop(0, 0, 2, 2), new ImageTransform.Deskew(0, 2, 2), new ImageTransform.Crop(0, 0, 1, 1)];

    [Fact]
    public void Replaced_real_intermediates_are_disposed_but_final_and_source_remain_alive()
    {
        using var source = Source(); var before = source.GetPixelSpan().ToArray(); var created = new List<SKBitmap>();
        try
        {
            var steps = Steps();
            var result = Core(source, steps, (input, transform) =>
            { Assert.NotEqual(IntPtr.Zero, input.Handle); var next = Default(input, transform); created.Add(next); return next; });
            Assert.Equal(3, created.Count); Assert.Equal(IntPtr.Zero, created[0].Handle); Assert.Equal(IntPtr.Zero, created[1].Handle);
            Assert.Same(created[2], result.Bitmap); Assert.NotEqual(IntPtr.Zero, result.Bitmap.Handle);
            Assert.Equal(source.GetPixel(0, 0), result.Bitmap.GetPixel(0, 0));
            Assert.Equal(steps, result.AppliedTransforms); Assert.Equal(string.Join(">", steps.Select(t => t.Description)), result.TransformChain);
            Assert.Equal(before, source.GetPixelSpan().ToArray()); Assert.NotEqual(IntPtr.Zero, source.Handle);
            result.Bitmap.Dispose(); Assert.Equal(IntPtr.Zero, result.Bitmap.Handle); Assert.NotEqual(IntPtr.Zero, source.Handle);
        }
        finally { Cleanup(created, source); } // Also cleans up failing old-algorithm observations.
    }

    [Fact]
    public void A_later_real_unsupported_transform_releases_the_previous_owned_intermediate()
    {
        using var source = Source(); var created = new List<SKBitmap>();
        try
        {
            var error = Assert.Throws<LeviathanException>(() => Core(source,
                [new ImageTransform.Crop(0, 0, 1, 1), new ImageTransform.NonlinearDewarp("generated-control")],
                (input, transform) => { var next = Default(input, transform); created.Add(next); return next; }));
            Assert.Equal(ErrorCode.StageContractViolation, error.Error.Code);
            Assert.Single(created); Assert.Equal(IntPtr.Zero, created[0].Handle); Assert.NotEqual(IntPtr.Zero, source.Handle);
        }
        finally { Cleanup(created, source); }
    }

    [Fact]
    public void Metadata_publication_failure_releases_final_owned_bitmap_and_preserves_original_exception()
    {
        using var source = Source(); var created = new List<SKBitmap>(); var failure = new FixtureFailure();
        try
        {
            var error = Assert.Throws<FixtureFailure>(() => Core(source,
                [new ImageTransform.Crop(0, 0, 1, 1), new ThrowsDescription(failure)],
                (input, transform) =>
                {
                    var next = transform is ThrowsDescription ? new SKBitmap(1, 1) : Default(input, transform);
                    created.Add(next); return next;
                }));
            Assert.Same(failure, error); Assert.Equal(2, created.Count);
            Assert.All(created, bitmap => Assert.Equal(IntPtr.Zero, bitmap.Handle)); Assert.NotEqual(IntPtr.Zero, source.Handle);
        }
        finally { Cleanup(created, source); }
    }

    [Fact]
    public void Internal_source_alias_never_transfers_or_disposes_caller_owned_source()
    {
        using var source = Source(); var created = new List<SKBitmap>();
        try
        {
            var result = Core(source, [new ImageTransform.Crop(0, 0, 1, 1), new ImageTransform.Deskew(0, 1, 1)],
                (input, transform) =>
                { if (transform is ImageTransform.Deskew) return source; var next = Default(input, transform); created.Add(next); return next; });
            Assert.Same(source, result.Bitmap); Assert.Single(created); Assert.Equal(IntPtr.Zero, created[0].Handle);
            Assert.NotEqual(IntPtr.Zero, source.Handle);
        }
        finally { Cleanup(created, source); }
    }

    [Fact]
    public void Internal_identity_step_does_not_dispose_the_still_returned_owned_bitmap()
    {
        using var source = Source(); var created = new List<SKBitmap>();
        try
        {
            var result = Core(source, [new ImageTransform.Crop(0, 0, 1, 1), new ImageTransform.Deskew(0, 1, 1)],
                (input, transform) =>
                { if (transform is ImageTransform.Deskew) return input; var next = Default(input, transform); created.Add(next); return next; });
            Assert.Single(created); Assert.Same(created[0], result.Bitmap); Assert.NotEqual(IntPtr.Zero, result.Bitmap.Handle);
            Assert.NotEqual(IntPtr.Zero, source.Handle);
        }
        finally { Cleanup(created, source); }
    }

    [Fact]
    public void First_step_failure_keeps_caller_owned_source_alive()
    {
        using var source = Source(); var before = source.GetPixelSpan().ToArray(); var failure = new FixtureFailure();
        var error = Assert.Throws<FixtureFailure>(() => Core(source, [new ImageTransform.Crop(0, 0, 1, 1)], (_, _) => throw failure));
        Assert.Same(failure, error); Assert.Equal(before, source.GetPixelSpan().ToArray()); Assert.NotEqual(IntPtr.Zero, source.Handle);
    }

    [Fact]
    public void Public_empty_transform_sequence_keeps_original_reference_and_empty_metadata()
    {
        using var source = Source(); var before = source.GetPixelSpan().ToArray();
        var result = DerivativeFactory.Apply(source, []);
        Assert.Same(source, result.Bitmap); Assert.Empty(result.AppliedTransforms); Assert.Equal("", result.TransformChain);
        Assert.Equal(before, source.GetPixelSpan().ToArray()); Assert.NotEqual(IntPtr.Zero, source.Handle);
    }

    [Fact]
    public void Real_public_crop_deskew_crop_keeps_exact_pixels_metadata_and_source()
    {
        using var source = Source(); var before = source.GetPixelSpan().ToArray(); var steps = Steps();
        var result = DerivativeFactory.Apply(source, steps); using var bitmap = result.Bitmap;
        Assert.NotSame(source, bitmap); Assert.Equal((1, 1), (bitmap.Width, bitmap.Height));
        Assert.Equal(source.GetPixel(0, 0), bitmap.GetPixel(0, 0));
        Assert.Equal(steps, result.AppliedTransforms); Assert.Equal(string.Join(">", steps.Select(t => t.Description)), result.TransformChain);
        Assert.Equal(before, source.GetPixelSpan().ToArray()); Assert.NotEqual(IntPtr.Zero, source.Handle);
    }

    private sealed class FixtureFailure : Exception;
    private sealed record ThrowsDescription(FixtureFailure Failure) : ImageTransform
    {
        public override string Description => throw Failure;
        public override SKPoint? Map(SKPoint source) => source;
    }
}
