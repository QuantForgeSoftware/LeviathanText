using LeviathanText.Contracts;
using LeviathanText.Infrastructure;
using SkiaSharp;

namespace LeviathanText.Cli.Tests;

/// <summary>Process-level TEI profile commands: import creates layers, export carries the link.</summary>
public sealed class TeiCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lt-tei-cli", Guid.NewGuid().ToString("N"));

    public TeiCommandTests() => Directory.CreateDirectory(_root);

    private string PathOf(string name) => Path.Combine(_root, name);

    private int Run(params string[] args) =>
        LeviathanText.Cli.Program.Main(["--store", PathOf("store.db"), .. args]);

    private string RunCapturing(params string[] args)
    {
        var original = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            Assert.Equal(0, Run(args));
        }
        finally
        {
            Console.SetOut(original);
        }
        return writer.ToString();
    }

    [Fact]
    public void Import_tei_creates_layers_and_export_restores_the_image_link()
    {
        var projectId = RunCapturing("project", "new", "tei-cli").Trim();

        var xml = PathOf("record.xml");
        File.WriteAllText(xml, """
            <TEI xmlns="http://www.tei-c.org/ns/1.0">
              <teiHeader><fileDesc><titleStmt><title>CLI TEI</title></titleStmt>
                <publicationStmt><p>x</p></publicationStmt></fileDesc></teiHeader>
              <facsimile><surface xml:id="s"><graphic n="screen" url="source.jpg"/></surface></facsimile>
              <text><body><div type="edition"><ab xml:id="l"><lb n="1"/>αβ<supplied reason="editorial">γ</supplied></ab></div></body></text>
            </TEI>
            """);
        var image = PathOf("page.png");
        using (var bitmap = new SKBitmap(80, 48))
        {
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.LightGray);
            using var data = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.Create(image);
            data.SaveTo(stream);
        }

        var imported = RunCapturing("import", "tei", projectId, xml, image, "--kind", "inscription-face");
        Assert.Contains("1 TEI line(s)", imported);

        var duplicate = RunCapturing("import", "tei", projectId, xml, image,
            "--kind", "inscription-face", "--skip-duplicates");
        Assert.Contains("skipped duplicate source", duplicate);
        using (var store = new LocalStore(PathOf("store.db")))
            Assert.Single(store.LoadProject(EntityId.Parse(projectId, EntityKind.Collection))!.Objects);

        var exported = RunCapturing("export", "tei", projectId, SurfaceId(projectId));
        Assert.Contains("<facsimile>", exported);
        Assert.Contains("source.jpg", exported);
        Assert.Contains("n=\"1\"", exported);
        Assert.Contains("<supplied", exported);

        var pdf = PathOf("out.pdf");
        Assert.Equal(0, Run("export", "searchable-pdf", projectId, SurfaceId(projectId), pdf));
        Assert.Equal("%PDF"u8.ToArray(), File.ReadAllBytes(pdf)[..4]);

        var boundaries = PathOf("boundaries.csv");
        Assert.Equal(0, Run("export", "word-boundaries", projectId, "--out", boundaries));
        Assert.Contains("lineId,revisionId,layer,scalarPosition", File.ReadAllText(boundaries));

        var edition = PathOf("edition.html");
        Assert.Equal(0, Run("export", "edition-html", projectId, SurfaceId(projectId), "--out", edition));
        Assert.Contains("<html", File.ReadAllText(edition));

        var projectEdition = PathOf("project-edition.html");
        Assert.Equal(0, Run("export", "edition-html", projectId, "--all", "--out", projectEdition));
        Assert.Contains("surface-section", File.ReadAllText(projectEdition));
    }

    [Fact]
    public void Validate_tei_reports_profile_shape_and_strict_losses()
    {
        var xml = PathOf("validate.xml");
        File.WriteAllText(xml, """
            <TEI xmlns="http://www.tei-c.org/ns/1.0"><text><body>
              <div type="edition"><ab><lb/>α<foo>β</foo></ab></div>
            </body></text></TEI>
            """);

        Assert.Equal(0, Run("validate", "tei", xml));
        Assert.Equal(1, Run("validate", "tei", xml, "--strict"));
    }

    [Fact]
    public void Validate_tei_can_use_an_operator_supplied_xsd()
    {
        var schema = PathOf("tei.xsd");
        File.WriteAllText(schema, """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
                       targetNamespace="http://www.tei-c.org/ns/1.0"
                       elementFormDefault="qualified">
              <xs:element name="TEI">
                <xs:complexType><xs:sequence>
                  <xs:element name="text" minOccurs="1" maxOccurs="1"/>
                </xs:sequence></xs:complexType>
              </xs:element>
            </xs:schema>
            """);
        var valid = PathOf("valid.xml");
        File.WriteAllText(valid, """
            <TEI xmlns="http://www.tei-c.org/ns/1.0"><text><body>
              <div type="edition"><ab>α</ab></div>
            </body></text></TEI>
            """);
        var invalid = PathOf("invalid.xml");
        File.WriteAllText(invalid, """<TEI xmlns="http://www.tei-c.org/ns/1.0"/>""");

        Assert.Equal(0, Run("validate", "tei", valid, "--schema", schema));
        Assert.Equal(1, Run("validate", "tei", invalid, "--schema", schema, "--strict"));

        var hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
            File.ReadAllBytes(schema)));
        Assert.Equal(0, Run("validate", "tei", valid, "--schema", schema,
            "--expect-schema-sha256", hash));
        Assert.Equal(1, Run("validate", "tei", valid, "--schema", schema,
            "--expect-schema-sha256", new string('0', 64)));
    }

    private string SurfaceId(string projectId)
    {
        using var store = new LocalStore(PathOf("store.db"));
        var project = store.LoadProject(EntityId.Parse(projectId, EntityKind.Collection))!;
        return project.Objects[0].Surfaces[0].Id.ToString();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
