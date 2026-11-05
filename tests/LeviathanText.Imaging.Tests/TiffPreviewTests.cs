using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LeviathanText.Imaging.Tests;

public sealed class TiffPreviewTests
{
    [Fact]
    public void Multi_frame_tiff_decoders_preserve_first_frame_pixels_and_source_identity()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lt-multiframe-{Guid.NewGuid():N}.tif");
        try
        {
            using (var source = new Image<Rgba32>(12, 8, new Rgba32(200, 20, 10)))
            using (var later = new Image<Rgba32>(12, 8, new Rgba32(10, 20, 200)))
            {
                source.Frames.AddFrame(later.Frames.RootFrame);
                source.SaveAsTiff(path);
            }
            using var full = ImageDecoder.Decode(path);
            using var bounded = ImageDecoder.DecodeBounded(path, maxPixels: 24);
            Assert.Equal(12, full.Bitmap.Width);
            Assert.Equal(8, full.Bitmap.Height);
            Assert.Equal(12, bounded.OriginalWidth);
            Assert.Equal(8, bounded.OriginalHeight);
            Assert.True(bounded.IsScaled);
            Assert.Equal(new SkiaSharp.SKColor(200, 20, 10), full.Bitmap.GetPixel(0, 0));
            Assert.Equal(full.Bitmap.GetPixel(0, 0), bounded.Bitmap.GetPixel(0, 0));
            Assert.Equal(ImageDecoder.ChecksumOf(File.ReadAllBytes(path)), full.SourceChecksum);
            Assert.Equal(full.SourceChecksum, bounded.SourceChecksum);
        }
        finally { File.Delete(path); }
    }
}
