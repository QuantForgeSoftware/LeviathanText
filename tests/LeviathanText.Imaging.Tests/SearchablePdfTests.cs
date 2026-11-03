using SkiaSharp;
using UglyToad.PdfPig;

namespace LeviathanText.Imaging.Tests;

public sealed class SearchablePdfTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"lt-pdf-{Guid.NewGuid():N}");

    public SearchablePdfTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void Exported_pdf_contains_selectable_line_text()
    {
        var imagePath = Path.Combine(_root, "page.png");
        using (var bitmap = new SKBitmap(200, 100))
        {
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.LightGray);
            using var data = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.Create(imagePath);
            data.SaveTo(stream);
        }

        var bytes = SearchablePdf.Export(imagePath, 200, 100,
            [new SearchablePdf.TextLine("αβγ", 10, 10, 100, 40)], "test");

        using var pdf = PdfDocument.Open(bytes);
        Assert.Contains("αβγ", pdf.GetPage(1).Text);
    }

    [Fact]
    public void Missing_image_still_produces_a_bounded_page()
    {
        var bytes = SearchablePdf.Export(Path.Combine(_root, "missing.png"), 120, 80,
            [new SearchablePdf.TextLine("δ", 1, 1, 20, 20)], "blank");
        using var pdf = PdfDocument.Open(bytes);
        Assert.Equal(1, pdf.NumberOfPages);
        Assert.Contains("δ", pdf.GetPage(1).Text);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
