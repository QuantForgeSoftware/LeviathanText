using LeviathanText.Contracts;
using SkiaSharp;

namespace LeviathanText.Imaging.Tests;

public class PdfRendererTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"leviathan-pdf-{Guid.NewGuid():N}");

    public PdfRendererTests() => Directory.CreateDirectory(_dir);

    private string MakePdf(int pages = 2)
    {
        var path = Path.Combine(_dir, "synthetic.pdf");
        using var document = SKDocument.CreatePdf(path);
        for (int i = 0; i < pages; i++)
        {
            using var canvas = document.BeginPage(200, 120);
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.Black, StrokeWidth = 2 };
            canvas.DrawLine(10, 20 + i * 10, 190, 20 + i * 10, paint);
            canvas.DrawText($"page {i + 1}", 20, 70, SKTextAlign.Left,
                new SKFont(SKTypeface.FromFamilyName("serif"), 18), paint);
            document.EndPage();
        }
        document.Close();
        return path;
    }

    [Fact]
    public void Renders_pages_to_traceable_png_derivatives()
    {
        var pdf = MakePdf();
        var output = Path.Combine(_dir, "images");
        var pages = PdfRenderer.Render(pdf, output, dpi: 72);

        Assert.Equal(2, pages.Count);
        Assert.Equal([1, 2], pages.Select(p => p.PageNumber));
        Assert.All(pages, page =>
        {
            Assert.True(File.Exists(page.ImagePath));
            Assert.True(page.WidthPx > 100);
            Assert.True(page.HeightPx > 50);
            Assert.Equal("sha256", page.Checksum.Algorithm);
            Assert.Equal(64, page.Checksum.Digest.Length);
        });
        Assert.True(new FileInfo(pages[0].ImagePath).Length > 100);
    }

    [Fact]
    public void Page_limit_is_honoured()
    {
        var pdf = MakePdf(3);
        var pages = PdfRenderer.Render(pdf, Path.Combine(_dir, "limited"), dpi: 72, maxPages: 1);
        Assert.Single(pages);
    }

    [Fact]
    public void Malformed_pdf_is_rejected_with_the_closed_error_code()
    {
        var path = Path.Combine(_dir, "bad.pdf");
        File.WriteAllText(path, "not a pdf");
        var ex = Assert.Throws<LeviathanException>(() => PdfRenderer.Render(path, Path.Combine(_dir, "bad")));
        Assert.Equal(ErrorCode.RejectedArchive, ex.Error.Code);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
