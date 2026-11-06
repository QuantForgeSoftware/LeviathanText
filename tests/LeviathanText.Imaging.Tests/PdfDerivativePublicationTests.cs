using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using LeviathanText.Contracts;
using SkiaSharp;

namespace LeviathanText.Imaging.Tests;

// Generated tiny PDFs only. These public-route cases can run against the preserved baseline.
public sealed class PdfDerivativeCollisionTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("lt-pdf-publication-").FullName;
    private string Pdf(int pages)
    {
        var path = Path.Combine(root, "source.pdf");
        using var document = SKDocument.CreatePdf(path);
        for (int page = 0; page < pages; page++)
        {
            using var canvas = document.BeginPage(16, 12);
            canvas.Clear(page == 0 ? SKColors.White : SKColors.Black);
            document.EndPage();
        }
        document.Close(); return path;
    }
    [Fact]
    public void Existing_first_derivative_bytes_are_never_replaced()
    {
        var pdf = Pdf(1); var output = Path.Combine(root, "images"); Directory.CreateDirectory(output);
        var target = Path.Combine(output, "source-page-0001.png");
        var original = Encoding.UTF8.GetBytes("Existing capture bytes must survive"); File.WriteAllBytes(target, original);
        var error = Assert.Throws<LeviathanException>(() => PdfRenderer.Render(pdf, output, dpi: 72));
        Assert.Equal(ErrorCode.StageContractViolation, error.Error.Code);
        Assert.Equal("pdf-output-exists", error.Error.Details!["reason"]);
        Assert.Equal(original, File.ReadAllBytes(target)); Assert.Equal(target, Assert.Single(Directory.GetFiles(output)));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_later_page_obstruction_is_detected_before_any_first_page_publication(bool directory)
    {
        var pdf = Pdf(2); var output = Path.Combine(root, "images"); Directory.CreateDirectory(output);
        var blocked = Path.Combine(output, "source-page-0002.png");
        var sentinel = Encoding.UTF8.GetBytes("Keep page two unchanged");
        if (directory) Directory.CreateDirectory(blocked); else File.WriteAllBytes(blocked, sentinel);
        var error = Assert.Throws<LeviathanException>(() => PdfRenderer.Render(pdf, output, dpi: 72));
        Assert.Equal(ErrorCode.StageContractViolation, error.Error.Code);
        Assert.Equal("pdf-output-exists", error.Error.Details!["reason"]);
        Assert.False(File.Exists(Path.Combine(output, "source-page-0001.png")));
        if (directory) Assert.True(Directory.Exists(blocked)); else Assert.Equal(sentinel, File.ReadAllBytes(blocked));
    }
    public void Dispose() => Directory.Delete(root, recursive: true);
}

// Candidate-only publication primitive controls: no PDFium concurrency or large raster.
public sealed class PdfDerivativeExclusiveWriterTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("lt-pdf-exclusive-").FullName;
    private static void Publish(string path, byte[] bytes)
    {
        var method = typeof(PdfRenderer).GetMethod("PublishNewPage", BindingFlags.Static | BindingFlags.NonPublic)!;
        try { method.Invoke(null, [path, bytes]); }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
    [Fact]
    public void A_target_created_after_preflight_is_refused_without_replacement()
    {
        var path = Path.Combine(root, "page.png"); Assert.False(Path.Exists(path));
        var original = new byte[] { 0, 255, 4, 3 }; File.WriteAllBytes(path, original);
        var error = Assert.Throws<LeviathanException>(() => Publish(path, [9, 8, 7]));
        Assert.Equal(ErrorCode.StageContractViolation, error.Error.Code);
        Assert.Equal("pdf-output-not-created", error.Error.Details!["reason"]);
        Assert.Equal(original, File.ReadAllBytes(path));
    }
    [Fact]
    public async Task Competing_exclusive_publishers_produce_one_complete_winner_and_one_controlled_refusal()
    {
        var path = Path.Combine(root, "page.png"); using var start = new Barrier(2);
        var first = new byte[] { 0, 255, 1, 2 }; var second = new byte[] { 9, 8, 0, 128 };
        Task<Exception?> Attempt(byte[] bytes) => Task.Run(() =>
        {
            if (!start.SignalAndWait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Owned publication starters did not rendezvous.");
            try { Publish(path, bytes); return (Exception?)null; } catch (Exception error) { return error; }
        });
        var outcomes = await Task.WhenAll(Attempt(first), Attempt(second));
        Assert.Single(outcomes, error => error is null);
        var refused = Assert.IsType<LeviathanException>(Assert.Single(outcomes, error => error is not null));
        Assert.Equal(ErrorCode.StageContractViolation, refused.Error.Code);
        Assert.Equal("pdf-output-not-created", refused.Error.Details!["reason"]);
        var actual = File.ReadAllBytes(path); Assert.True(actual.SequenceEqual(first) || actual.SequenceEqual(second));
        Assert.Equal(path, Assert.Single(Directory.GetFiles(root)));
    }
    public void Dispose() => Directory.Delete(root, recursive: true);
}
