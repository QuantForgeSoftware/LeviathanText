using LeviathanText.Contracts;
using SkiaSharp;

namespace LeviathanText.Application.Tests;

public sealed class PdfRotationImportTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Refused_text_extraction_leaves_no_project_or_derivative_changes_and_image_only_retry_succeeds(bool existingCache)
    {
        var directory = Directory.CreateTempSubdirectory("lt-rotated-import-");
        try
        {
            var path = Path.Combine(directory.FullName, "rotated.pdf");
            using (var pdf = SKDocument.CreatePdf(path))
            {
                using var canvas = pdf.BeginPage(300, 200);
                canvas.Clear(SKColors.White);
                canvas.RotateDegrees(90, 150, 100);
                using var paint = new SKPaint { Color = SKColors.Black };
                using var font = new SKFont(SKTypeface.FromFamilyName("serif"), 20);
                canvas.DrawText("ABCD", 100, 100, SKTextAlign.Left, font, paint);
                pdf.EndPage();
                pdf.Close();
            }
            var store = new InMemoryStore();
            var service = new ProjectService(store);
            var interchange = new InterchangeService(service);
            var project = service.Create("rotated reference");
            var output = Path.Combine(directory.FullName, "images");
            var sentinel = Path.Combine(output, "existing.capture");
            if (existingCache)
            {
                Directory.CreateDirectory(output);
                File.WriteAllText(sentinel, "existing derivative must survive");
            }

            var ex = Assert.Throws<LeviathanException>(() => interchange.ImportPdf(project, path, output, dpi: 72, extractText: true));

            Assert.Equal(ErrorCode.StageContractViolation, ex.Error.Code);
            var unchanged = store.LoadProject(project.Id)!;
            Assert.Equal(project.Version, unchanged.Version);
            Assert.Empty(unchanged.Objects);
            if (existingCache)
            {
                Assert.Equal(sentinel, Assert.Single(Directory.GetFiles(output)));
                Assert.Equal("existing derivative must survive", File.ReadAllText(sentinel));
            }
            else Assert.False(Directory.Exists(output));

            var (imported, pages, warnings) = interchange.ImportPdf(unchanged, path, output, dpi: 72, extractText: false);
            var surface = Assert.Single(imported.Surfaces);
            Assert.Empty(surface.Lines);
            Assert.Empty(surface.Revisions);
            Assert.True(File.Exists(Assert.Single(pages).ImagePath));
            Assert.Contains("no text/layout was inferred", warnings);
            Assert.Single(store.LoadProject(project.Id)!.Objects);
        }
        finally { directory.Delete(recursive: true); }
    }
}
