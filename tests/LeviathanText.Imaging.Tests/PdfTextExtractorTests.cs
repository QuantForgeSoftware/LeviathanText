using LeviathanText.Contracts;
using SkiaSharp;

namespace LeviathanText.Imaging.Tests;

public class PdfTextExtractorTests : IDisposable
{
    [Theory]
    [InlineData(90, 0, false, false)]
    [InlineData(180, 0, false, false)]
    [InlineData(270, 0, false, false)]
    [InlineData(90, 0, false, true)]
    [InlineData(0, 90, false, false)]
    [InlineData(0, 180, false, false)]
    [InlineData(0, 270, false, false)]
    [InlineData(0, 45, false, false)]
    [InlineData(0, 90, true, false)]
    public void Unsupported_page_or_glyph_rotation_is_explicitly_refused_before_returning_false_reference_geometry(
        int pageRotation, int textRotation, bool mixed, bool inheritedRotation)
    {
        var path = PdfRotationFixture.Write(_dir, pageRotation, textRotation, mixed, inheritedRotation);

        var ex = Assert.Throws<LeviathanException>(() => PdfTextExtractor.Extract(path));

        Assert.Equal(ErrorCode.StageContractViolation, ex.Error.Code);
        Assert.Equal(path + "#page-1", ex.Error.Scope);
        Assert.Equal(pageRotation.ToString(), ex.Error.Details!["pageRotation"]);
        Assert.Contains("without text extraction", ex.Error.Message);
        Assert.False(string.IsNullOrWhiteSpace(ex.Error.Details["textOrientation"]));
    }

    [Fact]
    public void Unrotated_reference_box_aligns_with_actual_raster_ink()
    {
        var path = PdfRotationFixture.Write(_dir);
        var text = Assert.Single(PdfTextExtractor.Extract(path));
        var line = Assert.Single(text.Lines);
        Assert.Equal("ABCD", line.Text);
        var rendered = Assert.Single(PdfRenderer.Render(path, Path.Combine(_dir, "plain"), dpi: 72));
        using var image = SKBitmap.Decode(rendered.ImagePath);
        Assert.Equal(300, image.Width);
        Assert.Equal(200, image.Height);
        var ink = PdfRasterInk.Bounds(image);
        Assert.InRange(Math.Abs(line.Left - ink.Left), 0, 2);
        Assert.InRange(Math.Abs((text.Height - line.Top) - ink.Top), 0, 2);
        Assert.InRange(Math.Abs(line.Right - ink.Right), 0, 2);
        Assert.InRange(Math.Abs((text.Height - line.Bottom) - ink.Bottom), 0, 2);
    }

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
        canvas.DrawText("left column", 20, 150, SKTextAlign.Left, font, paint);
        canvas.DrawText("right column", 260, 150, SKTextAlign.Left, font, paint);
        document.EndPage();
        document.Close();
        return path;
    }

    [Fact]
    public void Extracts_words_into_coordinate_bearing_lines()
    {
        var pages = PdfTextExtractor.Extract(MakePdf());
        var page = Assert.Single(pages);
        Assert.Equal(4, page.Lines.Count);
        Assert.Contains(page.Lines, l => l.Text.Contains("alpha beta", StringComparison.Ordinal));
        Assert.Contains(page.Lines, l => l.Text.Contains("gamma delta", StringComparison.Ordinal));
        Assert.Contains(page.Lines, l => l.Text.Contains("left column", StringComparison.Ordinal));
        Assert.Contains(page.Lines, l => l.Text.Contains("right column", StringComparison.Ordinal));
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
