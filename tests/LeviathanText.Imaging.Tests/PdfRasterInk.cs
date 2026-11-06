using SkiaSharp;

namespace LeviathanText.Imaging.Tests;

internal static class PdfRasterInk
{
    public static (int Left, int Top, int Right, int Bottom) Bounds(SKBitmap image)
    {
        int left = image.Width, top = image.Height, right = -1, bottom = -1;
        for (int y = 0; y < image.Height; y++)
        for (int x = 0; x < image.Width; x++)
        {
            var pixel = image.GetPixel(x, y);
            if (pixel.Red >= 100 || pixel.Green >= 100 || pixel.Blue >= 100) continue;
            left = Math.Min(left, x); top = Math.Min(top, y);
            right = Math.Max(right, x); bottom = Math.Max(bottom, y);
        }
        Assert.True(right >= left && bottom >= top, "Generated PDF must contain visible raster ink.");
        return (left, top, right + 1, bottom + 1);
    }
}
