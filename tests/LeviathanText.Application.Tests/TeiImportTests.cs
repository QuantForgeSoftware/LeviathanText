using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using SkiaSharp;

namespace LeviathanText.Application.Tests;

public sealed class TeiImportTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"lt-tei-{Guid.NewGuid():N}");

    public TeiImportTests() => Directory.CreateDirectory(_dir);

    private (string Xml, string Image, string Content) Fixture()
    {
        var content = """
            <TEI xmlns="http://www.tei-c.org/ns/1.0">
              <teiHeader><fileDesc>
                <titleStmt><title>Invented TEI line</title></titleStmt>
                <publicationStmt><availability><licence>CC BY 4.0</licence></availability></publicationStmt>
                <sourceDesc><p><idno type="TM">12345</idno></p></sourceDesc>
              </fileDesc></teiHeader>
              <facsimile><surface xml:id="surf-source"><graphic n="screen" url="source.jpg" width="640" height="480"/></surface></facsimile>
              <text><body><pb n="001r" type="folio"/><div type="edition" subtype="primary"><ab xml:id="source-line">
                <lb n="1"/>Ὀπετρηία <unclear reason="illegible">Λη</unclear>τώι
                <lb n="2"/>χρηστ<supplied reason="editorial">ὴ</supplied> <choice><abbr>κϲ</abbr><expan>κυρίου</expan></choice>
              </ab></div></body></text>
            </TEI>
            """;
        var xml = Path.Combine(_dir, "record.xml");
        File.WriteAllText(xml, content);

        var image = Path.Combine(_dir, "page.png");
        using (var bitmap = new SKBitmap(300, 200))
        {
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.LightGray);
            using var data = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.OpenWrite(image);
            data.SaveTo(stream);
        }
        return (xml, image, content);
    }

    [Fact]
    public void Import_creates_layered_lines_geometry_free_with_source_attachment()
    {
        var (xml, image, content) = Fixture();
        var store = new InMemoryStore();
        var projects = new ProjectService(store);
        var project = projects.Create("tei-import");

        var (imported, notes, imageUrl) = new InterchangeService(projects).ImportTei(project, xml, image, "inscription-face");

        var surface = imported.Surfaces[0];
        Assert.Equal("inscription-face", surface.Kind);
        Assert.Equal(2, surface.Lines.Count);
        Assert.Equal(new int?[] { 1, 2 }, surface.Lines.Select(l => l.LineNumber));
        Assert.All(surface.Lines, l => Assert.Empty(l.Polygon));
        Assert.Equal("source-line", surface.Lines[0].Tags["source-id"][0]);

        var l1 = surface.Revisions.Where(r => r.Layer == TextLayer.L1Diplomatic).ToList();
        var l2 = surface.Revisions.Where(r => r.Layer == TextLayer.L2Editorial).ToList();
        Assert.Equal(2, l1.Count);
        // Only the line whose editorial reading differs from L1 needs an L2 revision.
        var l2Line = Assert.Single(l2);
        Assert.DoesNotContain("ὴ", l1[1].Text);
        Assert.Contains("ὴ", l2Line.Text);
        Assert.DoesNotContain("κυρίου", l1[1].Text);
        Assert.Contains("κυρίου", l2Line.Text);

        Assert.Contains(surface.Annotations, a => a.Kind == AnnotationKind.Illegible);
        Assert.Contains(surface.Annotations, a => a.Kind == AnnotationKind.SuppliedEditorial);
        var abbreviation = Assert.Single(surface.Annotations, a => a.Kind == AnnotationKind.Abbreviation);
        Assert.NotNull(abbreviation.ExpansionRevisionId);

        Assert.Equal(MaterialTrack.Inscription, imported.Track);
        Assert.Equal("Invented TEI line", imported.Metadata["tei:title"]);
        Assert.Equal("001r", imported.Metadata["tei:folio"]);
        Assert.Equal("CC BY 4.0", imported.Metadata["tei:licence"]);
        Assert.Equal("12345", imported.ExternalRefs["tei:TM"]);
        Assert.Equal("source.jpg", imageUrl);

        var attachment = Assert.Single(imported.Attachments);
        Assert.Equal("original-tei-markup", attachment.Role);
        Assert.Equal(content, attachment.Content);
        Assert.Equal(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(content))), attachment.Sha256);

        var reloaded = store.LoadProject(project.Id)!;
        Assert.Single(reloaded.Objects[0].Attachments);

        var duplicate = new InterchangeService(projects).FindTeiDuplicate(reloaded, content);
        Assert.NotNull(duplicate);
        Assert.Equal(imported.Id, duplicate!.Id);
        Assert.Null(new InterchangeService(projects).FindTeiDuplicate(reloaded, content + "<!-- changed -->"));

        var crosswalk = Assert.Single(ProvenanceCrosswalk.Build(reloaded));
        Assert.Equal("tei:surfaceId=surf-source", crosswalk.SurfaceRefs);
        Assert.Contains("tei:TM=12345", crosswalk.ObjectRefs);
        Assert.Equal(2, crosswalk.Lines);
        Assert.Equal("source-line", crosswalk.LineSourceIds);
        var csv = ProvenanceCrosswalk.ToCsv([crosswalk]);
        Assert.Contains("objectId,objectRefs,surfaceId,surfaceRefs", csv);
        Assert.Contains("tei:surfaceId=surf-source", csv);

        var lineRows = ProvenanceCrosswalk.BuildLines(reloaded);
        Assert.Equal(2, lineRows.Count);
        Assert.Contains(lineRows, row => row.LineSourceId == "source-line" && row.LineNumber == 1);
        var lineCsv = ProvenanceCrosswalk.ToLineCsv(lineRows);
        Assert.Contains("lineId,lineSourceId,lineNumber", lineCsv);
        Assert.Contains("source-line", lineCsv);
    }

    [Fact]
    public void Page_filter_imports_only_the_requested_folio_and_is_page_aware_for_duplicates()
    {
        var (_, image, _) = Fixture();
        var content = """
            <TEI xmlns="http://www.tei-c.org/ns/1.0"><text><body>
              <div type="edition">
                <pb n="001r"/><ab><lb/>αλφα</ab>
                <pb n="001v" break="no"/><ab><lb/>βητα</ab>
              </div>
            </body></text></TEI>
            """;
        var xml = Path.Combine(_dir, "two-pages.xml");
        File.WriteAllText(xml, content);
        var store = new InMemoryStore();
        var projects = new ProjectService(store);
        var project = projects.Create("tei-pages");
        var interchange = new InterchangeService(projects);

        var (imported, _, _) = interchange.ImportTei(project, xml, image, "page", "001v");

        var line = Assert.Single(imported.Surfaces[0].Lines);
        Assert.Equal("βητα", Assert.Single(imported.Surfaces[0].Revisions,
            r => r.Layer == TextLayer.L1Diplomatic).Text);
        Assert.Equal("001v", imported.Metadata["tei:importPage"]);
        Assert.Equal("001v", imported.Metadata["tei:folio"]);
        Assert.Null(interchange.FindTeiDuplicate(store.LoadProject(project.Id)!, content, "001r"));
        Assert.NotNull(interchange.FindTeiDuplicate(store.LoadProject(project.Id)!, content, "001v"));
        Assert.Contains("<pb n=\"001v\" type=\"folio\" break=\"no\" />",
            interchange.ExportTeiWithReport(projects.Load(project.Id), imported.Surfaces[0].Id, "selected folio").Xml);
    }

    [Theory]
    [InlineData("<licence target=\"https://creativecommons.org/licenses/by/4.0/\">Source credit</licence>")]
    [InlineData("<p>Source credit</p><p><![CDATA[This work is licensed under <a rel=\"license\" href=\"https://creativecommons.org/licenses/by/4.0/\">CC BY 4.0<br/></a>.]]></p>")]
    public void Export_preserves_licensed_source_and_nonbreaking_folio_from_new_and_legacy_projects(string availability)
    {
        var (xml, image, fixture) = Fixture();
        var source = fixture.Replace("<licence>CC BY 4.0</licence>", availability, StringComparison.Ordinal)
            .Replace("<pb n=\"001r\" type=\"folio\"/>", "<pb n=\"001r\" type=\"folio\" break=\"no\"/>", StringComparison.Ordinal);
        File.WriteAllText(xml, source);
        var store = new InMemoryStore();
        var projects = new ProjectService(store);
        var project = projects.Create("source-provenance");
        var interchange = new InterchangeService(projects);
        var (imported, _, _) = interchange.ImportTei(project, xml, image);
        var loaded = projects.Load(project.Id);
        var surfaceId = imported.Surfaces[0].Id;
        const string licence = "<licence target=\"https://creativecommons.org/licenses/by/4.0/\">Source credit</licence>";
        const string folio = "<pb n=\"001r\" type=\"folio\" break=\"no\" />";

        var fresh = interchange.ExportTeiWithReport(loaded, surfaceId, "source").Xml;
        Assert.Contains(licence, fresh);
        Assert.Contains(folio, fresh);

        var legacyMetadata = imported.Metadata
            .Where(pair => pair.Key is not ("tei:licenceTarget" or "tei:folioBreak"))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        var legacy = loaded with { Objects = [imported with { Metadata = legacyMetadata }] };
        var old = interchange.ExportTeiWithReport(legacy, surfaceId, "source").Xml;
        Assert.Contains(licence, old);
        Assert.Contains(folio, old);
        Assert.Equal(source, Assert.Single(imported.Attachments).Content);
    }

    [Fact]
    public void Export_restores_facsimile_line_numbers_and_loss_report()
    {
        var (xml, image, _) = Fixture();
        var store = new InMemoryStore();
        var projects = new ProjectService(store);
        var project = projects.Create("tei-export");
        var interchange = new InterchangeService(projects);
        var (imported, _, _) = interchange.ImportTei(project, xml, image, "inscription-face");

        var reloaded = store.LoadProject(project.Id)!;
        var surfaceId = imported.Surfaces[0].Id;
        var exported = interchange.ExportTeiWithReport(reloaded, surfaceId, "roundtrip");

        Assert.Contains("<facsimile>", exported.Xml);
        Assert.Contains("source.jpg", exported.Xml);
        Assert.Contains("<pb n=\"001r\" type=\"folio\" />", exported.Xml);
        Assert.Contains("n=\"1\"", exported.Xml);
        Assert.Contains("type=\"TM\"", exported.Xml);
        Assert.Contains("CC BY 4.0", exported.Xml);
        Assert.True(exported.Report.IsLossless, exported.Report.ToString());

        var reparsed = TeiXml.Import(exported.Xml);
        Assert.Equal("roundtrip", reparsed.Document!.Title);
        Assert.Equal(new int?[] { 1, 2 }, reparsed.Document.Lines.Select(l => l.Number));
        Assert.Equal("CC BY 4.0", reparsed.Document.Licence);
        Assert.Equal(2, reparsed.Revisions.Count(r => r.Layer == TextLayer.L1Diplomatic));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Repeated_source_ids_are_scoped_per_import_with_all_revision_links(bool anotherProject, bool longId)
    {
        var (_, image, _) = Fixture();
        var sourceId = longId ? new string('a', 55) : "line1";
        var store = new InMemoryStore();
        var projects = new ProjectService(store);
        var firstProject = projects.Create("first");
        var interchange = new InterchangeService(projects);
        string Write(string name, string text)
        {
            var path = Path.Combine(_dir, name + ".xml");
            File.WriteAllText(path, $"<TEI xmlns=\"http://www.tei-c.org/ns/1.0\"><text><body><div type=\"edition\"><ab xml:id=\"{sourceId}\"><lb/>{text}<choice><abbr>κϲ</abbr><expan>κυρίου</expan></choice></ab></div></body></text></TEI>");
            return path;
        }
        var first = interchange.ImportTei(firstProject, Write("first", "α"), image).Imported.Surfaces.Single();
        var secondProject = anotherProject ? projects.Create("second") : projects.Load(firstProject.Id);
        var second = interchange.ImportTei(secondProject, Write("second", "β"), image).Imported.Surfaces.Single();
        Assert.NotEqual(first.Lines.Single().Id, second.Lines.Single().Id);
        Assert.Empty(first.Revisions.Select(r => r.Id).Intersect(second.Revisions.Select(r => r.Id)));
        Assert.Empty(first.Annotations.Select(a => a.Id).Intersect(second.Annotations.Select(a => a.Id)));
        Assert.Equal(secondProject.Id, projects.LoadProjectForLine(second.Lines.Single().Id)!.Id);
        foreach (var surface in new[] { first, second })
        {
            var line = surface.Lines.Single();
            Assert.Equal(sourceId, line.Tags["source-id"].Single());
            Assert.Equal(2, surface.Revisions.Select(r => r.Id).Distinct().Count());
            Assert.All(surface.Revisions, r => Assert.Equal(line.Id, r.ScopeId));
            Assert.Contains(surface.Revisions, r => r.Id == line.DiplomaticRevisionId);
            var abbreviation = surface.Annotations.Single();
            Assert.Equal(line.DiplomaticRevisionId, abbreviation.AnchoredRevisionId);
            Assert.Contains(surface.Revisions, r => r.Id == abbreviation.ExpansionRevisionId && r.Layer == TextLayer.L2Editorial);
        }
        new TranscriptionService(store).AppendLineRevision(projects.Load(secondProject.Id), second.Lines.Single().Id,
            TextLayer.L1Diplomatic, "γ", new AgentRef { Id = "synthetic-reviewer", IsModel = false });
        var firstAfter = projects.Load(firstProject.Id).Objects.SelectMany(o => o.Surfaces).Single(s => s.Id == first.Id);
        Assert.Equal("ακϲ", firstAfter.Revisions.Single(r => r.Id == firstAfter.Lines.Single().DiplomaticRevisionId).Text);
        var exported = interchange.ExportTeiWithReport(projects.Load(secondProject.Id), second.Id, "scoped");
        Assert.Contains($"xml:id=\"{sourceId}\"", exported.Xml);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
