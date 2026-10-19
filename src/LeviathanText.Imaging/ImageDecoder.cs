using LeviathanText.Contracts;
using SkiaSharp;

namespace LeviathanText.Imaging;

/// <summary>
/// Image decoding with bounded resource use (todo §4 Imaging module; todo §7 file-size
/// limits). Decoded bitmaps are disposed via the returned handle.
/// </summary>
public static class ImageDecoder
{
    /// <summary>Default archive/image size guard: 512 MB encoded, 12000×12000 pixels.</summary>
    public const long MaxEncodedBytes = 512L * 1024 * 1024;
    public const int MaxDimension = 12000;

    public sealed record DecodedImage(SKBitmap Bitmap, Checksum SourceChecksum, long EncodedBytes) : IDisposable
    {
        public void Dispose() => Bitmap.Dispose();
    }

    public static DecodedImage Decode(string path)
    {
        var file = new FileInfo(path);
        if (!file.Exists)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = path,
                Message = "Image file not found.",
            });
        if (file.Length > MaxEncodedBytes)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.RejectedArchive,
                Scope = path,
                Details = new Dictionary<string, string> { ["bytes"] = file.Length.ToString(), ["max"] = MaxEncodedBytes.ToString() },
                Message = "Encoded image exceeds the configured size limit.",
            });

        var bytes = File.ReadAllBytes(path);
        if (bytes.Length > MaxEncodedBytes)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.RejectedArchive,
                Scope = path,
                Details = new Dictionary<string, string> { ["bytes"] = bytes.Length.ToString(), ["max"] = MaxEncodedBytes.ToString() },
                Message = "Encoded image exceeds the configured size limit.",
            });

        var checksum = ChecksumOf(bytes);
        var bitmap = SKBitmap.Decode(bytes);
        if (bitmap is null)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.RejectedArchive,
                Scope = path,
                Message = "Image could not be decoded.",
            });
        if (bitmap.Width > MaxDimension || bitmap.Height > MaxDimension)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.RejectedArchive,
                Scope = path,
                Details = new Dictionary<string, string> { ["width"] = bitmap.Width.ToString(), ["height"] = bitmap.Height.ToString() },
                Message = "Image dimensions exceed the configured limit.",
            });

        return new DecodedImage(bitmap, checksum, bytes.Length);
    }

    public static Checksum ChecksumOf(Stream stream)
    {
        stream.Position = 0;
        var hash = System.Security.Cryptography.SHA256.HashData(stream);
        return new Checksum { Algorithm = "sha256", Digest = Convert.ToHexStringLower(hash) };
    }

    public static Checksum ChecksumOf(ReadOnlySpan<byte> bytes)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return new Checksum { Algorithm = "sha256", Digest = Convert.ToHexStringLower(hash) };
    }
}

/// <summary>
/// A recorded image transform (todo §3: crop/deskew/dewarp produce traceable
/// derivatives; information loss is recorded, not assumed away). Affine transforms are
/// invertible and support coordinate mapping; nonlinear dewarp is recorded but has no
/// coordinate mapping until implemented (explicitly unsupported, not silently absent).
/// </summary>
public abstract record ImageTransform
{
    /// <summary>Bounded, human-readable identity recorded on the derivative.</summary>
    public abstract string Description { get; }

    /// <summary>Map a point from the source frame into the derivative frame; null when the transform cannot map points.</summary>
    public abstract SKPoint? Map(SKPoint source);

    public sealed record Crop(int Left, int Top, int Width, int Height) : ImageTransform
    {
        public override string Description => $"crop:l{Left},t{Top},w{Width},h{Height}";
        public override SKPoint? Map(SKPoint source) => new(source.X - Left, source.Y - Top);
    }

    public sealed record Deskew(double AngleDegrees, int SourceWidth, int SourceHeight) : ImageTransform
    {
        private static SKPoint Pivot(double w, double h) => new((float)(w / 2), (float)(h / 2));

        public override string Description => $"deskew:{AngleDegrees:0.###}deg";

        /// <summary>
        /// Replicates the canvas mapping device = T(pivot)·R(angle)·T(-pivot)·source
        /// used by <see cref="DerivativeFactory"/> when deskewing about the center.
        /// </summary>
        public override SKPoint? Map(SKPoint source)
        {
            var pivot = Pivot(SourceWidth, SourceHeight);
            var rel = source - pivot;
            var rad = AngleDegrees * Math.PI / 180.0;
            var cos = Math.Cos(rad);
            var sin = Math.Sin(rad);
            return new SKPoint(
                pivot.X + (float)(rel.X * cos - rel.Y * sin),
                pivot.Y + (float)(rel.X * sin + rel.Y * cos));
        }
    }

    /// <summary>Recorded but not mappable: any geometry in this derivative's frame must not be transferred (todo §3).</summary>
    public sealed record NonlinearDewarp(string ControlPointsRef) : ImageTransform
    {
        public override string Description => $"dewarp:{ControlPointsRef}";
        public override SKPoint? Map(SKPoint source) => null; // explicitly unmappable
    }
}

/// <summary>
/// Produces and records derivatives: crop/deskew an original capture into a derivative
/// image whose lineage and transforms are kept (acceptance invariant: original remains
/// retrievable and the full parent chain is recorded).
/// </summary>
public static class DerivativeFactory
{
    public sealed record DerivativeResult(
        SKBitmap Bitmap,
        IReadOnlyList<ImageTransform> AppliedTransforms,
        string TransformChain);

    public static DerivativeResult Apply(SKBitmap source, IReadOnlyList<ImageTransform> transforms)
    {
        var bitmap = source;
        var applied = new List<ImageTransform>();
        foreach (var transform in transforms)
        {
            bitmap = transform switch
            {
                ImageTransform.Crop crop => CropBitmap(bitmap, crop),
                ImageTransform.Deskew deskew => DeskewBitmap(bitmap, deskew),
                ImageTransform.NonlinearDewarp => throw new LeviathanException(new LeviathanError
                {
                    Code = ErrorCode.StageContractViolation,
                    Scope = "imaging",
                    RemedyHint = RemedyHint.None,
                    Message = "Nonlinear dewarp is recorded as a transform but has no implementation yet; refusing to fake it.",
                }),
                _ => throw new InvalidOperationException(transform.Description),
            };
            applied.Add(transform);
        }
        return new DerivativeResult(bitmap, applied, string.Join(">", applied.Select(t => t.Description)));
    }

    private static SKBitmap CropBitmap(SKBitmap source, ImageTransform.Crop crop)
    {
        if (crop.Left < 0 || crop.Top < 0 || crop.Width <= 0 || crop.Height <= 0
            || crop.Left + crop.Width > source.Width || crop.Top + crop.Height > source.Height)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = "imaging",
                Details = new Dictionary<string, string>
                {
                    ["requested"] = crop.Description,
                    ["source"] = $"{source.Width}x{source.Height}",
                },
                Message = "Crop bounds fall outside the source image.",
            });

        var result = new SKBitmap(crop.Width, crop.Height);
        using var canvas = new SKCanvas(result);
        canvas.DrawBitmap(source, new SKPoint(-crop.Left, -crop.Top), new SKSamplingOptions(SKFilterMode.Nearest));
        return result;
    }

    private static SKBitmap DeskewBitmap(SKBitmap source, ImageTransform.Deskew deskew)
    {
        var rotated = new SKBitmap(source.Width, source.Height);
        using var canvas = new SKCanvas(rotated);
        canvas.Translate(source.Width / 2f, source.Height / 2f);
        canvas.RotateDegrees((float)deskew.AngleDegrees);
        canvas.Translate(-source.Width / 2f, -source.Height / 2f);
        canvas.DrawBitmap(source, new SKPoint(0, 0), new SKSamplingOptions(SKFilterMode.Linear));
        return rotated;
    }
}
