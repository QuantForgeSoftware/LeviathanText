using LeviathanText.Application;
using SkiaSharp;

namespace LeviathanText.Application.Tests;

public sealed class MigrationImportTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"lt-migrate-import-{Guid.NewGuid():N}");

    public MigrationImportTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void Imports_page_documents_into_a_new_project_and_reports_skipped_records()
    {
        var page = Path.Combine(_root, "page1.xml");
        File.WriteAllText(page, """
            <PcGts xmlns="http://schema.primaresearch.org/PAGE/gts/pagecontent/2019-07-15">
              <Page imageFilename="page1.png" imageWidth="80" imageHeight="48">
                <TextRegion id="r"><Coords points="0,0 79,0 79,47 0,47"/>
                  <TextLine id="l"><Coords points="8,8 72,8 72,40 8,40"/>
                    <TextEquiv><Unicode>αβ</Unicode></TextEquiv>
                  </TextLine>
                </TextRegion>
              </Page>
            </PcGts>
            """);
        using (var bitmap = new SKBitmap(80, 48))
        {
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.LightGray);
            using var data = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.Create(Path.Combine(_root, "page1.png"));
            data.SaveTo(stream);
        }
        File.WriteAllText(Path.Combine(_root, "orphan.xml"),
            """<TEI xmlns="http://www.tei-c.org/ns/1.0"><text><body><div type="edition"><ab>α</ab></div></body></text></TEI>""");

        var store = new InMemoryStore();
        var projects = new ProjectService(store);
        var (projectId, rows) = MigrationImport.Import(projects, new InterchangeService(projects), _root, "migrated");

        Assert.Equal(2, rows.Count);
        Assert.Single(rows, row => row.Status == "imported" && row.Kind == "page-xml");
        Assert.Single(rows, row => row.Status == "skipped" && row.Kind == "tei-epidoc");
        Assert.Contains("imported=1", MigrationImport.Summary(rows));
        var project = store.LoadProject(projectId)!;
        Assert.Single(project.Objects);
        var csv = MigrationImport.ToCsv(rows);
        Assert.Contains("source,kind,status,objectId,surfaceId,note", csv);
        Assert.Contains("page1.xml", csv);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
