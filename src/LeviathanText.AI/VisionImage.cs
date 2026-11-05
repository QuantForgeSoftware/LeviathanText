using System.Security.Cryptography;
using System.Buffers.Binary;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace LeviathanText.AI;

/// <summary>An immutable, decoded-and-checked snapshot of the exact PNG sent to the model.</summary>
public sealed class VisionImage
{
    public const int MaxEncodedBytes = 8 * 1024 * 1024;
    public const long MaxPixels = 4_000_000;
    private readonly byte[] _png;

    private VisionImage(byte[] png, string sha256, int width, int height)
    {
        _png = png;
        Sha256 = sha256;
        Width = width;
        Height = height;
    }

    public string Sha256 { get; }
    public int Width { get; }
    public int Height { get; }
    public int EncodedBytes => _png.Length;

    /// <summary>
    /// Copies before hashing/decoding. Invalid bytes, a stale digest or an exceeded bound
    /// throw InvalidDataException; callers must retain that input rejection in their run log.
    /// No resize, pixel conversion or re-encoding changes the transmitted bytes.
    /// </summary>
    public static VisionImage FromPng(ReadOnlySpan<byte> png, string expectedSha256)
    {
        if (png.Length is 0 or > MaxEncodedBytes)
            throw new InvalidDataException("PNG must contain between 1 byte and 8 MiB.");
        if (expectedSha256 is null || expectedSha256.Length != 64
            || expectedSha256.Any(c => !char.IsAsciiHexDigit(c)))
            throw new InvalidDataException("A SHA-256 digest of the exact PNG bytes is required.");

        var snapshot = png.ToArray();
        var actualHash = Convert.ToHexStringLower(SHA256.HashData(snapshot));
        if (!string.Equals(actualHash, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("PNG SHA-256 does not match the pinned image.");

        try
        {
            var options = new DecoderOptions { MaxFrames = 1, SkipMetadata = true };
            if (Image.DetectFormat(snapshot) is not PngFormat)
                throw new InvalidDataException("Only PNG image bytes are supported.");
            for (int offset = 8; offset <= snapshot.Length - 12;)
            {
                uint length = BinaryPrimitives.ReadUInt32BigEndian(snapshot.AsSpan(offset, 4));
                if (snapshot.AsSpan(offset + 4, 4).SequenceEqual("acTL"u8))
                    throw new InvalidDataException("Animated PNG input is not supported; pin a single static frame.");
                long next = (long)offset + 12 + length;
                if (next > snapshot.Length) throw new InvalidDataException("PNG chunk length exceeds the available bytes.");
                offset = (int)next;
            }
            var info = Image.Identify(options, snapshot);
            if (info.Width <= 0 || info.Height <= 0 || (long)info.Width * info.Height > MaxPixels)
                throw new InvalidDataException("PNG exceeds the four-million-pixel limit.");
            using var decoded = Image.Load<Rgba32>(options, snapshot);
            if (decoded.Width != info.Width || decoded.Height != info.Height)
                throw new InvalidDataException("Decoded PNG dimensions differ from its header.");
            return new VisionImage(snapshot, actualHash, info.Width, info.Height);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException or NotSupportedException)
        {
            throw new InvalidDataException("PNG could not be decoded.", ex);
        }
    }

    internal string ToDataUri() => "data:image/png;base64," + Convert.ToBase64String(_png);
}
