using LeviathanText.Formats;
using LeviathanText.Contracts;
using SkiaSharp;
using System.Text.Json;

namespace LeviathanText.Application.Tests;

public sealed class TeiLanguagePersistenceTests
{
    [Theory]
    [InlineData("not-json")]
    [InlineData("null")]
    [InlineData("[null]")]
    [InlineData("[1]")]
    [InlineData("[\"\"]")]
    [InlineData("[\"\\u0000\"]")]
    [InlineData("oversized")]
    public void Malformed_stored_header_metadata_is_a_controlled_domain_error_without_mutation(string value)
    {
        var store = new InMemoryStore(); var projects = new ProjectService(store); var project = projects.Create("invalid languages");
        var owner = new NativeObject
        {
            Id = new EntityId(EntityKind.PhysicalObject, "language-fixture"),
            Metadata = new Dictionary<string, string> { ["tei:declaredLanguages"] = value == "oversized" ? new string(' ', 256 * 1024 + 1) : value },
            Surfaces = [new NativeSurface { Id = new EntityId(EntityKind.Surface, "language-fixture"), Kind = "page", Captures = [], Regions = [], Lines = [] }],
        };
        var generated = project with { Objects = [owner] };
        var before = NativeJson.Save(generated);
        var error = Assert.Throws<LeviathanException>(() => new InterchangeService(projects).ExportTeiWithReport(generated, owner.Surfaces[0].Id, "invalid"));
        Assert.Equal(ErrorCode.StageContractViolation, error.Error.Code);
        Assert.Equal(before, NativeJson.Save(generated));
        Assert.Empty(projects.Load(project.Id).Objects);
    }

    [Fact]
    public void Legacy_source_attachment_does_not_silently_repopulate_stored_language_arrays()
    {
        var store = new InMemoryStore(); var projects = new ProjectService(store); var project = projects.Create("legacy languages");
        var owner = new NativeObject
        {
            Id = new EntityId(EntityKind.PhysicalObject, "legacy-language"),
            Metadata = new Dictionary<string, string> { ["tei:sourceSha256"] = "legacy" },
            Attachments = [new NativeAttachment { Id = "legacy", Name = "legacy.xml", MediaType = "application/tei+xml", Role = "original-tei-markup", Sha256 = "legacy", Content = "<TEI xmlns='http://www.tei-c.org/ns/1.0'><teiHeader><profileDesc><langUsage><language ident='grc'/></langUsage></profileDesc></teiHeader><text xml:lang='grc'><body/></text></TEI>" }],
            Surfaces = [new NativeSurface { Id = new EntityId(EntityKind.Surface, "legacy-language"), Kind = "page", Captures = [], Regions = [], Lines = [] }],
        };
        var generated = project with { Objects = [owner] }; var before = NativeJson.Save(generated);
        var exported = new InterchangeService(projects).ExportTeiWithReport(generated, owner.Surfaces[0].Id, "legacy");
        Assert.DoesNotContain("langUsage", exported.Xml);
        Assert.Equal(before, NativeJson.Save(generated));
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Declared_header_languages_and_effective_line_language_survive_native_reload_export(bool inherit)
    {
        var directory = Directory.CreateTempSubdirectory("lt-tei-languages-");
        try
        {
            var source = "<TEI xmlns='http://www.tei-c.org/ns/1.0'><teiHeader><profileDesc><langUsage><language ident='grc'/><language ident='la'/></langUsage></profileDesc></teiHeader><text" + (inherit ? " xml:lang='grc'" : "") + "><body><div type='edition'><ab><lb/>α</ab><ab xml:lang=''><lb/>β</ab></div></body></text></TEI>";
            var xml = Path.Combine(directory.FullName, "source.xml"); File.WriteAllText(xml, source);
            var image = Path.Combine(directory.FullName, "page.png");
            using (var bitmap = new SKBitmap(10, 10))
            using (var png = bitmap.Encode(SKEncodedImageFormat.Png, 100)) File.WriteAllBytes(image, png.ToArray());
            var store = new InMemoryStore(); var projects = new ProjectService(store); var project = projects.Create("languages");
            var interchange = new InterchangeService(projects); interchange.ImportTei(project, xml, image);
            var loaded = projects.Load(project.Id); var owner = Assert.Single(loaded.Objects); var surface = Assert.Single(owner.Surfaces);
            Assert.True(owner.Metadata.ContainsKey("tei:declaredLanguages"));
            Assert.Equal(new[] { "grc", "la" }, JsonSerializer.Deserialize<string[]>(owner.Metadata["tei:declaredLanguages"]));
            Assert.Equal(inherit ? new[] { "grc" } : Array.Empty<string>(), surface.Lines[0].Languages);
            Assert.Empty(surface.Lines[1].Languages);
            var before = NativeJson.Save(loaded);
            var exported = interchange.ExportTeiWithReport(loaded, surface.Id, "languages");
            var parsed = TeiXml.Import(exported.Xml).Document!;
            Assert.Equal(new[] { "grc", "la" }, parsed.Languages);
            Assert.Equal(surface.Lines[0].Languages, parsed.Lines[0].Languages);
            Assert.Empty(parsed.Lines[1].Languages);
            Assert.Equal(source, Assert.Single(owner.Attachments).Content);
            Assert.Equal(before, NativeJson.Save(projects.Load(project.Id)));
        }
        finally { directory.Delete(true); }
    }
}
