using LeviathanText.Contracts;
using LeviathanText.Formats;
using Xunit;

namespace LeviathanText.Formats.Tests;

public class AbbyyXmlTests
{
    private static (NativeSurface Surface, NativeCapture Capture) Sample()
    {
        var captureId = new EntityId(EntityKind.Capture, "cap1");
        var regionId = new EntityId(EntityKind.Region, "reg1");
        var lineId = new EntityId(EntityKind.Line, "ln1");
        var revision = new TextRevision
        {
            Id = new EntityId(EntityKind.TranscriptionRevision, "r1"), ScopeId = lineId,
            Layer = TextLayer.L1Diplomatic, Author = SampleProject.Agent,
            CreatedAt = new DateTimeOffset(2026, 10, 20, 0, 0, 0, TimeSpan.Zero), Text = "αβγ",
        };
        return (
            new NativeSurface
            {
                Id = new EntityId(EntityKind.Surface, "s1"), Kind = "page",
                Captures = [],
                Regions = [new NativeRegion { Id = regionId, FrameRef = captureId, LayoutSource = LayoutSource.Manual, ReadingOrderIndex = 0 }],
                Lines = [new NativeLine
                {
                    Id = lineId, RegionId = regionId, FrameRef = captureId,
                    Polygon = [new(10, 10), new(190, 10), new(190, 40), new(10, 40)],
                    Baseline = [new(10, 38), new(190, 38)], DiplomaticRevisionId = revision.Id,
                }],
                Revisions = [revision],
            },
            new NativeCapture
            {
                Id = captureId,
                Frame = new CoordinateFrame { ImageRef = captureId, ImageChecksum = SampleProject.Sum },
                ImageLocation = "fixture.png", WidthPx = 200, HeightPx = 60,
            });
    }

    [Fact]
    public void Export_import_roundtrip_preserves_text_and_bounds()
    {
        var (surface, capture) = Sample();
        var xml = AbbyyXml.Export(surface, capture, surface.Revisions);
        Assert.Contains("FineReader10-schema-v1", xml);
        Assert.Contains("αβγ", xml);
        var imported = AbbyyXml.Import(xml, capture.Id, SampleProject.Sum);
        Assert.Equal("αβγ", Assert.Single(imported.Revisions).Text);
        var line = Assert.Single(imported.Lines);
        Assert.Equal(new PointD(10, 10), line.Polygon[0]);
        Assert.Equal(new PointD(190, 40), line.Polygon[2]);
        Assert.Equal(new PointD(10, 38), line.Baseline[0]);
    }

    [Fact]
    public void Legacy_detection_identifies_abbyy()
    {
        var (surface, capture) = Sample();
        var xml = AbbyyXml.Export(surface, capture, surface.Revisions);
        Assert.Equal(LegacyFormat.Abbyy, LegacyFormats.Detect(xml));
    }

    [Fact]
    public void External_entities_and_wrong_namespace_are_rejected()
    {
        var (surface, capture) = Sample();
        var valid = AbbyyXml.Export(surface, capture, surface.Revisions);
        Assert.Throws<InvalidDataException>(() => AbbyyXml.Import(
            valid.Replace("FineReader10-schema-v1.xml", "example.test/abbyy"), capture.Id, SampleProject.Sum));
        Assert.Throws<InvalidDataException>(() => AbbyyXml.Import(
            "<!DOCTYPE document [<!ENTITY x SYSTEM \"file:///etc/passwd\">]>" + valid, capture.Id, SampleProject.Sum));
    }

    private static AbbyyXml.ImportResult ImportBody(string body) => AbbyyXml.Import(
        $"<document xmlns=\"{AbbyyXml.NamespaceUri}\">{body}</document>",
        new EntityId(EntityKind.Capture, "cap1"), SampleProject.Sum);

    [Fact]
    public void Import_preserves_block_bounds_and_standalone_character_spaces()
    {
        var result = ImportBody("""
            <page><block blockType="Text" l="2" t="3" r="90" b="50"><text><par>
            <line l="10" t="10" r="80" b="40" baseline="36"><formatting>
            <charParams> </charParams><charParams>α</charParams><charParams> </charParams>
            <charParams>β</charParams><charParams> </charParams>
            </formatting></line></par></text></block></page>
            """);
        Assert.Equal(" α β ", Assert.Single(result.Revisions).Text);
        Assert.Equal(new PointD(2, 3), Assert.Single(result.Regions).Polygon[0]);
        Assert.Equal(new PointD(90, 50), Assert.Single(result.Regions).Polygon[2]);
        Assert.Equal(new PointD(10, 36), Assert.Single(result.Lines).Baseline[0]);
    }

    [Fact]
    public void Import_does_not_promote_recognition_alternatives_to_diplomatic_text()
    {
        var result = ImportBody("""
            <page><block blockType="Text"><text><par><line l="0" t="0" r="20" b="20" baseline="18">
            <formatting><charParams>α<charRecVariants><charRecVariant>ω</charRecVariant></charRecVariants></charParams>
            <wordRecVariants><wordRecVariant><variantText><charParams>ψ</charParams></variantText></wordRecVariant></wordRecVariants>
            <charParams>β</charParams></formatting></line></par></text></block></page>
            """);
        Assert.Equal("αβ", Assert.Single(result.Revisions).Text);
        Assert.Contains(result.Unsupported, issue => issue.Element == "recognition-variants" && issue.Severity == PageXml.IssueSeverity.Loss);
    }

    [Fact]
    public void Import_reports_extra_pages_and_does_not_treat_table_lines_as_text_blocks()
    {
        var result = ImportBody("""
            <page><block blockType="Table"><row><cell><text><par><line l="0" t="0" r="20" b="20"><formatting><charParams>table</charParams></formatting></line></par></text></cell></row></block></page>
            <page><block blockType="Text"><text><par><line l="0" t="0" r="20" b="20"><formatting><charParams>second page</charParams></formatting></line></par></text></block></page>
            """);
        Assert.Empty(result.Regions);
        Assert.Empty(result.Lines);
        Assert.Empty(result.Revisions);
        Assert.Contains(result.Unsupported, issue => issue.Element == "page");
        Assert.Contains(result.Unsupported, issue => issue.Element == "blockType");
    }

    [Theory]
    [InlineData("l=\"bad\" t=\"0\" r=\"20\" b=\"20\"")]
    [InlineData("l=\"30\" t=\"0\" r=\"20\" b=\"20\"")]
    [InlineData("l=\"0\" t=\"0\" b=\"20\"")]
    public void Invalid_line_bounds_are_reported_without_inventing_zero_geometry(string bounds)
    {
        var result = ImportBody($"<page><block blockType=\"Text\"><text><par><line {bounds} baseline=\"18\"><formatting><charParams>α</charParams></formatting></line></par></text></block></page>");
        Assert.Empty(Assert.Single(result.Lines).Polygon);
        Assert.Empty(Assert.Single(result.Lines).Baseline);
        Assert.Equal("α", Assert.Single(result.Revisions).Text);
        Assert.Contains(result.Unsupported, issue => issue.Element == "line-geometry");
    }

    [Fact]
    public void Discarded_character_geometry_confidence_and_formatting_are_reported_once()
    {
        var result = ImportBody("""
            <page><block><text><par><line l="0" t="0" r="20" b="20" baseline="18"><formatting lang="Greek" bold="1">
            <charParams l="0" t="0" r="10" b="20" charConfidence="90">α</charParams>
            <charParams l="10" t="0" r="20" b="20" charConfidence="80">β</charParams>
            </formatting></line></par></text></block></page>
            """);
        Assert.Equal("αβ", Assert.Single(result.Revisions).Text);
        Assert.Single(result.Unsupported, issue => issue.Element == "character-metadata");
        Assert.Single(result.Unsupported, issue => issue.Element == "formatting-metadata");
    }

    [Fact]
    public void Import_does_not_invent_text_when_only_word_alternatives_are_available()
    {
        var result = ImportBody("""
            <page><block><text><par><line l="0" t="0" r="20" b="20"><formatting>
            <wordRecVariants><wordRecVariant><variantText><charParams>ψ</charParams></variantText></wordRecVariant></wordRecVariants>
            </formatting></line></par></text></block></page>
            """);
        Assert.Empty(result.Revisions);
        Assert.Null(Assert.Single(result.Lines).DiplomaticRevisionId);
        Assert.Contains(result.Unsupported, issue => issue.Element == "recognition-variants");
    }

    [Theory]
    [InlineData("", 20)]
    [InlineData("baseline=\"18\"", 18)]
    [InlineData("baseline=\"invalid\"", null)]
    public void Baseline_fallback_is_explicit_and_invalid_declared_baseline_is_not_replaced(string attribute, int? expectedY)
    {
        var result = ImportBody($"<page><block><text><par><line l=\"0\" t=\"0\" r=\"20\" b=\"20\" {attribute}/></par></text></block></page>");
        var line = Assert.Single(result.Lines);
        if (expectedY is { } y) Assert.Equal(new PointD(0, y), line.Baseline[0]);
        else Assert.Empty(line.Baseline);
        if (attribute.Length == 0)
            Assert.Contains(result.Unsupported, issue => issue.Element == "baseline-fallback" && issue.Severity == PageXml.IssueSeverity.Note);
        else if (expectedY is null)
            Assert.Contains(result.Unsupported, issue => issue.Element == "baseline" && issue.Severity == PageXml.IssueSeverity.Loss);
        else
            Assert.Empty(result.Unsupported);
    }
}
