using Xunit;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using LeviathanText.Contracts;
using LeviathanText.Imaging;
using SkiaSharp;

namespace LeviathanText.Imaging.Tests;
sealed class Deriv02GeneratedFault : Exception
{
    public Deriv02GeneratedFault() : base("generated-deriv02-fault")
    {
    }
}

static class Deriv02CaseLogic
{
    static string Hash(byte[] b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
    static byte[] Pixels(SKBitmap b)
    {
        var a = new byte[b.Width * b.Height * 4];
        int i = 0;
        for (int y = 0; y < b.Height; y++)
            for (int x = 0; x < b.Width; x++)
            {
                var c = b.GetPixel(x, y);
                a[i++] = c.Red;
                a[i++] = c.Green;
                a[i++] = c.Blue;
                a[i++] = c.Alpha;
            }

        return a;
    }

    static object View(SKBitmap b) => new
    {
        b.Width,
        b.Height,
        ColorType = b.ColorType.ToString(),
        AlphaType = b.AlphaType.ToString(),
        rgba = Hash(Pixels(b)),
        live = b.Handle != IntPtr.Zero
    };
    static SKBitmap Source()
    {
        var b = new SKBitmap(12, 8, SKColorType.Bgra8888, SKAlphaType.Premul);
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 12; x++)
                b.SetPixel(x, y, new SKColor((byte)(x * 19), (byte)(y * 31), (byte)((x + y) * 11), (byte)(x % 4 == 0 ? 0 : x % 4 == 1 ? 64 : x % 4 == 2 ? 128 : 255)));
        return b;
    }

    internal static void Generate(string dir)
    {
        Directory.CreateDirectory(dir);
        using var b = Source();
        using var image = SKImage.FromBitmap(b);
        foreach (var f in new[]
        {
            SKEncodedImageFormat.Png,
            SKEncodedImageFormat.Jpeg
        }

        )
        {
            using var d = image.Encode(f, 92);
            File.WriteAllBytes(Path.Combine(dir, f == SKEncodedImageFormat.Png ? "pattern.png" : "pattern.jpg"), d.ToArray());
        }

        using var m = new MemoryStream();
        m.Write(System.Text.Encoding.ASCII.GetBytes("P6\n12 8\n255\n"));
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 12; x++)
                m.Write(new byte[] { (byte)(x * 19 + 1), (byte)(y * 31 + 1), (byte)((x + y) * 11 + 1) });
        File.WriteAllBytes(Path.Combine(dir, "pattern.ppm"), m.ToArray());
    }

    static object Public(string dir)
    {
        var r = new SortedDictionary<string, object>();
        foreach (var name in new[]
        {
            "pattern.png",
            "pattern.jpg",
            "pattern.ppm"
        }

        )
        {
            var p = Path.Combine(dir, name);
            using (var d = ImageDecoder.Decode(p))
                r[name + "/decode"] = new
                {
                    bitmap = View(d.Bitmap),
                    checksum = d.SourceChecksum.ToString(),
                    d.EncodedBytes
                };
            foreach (long budget in new long[]
            {
                1,
                4,
                12,
                96
            }

            )
            {
                using var d = ImageDecoder.DecodeBounded(File.ReadAllBytes(p), "generated", budget);
                r[name + "/bounded/" + budget] = new
                {
                    bitmap = View(d.Bitmap),
                    d.OriginalWidth,
                    d.OriginalHeight,
                    d.ScaleX,
                    d.ScaleY,
                    checksum = d.SourceChecksum.ToString()
                };
            }

            foreach (var(top, h)in new[]
            {
                (-2, 3),
                (2, 3),
                (7, 4)
            }

            )
            {
                using var b = ImageDecoder.DecodeBand(p, top, h);
                r[name + "/band/" + top] = View(b);
            }
        }

        using var source = Source();
        var before = Hash(Pixels(source));
        var noop = DerivativeFactory.Apply(source, Array.Empty<ImageTransform>());
        r["noop"] = new
        {
            alias = ReferenceEquals(source, noop.Bitmap),
            bitmap = View(source),
            noop.TransformChain
        };
        foreach (var(name, ts)in new[]
        {
            ("crop", new ImageTransform[] { new ImageTransform.Crop(2, 1, 7, 5) }),
            ("deskew", new ImageTransform[] { new ImageTransform.Deskew(13, 12, 8) }),
            ("chain", new ImageTransform[] { new ImageTransform.Crop(2, 1, 7, 5), new ImageTransform.Deskew(-7, 7, 5) })
        }

        )
        {
            var d = DerivativeFactory.Apply(source, ts);
            using var b = d.Bitmap;
            var point = new SKPoint(4, 3);
            foreach (var t in ts)
                point = t.Map(point)!.Value;
            r[name] = new
            {
                bitmap = View(b),
                d.TransformChain,
                transforms = d.AppliedTransforms.Select(x => x.Description),
                point = new[]
                {
                    point.X,
                    point.Y
                }
            };
        }

        r["sourcePreserved"] = new
        {
            unchanged = before == Hash(Pixels(source)),
            live = source.Handle != IntPtr.Zero
        };
        return r;
    }

    static MethodInfo Method(string route) => route == "crop" || route == "deskew" || route == "chain" ? typeof(DerivativeFactory).GetMethod("ApplyWithBitmapProbe", BindingFlags.Static | BindingFlags.NonPublic)! : typeof(ImageDecoder).GetMethod(route == "bounded" ? "DecodeBoundedCore" : "DecodeBandCore", BindingFlags.Static | BindingFlags.NonPublic)!;
    static Delegate MakeProbe(Type type, Action<string, SKBitmap, SKCanvas?> callback)
    {
        var args = type.GetMethod("Invoke")!.GetParameters().Select(p => Expression.Parameter(p.ParameterType)).ToArray();
        return Expression.Lambda(type, Expression.Invoke(Expression.Constant(callback), Expression.Call(Expression.Convert(args[0], typeof(object)), typeof(object).GetMethod("ToString")!), args[1], args[2]), args).Compile();
    }

    internal static object Fault(string dir, string route, string file, long budget, string stage, int occurrence)
    {
        var captures = new List<SKBitmap>();
        var canvases = new List<SKCanvas>();
        var visits = new List<string>();
        int seen = 0;
        bool fired = false;
        var fault = new Deriv02GeneratedFault();
        using var source = Source();
        var original = Hash(Pixels(source));
        var method = Method(route) ?? throw new Exception("Required matched-extraction seam missing; NOT an original behavior failure.");
        var probe = MakeProbe(method.GetParameters()[^1].ParameterType, (s, b, c) =>
        {
            visits.Add(s);
            if (!captures.Contains(b))
                captures.Add(b);
            if (c != null && !canvases.Contains(c))
                canvases.Add(c);
            if (s == stage && ++seen == occurrence)
            {
                fired = true;
                throw fault;
            }
        });
        object? result = null;
        Exception? error = null;
        try
        {
            result = route switch
            {
                "bounded" => method.Invoke(null, new object? [] { File.ReadAllBytes(Path.Combine(dir, file)), "generated", budget, probe }),
                "band" => method.Invoke(null, new object? [] { Path.Combine(dir, file), 2, 3, probe }),
                _ => method.Invoke(null, new object? [] { source, route == "crop" ? new ImageTransform[] { new ImageTransform.Crop(2, 1, 7, 5) } : route == "deskew" ? new ImageTransform[] { new ImageTransform.Deskew(13, 12, 8) } : new ImageTransform[] { new ImageTransform.Crop(2, 1, 7, 5), new ImageTransform.Deskew(-7, 7, 5) }, probe })};
        }
        catch (TargetInvocationException e)
        {
            error = e.InnerException;
        }

        var states = captures.Select(b => b.Handle == IntPtr.Zero).ToArray();
        var canvasStates = canvases.Select(c => c.Handle == IntPtr.Zero).ToArray();
        bool preserved = source.Handle != IntPtr.Zero && original == Hash(Pixels(source));
        bool expectedError = route == "bounded" ? error is LeviathanException le && le.Error.Code == ErrorCode.RejectedArchive && le.Error.Scope == "generated" && le.Error.Details?["reason"] == "unsupported-image-format" && le.Error.Details?["exception"] == nameof(Deriv02GeneratedFault) : ReferenceEquals(error, fault);
        var returnedBitmap = result switch
        {
            ImageDecoder.ScaledImage si => si.Bitmap,
            SKBitmap sb => sb,
            DerivativeFactory.DerivativeResult dr => dr.Bitmap,
            _ => null
        };
        bool returnedLive = returnedBitmap?.Handle != IntPtr.Zero && returnedBitmap != null;
        bool successOwnership = error == null && returnedLive && captures.All(b => ReferenceEquals(b, returnedBitmap) || b.Handle == IntPtr.Zero) && canvasStates.All(x => x) && preserved;
        bool pass = stage == "none" ? successOwnership : fired && expectedError && states.Length > 0 && states.All(x => x) && canvasStates.All(x => x) && preserved;
        // Capture disposal evidence BEFORE controlled test cleanup, no reliance on finalizers.
        if (result is IDisposable disposable)
            disposable.Dispose();
        if (result is DerivativeFactory.DerivativeResult d && !ReferenceEquals(d.Bitmap, source))
            d.Bitmap.Dispose();
        foreach (var c in canvases)
            if (c.Handle != IntPtr.Zero)
                c.Dispose();
        foreach (var b in captures)
            if (b.Handle != IntPtr.Zero)
                b.Dispose();
        return new
        {
            pass,
            fired,
            expectedError,
            returnedLive,
            successOwnership,
            destinationsDisposed = states,
            canvasesDisposed = canvasStates,
            sourcePreserved = preserved,
            visits,
            error = error?.GetType().Name,
            errorCode = (error as LeviathanException)?.Error.Code.ToString()
        };
    }
}

public sealed class BitmapExceptionalOwnershipTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "leviathan-deriv02-" + Guid.NewGuid().ToString("N"));
    public BitmapExceptionalOwnershipTests() => Deriv02CaseLogic.Generate(directory);
    public void Dispose()
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }

    [Theory]
    [InlineData("bounded", "pattern.jpg", 1L, "DestinationAllocated", 1)]
    [InlineData("bounded", "pattern.jpg", 1L, "NativeInitialized", 1)]
    [InlineData("bounded", "pattern.jpg", 1L, "DestinationAllocated", 2)]
    [InlineData("bounded", "pattern.jpg", 1L, "NativeInitialized", 2)]
    [InlineData("bounded", "pattern.jpg", 1L, "BeforePublish", 1)]
    [InlineData("bounded", "pattern.jpg", 24L, "DestinationAllocated", 1)]
    [InlineData("bounded", "pattern.jpg", 24L, "NativeInitialized", 1)]
    [InlineData("bounded", "pattern.jpg", 24L, "BeforePublish", 1)]
    [InlineData("bounded", "pattern.png", 12L, "DestinationAllocated", 1)]
    [InlineData("bounded", "pattern.png", 12L, "CanvasCreated", 1)]
    [InlineData("bounded", "pattern.png", 12L, "NativeInitialized", 1)]
    [InlineData("bounded", "pattern.png", 12L, "BeforePublish", 1)]
    [InlineData("band", "pattern.jpg", 0L, "DestinationAllocated", 1)]
    [InlineData("band", "pattern.jpg", 0L, "NativeInitialized", 1)]
    [InlineData("band", "pattern.jpg", 0L, "BeforePublish", 1)]
    [InlineData("band", "pattern.ppm", 0L, "DestinationAllocated", 1)]
    [InlineData("band", "pattern.ppm", 0L, "NativeInitialized", 1)]
    [InlineData("band", "pattern.ppm", 0L, "BeforePublish", 1)]
    [InlineData("crop", "unused", 0L, "DestinationAllocated", 1)]
    [InlineData("crop", "unused", 0L, "CanvasCreated", 1)]
    [InlineData("crop", "unused", 0L, "NativeInitialized", 1)]
    [InlineData("crop", "unused", 0L, "BeforePublish", 1)]
    [InlineData("deskew", "unused", 0L, "DestinationAllocated", 1)]
    [InlineData("deskew", "unused", 0L, "CanvasCreated", 1)]
    [InlineData("deskew", "unused", 0L, "NativeInitialized", 1)]
    [InlineData("deskew", "unused", 0L, "BeforePublish", 1)]
    [InlineData("chain", "unused", 0L, "DestinationAllocated", 2)]
    [InlineData("chain", "unused", 0L, "CanvasCreated", 2)]
    [InlineData("chain", "unused", 0L, "NativeInitialized", 2)]
    [InlineData("chain", "unused", 0L, "BeforePublish", 2)]
    [InlineData("bounded", "pattern.jpg", 1L, "none", 1)]
    [InlineData("bounded", "pattern.jpg", 24L, "none", 1)]
    [InlineData("bounded", "pattern.png", 12L, "none", 1)]
    [InlineData("band", "pattern.jpg", 0L, "none", 1)]
    [InlineData("band", "pattern.ppm", 0L, "none", 1)]
    [InlineData("crop", "unused", 0L, "none", 1)]
    [InlineData("deskew", "unused", 0L, "none", 1)]
    [InlineData("chain", "unused", 0L, "none", 1)]
    public void Destination_cleanup_and_successful_handoff_are_exact(string route, string file, long budget, string stage, int occurrence)
    {
        var result = JsonSerializer.SerializeToElement(Deriv02CaseLogic.Fault(directory, route, file, budget, stage, occurrence));
        Assert.True(result.GetProperty("pass").GetBoolean(), $"{route}/{file}/budget={budget}/{stage}/{occurrence}: {result}");
    }

    [Fact]
    public void Public_crop_preserves_exact_pixels_coordinates_source_and_caller_ownership()
    {
        using var source = new SKBitmap(5, 4, SKColorType.Bgra8888, SKAlphaType.Premul);
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 5; x++)
                source.SetPixel(x, y, new SKColor((byte)(x * 40), (byte)(y * 60), (byte)(x + y), 255));
        var noop = DerivativeFactory.Apply(source, Array.Empty<ImageTransform>());
        Assert.Same(source, noop.Bitmap);
        Assert.Empty(noop.AppliedTransforms);
        Assert.Equal("", noop.TransformChain);
        var crop = new ImageTransform.Crop(2, 1, 3, 3);
        var result = DerivativeFactory.Apply(source, new ImageTransform[] { crop });
        using (result.Bitmap)
        {
            Assert.NotEqual(IntPtr.Zero, result.Bitmap.Handle);
            Assert.Equal("crop:l2,t1,w3,h3", result.TransformChain);
            Assert.Equal(new SKPoint(2, 2), crop.Map(new SKPoint(4, 3))!.Value);
            for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++)
                    Assert.Equal(source.GetPixel(x + 2, y + 1), result.Bitmap.GetPixel(x, y));
        }

        Assert.NotEqual(IntPtr.Zero, source.Handle);
        Assert.Equal(new SKColor(160, 180, 7, 255), source.GetPixel(4, 3));
    }
}
