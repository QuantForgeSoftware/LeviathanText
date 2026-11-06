using System.Security.Cryptography;
using System.Text.Json;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using SkiaSharp;

namespace LeviathanText.Application;

public sealed record CaptureDisplayDescriptor(
    int SchemaVersion, string ProjectId, string SurfaceId, string CaptureId, string SourceFrameRef,
    Checksum RecordedSourceChecksum, string RecordedChecksumStatus, Checksum ObservedSourceChecksum,
    int SourceWidth, int SourceHeight, int DisplayWidth, int DisplayHeight,
    double ScaleX, double ScaleY, string Orientation, string MediaType, string DerivativeId, Checksum DisplayChecksum);

public sealed class CaptureDisplayResult(CaptureDisplayDescriptor descriptor, byte[] pngBytes, NativeCapture sourceCapture) : IDisposable
{
    public CaptureDisplayDescriptor Descriptor { get; } = descriptor;
    public byte[] PngBytes { get; } = pngBytes;
    public NativeCapture SourceCapture { get; } = sourceCapture;
    private Action? _release;
    internal void OwnAdmission(Action release) => _release = release;
    public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
}

public interface ICaptureDisplayService
{
    Task<CaptureDisplayResult> RenderAsync(NativeProject project, EntityId surfaceId, EntityId captureId,
        CancellationToken cancellationToken = default);
}

/// <summary>Ephemeral, raw-frame PNG delivery. No native mutations, cache, remote fetch or TIFF fallback.</summary>
public sealed class CaptureDisplayService : ICaptureDisplayService, IDisposable
{
    public const int MaxEncodedBytes = 16 * 1024 * 1024;
    public const long MaxSourcePixels = 16_000_000;
    public const int MaxSourceDimension = 12_000;
    public const int MaxDisplayEdge = 2048;
    public const long MaxDisplayPixels = 4_000_000;
    public const int MaxOutputBytes = 16 * 1024 * 1024;
    private readonly SemaphoreSlim _decodeGate = new(1, 1);
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    public async Task<CaptureDisplayResult> RenderAsync(NativeProject project, EntityId surfaceId, EntityId captureId,
        CancellationToken cancellationToken = default)
    {
        var capture = ResolveCapture(project, surfaceId, captureId);
        // Admission precedes even the encoded snapshot allocation. The DI lifetime
        // is singleton, so concurrent requests do not each queue a 16-MiB buffer.
        await _decodeGate.WaitAsync(cancellationToken);
        bool transferred = false;
        try
        {
            var bytes = await ReadLocalSnapshot(capture, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            // Native codec work is synchronous: cancellation is checked around it,
            // not represented as a hard native timeout or a total RSS guarantee.
            var result = await Task.Run(() => Render(project, surfaceId, capture, bytes, cancellationToken), cancellationToken);
            result.OwnAdmission(() => _decodeGate.Release());
            transferred = true;
            return result;
        }
        catch (ArgumentException) { throw Error(ErrorCode.RejectedArchive, "The selected capture image could not be decoded safely."); }
        catch (InvalidOperationException) { throw Error(ErrorCode.RejectedArchive, "The selected capture image could not be decoded safely."); }
        catch (OutOfMemoryException) { throw Error(ErrorCode.OutOfMemory, "The selected capture exceeds currently available display resources."); }
        finally { if (!transferred) _decodeGate.Release(); }
    }

    public static NativeCapture ResolveCapture(NativeProject project, EntityId surfaceId, EntityId captureId)
    {
        var surfaces = project.Objects.SelectMany(obj => obj.Surfaces).Where(surface => surface.Id == surfaceId).ToArray();
        if (surfaces.Length == 0) throw Error(ErrorCode.InputMissing, "The selected capture is not available.");
        if (surfaces.Length != 1) throw Error(ErrorCode.StageContractViolation, "The selected surface identity is ambiguous.");
        var captures = surfaces[0].Captures.Where(capture => capture.Id == captureId).ToArray();
        if (captures.Length == 0) throw Error(ErrorCode.InputMissing, "The selected capture is not available.");
        if (captures.Length != 1) throw Error(ErrorCode.StageContractViolation, "The selected capture identity is ambiguous.");
        var selected = captures[0];
        var selectedFrame = selected.Frame;
        if (selectedFrame?.ImageChecksum is not { Algorithm: not null, Digest: not null } checksum
            || string.IsNullOrEmpty(checksum.Algorithm))
            throw Error(ErrorCode.StageContractViolation, "The capture has invalid recorded frame/checksum metadata.");
        if (selectedFrame.ImageRef.Kind is not (EntityKind.Capture or EntityKind.Derivative))
            throw Error(ErrorCode.FrameUnregistered, "A capture coordinate frame must identify a capture or image derivative.");
        foreach (var alias in project.Objects.SelectMany(obj => obj.Surfaces).SelectMany(surface => surface.Captures)
                     .Where(other => !ReferenceEquals(other, selected) && other.Frame?.ImageRef == selectedFrame.ImageRef))
        {
            var a = alias.Frame?.ImageChecksum;
            bool knownEqual = string.Equals(a?.Algorithm, "sha256", StringComparison.OrdinalIgnoreCase)
                && checksum.Algorithm.Equals("sha256", StringComparison.OrdinalIgnoreCase)
                && string.Equals(a?.Digest, checksum.Digest, StringComparison.OrdinalIgnoreCase);
            if (!knownEqual || alias.WidthPx is { } aw && selected.WidthPx is { } bw && aw != bw
                || alias.HeightPx is { } ah && selected.HeightPx is { } bh && ah != bh)
                throw Error(ErrorCode.FrameUnregistered, "Multiple captures claim this coordinate frame with incompatible or unverified source identity.");
        }
        return captures[0];
    }

    private static async Task<byte[]> ReadLocalSnapshot(NativeCapture capture, CancellationToken cancellationToken)
    {
        if (!Path.IsPathFullyQualified(capture.ImageLocation)
            || Uri.TryCreate(capture.ImageLocation, UriKind.Absolute, out var uri) && !uri.IsFile)
            throw Error(ErrorCode.StageContractViolation,
                "Capture display currently requires an absolute local image path. Remote/IIIF delivery is not supported by this endpoint.");
        try
        {
            var file = new FileInfo(capture.ImageLocation);
            if (!file.Exists) throw Error(ErrorCode.InputMissing, "The selected capture image is missing. Restore or rebind its local source.");
            if (file.LinkTarget is not null)
                throw Error(ErrorCode.RejectedPath, "Capture display does not follow a file symlink. Rebind the capture to its verified local source.");
            if (file.Length > MaxEncodedBytes)
                throw Error(ErrorCode.RejectedArchive, "Capture display input exceeds the 16-MiB encoded limit. Create a bounded source derivative explicitly.");
            var canonical = Path.Combine(CanonicalDirectory(file.Directory!.FullName), file.Name);
            if (new FileInfo(canonical).LinkTarget is not null)
                throw Error(ErrorCode.RejectedPath, "Capture display does not follow a file symlink. Rebind the capture to its verified local source.");
            await using var input = new FileStream(canonical, FileMode.Open, FileAccess.Read, FileShare.Read,
                64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var output = new MemoryStream((int)Math.Min(file.Length, MaxEncodedBytes));
            var chunk = new byte[64 * 1024];
            while (true)
            {
                var read = await input.ReadAsync(chunk, cancellationToken);
                if (read == 0) break;
                if (output.Length + read > MaxEncodedBytes)
                    throw Error(ErrorCode.RejectedArchive, "Capture display input exceeds the 16-MiB encoded limit.");
                await output.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
            }
            return output.ToArray();
        }
        catch (LeviathanException) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (IOException) { throw Error(ErrorCode.InputMissing, "The selected capture image could not be read. Restore or rebind its local source."); }
        catch (UnauthorizedAccessException) { throw Error(ErrorCode.RejectedPath, "The selected capture image is not readable by this server."); }
        catch (ArgumentException) { throw Error(ErrorCode.RejectedPath, "The selected capture has an invalid local image path."); }
    }

    public static void ValidateObservedFrameDimensions(NativeProject project, EntityId frameRef, int width, int height)
    {
        foreach (var claim in project.Objects.SelectMany(obj => obj.Surfaces).SelectMany(surface => surface.Captures)
                     .Where(other => other.Frame?.ImageRef == frameRef))
            if (claim.WidthPx is { } declaredWidth && declaredWidth != width
                || claim.HeightPx is { } declaredHeight && declaredHeight != height)
                throw Error(ErrorCode.FrameUnregistered, "Observed raw image dimensions do not match every recorded claim for this native capture frame.");
    }

    private static CaptureDisplayResult Render(NativeProject project, EntityId surfaceId, NativeCapture capture,
        byte[] bytes, CancellationToken cancellationToken)
    {
        var sourceHash = Hash(bytes);
        var frame = capture.Frame;
        var recorded = frame?.ImageChecksum;
        if (frame is null || recorded is null || string.IsNullOrEmpty(recorded.Algorithm) || recorded.Digest is null)
            throw Error(ErrorCode.StageContractViolation, "The capture has invalid recorded frame/checksum metadata.");
        var status = "unavailable";
        if (recorded.Algorithm.Equals("sha256", StringComparison.OrdinalIgnoreCase))
        {
            if (recorded.Digest.Length != 64 || !recorded.Digest.All(char.IsAsciiHexDigit)
                || !recorded.Digest.Equals(sourceHash, StringComparison.OrdinalIgnoreCase))
                throw Error(ErrorCode.ChecksumMismatch, "The capture source no longer matches its recorded checksum. Restore or explicitly rebind the source.");
            status = "verified";
        }
        else if (!recorded.Algorithm.Equals("unavailable", StringComparison.OrdinalIgnoreCase))
            throw Error(ErrorCode.StageContractViolation, "Capture display cannot verify this recorded checksum algorithm; SHA-256 or explicitly unavailable is required.");
        if (!bytes.AsSpan().StartsWith(PngSignature)
            && !(bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255))
            throw Error(ErrorCode.StageContractViolation, "Capture display supports PNG/JPEG only. TIFF and other formats require a separately secured bounded derivative route.");
        using var stream = new MemoryStream(bytes, writable: false);
        using var codec = SKCodec.Create(stream);
        if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Png or SKEncodedImageFormat.Jpeg))
            throw Error(ErrorCode.StageContractViolation, "Capture display supports PNG/JPEG only. TIFF and other formats require a separately secured bounded derivative route.");
        if (codec.FrameCount > 1 || HasAnimationChunk(bytes, codec.EncodedFormat))
            throw Error(ErrorCode.StageContractViolation, "Capture display requires a single static PNG/JPEG frame.");
        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || info.Width > MaxSourceDimension || info.Height > MaxSourceDimension
            || (long)info.Width * info.Height > MaxSourcePixels)
            throw Error(ErrorCode.RejectedArchive, "Capture display source exceeds the 12000-edge or 16-million-pixel limit. Create a bounded source derivative explicitly.");
        ValidateObservedFrameDimensions(project, frame.ImageRef, info.Width, info.Height);
        cancellationToken.ThrowIfCancellationRequested();
        using var bitmap = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        // GetPixels decodes raw pixels; codec.EncodedOrigin is deliberately not
        // applied. Re-encoding strips EXIF rather than changing native geometry.
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
            throw Error(ErrorCode.RejectedArchive, "The capture image is malformed or incomplete.");
        cancellationToken.ThrowIfCancellationRequested();
        var scale = Math.Min(1, Math.Min((double)MaxDisplayEdge / info.Width,
            Math.Min((double)MaxDisplayEdge / info.Height, Math.Sqrt((double)MaxDisplayPixels / ((long)info.Width * info.Height)))));
        var displayWidth = Math.Max(1, (int)Math.Floor(info.Width * scale));
        var displayHeight = Math.Max(1, (int)Math.Floor(info.Height * scale));
        using var resized = displayWidth == info.Width && displayHeight == info.Height ? null
            : bitmap.Resize(new SKImageInfo(displayWidth, displayHeight, SKColorType.Rgba8888, SKAlphaType.Premul),
                new SKSamplingOptions(SKFilterMode.Linear));
        if ((displayWidth != info.Width || displayHeight != info.Height) && resized is null)
            throw Error(ErrorCode.RejectedArchive, "The bounded capture preview could not be created.");
        using var image = SKImage.FromBitmap(resized ?? bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        if (encoded is null || encoded.Size > MaxOutputBytes)
            throw Error(ErrorCode.RejectedArchive, "The normalized capture preview exceeds the 16-MiB output limit.");
        cancellationToken.ThrowIfCancellationRequested();
        var png = encoded.ToArray();
        var displayHash = Hash(png);
        var identity = JsonSerializer.SerializeToUtf8Bytes(new
        {
            policy = "capture-display.v1", projectId = project.Id.ToString(), surfaceId = surfaceId.ToString(),
            captureId = capture.Id.ToString(), sourceFrameRef = frame.ImageRef.ToString(), recordedChecksum = recorded,
            sourceHash, sourceWidth = info.Width, sourceHeight = info.Height, displayWidth, displayHeight, displayHash,
        });
        var descriptor = new CaptureDisplayDescriptor(1, project.Id.ToString(), surfaceId.ToString(), capture.Id.ToString(),
            frame.ImageRef.ToString(), recorded, status, new Checksum { Algorithm = "sha256", Digest = sourceHash },
            info.Width, info.Height, displayWidth, displayHeight, (double)displayWidth / info.Width,
            (double)displayHeight / info.Height, "raw", "image/png", Hash(identity),
            new Checksum { Algorithm = "sha256", Digest = displayHash });
        return new(descriptor, png, capture);
    }

    private static bool HasAnimationChunk(byte[] bytes, SKEncodedImageFormat format)
    {
        if (format != SKEncodedImageFormat.Png) return false;
        var offset = 8;
        while (offset <= bytes.Length - 12)
        {
            var length = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4));
            if (length > (uint)(bytes.Length - offset - 12)) return false; // The codec refuses incomplete data.
            var type = bytes.AsSpan(offset + 4, 4);
            if (type.SequenceEqual("acTL"u8) || type.SequenceEqual("fcTL"u8) || type.SequenceEqual("fdAT"u8)) return true;
            offset += checked((int)length + 12);
        }
        return false;
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static string CanonicalDirectory(string path)
    {
        var directory = new DirectoryInfo(path);
        if (directory.LinkTarget is not null)
            return CanonicalDirectory(directory.ResolveLinkTarget(true)?.FullName
                ?? throw new IOException("Unresolvable source directory."));
        return directory.Parent is { } parent ? Path.Combine(CanonicalDirectory(parent.FullName), directory.Name) : directory.FullName;
    }
    private static LeviathanException Error(ErrorCode code, string message) => new(new LeviathanError
    {
        Code = code, Scope = "capture-display", Message = message,
    });
    public void Dispose() => _decodeGate.Dispose();
}
