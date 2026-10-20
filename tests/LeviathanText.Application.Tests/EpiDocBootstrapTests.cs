using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using SkiaSharp;

namespace LeviathanText.Application.Tests;

public sealed class EpiDocBootstrapTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"lt-epidoc-{Guid.NewGuid():N}");

    public EpiDocBootstrapTests() => Directory.CreateDirectory(_dir);

    private (string Xml, string Image) Fixture()
    {
        var xml = Path.Combine(_dir, "record.xml");
        File.WriteAllText(xml, """
            <TEI xmlns="http://www.tei-c.org/ns/1.0">
              <facsimile><graphic n="screen" url="ISic001151.jpg"/></facsimile>
              <text><body><div type="edition" subtype="primary"><ab>
                <lb n="1"/>Ὀπετρηία Λητώι χαῖρε
                <lb n="2"/>χρηστὴ καὶ εὐσεβής
              </ab></div></body></text>
            </TEI>
            """);

        var image = Path.Combine(_dir, "stone.png");
        using (var bitmap = new SKBitmap(300, 200))
        {
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.LightGray);
            using var paint = new SKPaint { Color = SKColors.Black };
            canvas.DrawRect(new SKRect(20, 40, 280, 70), paint);
            canvas.DrawRect(new SKRect(20, 110, 280, 140), paint);
            using var data = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.OpenWrite(image);
            data.SaveTo(stream);
        }
        return (xml, image);
    }

    [Fact]
    public void Import_creates_l2_reference_lines_without_l1_and_no_geometry()
    {
        var (xml, image) = Fixture();
        var store = new InMemoryStore();
        var projects = new ProjectService(store);
        var project = projects.Create("inscription-bootstrap");

        var (imported, notes, imageUrl) = new InterchangeService(projects).ImportEpiDoc(project, xml, image);

        var surface = imported.Surfaces[0];
        Assert.Equal("inscription-face", surface.Kind);
        Assert.Equal(2, surface.Lines.Count);
        Assert.All(surface.Lines, l =>
        {
            Assert.Null(l.DiplomaticRevisionId); // L1 stays empty for a human
            Assert.Empty(l.Polygon);
        });
        Assert.Equal(2, surface.Revisions.Count(r => r.Layer == TextLayer.L2Editorial));
        Assert.DoesNotContain(surface.Revisions, r => r.Layer == TextLayer.L1Diplomatic);
        Assert.Equal(300, surface.Captures[0].WidthPx);
        Assert.Equal("ISic001151.jpg", imageUrl);
        Assert.NotEmpty(notes);

        var reloaded = store.LoadProject(project.Id)!;
        var unplaced = new InterchangeService(projects).UnplacedLines(reloaded, surface.Id);
        Assert.Equal(2, unplaced.Count);
        Assert.Contains(unplaced, l => l.ReferenceText.Contains("χρηστὴ"));
    }

    [Fact]
    public void Directory_import_skips_records_without_images_and_reports_them()
    {
        var (xml, image) = Fixture();
        var records = Path.Combine(_dir, "records");
        var images = Path.Combine(_dir, "images");
        Directory.CreateDirectory(records);
        Directory.CreateDirectory(images);
        File.Copy(xml, Path.Combine(records, "ISic000001.xml")); // no image → skipped
        File.Copy(xml, Path.Combine(records, "ISic000002.xml"));
        File.Copy(image, Path.Combine(images, "ISic000002.png")); // resolved by record stem

        var store = new InMemoryStore();
        var projects = new ProjectService(store);
        var project = projects.Create("inscription-dir");

        var (imported, skipped, missing, notes) =
            new InterchangeService(projects).ImportEpiDocDirectory(project, records, images);

        Assert.Equal(1, imported);
        Assert.Equal(1, skipped);
        Assert.Contains("ISic000001", missing);
        Assert.Empty(notes);
        var reloaded = store.LoadProject(project.Id)!;
        Assert.Single(reloaded.Objects);
        Assert.Equal(2, reloaded.Objects[0].Surfaces[0].Lines.Count);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
