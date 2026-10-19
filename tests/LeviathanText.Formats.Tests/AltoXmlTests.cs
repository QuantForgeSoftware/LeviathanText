using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Formats.Tests;

public class AltoXmlTests
{
    private static (NativeSurface Surface, NativeCapture Capture) Sample()
    {
        var captureId = new EntityId(EntityKind.Capture, "cap1");
        var regionId = new EntityId(EntityKind.Region, "reg1");
        var lineId = new EntityId(EntityKind.Line, "ln1");
        var revision = new TextRevision
        {
            Id = new EntityId(EntityKind.TranscriptionRevision, "r1"),
            ScopeId = lineId,
            Layer = TextLayer.L1Diplomatic,
            Author = SampleProject.Agent,
            CreatedAt = new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero),
            Text = "α\u0305λ\u0305α\u0305νθρωπ",
        };

        return (
            new NativeSurface
            {
                Id = new EntityId(EntityKind.Surface, "s1"),
                Kind = "page",
                Captures = [],
                Regions =
                [
                    new NativeRegion
                    {
                        Id = regionId,
                        FrameRef = captureId,
                        Polygon = [new(0, 0), new(200, 0), new(200, 60), new(0, 60)],
                        LayoutSource = LayoutSource.Manual,
                        ReadingOrderIndex = 0,
                    },
                ],
                Lines =
                [
                    new NativeLine
                    {
                        Id = lineId,
                        RegionId = regionId,
                        FrameRef = captureId,
                        Polygon = [new(10, 10), new(190, 10), new(190, 40), new(10, 40)],
                        Baseline = [new(10, 38), new(190, 36)],
                        DiplomaticRevisionId = revision.Id,
                    },
                ],
                Revisions = [revision],
            },
            new NativeCapture
            {
                Id = captureId,
                Frame = new CoordinateFrame { ImageRef = captureId, ImageChecksum = SampleProject.Sum },
                ImageLocation = "fixture.png",
                WidthPx = 200,
                HeightPx = 60,
            });
    }

    [Fact]
    public void Export_produces_alto_v4_with_polygons_and_text()
    {
        var (surface, capture) = Sample();
        var xml = AltoXml.Export(surface, capture, surface.Revisions);

        Assert.Contains("http://www.loc.gov/standards/alto/ns-v4#", xml);
        Assert.Contains("POINTS=\"10,10 190,10 190,40 10,40\"", xml);
        Assert.Contains("α\u0305λ\u0305α\u0305νθρωπ", xml);
    }

    [Fact]
    public void Export_import_roundtrip_preserves_text_and_geometry()
    {
        var (surface, capture) = Sample();
        var xml = AltoXml.Export(surface, capture, surface.Revisions);
        var imported = AltoXml.Import(xml, capture.Id, SampleProject.Sum);

        var revision = Assert.Single(imported.Revisions);
        Assert.Equal("α\u0305λ\u0305α\u0305νθρωπ", revision.Text);
        var line = Assert.Single(imported.Lines);
        Assert.Equal(4, line.Polygon.Count);
        Assert.Equal(new PointD(10, 10), line.Polygon[0]);
        Assert.Empty(imported.Unsupported);
    }

    [Fact]
    public void Import_reports_unsupported_inline_elements()
    {
        var xml = """
            <?xml version="1.0"?>
            <alto xmlns="http://www.loc.gov/standards/alto/ns-v4#">
              <Layout><Page WIDTH="100" HEIGHT="50"><PrintSpace>
                <TextBlock ID="b1"><TextLine ID="l1">
                  <Shape><Polygon POINTS="0,0 90,0 90,20 0,20"/></Shape>
                  <Hyphenation/> 
                  <String CONTENT="abc"/>
                </TextLine></TextBlock>
              </PrintSpace></Page></Layout>
            </alto>
            """;
        var imported = AltoXml.Import(xml, new EntityId(EntityKind.Capture, "cap1"), SampleProject.Sum);
        Assert.Single(imported.Revisions);
        Assert.Contains(imported.Unsupported, i => i.Element == "Hyphenation");
    }

    [Fact]
    public void Wrong_namespace_is_refused()
    {
        var xml = """<alto xmlns="http://example.test/alto"><Layout/></alto>""";
        Assert.Throws<InvalidDataException>(() =>
            AltoXml.Import(xml, new EntityId(EntityKind.Capture, "c1"), SampleProject.Sum));
    }

    [Fact]
    public void External_entities_are_refused()
    {
        var xml = "<!DOCTYPE alto [<!ENTITY x SYSTEM \"file:///etc/passwd\">]>" +
                  "<alto xmlns=\"http://www.loc.gov/standards/alto/ns-v4#\"><Layout/></alto>";
        Assert.Throws<InvalidDataException>(() =>
            AltoXml.Import(xml, new EntityId(EntityKind.Capture, "c1"), SampleProject.Sum));
    }
}
