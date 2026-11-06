using System.Buffers.Binary;
using LeviathanText.Contracts;
using SkiaSharp;

namespace LeviathanText.Imaging.Tests;

public sealed class FallbackDimensionGuardTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Bounded_fallback_refuses_a_source_dimension_above_the_same_native_limit(bool fileRoute)
    {
        var bytes = Tga(ImageDecoder.MaxDimension + 1);
        using var codec = SKCodec.Create(new MemoryStream(bytes));
        Assert.Null(codec); // Exercise the actual fallback, not a native-codec header check.
        string path = Path.Combine(Path.GetTempPath(), "lt-fallback-limit-" + Guid.NewGuid().ToString("N") + ".tga");
        try
        {
            if (fileRoute) File.WriteAllBytes(path, bytes);
            var error = Assert.Throws<LeviathanException>(() =>
            {
                using var image = fileRoute ? ImageDecoder.DecodeBounded(path) : ImageDecoder.DecodeBounded(bytes, path);
            });
            Assert.Equal(ErrorCode.RejectedArchive, error.Error.Code);
            Assert.Equal(path, error.Error.Scope);
            Assert.Equal((ImageDecoder.MaxDimension + 1).ToString(), error.Error.Details!["width"]);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Maximum_admitted_fallback_dimension_keeps_pixels_source_identity_and_literal_budget()
    {
        var bytes = Tga(ImageDecoder.MaxDimension);
        using var image = ImageDecoder.DecodeBounded(bytes, "generated.tga", 128);
        Assert.Equal(ImageDecoder.MaxDimension, image.OriginalWidth); Assert.Equal(1, image.OriginalHeight);
        Assert.InRange((long)image.Bitmap.Width * image.Bitmap.Height, 1, 128);
        Assert.Equal(ImageDecoder.ChecksumOf(bytes), image.SourceChecksum);
        Assert.Equal(new SKColor(17, 9, 3), image.Bitmap.GetPixel(0, 0));
    }

    private static byte[] Tga(int width)
    {
        var bytes = new byte[18 + width * 3]; bytes[2] = 2;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), checked((ushort)width));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), 1); bytes[16] = 24; bytes[17] = 0x20;
        for (int offset = 18; offset < bytes.Length; offset += 3) { bytes[offset] = 3; bytes[offset + 1] = 9; bytes[offset + 2] = 17; }
        return bytes;
    }
}
