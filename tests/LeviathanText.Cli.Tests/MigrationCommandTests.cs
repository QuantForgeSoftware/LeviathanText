using SkiaSharp;

namespace LeviathanText.Cli.Tests;

public sealed class MigrationCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lt-migrate-cli", Guid.NewGuid().ToString("N"));

    public MigrationCommandTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void Inventory_writes_a_read_only_report()
    {
        File.WriteAllText(Path.Combine(_root, "page.xml"),
            """<PcGts xmlns="http://schema.primaresearch.org/PAGE/gts/pagecontent/2019-07-15"/>""");
        var output = Path.Combine(_root, "report.json");

        var exit = LeviathanText.Cli.Program.Main(
            ["--store", Path.Combine(_root, "store.db"), "migrate", "inventory", _root, "--out", output]);

        Assert.Equal(0, exit);
        var json = File.ReadAllText(output);
        Assert.Contains("\"page-xml\"", json);
        Assert.Contains("page.xml", json);
    }

    [Fact]
    public void Import_writes_a_ledger_and_creates_the_project()
    {
        File.WriteAllText(Path.Combine(_root, "page.xml"), """
            <PcGts xmlns="http://schema.primaresearch.org/PAGE/gts/pagecontent/2019-07-15">
              <Page imageFilename="page.png" imageWidth="40" imageHeight="24">
                <TextRegion id="r"><Coords points="0,0 39,0 39,23 0,23"/>
                  <TextLine id="l"><Coords points="4,4 36,4 36,20 4,20"/>
                    <TextEquiv><Unicode>α</Unicode></TextEquiv>
                  </TextLine>
                </TextRegion>
              </Page>
            </PcGts>
            """);
        using (var bitmap = new SKBitmap(40, 24))
        {
            using var data = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.Create(Path.Combine(_root, "page.png"));
            data.SaveTo(stream);
        }
        var ledger = Path.Combine(_root, "ledger.csv");

        var exit = LeviathanText.Cli.Program.Main(
            ["--store", Path.Combine(_root, "store.db"), "migrate", "import", _root, "migrated", "--out", ledger]);

        Assert.Equal(0, exit);
        var csv = File.ReadAllText(ledger);
        Assert.Contains("imported", csv);
        Assert.Contains("page.xml", csv);
    }

    [Fact]
    public void Project_audit_accepts_a_clean_project()
    {
        var original = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        string projectId;
        try
        {
            Assert.Equal(0, LeviathanText.Cli.Program.Main(
                ["--store", Path.Combine(_root, "audit.db"), "project", "new", "audit"]));
            projectId = writer.ToString().Trim().Split('\n')[0];
        }
        finally
        {
            Console.SetOut(original);
        }

        Assert.Equal(0, LeviathanText.Cli.Program.Main(
            ["--store", Path.Combine(_root, "audit.db"), "project", "audit", projectId, "--json"]));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
