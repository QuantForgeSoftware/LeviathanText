using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Formats.Tests;

public class NativeJsonTests
{
    [Fact]
    public void Project_roundtrips_exactly()
    {
        var project = SampleProject.Build();
        var json = NativeJson.Save(project);
        var parsed = NativeJson.Load(json);
        // Record equality does not compare collections by content, so round-trip
        // equality is checked by re-serialization: deterministic output must be identical.
        Assert.Equal(json, NativeJson.Save(parsed));
    }

    [Fact]
    public void Envelope_carries_the_contract_version()
    {
        var json = NativeJson.Save(SampleProject.Build());
        Assert.Contains("\"contractVersion\":\"C0-0.4-draft\"", json);
    }

    [Fact]
    public void Foreign_contract_version_is_refused()
    {
        var json = NativeJson.Save(SampleProject.Build()).Replace("C0-0.4-draft", "C0-0.1-draft");
        var ex = Assert.Throws<LeviathanException>(() => NativeJson.Load(json));
        Assert.Equal(ErrorCode.ProfileVersionUnsupported, ex.Error.Code);
    }
}

public class DiplomaticExportTests
{
    [Fact]
    public void Export_returns_l1_text_in_reading_order()
    {
        var project = SampleProject.Build();
        var surface = project.Objects[0].Surfaces[0];
        var (text, _) = DiplomaticExport.Export(surface, surface.Revisions);
        Assert.Equal("α\u0305λ\u0305α\u0305νθρωπ", text);
    }

    [Fact]
    public void Loss_report_lists_all_dropped_categories()
    {
        var project = SampleProject.Build();
        var surface = project.Objects[0].Surfaces[0];
        var (_, report) = DiplomaticExport.Export(surface, surface.Revisions);

        Assert.Equal("diplomatic-plain-text", report.Format);
        Assert.Contains(report.Entries, e => e.Category == "geometry" && e.Count > 0);
        Assert.Contains(report.Entries, e => e.Category == "uncertainty" && e.Count > 0);
        Assert.Contains(report.Entries, e => e.Category == "alternatives" && e.Count > 0);
        Assert.Contains(report.Entries, e => e.Category == "provenance" && e.Count > 0);
        Assert.False(report.IsLossless);
    }

    [Fact]
    public void Untranscribed_lines_are_marked_explicitly()
    {
        var project = SampleProject.Build();
        var surface = project.Objects[0].Surfaces[0];
        var empty = surface with
        {
            Lines = [new NativeLine { Id = new EntityId(EntityKind.Line, "ln2"), RegionId = surface.Regions[0].Id, FrameRef = surface.Captures[0].Id }],
            Revisions = [],
        };
        var (text, _) = DiplomaticExport.Export(empty, []);
        Assert.Contains("not-yet-transcribed", text);
    }
}

public class PageXmlTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [Fact]
    public void Import_extracts_regions_lines_and_text()
    {
        var result = PageXml.Import(File.ReadAllText(FixturePath("sample.page.xml")),
            new EntityId(EntityKind.Capture, "cap1"), SampleProject.Sum);

        Assert.Single(result.Regions);
        Assert.Equal(2, result.Lines.Count);
        Assert.Equal(2, result.Revisions.Count);
        Assert.All(result.Revisions, r => Assert.Equal(TextLayer.L1Diplomatic, r.Layer));

        var line1 = result.Lines[0];
        Assert.NotEmpty(line1.Baseline); // baseline imported
        var revision1 = result.Revisions[0];
        Assert.Equal(11, ScalarText.Count(revision1.Text)); // α+overbar ×3 (6 scalars) + ν θ ρ ω π (5)
        Assert.Contains("νθρωπ", revision1.Text);
    }

    [Fact]
    public void Unsupported_page_elements_are_reported_not_flattened()
    {
        var result = PageXml.Import(File.ReadAllText(FixturePath("sample.page.xml")),
            new EntityId(EntityKind.Capture, "cap1"), SampleProject.Sum);
        Assert.Contains(result.Unsupported, i => i.Element == "GraphicRegion");
    }

    [Fact]
    public void Imported_geometry_survives_export_roundtrip()
    {
        var imported = PageXml.Import(File.ReadAllText(FixturePath("sample.page.xml")),
            new EntityId(EntityKind.Capture, "cap1"), SampleProject.Sum);

        var surface = new NativeSurface
        {
            Id = new EntityId(EntityKind.Surface, "s1"),
            Kind = "papyrus-side",
            Captures = [new NativeCapture
            {
                Id = new EntityId(EntityKind.Capture, "cap1"),
                Frame = new CoordinateFrame { ImageRef = new EntityId(EntityKind.Capture, "cap1"), ImageChecksum = SampleProject.Sum },
                ImageLocation = "fixture-papyrus-01.jpg",
            }],
            Regions = imported.Regions,
            Lines = imported.Lines,
            Revisions = imported.Revisions,
        };

        var exported = PageXml.Export(surface, surface.Captures[0], imported.Revisions);
        var reparsed = PageXml.Import(exported, new EntityId(EntityKind.Capture, "cap1"), SampleProject.Sum);

        Assert.Equal(imported.Regions.Count, reparsed.Regions.Count);
        Assert.Equal(imported.Lines.Count, reparsed.Lines.Count);
        Assert.Equal(imported.Revisions.Select(r => r.Text), reparsed.Revisions.Select(r => r.Text));
        Assert.Empty(reparsed.Unsupported); // supported subset round-trips without loss
    }

    [Fact]
    public void External_entities_are_refused()
    {
        var xml = "<!DOCTYPE PcGts [<!ENTITY xxe SYSTEM \"file:///etc/passwd\">]>" +
                  "<PcGts xmlns=\"http://schema.primaresearch.org/PAGE/gts/pagecontent/2019-07-15\"><Page/></PcGts>";
        Assert.Throws<InvalidDataException>(() =>
            PageXml.Import(xml, new EntityId(EntityKind.Capture, "cap1"), SampleProject.Sum));
    }
}

public class TeiXmlTests
{
    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [Fact]
    public void Import_preserves_unclear_gap_and_alternatives_as_annotations()
    {
        var result = TeiXml.Import(File.ReadAllText(FixturePath("sample.tei.xml")));

        Assert.Single(result.Revisions);
        var revision = result.Revisions[0];

        // Text has no bracket characters and no editorial underdots — they became annotations.
        Assert.DoesNotContain('<', revision.Text);
        Assert.DoesNotContain('[', revision.Text);

        Assert.Contains(result.Annotations, a => a.Kind == AnnotationKind.Unclear);
        Assert.Contains(result.Annotations, a => a.Kind == AnnotationKind.Lost && a.GapExtent == GapExtent.Unknown);
        Assert.Contains(result.Annotations, a => a.Kind == AnnotationKind.SuppliedEditorial);
        var alt = Assert.Single(result.Annotations, a => a.Kind == AnnotationKind.AlternativeReading);
        Assert.Equal("ω", alt.AlternativeText);
    }

    [Fact]
    public void Export_import_roundtrip_preserves_text_and_distinctions()
    {
        var imported = TeiXml.Import(File.ReadAllText(FixturePath("sample.tei.xml")));
        var revision = imported.Revisions[0];
        // The importer binds produced annotations to their revision; export them as-is.
        var lineAnnotations = imported.Annotations;

        var exported = TeiXml.Export([(revision.ScopeId, revision, lineAnnotations)], "roundtrip");
        var reparsed = TeiXml.Import(exported);

        Assert.Equal(revision.Text, reparsed.Revisions[0].Text);

        // All four scholarly distinctions survive the round trip.
        foreach (var kind in new[] { AnnotationKind.Unclear, AnnotationKind.Lost, AnnotationKind.AlternativeReading, AnnotationKind.SuppliedEditorial })
            Assert.Contains(reparsed.Annotations, a => a.Kind == kind);

        // Alternative reading content preserved.
        Assert.Equal("ω", Assert.Single(reparsed.Annotations, a => a.Kind == AnnotationKind.AlternativeReading).AlternativeText);
    }

    [Fact]
    public void External_entities_are_refused()
    {
        var xml = "<!DOCTYPE TEI [<!ENTITY x SYSTEM \"http://evil/x\">]><TEI xmlns=\"http://www.tei-c.org/ns/1.0\"><text/></TEI>";
        Assert.Throws<InvalidDataException>(() => TeiXml.Import(xml));
    }
}
