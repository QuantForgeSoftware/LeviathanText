using LeviathanText.Contracts;
using LeviathanText.Formats;
using SkiaSharp;

namespace LeviathanText.Application.Tests;

public sealed class TeiPhysicalImportTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("lt-tei-physical-app-").FullName;
    private static string Document(string content) => "<TEI xmlns='http://www.tei-c.org/ns/1.0'><text><body><div type='edition'>" + content + "</div></body></text></TEI>";

    [Theory]
    [InlineData("alternative")]
    [InlineData("context")]
    [InlineData("oversized")]
    [InlineData("xml")]
    public void Parser_refusal_is_typed_and_precedes_decode_without_any_persisted_mutation(string kind)
    {
        var source = kind switch
        {
            "alternative" => Document("<ab><lb/><choice><orig>A<lb/>B</orig><reg>C</reg></choice></ab>"),
            "context" => Document("<ab data-tei-source-context='!'><lb/>A</ab>"),
            "oversized" => Document("<ab data-tei-source-context='" + new string('x', 65537) + "'><lb/>A</ab>"),
            _ => "<TEI><private-source-token>",
        };
        var xml = Path.Combine(directory, "source.xml"); File.WriteAllText(xml, source);
        var image = Path.Combine(directory, "existing-invalid.png"); File.WriteAllText(image, "not image bytes");
        var store = new InMemoryStore(); var projects = new ProjectService(store);
        var project = projects.Create("refusal");
        var before = NativeJson.Save(store.LoadProject(project.Id)!);

        var error = Assert.Throws<LeviathanException>(() => new InterchangeService(projects).ImportTei(project, xml, image));

        Assert.Equal(ErrorCode.StageContractViolation, error.Error.Code);
        Assert.DoesNotContain("private-source-token", error.Message);
        Assert.Equal(before, NativeJson.Save(store.LoadProject(project.Id)!));
        Assert.Equal(1, store.LoadProject(project.Id)!.Version);
        Assert.Empty(store.LoadProject(project.Id)!.Objects);
        Assert.Equal(new[] { "existing-invalid.png", "source.xml" }, Directory.GetFiles(directory).Select(Path.GetFileName).Order());
    }

    [Fact]
    public void Physical_metadata_survives_actual_native_save_reload_and_export()
    {
        var source = Document("<lb xml:id='one' n='1'/><ab n='V1' xml:lang='grc'>A</ab><ab n='V2' xml:lang='la'>B<lb xml:id='two' n='2' break='no'/>C</ab>");
        var xml = Path.Combine(directory, "source.xml"); File.WriteAllText(xml, source);
        var image = Path.Combine(directory, "page.png");
        using (var bitmap = new SKBitmap(20, 20))
        using (var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100)) File.WriteAllBytes(image, encoded.ToArray());
        var store = new InMemoryStore(); var projects = new ProjectService(store);
        var project = projects.Create("contexts"); var interchange = new InterchangeService(projects);
        var imported = interchange.ImportTei(project, xml, image).Imported;
        var loaded = projects.Load(project.Id); var surface = Assert.Single(Assert.Single(loaded.Objects).Surfaces);
        Assert.Equal(2, loaded.Version);
        Assert.Equal(new[] { "AB", "C" }, surface.Revisions.Select(r => r.Text));
        Assert.Equal(new[] { "grc", "la" }, surface.Lines[0].Languages);
        var context = Assert.Single(surface.Lines[0].Tags["tei-source-context"]);
        var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(context));
        Assert.Contains("V1", decoded); Assert.Contains("V2", decoded);
        Assert.Equal(source, Assert.Single(imported.Attachments).Content);
        Assert.Equal("no", Assert.Single(surface.Lines[1].Tags["tei-lb-break"]));
        var before = NativeJson.Save(loaded);
        var exported = interchange.ExportTeiWithReport(loaded, surface.Id, "contexts");
        Assert.Contains(exported.Report.Entries, entry => entry.Category == "line-languages");
        var reparsed = TeiXml.Import(exported.Xml);
        Assert.Contains("tei-source-context=" + context, reparsed.Document!.Lines[0].Tags);
        Assert.Contains("tei-lb-break=no", reparsed.Document.Lines[1].Tags);
        // Native imports namespace internal IDs; source-declared XML identities are
        // preserved separately and remain the interchange identity on export.
        Assert.Equal(surface.Lines.Select(l => l.Tags["source-id"].Single()), reparsed.Document.Lines.Select(l => l.XmlId));
        Assert.Equal(before, NativeJson.Save(projects.Load(project.Id)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Missing_file_checks_keep_precedence_over_source_parsing(bool missingSource)
    {
        var xml = Path.Combine(directory, "source.xml");
        var image = Path.Combine(directory, "image.png");
        if (missingSource) File.WriteAllText(image, "invalid image");
        else File.WriteAllText(xml, "<malformed-source>");
        var store = new InMemoryStore(); var projects = new ProjectService(store);
        var project = projects.Create("missing"); var before = NativeJson.Save(store.LoadProject(project.Id)!);
        var error = Assert.Throws<LeviathanException>(() => new InterchangeService(projects).ImportTei(project, xml, image));
        Assert.Equal(ErrorCode.InputMissing, error.Error.Code);
        Assert.Equal(missingSource ? xml : image, error.Error.Scope);
        Assert.Equal(before, NativeJson.Save(store.LoadProject(project.Id)!));
    }

    [Fact]
    public void Valid_source_does_not_reclassify_independent_image_decode_failure()
    {
        var xml = Path.Combine(directory, "source.xml"); File.WriteAllText(xml, Document("<ab><lb/>A</ab>"));
        var image = Path.Combine(directory, "image.png"); File.WriteAllText(image, "invalid image");
        var store = new InMemoryStore(); var projects = new ProjectService(store);
        var project = projects.Create("decode"); var before = NativeJson.Save(store.LoadProject(project.Id)!);
        var error = Assert.Throws<LeviathanException>(() => new InterchangeService(projects).ImportTei(project, xml, image));
        Assert.Equal(ErrorCode.RejectedArchive, error.Error.Code);
        Assert.Equal(image, error.Error.Scope);
        Assert.Equal(before, NativeJson.Save(store.LoadProject(project.Id)!));
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);
}
