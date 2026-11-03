using SkiaSharp;

namespace LeviathanText.Imaging;

/// <summary>
/// Bounded searchable-PDF export: the original page image remains the visible evidence and
/// each supplied line text is drawn invisibly at its source-frame rectangle so a PDF reader
/// can select/search it. This is a convenience export, not a scholarly format: annotations,
/// alternatives and layers remain in the native/TEI records.
/// </summary>
public static class SearchablePdf
{
    /// <summary>Source frames are mapped at this nominal dpi into PDF points.</summary>
    public const double SourceDpi = 300;
    public const int MaxPagePixels = 40_000;

    public sealed record TextLine(string Text, double Left, double Top, double Right, double Bottom);

    public static byte[] Export(string imagePath, int width, int height,
        IReadOnlyList<TextLine> lines, string title)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "PDF page dimensions must be positive.");
        if (width > MaxPagePixels || height > MaxPagePixels)
            throw new ArgumentOutOfRangeException(nameof(width), "PDF page dimensions exceed the bounded limit.");

        using var bitmap = LoadImage(imagePath);
        double scale = 72.0 / SourceDpi;
        int pageWidth = Math.Max(1, (int)Math.Ceiling(width * scale));
        int pageHeight = Math.Max(1, (int)Math.Ceiling(height * scale));

        using var stream = new MemoryStream();
        using (var document = SKDocument.CreatePdf(stream))
        {
            using (var canvas = document.BeginPage(pageWidth, pageHeight))
            {
                canvas.Clear(SKColors.White);
                if (bitmap is not null)
                {
                    using var image = SKImage.FromBitmap(bitmap);
                    canvas.DrawImage(image, new SKRect(0, 0, pageWidth, pageHeight));
                }

                using var font = new SKFont(SKTypeface.Default, 10);
                using var paint = new SKPaint { Color = SKColors.Transparent };
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line.Text)) continue;
                    double lineHeight = Math.Max(1, (line.Bottom - line.Top) * scale);
                    font.Size = (float)Math.Clamp(lineHeight * 0.8, 1, 72);
                    float x = (float)(line.Left * scale);
                    float y = (float)(line.Bottom * scale);
                    canvas.DrawText(line.Text, x, y, SKTextAlign.Left, font, paint);
                }
                document.EndPage();
            }
            document.Close();
        }
        return stream.ToArray();
    }

    private static SKBitmap? LoadImage(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath)) return null;
        using var decoded = ImageDecoder.DecodeBounded(imagePath, 40_000_000);
        return decoded.Bitmap.Copy();
    }
}
