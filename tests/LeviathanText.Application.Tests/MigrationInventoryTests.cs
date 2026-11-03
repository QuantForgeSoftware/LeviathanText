using LeviathanText.Application;

namespace LeviathanText.Application.Tests;

public sealed class MigrationInventoryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"lt-inventory-{Guid.NewGuid():N}");

    public MigrationInventoryTests() => Directory.CreateDirectory(_root);

    private string Write(string relative, string content)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Scan_classifies_public_formats_without_modifying_the_tree()
    {
        var image = Path.Combine(_root, "images", "page.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(image)!);
        File.WriteAllBytes(image, [1, 2, 3, 4]);
        Write("page.xml", """<PcGts xmlns="http://schema.primaresearch.org/PAGE/gts/pagecontent/2019-07-15"/>""");
        Write("record.xml", """<TEI xmlns="http://www.tei-c.org/ns/1.0"/>""");
        Write("alto.xml", """<alto xmlns="http://www.loc.gov/standards/alto/ns-v4#"/>""");
        Write("manifest.json", """{"@context":"http://iiif.io/api/presentation/3/context.json","type":"Manifest"}""");
        Write("model/manifest.json", """{"architecture":"crnn","labels":["a"]}""");
        Write("model/weights.safetensors", "not really weights");
        Write("scripts/run.py", "print('x')");
        Write("notes.txt", "notes");
        Write("bundle.zip", "archive");
        Write("blob.bin", "unknown");

        var before = Directory.GetFiles(_root, "*", SearchOption.AllDirectories)
            .ToDictionary(f => f, f => File.GetLastWriteTimeUtc(f));
        var report = MigrationInventory.Scan(_root);
        var after = Directory.GetFiles(_root, "*", SearchOption.AllDirectories)
            .ToDictionary(f => f, f => File.GetLastWriteTimeUtc(f));

        Assert.Equal(before, after);
        Assert.Equal(11, report.FileCount);
        Assert.Equal(1, report.Counts["image"]);
        Assert.Equal(1, report.Counts["page-xml"]);
        Assert.Equal(1, report.Counts["tei-epidoc"]);
        Assert.Equal(1, report.Counts["alto-xml"]);
        Assert.Equal(1, report.Counts["iiif-manifest"]);
        Assert.Equal(1, report.Counts["model-metadata"]);
        Assert.Equal(1, report.Counts["model-weight"]);
        Assert.Equal(1, report.Counts["script"]);
        Assert.Equal(1, report.Counts["archive"]);
        Assert.Equal(1, report.Counts["text"]);
        Assert.Equal(1, report.Counts["other"]);
        Assert.All(report.Files, f => Assert.NotNull(f.Sha256));

        var json = MigrationInventory.ToJson(report);
        Assert.Contains("\"page-xml\"", json);
        Assert.Contains("images/page.jpg", json);
    }

    [Fact]
    public void Symbolic_link_directories_are_not_followed()
    {
        if (OperatingSystem.IsWindows()) return; // creating directory links needs privileges
        var outside = Path.Combine(Path.GetTempPath(), $"lt-inventory-outside-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "secret.txt"), "outside");
        try
        {
            Directory.CreateSymbolicLink(Path.Combine(_root, "link"), outside);
            Write("inside.txt", "inside");

            var report = MigrationInventory.Scan(_root);

            Assert.Contains(report.Files, f => f.RelativePath == "inside.txt");
            Assert.DoesNotContain(report.Files, f => f.RelativePath.Contains("secret", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public void File_limit_bounds_the_scan_and_is_reported()
    {
        for (int i = 0; i < 5; i++) Write($"file{i}.txt", "x");

        var report = MigrationInventory.Scan(_root, maxFiles: 2);

        Assert.Equal(2, report.FileCount);
        Assert.Contains(report.Notes, note => note.Contains("stopped after 2", StringComparison.Ordinal));
    }

    [Fact]
    public void Missing_root_is_reported_rather_than_treated_as_empty()
    {
        Assert.Throws<DirectoryNotFoundException>(() =>
            MigrationInventory.Scan(Path.Combine(_root, "missing")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
