using LeviathanText.Contracts;
using SkiaSharp;

namespace LeviathanText.Imaging.Tests;

public class PdfTextExtractorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"leviathan-pdftext-{Guid.NewGuid():N}");

    public PdfTextExtractorTests() => Directory.CreateDirectory(_dir);

    private string MakePdf()
    {
        var path = Path.Combine(_dir, "text.pdf");
        using var document = SKDocument.CreatePdf(path);
        using var canvas = document.BeginPage(400, 200);
        canvas.Clear(SKColors.White);
        using var paint = new SKPaint { Color = SKColors.Black };
        using var font = new SKFont(SKTypeface.FromFamilyName("serif"), 20);
        canvas.DrawText("alpha beta", 20, 50, SKTextAlign.Left, font, paint);
        canvas.DrawText("gamma delta", 20, 100, SKTextAlign.Left, font, paint);
        document.EndPage();
        document.Close();
        return path;
    }

    [Fact]
    public void Extracts_words_into_coordinate_bearing_lines()
    {
        var pages = PdfTextExtractor.Extract(MakePdf());
        var page = Assert.Single(pages);
        Assert.Equal(2, page.Lines.Count);
        Assert.Contains(page.Lines, l => l.Text.Contains("alpha beta", StringComparison.Ordinal));
        Assert.Contains(page.Lines, l => l.Text.Contains("gamma delta", StringComparison.Ordinal));
        Assert.All(page.Lines, line =>
        {
            Assert.True(line.Right > line.Left);
            Assert.True(line.Top > line.Bottom);
        });
    }

    [Fact]
    public void Malformed_pdf_is_rejected()
    {
        var path = Path.Combine(_dir, "bad.pdf");
        File.WriteAllText(path, "not a pdf");
        var ex = Assert.Throws<LeviathanException>(() => PdfTextExtractor.Extract(path));
        Assert.Equal(ErrorCode.RejectedArchive, ex.Error.Code);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
