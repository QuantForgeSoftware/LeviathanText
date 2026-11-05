using System.Runtime.CompilerServices;
using SkiaSharp;

namespace LeviathanText.Layout;

/// <summary>One stride-aware pixel snapshot for projection and content-bound scans.</summary>
internal readonly struct LayoutRaster(SKBitmap bitmap)
{
    private readonly byte[] _bytes = bitmap.Bytes;
    private readonly bool _isGray8 = bitmap.ColorType == SKColorType.Gray8;

    public int Width { get; } = bitmap.Width;
    public int Height { get; } = bitmap.Height;
    public int RowBytes { get; } = bitmap.RowBytes;
    public int BytesPerPixel { get; } = bitmap.Info.BytesPerPixel;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Luminance(int offset)
    {
        if (_isGray8)
        {
            var value = _bytes[offset];
            // Use the same arithmetic as equal RGB channels, including histogram rounding.
            return (0.299f * value + 0.587f * value + 0.114f * value) / 255f;
        }

        // Preserve the projection detector's existing color-channel weights and order.
        byte blue = _bytes[offset], green = _bytes[offset + 1], red = _bytes[offset + 2];
        return (0.299f * red + 0.587f * green + 0.114f * blue) / 255f;
    }
}
