using System.Text;
using LeviathanText.Contracts;
using SkiaSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using Xunit;

namespace LeviathanText.Imaging.Tests;

public sealed class FallbackPixelTransferTests
{
    [Theory]
    [InlineData(false, 0)] [InlineData(false, 16)] [InlineData(false, 32)] [InlineData(false, 48)]
    [InlineData(true, 0)] [InlineData(true, 16)] [InlineData(true, 32)] [InlineData(true, 48)]
    public void Every_alpha_value_survives_tga_origins_and_rle_with_exact_legacy_pixels(bool rle, int origin)
        => AssertPublicFallback(Tga(rle, origin), $"tga-{rle}-{origin}");

    [Theory]
    [InlineData("qoi-alpha")] [InlineData("tiff-alpha")] [InlineData("pgm16-legacy")]
    [InlineData("ppm16-legacy")] [InlineData("ppm-first-whitespace-legacy")]
    public void Other_fallback_formats_keep_parser_pixels_and_public_metadata(string format)
        => AssertPublicFallback(format switch
        {
            "qoi-alpha" => Qoi(), "tiff-alpha" => Tiff(false),
            "pgm16-legacy" => Pnm(false, true), "ppm16-legacy" => Pnm(true, true),
            _ => Pnm(true, false),
        }, format);

    [Fact]
    public void Multi_frame_tiff_still_uses_first_frame_and_exact_encoded_identity()
        => AssertPublicFallback(Tiff(true), "tiff-multi");

    [Fact]
    public void Invalid_fallback_input_keeps_typed_refusal_and_source_bytes()
    {
        byte[] bytes = Encoding.ASCII.GetBytes("generated invalid fallback bytes");
        var original = bytes.ToArray(); var path = Temporary(bytes);
        try
        {
            var error = Assert.Throws<LeviathanException>(() => ImageDecoder.Decode(path));
            Assert.Equal(ErrorCode.RejectedArchive, error.Error.Code);
            Assert.Equal(original, File.ReadAllBytes(path));
        }
        finally { File.Delete(path); }
    }

    private static void AssertPublicFallback(byte[] bytes, string caseName)
    {
        // This is an explicit platform admission control: these cases must
        // exercise the ImageSharp fallback, rather than a native codec.
        using (var native = SKCodec.Create(new MemoryStream(bytes, writable: false))) Assert.Null(native);
        using var parsed = Image.Load<Rgba32>(new SixLabors.ImageSharp.Formats.DecoderOptions { MaxFrames = 1 }, bytes);
        using var expected = new SKBitmap(parsed.Width, parsed.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        parsed.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    var p = row[x];
                    // Deliberate legacy transfer oracle. Parser quirks are
                    // retained; this test does not replace them with spec math.
                    expected.SetPixel(x, y, new SKColor(p.R, p.G, p.B, p.A));
                }
            }
        });
        var path = Temporary(bytes);
        try
        {
            var checksum = ImageDecoder.ChecksumOf(bytes);
            Assert.Equal((parsed.Width, parsed.Height), ImageDecoder.Dimensions(path));
            using var full = ImageDecoder.Decode(path);
            using var bounded = ImageDecoder.DecodeBounded(path, 1_000_000);
            using var band = ImageDecoder.DecodeBand(path, 1, Math.Max(1, parsed.Height - 2));
            Assert.Equal(checksum, full.SourceChecksum); Assert.Equal(bytes.Length, full.EncodedBytes);
            Assert.Equal(checksum, bounded.SourceChecksum);
            Assert.Equal(parsed.Width, bounded.OriginalWidth); Assert.Equal(parsed.Height, bounded.OriginalHeight);
            Assert.Equal(1, bounded.ScaleX); Assert.Equal(1, bounded.ScaleY); Assert.False(bounded.IsScaled);
            Assert.Equal(parsed.Width, full.Bitmap.Width); Assert.Equal(parsed.Height, full.Bitmap.Height);
            Assert.Equal(parsed.Width, bounded.Bitmap.Width); Assert.Equal(parsed.Height, bounded.Bitmap.Height);
            Assert.Equal(parsed.Width, band.Width); Assert.Equal(Math.Max(1, parsed.Height - 2), band.Height);
            for (int y = 0; y < parsed.Height; y++) for (int x = 0; x < parsed.Width; x++)
            {
                Assert.Equal(expected.GetPixel(x, y), full.Bitmap.GetPixel(x, y));
                Assert.Equal(expected.GetPixel(x, y), bounded.Bitmap.GetPixel(x, y));
                if (y >= 1 && y < parsed.Height - 1) Assert.Equal(expected.GetPixel(x, y), band.GetPixel(x, y - 1));
            }
            Assert.Equal(bytes, File.ReadAllBytes(path));
            string? output = Environment.GetEnvironmentVariable("LEVIATHANTEXT_PIXEL_PROOF_OUTPUT");
            if (output is not null)
            {
                Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output, caseName + ".json"), System.Text.Json.JsonSerializer.Serialize(new
                {
                    caseName, sourceSha256 = checksum.Digest, encodedBytes = bytes.Length,
                    width = parsed.Width, height = parsed.Height,
                    fullRgbaSha256 = PixelHash(full.Bitmap), boundedRgbaSha256 = PixelHash(bounded.Bitmap), bandRgbaSha256 = PixelHash(band),
                    fullColor = full.Bitmap.ColorType.ToString(), fullAlpha = full.Bitmap.AlphaType.ToString(),
                    bounded.OriginalWidth, bounded.OriginalHeight, bounded.ScaleX, bounded.ScaleY,
                }));
            }
        }
        finally { File.Delete(path); }
    }

    private static string PixelHash(SKBitmap bitmap)
    {
        var raw = new byte[bitmap.Width * bitmap.Height * 4]; int p = 0;
        for (int y = 0; y < bitmap.Height; y++) for (int x = 0; x < bitmap.Width; x++)
        { var c = bitmap.GetPixel(x, y); raw[p++] = c.Red; raw[p++] = c.Green; raw[p++] = c.Blue; raw[p++] = c.Alpha; }
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(raw)).ToLowerInvariant();
    }
    private static string Temporary(byte[] bytes)
    {
        var path = Path.Combine(Path.GetTempPath(), $"lt-fallback-pixels-{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(path, bytes); return path;
    }
    private static Rgba32 Pixel(int index) => new((byte)(index * 17 + 3), (byte)(index * 43 + 7), (byte)(index * 89 + 11), (byte)index);
    private static byte[] Tga(bool rle, int origin)
    {
        using var stream = new MemoryStream(); var header = new byte[18];
        header[2] = rle ? (byte)10 : (byte)2; header[12] = 16; header[14] = 16; header[16] = 32; header[17] = (byte)(origin | 8); stream.Write(header);
        for (int i = 0; i < 256; i++)
        {
            if (rle && i % 128 == 0) stream.WriteByte(127); // Two legal raw RLE packets.
            var p = Pixel(i); stream.Write([p.B, p.G, p.R, p.A]);
        }
        return stream.ToArray();
    }
    private static byte[] Qoi()
    {
        using var stream = new MemoryStream(); stream.Write("qoif"u8);
        stream.Write([0, 0, 0, 16, 0, 0, 0, 16, 4, 0]);
        for (int i = 0; i < 256; i++) { var p = Pixel(i); stream.Write([255, p.R, p.G, p.B, p.A]); }
        stream.Write([0, 0, 0, 0, 0, 0, 0, 1]); return stream.ToArray();
    }
    private static byte[] Pnm(bool rgb, bool sixteen)
    {
        using var stream = new MemoryStream(); stream.Write(Encoding.ASCII.GetBytes($"P{(rgb ? 6 : 5)}\n# generated parser-compatibility control\n16 16\n{(sixteen ? 65535 : 255)}\n"));
        for (int i = 0; i < 256 * (rgb ? 3 : 1); i++)
        {
            if (sixteen) { int value = (i * 997 + 7) & 65535; stream.WriteByte((byte)(value >> 8)); stream.WriteByte((byte)value); }
            else stream.WriteByte(i == 0 ? (byte)9 : (byte)(i * 19 + 17));
        }
        return stream.ToArray();
    }
    private static byte[] Tiff(bool multi)
    {
        using var image = new Image<Rgba32>(16, 16);
        image.ProcessPixelRows(a => { for (int y = 0; y < 16; y++) { var row = a.GetRowSpan(y); for (int x = 0; x < 16; x++) row[x] = Pixel(y * 16 + x); } });
        if (multi) { using var later = new Image<Rgba32>(16, 16, new Rgba32(255, 0, 255, 255)); image.Frames.AddFrame(later.Frames.RootFrame); }
        using var stream = new MemoryStream(); image.Save(stream, new TiffEncoder { BitsPerPixel = TiffBitsPerPixel.Bit32, Compression = TiffCompression.Deflate, PhotometricInterpretation = TiffPhotometricInterpretation.Rgb }); return stream.ToArray();
    }
}
