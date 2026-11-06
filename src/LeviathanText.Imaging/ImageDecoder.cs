using LeviathanText.Contracts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
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
        ValidatePixelBudget(maxPixels, path);
        if (file.Length > MaxEncodedBytes)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.RejectedArchive,
                Scope = path,
                Details = new Dictionary<string, string> { ["bytes"] = file.Length.ToString(), ["max"] = MaxEncodedBytes.ToString() },
                Message = "Encoded image exceeds the configured size limit.",
            });

        return DecodeBounded(File.ReadAllBytes(path), path, maxPixels);
    }

    public static ScaledImage DecodeBounded(byte[] bytes, string path, long maxPixels = 12_000_000)
        => DecodeBoundedCore(bytes, path, maxPixels, null);

    internal static ScaledImage DecodeBoundedCore(byte[] bytes, string path, long maxPixels, BitmapConstructionProbe? probe)
    {
        ValidatePixelBudget(maxPixels, path);
        if (bytes.LongLength > MaxEncodedBytes)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.RejectedArchive,
                Scope = path,
                Message = "Encoded image exceeds the configured size limit.",
            });
        var checksum = ChecksumOf(bytes);

        try
        {
            using var codec = SKCodec.Create(new MemoryStream(bytes));
            if (codec is null)
            {
                var fallback = TryDecodeWithImageSharp(bytes, path)
                    ?? throw new LeviathanException(new LeviathanError
                    {
                        Code = ErrorCode.RejectedArchive,
                        Scope = path,
                        Details = new Dictionary<string, string> { ["reason"] = "unsupported-image-format" },
                        Message = "Image format is not supported by the current imaging backend.",
                    });
                long fallbackPixels = (long)fallback.Width * fallback.Height;
                int originalWidth = fallback.Width, originalHeight = fallback.Height;
                if (fallbackPixels <= maxPixels) return new ScaledImage(fallback, originalWidth, originalHeight, checksum);
                float factor = (float)Math.Sqrt((double)maxPixels / fallbackPixels);
                int targetW = Math.Max(1, (int)Math.Round(fallback.Width * factor));
                int targetH = Math.Max(1, (int)Math.Round(fallback.Height * factor));
                if ((long)targetW * targetH > maxPixels)
                    (targetW, targetH) = IntegerFit(originalWidth, originalHeight, maxPixels);
                try
                {
                    var scaledFallback = fallback.Resize(new SKImageInfo(targetW, targetH), new SKSamplingOptions(SKFilterMode.Linear))
                        ?? throw new InvalidDataException("Fallback image could not be resampled.");
                    return new ScaledImage(scaledFallback, originalWidth, originalHeight, checksum);
                }
                finally { fallback.Dispose(); }
            }

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
                bool scaledTransferred = false;
                try
                {
                    probe?.Invoke(BitmapConstructionStage.DestinationAllocated, scaled, null);
                    var result = codec.GetPixels(scaled.Info, scaled.GetPixels());
                    if (result is not SKCodecResult.Success and not SKCodecResult.IncompleteInput)
                    {
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
                    probe?.Invoke(BitmapConstructionStage.NativeInitialized, scaled, null);
                    if ((long)scaled.Width * scaled.Height > maxPixels)
                    {
                        var (fitWidth, fitHeight) = IntegerFit(info.Width, info.Height, maxPixels);
                        var fitted = scaled.Resize(new SKImageInfo(fitWidth, fitHeight, scaled.ColorType, scaled.AlphaType),
                            new SKSamplingOptions(SKFilterMode.Linear))
                            ?? throw new InvalidDataException("Native image intermediate could not be resampled.");
                        bool fittedTransferred = false;
                        try
                        {
                            probe?.Invoke(BitmapConstructionStage.DestinationAllocated, fitted, null);
                            probe?.Invoke(BitmapConstructionStage.NativeInitialized, fitted, null);
                            var fittedResult = new ScaledImage(fitted, info.Width, info.Height, checksum);
                            probe?.Invoke(BitmapConstructionStage.BeforePublish, fitted, null);
                            fittedTransferred = true;
                            return fittedResult;
                        }
                        finally { if (!fittedTransferred) fitted.Dispose(); }
                    }
                    var scaledResult = new ScaledImage(scaled, info.Width, info.Height, checksum);
                    probe?.Invoke(BitmapConstructionStage.BeforePublish, scaled, null);
                    scaledTransferred = true;
                    return scaledResult;
                }
                finally { if (!scaledTransferred) scaled.Dispose(); }
            }

            // Formats without codec-level scaling (e.g. PNG in this build): decode full and
            // resample. This still bounds the bitmap handed to downstream stages, but the
            // temporary full decode is bounded only by MaxDimension — a documented limit.
            int targetWidth = Math.Max(1, (int)Math.Round(info.Width * (double)desired));
            int targetHeight = Math.Max(1, (int)Math.Round(info.Height * (double)desired));
            if ((long)targetWidth * targetHeight > maxPixels)
                (targetWidth, targetHeight) = IntegerFit(info.Width, info.Height, maxPixels);
            using var decodedFull = SKBitmap.Decode(new MemoryStream(bytes))
                ?? throw new LeviathanException(new LeviathanError
                {
                    Code = ErrorCode.RejectedArchive,
                    Scope = path,
                    Message = "Image could not be decoded.",
                });
            var resampled = new SKBitmap(targetWidth, targetHeight, info.ColorType, info.AlphaType);
            bool resampledTransferred = false;
            try
            {
                probe?.Invoke(BitmapConstructionStage.DestinationAllocated, resampled, null);
                using (var canvas = new SKCanvas(resampled))
                {
                    probe?.Invoke(BitmapConstructionStage.CanvasCreated, resampled, canvas);
                    canvas.DrawBitmap(decodedFull,
                        new SKRect(0, 0, decodedFull.Width, decodedFull.Height),
                        new SKRect(0, 0, targetWidth, targetHeight),
                        new SKSamplingOptions(SKFilterMode.Linear));
                    probe?.Invoke(BitmapConstructionStage.NativeInitialized, resampled, canvas);
                }
                var resampledResult = new ScaledImage(resampled, info.Width, info.Height, checksum);
                probe?.Invoke(BitmapConstructionStage.BeforePublish, resampled, null);
                resampledTransferred = true;
                return resampledResult;
            }
            finally { if (!resampledTransferred) resampled.Dispose(); }
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

    private static void ValidatePixelBudget(long maxPixels, string path)
    {
        if (maxPixels <= 0)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation, Scope = path,
                Details = new Dictionary<string, string> { ["reason"] = "invalid-pixel-budget" },
                Message = "The decoded pixel budget must be positive.",
            });
    }

    private static (int Width, int Height) IntegerFit(int width, int height, long budget)
    {
        double scale = Math.Min(1, Math.Sqrt((double)budget / ((long)width * height)));
        int fitWidth = Math.Clamp((int)Math.Floor(width * scale), 1, width);
        int fitHeight = Math.Clamp((int)Math.Floor(height * scale), 1, height);
        if ((long)fitWidth * fitHeight > budget)
        {
            if (fitWidth >= fitHeight) fitWidth = (int)Math.Max(1, Math.Min(fitWidth, budget / fitHeight));
            else fitHeight = (int)Math.Max(1, Math.Min(fitHeight, budget / fitWidth));
        }
        return (fitWidth, fitHeight);
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
        SKBitmap? bitmap = null;
        try
        {
            using var input = new MemoryStream(bytes, writable: false);
            using var codec = SKCodec.Create(input);
            if (codec is not null && (codec.Info.Width > MaxDimension || codec.Info.Height > MaxDimension))
                throw new LeviathanException(new LeviathanError
                {
                    Code = ErrorCode.RejectedArchive,
                    Scope = path,
                    Details = new Dictionary<string, string> { ["width"] = codec.Info.Width.ToString(), ["height"] = codec.Info.Height.ToString() },
                    Message = "Image dimensions exceed the configured limit.",
                });
            bitmap = SKBitmap.Decode(bytes);
        }
        catch (LeviathanException) { throw; }
        catch (Exception)
        {
            // Some codecs are missing in a given SkiaSharp build (e.g. TIFF).
            // Try the bounded ImageSharp fallback below.
        }
        bitmap ??= TryDecodeWithImageSharp(bytes, path);
        if (bitmap is null)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.RejectedArchive,
                Scope = path,
                Details = new Dictionary<string, string> { ["reason"] = "unsupported-image-format" },
                Message = "Image format is not supported by the current imaging backend.",
            });
        if (bitmap.Width > MaxDimension || bitmap.Height > MaxDimension)
        {
            int width = bitmap.Width, height = bitmap.Height;
            bitmap.Dispose();
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.RejectedArchive,
                Scope = path,
                Details = new Dictionary<string, string> { ["width"] = width.ToString(), ["height"] = height.ToString() },
                Message = "Image dimensions exceed the configured limit.",
            });
        }

        return new DecodedImage(bitmap, checksum, bytes.Length);
    }

    /// <summary>
    /// Decode one horizontal band without materializing the whole image. JPEG and other
    /// codecs with top-down scanline support use bounded scanline decoding; unsupported
    /// codecs fall back to a full decode and subset. This is the line-crop path for very
    /// large JPEG pages, not a general tiling backend.
    /// </summary>
    public static SKBitmap DecodeBand(string path, int top, int height)
        => DecodeBandCore(path, top, height, null);

    internal static SKBitmap DecodeBandCore(string path, int top, int height, BitmapConstructionProbe? probe)
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
                        bool bandTransferred = false;
                        try
                        {
                            probe?.Invoke(BitmapConstructionStage.DestinationAllocated, band, null);
                            var rows = codec.GetScanlines(band.GetPixels(), height, band.RowBytes);
                            probe?.Invoke(BitmapConstructionStage.NativeInitialized, band, null);
                            if (rows == height)
                            {
                                probe?.Invoke(BitmapConstructionStage.BeforePublish, band, null);
                                bandTransferred = true;
                                return band;
                            }
                        }
                        finally { if (!bandTransferred) band.Dispose(); }
                    }
                }
            }
        }

        using var decoded = Decode(path);
        var subset = new SKBitmap();
        bool subsetTransferred = false;
        try
        {
            probe?.Invoke(BitmapConstructionStage.DestinationAllocated, subset, null);
            decoded.Bitmap.ExtractSubset(subset, new SKRectI(0, top, Math.Min(width, decoded.Bitmap.Width), Math.Min(top + height, decoded.Bitmap.Height)));
            probe?.Invoke(BitmapConstructionStage.NativeInitialized, subset, null);
            probe?.Invoke(BitmapConstructionStage.BeforePublish, subset, null);
            subsetTransferred = true;
            return subset;
        }
        finally { if (!subsetTransferred) subset.Dispose(); }
    }

    /// <summary>Read image dimensions without retaining the decoded bitmap.</summary>
    public static (int Width, int Height) Dimensions(string path)
    {
        using var codec = SKCodec.Create(path);
        if (codec is not null) return (codec.Info.Width, codec.Info.Height);
        try
        {
            var info = Image.Identify(path);
            if (info is not null) return (info.Width, info.Height);
        }
        catch (Exception) { /* fall through to the contract error */ }
        throw new LeviathanException(new LeviathanError
        {
            Code = ErrorCode.RejectedArchive,
            Scope = path,
            Message = "Image format is not supported by the current imaging backend.",
        });
    }

    private static SKBitmap? TryDecodeWithImageSharp(ReadOnlySpan<byte> bytes, string path)
    {
        try
        {
            // Apply the same source-dimension limit before fallback raster allocation.
            // A tiny requested preview does not authorize decoding an oversized source.
            var info = Image.Identify(new SixLabors.ImageSharp.Formats.DecoderOptions
            {
                MaxFrames = 1, SkipMetadata = true,
            }, bytes);
            if (info.Width <= 0 || info.Height <= 0 || info.Width > MaxDimension || info.Height > MaxDimension)
                throw new LeviathanException(new LeviathanError
                {
                    Code = ErrorCode.RejectedArchive, Scope = path,
                    Details = new Dictionary<string, string> { ["width"] = info.Width.ToString(), ["height"] = info.Height.ToString() },
                    Message = "Image dimensions exceed the configured limit.",
                });
            // This API returns one bitmap. Do not decode unused TIFF/animated-image
            // frames into memory before copying the first frame into that bitmap.
            using var image = Image.Load<Rgba32>(new SixLabors.ImageSharp.Formats.DecoderOptions
            {
                MaxFrames = 1,
            }, bytes);
            var bitmap = new SKBitmap(image.Width, image.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            try
            {
                if (bitmap.ColorType == SKColorType.Bgra8888 && bitmap.AlphaType == SKAlphaType.Premul)
                {
                    using var pixels = bitmap.PeekPixels();
                    if (pixels is not null)
                    {
                        image.ProcessPixelRows(accessor =>
                        {
                            var destination = pixels.GetPixelSpan<byte>();
                            int rowBytes = pixels.RowBytes;
                            int minimumRow = checked(image.Width * 4);
                            int requiredBytes = checked((image.Height - 1) * rowBytes + minimumRow);
                            if (rowBytes < minimumRow || destination.Length < requiredBytes)
                                throw new InvalidDataException("Fallback destination pixel view is incomplete.");
                            for (int y = 0; y < accessor.Height; y++)
                            {
                                var row = accessor.GetRowSpan(y);
                                int offset = checked(y * rowBytes);
                                for (int x = 0; x < row.Length; x++)
                                {
                                    var p = row[x];
                                    var value = SKPMColor.PreMultiply(new SKColor(p.R, p.G, p.B, p.A));
                                    destination[offset++] = value.Blue;
                                    destination[offset++] = value.Green;
                                    destination[offset++] = value.Red;
                                    destination[offset++] = value.Alpha;
                                }
                            }
                        });
                        return bitmap;
                    }
                }
                // Preserve the established transfer for any other destination
                // representation or unavailable direct pixel view.
                image.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        var row = accessor.GetRowSpan(y);
                        for (int x = 0; x < row.Length; x++)
                        {
                            var p = row[x];
                            bitmap.SetPixel(x, y, new SKColor(p.R, p.G, p.B, p.A));
                        }
                    }
                });
                return bitmap;
            }
            catch { bitmap.Dispose(); throw; }
        }
        catch (LeviathanException) { throw; }
        catch (Exception)
        {
            return null;
        }
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
        => ApplyCore(source, transforms, ApplyTransform);

    internal static DerivativeResult ApplyCore(SKBitmap source, IReadOnlyList<ImageTransform> transforms,
        Func<SKBitmap, ImageTransform, SKBitmap> applyTransform)
    {
        var bitmap = source;
        bool ownsBitmap = false;
        var applied = new List<ImageTransform>();
        try
        {
            foreach (var transform in transforms)
            {
                var next = applyTransform(bitmap, transform);
                var previous = bitmap;
                bool ownedPrevious = ownsBitmap;
                bitmap = next;
                ownsBitmap = !ReferenceEquals(bitmap, source);
                if (ownedPrevious && !ReferenceEquals(previous, next)) previous.Dispose();
                applied.Add(transform);
            }
            // Construct metadata/result before relinquishing ownership: a later
            // failure must still release the final owned intermediate.
            var result = new DerivativeResult(bitmap, applied, string.Join(">", applied.Select(t => t.Description)));
            ownsBitmap = false;
            return result;
        }
        finally
        {
            if (ownsBitmap) bitmap.Dispose();
        }
    }

    internal static DerivativeResult ApplyWithBitmapProbe(SKBitmap source, IReadOnlyList<ImageTransform> transforms,
        BitmapConstructionProbe? probe) => ApplyCore(source, transforms, (bitmap, transform) => ApplyTransformCore(bitmap, transform, probe));

    private static SKBitmap ApplyTransform(SKBitmap bitmap, ImageTransform transform) => ApplyTransformCore(bitmap, transform, null);

    private static SKBitmap ApplyTransformCore(SKBitmap bitmap, ImageTransform transform, BitmapConstructionProbe? probe) => transform switch
    {
        ImageTransform.Crop crop => CropBitmap(bitmap, crop, probe),
        ImageTransform.Deskew deskew => DeskewBitmap(bitmap, deskew, probe),
        ImageTransform.NonlinearDewarp => throw new LeviathanException(new LeviathanError
        {
            Code = ErrorCode.StageContractViolation,
            Scope = "imaging",
            RemedyHint = RemedyHint.None,
            Message = "Nonlinear dewarp is recorded as a transform but has no implementation yet; refusing to fake it.",
        }),
        _ => throw new InvalidOperationException(transform.Description),
    };

    private static SKBitmap CropBitmap(SKBitmap source, ImageTransform.Crop crop, BitmapConstructionProbe? probe)
    {
        if (crop.Left < 0 || crop.Top < 0 || crop.Width <= 0 || crop.Height <= 0
            || crop.Left > source.Width || crop.Top > source.Height
            || crop.Width > source.Width - crop.Left || crop.Height > source.Height - crop.Top)
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
        bool transferred = false;
        try
        {
            probe?.Invoke(BitmapConstructionStage.DestinationAllocated, result, null);
            using (var canvas = new SKCanvas(result))
            {
                probe?.Invoke(BitmapConstructionStage.CanvasCreated, result, canvas);
                canvas.DrawBitmap(source, new SKPoint(-crop.Left, -crop.Top), new SKSamplingOptions(SKFilterMode.Nearest));
                probe?.Invoke(BitmapConstructionStage.NativeInitialized, result, canvas);
                probe?.Invoke(BitmapConstructionStage.BeforePublish, result, canvas);
            }
            transferred = true;
            return result;
        }
        finally { if (!transferred) result.Dispose(); }
    }

    private static SKBitmap DeskewBitmap(SKBitmap source, ImageTransform.Deskew deskew, BitmapConstructionProbe? probe)
    {
        var rotated = new SKBitmap(source.Width, source.Height);
        bool transferred = false;
        try
        {
            probe?.Invoke(BitmapConstructionStage.DestinationAllocated, rotated, null);
            using (var canvas = new SKCanvas(rotated))
            {
                probe?.Invoke(BitmapConstructionStage.CanvasCreated, rotated, canvas);
                canvas.Translate(source.Width / 2f, source.Height / 2f);
                canvas.RotateDegrees((float)deskew.AngleDegrees);
                canvas.Translate(-source.Width / 2f, -source.Height / 2f);
                canvas.DrawBitmap(source, new SKPoint(0, 0), new SKSamplingOptions(SKFilterMode.Linear));
                probe?.Invoke(BitmapConstructionStage.NativeInitialized, rotated, canvas);
                probe?.Invoke(BitmapConstructionStage.BeforePublish, rotated, canvas);
            }
            transferred = true;
            return rotated;
        }
        finally { if (!transferred) rotated.Dispose(); }
    }
}
