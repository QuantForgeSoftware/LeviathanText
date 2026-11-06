using System.Buffers.Binary;
using LeviathanText.Contracts;
using SkiaSharp;

namespace LeviathanText.Imaging.Tests;

public sealed class ImageDimensionPreflightTests
{
    [Theory]
    [InlineData(12001, 1)]
    [InlineData(1, 12001)]
    public void Oversized_header_is_rejected_by_dimensions_before_invalid_pixel_data(int width, int height)
    {
        var path = Path.Combine(Path.GetTempPath(), $"lt-dimension-{Guid.NewGuid():N}.png");
        try
        {
            File.WriteAllBytes(path, Png(width, height));
            var error = Assert.Throws<LeviathanException>(() => ImageDecoder.Decode(path));
            Assert.Equal(ErrorCode.RejectedArchive, error.Error.Code);
            Assert.Equal(width.ToString(), error.Error.Details!["width"]);
            Assert.Equal(height.ToString(), error.Error.Details["height"]);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Valid_limit_width_retains_exact_pixels_and_encoded_checksum()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lt-dimension-{Guid.NewGuid():N}.png");
        try
        {
            using var bitmap = new SKBitmap(ImageDecoder.MaxDimension, 1);
            bitmap.Erase(SKColors.AntiqueWhite);
            using var encoded = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
            var bytes = encoded.ToArray(); File.WriteAllBytes(path, bytes);
            using var decoded = ImageDecoder.Decode(path);
            Assert.Equal(ImageDecoder.MaxDimension, decoded.Bitmap.Width);
            Assert.Equal(SKColors.AntiqueWhite, decoded.Bitmap.GetPixel(ImageDecoder.MaxDimension - 1, 0));
            Assert.Equal(ImageDecoder.ChecksumOf(bytes), decoded.SourceChecksum);
        }
        finally { File.Delete(path); }
    }

    // A bounded valid PNG header followed by an invalid zlib payload. Header-only
    // dimension rejection must not need to allocate/decode the declared pixel frame.
    private static byte[] Png(int width, int height)
    {
        using var output = new MemoryStream(); output.Write([137,80,78,71,13,10,26,10]);
        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width); BinaryPrimitives.WriteInt32BigEndian(header[4..], height);
        header[8] = 8; header[9] = 2;
        Chunk(output, "IHDR"u8, header); Chunk(output, "IDAT"u8, [0,0,0,0]); Chunk(output, "IEND"u8, []);
        return output.ToArray();
    }
    private static void Chunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> number = stackalloc byte[4]; BinaryPrimitives.WriteInt32BigEndian(number, data.Length); output.Write(number); output.Write(type); output.Write(data);
        uint crc = uint.MaxValue;
        foreach (byte value in type.ToArray().Concat(data.ToArray()))
        {
            crc ^= value;
            for (int bit=0;bit<8;bit++) crc=(crc>>1)^((crc&1)==0?0:0xedb88320u);
        }
        BinaryPrimitives.WriteUInt32BigEndian(number, ~crc); output.Write(number);
    }
}
