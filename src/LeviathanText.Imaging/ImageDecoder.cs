using LeviathanText.Contracts;
using SkiaSharp;

namespace LeviathanText.Imaging;

/// <summary>
/// Image decoding with bounded resource use (project-plan §4 Imaging module; project-plan §7 file-size
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

    /// <summary>
    /// Bounded-pixel decode: images above <paramref name="maxPixels"/> are decoded at a
    /// reduced scale (codec-level scaling, so the full bitmap never exists in memory).
    /// Callers map coordinates back with <see cref="ScaledImage.ScaleX"/>/<see cref="ScaledImage.ScaleY"/>.
    /// This is the preview path for layout on very large captures; recognition still needs
    /// full-resolution pixels for line crops (tiled region decode is not implemented).
    /// </summary>
    public sealed record ScaledImage(SKBitmap Bitmap, int OriginalWidth, int OriginalHeight, Checksum SourceChecksum) : IDisposable
    {
        public double ScaleX => OriginalWidth == 0 ? 1 : (double)Bitmap.Width / OriginalWidth;
        public double ScaleY => OriginalHeight == 0 ? 1 : (double)Bitmap.Height / OriginalHeight;
        public bool IsScaled => ScaleX < 0.999 || ScaleY < 0.999;
        public void Dispose() => Bitmap.Dispose();
    }

    public static ScaledImage DecodeBounded(string path, long maxPixels = 12_000_000)
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
        var checksum = ChecksumOf(bytes);

        try
        {
            using var codec = SKCodec.Create(new MemoryStream(bytes));
            if (codec is null)
                throw new LeviathanException(new LeviathanError
                {
                    Code = ErrorCode.RejectedArchive,
                    Scope = path,
                    Details = new Dictionary<string, string> { ["reason"] = "unsupported-image-format" },
                    Message = "Image format is not supported by the current imaging backend.",
                });

            var info = codec.Info;
            if (info.Width > MaxDimension || info.Height > MaxDimension)
                throw new LeviathanException(new LeviathanError
                {
                    Code = ErrorCode.RejectedArchive,
                    Scope = path,
                    Details = new Dictionary<string, string> { ["width"] = info.Width.ToString(), ["height"] = info.Height.ToString() },
                    Message = "Image dimensions exceed the configured limit.",
                });

            long pixels = (long)info.Width * info.Height;
            if (pixels <= maxPixels)
            {
                var full = SKBitmap.Decode(new MemoryStream(bytes));
                if (full is null)
                    throw new LeviathanException(new LeviathanError
                    {
                        Code = ErrorCode.RejectedArchive,
                        Scope = path,
                        Message = "Image could not be decoded.",
                    });
                return new ScaledImage(full, info.Width, info.Height, checksum);
            }

            var desired = (float)Math.Sqrt((double)maxPixels / pixels);
            var dimensions = codec.GetScaledDimensions(desired);

            if (dimensions.Width < info.Width || dimensions.Height < info.Height)
            {
                // Codec-level scaling (JPEG/WebP): the full bitmap never exists.
                var scaled = new SKBitmap(dimensions.Width, dimensions.Height, info.ColorType, info.AlphaType);
                var result = codec.GetPixels(scaled.Info, scaled.GetPixels());
                if (result is not SKCodecResult.Success and not SKCodecResult.IncompleteInput)
                {
                    scaled.Dispose();
                    throw new LeviathanException(new LeviathanError
                    {
                        Code = ErrorCode.RejectedArchive,
                        Scope = path,
                        Details = new Dictionary<string, string> { ["reason"] = "scaled-decode-failed", ["result"] = result.ToString() },
                        Message = "Scaled decode failed for this image.",
                    });
                }
                if (result == SKCodecResult.IncompleteInput)
                    scaled.Erase(SKColors.Black);
                return new ScaledImage(scaled, info.Width, info.Height, checksum);
            }

            // Formats without codec-level scaling (e.g. PNG in this build): decode full and
            // resample. This still bounds the bitmap handed to downstream stages, but the
            // temporary full decode is bounded only by MaxDimension — a documented limit.
            int targetWidth = Math.Max(1, (int)Math.Round(info.Width * (double)desired));
            int targetHeight = Math.Max(1, (int)Math.Round(info.Height * (double)desired));
            using var decodedFull = SKBitmap.Decode(new MemoryStream(bytes))
                ?? throw new LeviathanException(new LeviathanError
                {
                    Code = ErrorCode.RejectedArchive,
                    Scope = path,
                    Message = "Image could not be decoded.",
                });
            var resampled = new SKBitmap(targetWidth, targetHeight, info.ColorType, info.AlphaType);
            using (var canvas = new SKCanvas(resampled))
            {
                canvas.DrawBitmap(decodedFull,
                    new SKRect(0, 0, decodedFull.Width, decodedFull.Height),
                    new SKRect(0, 0, targetWidth, targetHeight),
                    new SKSamplingOptions(SKFilterMode.Linear));
            }
            return new ScaledImage(resampled, info.Width, info.Height, checksum);
        }
        catch (LeviathanException) { throw; }
        catch (Exception ex)
        {
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.RejectedArchive,
                Scope = path,
                Details = new Dictionary<string, string> { ["reason"] = "unsupported-image-format", ["exception"] = ex.GetType().Name },
                Message = "Image format is not supported by the current imaging backend.",
            });
        }
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
        SKBitmap? bitmap;
        try
        {
            bitmap = SKBitmap.Decode(bytes);
        }
        catch (Exception ex)
        {
            // Some codecs are missing in a given SkiaSharp build (e.g. TIFF: Decode throws
            // ArgumentNullException 'codec'). Surface that as a contract error, not a crash.
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.RejectedArchive,
                Scope = path,
                Details = new Dictionary<string, string> { ["reason"] = "unsupported-image-format", ["exception"] = ex.GetType().Name },
                Message = "Image format is not supported by the current imaging backend.",
            });
        }
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

    /// <summary>
    /// Decode one horizontal band without materializing the whole image. JPEG and other
    /// codecs with top-down scanline support use bounded scanline decoding; unsupported
    /// codecs fall back to a full decode and subset. This is the line-crop path for very
    /// large JPEG pages, not a general tiling backend.
    /// </summary>
    public static SKBitmap DecodeBand(string path, int top, int height)
    {
        var (width, pageHeight) = Dimensions(path);
        top = Math.Clamp(top, 0, Math.Max(0, pageHeight - 1));
        height = Math.Clamp(height, 1, pageHeight - top);
        if (width <= 0 || height <= 0)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = path,
                Message = "Requested image band is empty or outside the image.",
            });

        using (var codec = SKCodec.Create(path))
        {
            if (codec is not null && codec.ScanlineOrder == SKCodecScanlineOrder.TopDown)
            {
                // StartScanlineDecode needs the full image info; the output bitmap is
                // still only the requested band, so peak memory is width × band height.
                var decodeInfo = new SKImageInfo(codec.Info.Width, codec.Info.Height,
                    SKColorType.Bgra8888, SKAlphaType.Premul);
                var started = codec.StartScanlineDecode(decodeInfo);
                if (started == SKCodecResult.Success)
                {
                    if (top > 0 && !codec.SkipScanlines(top))
                    {
                        // Fall through to the full-decode fallback.
                    }
                    else
                    {
                        var bandInfo = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
                        var band = new SKBitmap(bandInfo);
                        var rows = codec.GetScanlines(band.GetPixels(), height, band.RowBytes);
                        if (rows == height) return band;
                        band.Dispose();
                    }
                }
            }
        }

        using var full = SKBitmap.Decode(path)
            ?? throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.RejectedArchive,
                Scope = path,
                Message = "Image could not be decoded.",
            });
        var subset = new SKBitmap();
        full.ExtractSubset(subset, new SKRectI(0, top, Math.Min(width, full.Width), Math.Min(top + height, full.Height)));
        return subset;
    }

    /// <summary>Read image dimensions without retaining the decoded bitmap.</summary>
    public static (int Width, int Height) Dimensions(string path)
    {
        using var codec = SKCodec.Create(path);
        if (codec is null)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.RejectedArchive,
                Scope = path,
                Message = "Image format is not supported by the current imaging backend.",
            });
        return (codec.Info.Width, codec.Info.Height);
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
/// A recorded image transform (project-plan §3: crop/deskew/dewarp produce traceable
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

    /// <summary>Recorded but not mappable: any geometry in this derivative's frame must not be transferred (project-plan §3).</summary>
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
