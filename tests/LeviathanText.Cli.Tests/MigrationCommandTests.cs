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

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
