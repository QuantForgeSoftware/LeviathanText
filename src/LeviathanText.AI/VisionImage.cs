using System.Security.Cryptography;
using System.Buffers.Binary;
using SkiaSharp;

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
            if (snapshot.Length < 33 || !snapshot.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
                throw new InvalidDataException("Only PNG image bytes are supported.");
            int offset = 8;
            bool header = false, pixels = false, ended = false;
            int width = 0, height = 0;
            while (offset <= snapshot.Length - 12)
            {
                uint length = BinaryPrimitives.ReadUInt32BigEndian(snapshot.AsSpan(offset, 4));
                var type = snapshot.AsSpan(offset + 4, 4);
                if (type.SequenceEqual("acTL"u8) || type.SequenceEqual("fcTL"u8) || type.SequenceEqual("fdAT"u8))
                    throw new InvalidDataException("Animated PNG input is not supported; pin a single static frame.");
                long next = (long)offset + 12 + length;
                if (next > snapshot.Length) throw new InvalidDataException("PNG chunk length exceeds the available bytes.");
                if (!header)
                {
                    if (!type.SequenceEqual("IHDR"u8) || length != 13) throw new InvalidDataException("PNG must begin with IHDR.");
                    uint w = BinaryPrimitives.ReadUInt32BigEndian(snapshot.AsSpan(offset + 8, 4));
                    uint h = BinaryPrimitives.ReadUInt32BigEndian(snapshot.AsSpan(offset + 12, 4));
                    if (w == 0 || h == 0 || (ulong)w * h > MaxPixels)
                        throw new InvalidDataException("PNG exceeds the four-million-pixel limit.");
                    width = (int)w; height = (int)h; header = true;
                }
                else if (type.SequenceEqual("IHDR"u8)) throw new InvalidDataException("Duplicate PNG IHDR.");
                if (Crc32(snapshot.AsSpan(offset + 4, checked((int)length + 4)))
                    != BinaryPrimitives.ReadUInt32BigEndian(snapshot.AsSpan((int)next - 4, 4)))
                    throw new InvalidDataException("PNG chunk CRC does not match.");
                if (type.SequenceEqual("IDAT"u8)) pixels = true;
                offset = (int)next;
                if (type.SequenceEqual("IEND"u8))
                {
                    if (length != 0 || offset != snapshot.Length) throw new InvalidDataException("PNG IEND or trailing bytes are invalid.");
                    ended = true; break;
                }
            }
            if (!header || !pixels || !ended) throw new InvalidDataException("PNG is incomplete.");
            using var input = new MemoryStream(snapshot, writable: false);
            using var codec = SKCodec.Create(input);
            if (codec is null || codec.EncodedFormat != SKEncodedImageFormat.Png)
                throw new InvalidDataException("Only decodable PNG image bytes are supported.");
            if (codec.Info.Width != width || codec.Info.Height != height)
                throw new InvalidDataException("Decoded PNG dimensions differ from its header.");
            using var decoded = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
            if (decoded.GetPixels() == IntPtr.Zero || codec.GetPixels(decoded.Info, decoded.GetPixels()) != SKCodecResult.Success)
                throw new InvalidDataException("PNG could not be completely decoded.");
            return new VisionImage(snapshot, actualHash, width, height);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            throw new InvalidDataException("PNG could not be decoded.", ex);
        }
    }

    private static readonly uint[] CrcTable = CreateCrcTable();
    private static uint[] CreateCrcTable()
    {
        var table = new uint[8 * 256];
        for (uint i = 0; i < 256; i++)
        {
            uint crc = i;
            for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xedb88320u);
            table[i] = crc;
        }
        for (int slice = 1; slice < 8; slice++)
            for (int i = 0; i < 256; i++)
            {
                uint previous = table[(slice - 1) * 256 + i];
                table[slice * 256 + i] = (previous >> 8) ^ table[previous & 255];
            }
        return table;
    }
    private static uint Crc32(ReadOnlySpan<byte> data)
    {
        if (!System.Runtime.Intrinsics.Arm.Crc32.Arm64.IsSupported) return Crc32Portable(data);
        uint crc = uint.MaxValue;
        while (data.Length >= 8)
        {
            crc = System.Runtime.Intrinsics.Arm.Crc32.Arm64.ComputeCrc32(crc, BinaryPrimitives.ReadUInt64LittleEndian(data));
            data = data[8..];
        }
        foreach (byte value in data) crc = System.Runtime.Intrinsics.Arm.Crc32.ComputeCrc32(crc, value);
        return ~crc;
    }
    private static uint Crc32Portable(ReadOnlySpan<byte> data)
    {
        uint crc = uint.MaxValue;
        while (data.Length >= 8)
        {
            ulong block = BinaryPrimitives.ReadUInt64LittleEndian(data) ^ crc;
            crc = CrcTable[7 * 256 + (byte)block]
                ^ CrcTable[6 * 256 + (byte)(block >> 8)]
                ^ CrcTable[5 * 256 + (byte)(block >> 16)]
                ^ CrcTable[4 * 256 + (byte)(block >> 24)]
                ^ CrcTable[3 * 256 + (byte)(block >> 32)]
                ^ CrcTable[2 * 256 + (byte)(block >> 40)]
                ^ CrcTable[256 + (byte)(block >> 48)]
                ^ CrcTable[(byte)(block >> 56)];
            data = data[8..];
        }
        foreach (byte value in data) crc = (crc >> 8) ^ CrcTable[(crc ^ value) & 255];
        return ~crc;
    }

    internal string ToDataUri() => "data:image/png;base64," + Convert.ToBase64String(_png);
}
