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
    public void Language_tags_and_split_inherit_from_block_and_line()
    {
        var xml = """
            <alto xmlns="http://www.loc.gov/standards/alto/ns-v4#">
              <Tags>
                <OtherTag ID="lang0" TYPE="language" LABEL="deu"/>
                <OtherTag ID="lang1" TYPE="language" LABEL="eng"/>
                <OtherTag ID="split0" TYPE="split" LABEL="train"/>
                <OtherTag ID="type0" TYPE="type" LABEL="heading"/>
              </Tags>
              <Layout><Page WIDTH="200" HEIGHT="60"><PrintSpace>
                <TextBlock ID="b1" LANG="hbo" TAGREFS="lang0 split0">
                  <TextLine ID="l1" LANG="heb" TAGREFS="lang1 type0">
                    <Shape><Polygon POINTS="10,10 190,10 190,40 10,40"/></Shape>
                    <String CONTENT="αβγ"/>
                  </TextLine>
                </TextBlock>
              </PrintSpace></Page></Layout>
            </alto>
            """;
        var imported = AltoXml.Import(xml, new EntityId(EntityKind.Capture, "cap1"), SampleProject.Sum);
        var region = Assert.Single(imported.Regions);
        Assert.Equal(["hbo", "deu"], region.Languages);
        Assert.Equal("train", region.Split);
        var line = Assert.Single(imported.Lines);
        Assert.Equal(["hbo", "deu", "heb", "eng"], line.Languages);
        Assert.Equal("train", line.Split);
        Assert.Contains("heading", line.Tags["type"]);

        var reparsed = AltoXml.Import(AltoXml.Export(
            new NativeSurface
            {
                Id = new EntityId(EntityKind.Surface, "s1"), Kind = "page", Captures = [],
                Regions = imported.Regions, Lines = imported.Lines, Revisions = imported.Revisions,
            },
            new NativeCapture
            {
                Id = new EntityId(EntityKind.Capture, "cap1"),
                Frame = new CoordinateFrame { ImageRef = new EntityId(EntityKind.Capture, "cap1"), ImageChecksum = SampleProject.Sum },
                ImageLocation = "x.png", WidthPx = 200, HeightPx = 60,
            }, imported.Revisions), new EntityId(EntityKind.Capture, "cap1"), SampleProject.Sum);
        Assert.Equal(["hbo", "deu"], Assert.Single(reparsed.Regions).Languages);
        Assert.Equal(["hbo", "deu", "heb", "eng"], Assert.Single(reparsed.Lines).Languages);
    }

    [Fact]
    public void Direction_baseline_and_declared_dimensions_inherit_and_roundtrip()
    {
        var xml = """
            <alto xmlns="http://www.loc.gov/standards/alto/ns-v4#">
              <Layout><Page WIDTH="200" HEIGHT="60" LANG="grc" BASEDIRECTION="rtl"><PrintSpace>
                <TextBlock ID="b1" BASEDIRECTION="ltr">
                  <TextLine ID="l1" BASELINE="10 50 190 48">
                    <Shape><Polygon POINTS="10,10 190,10 190,40 10,40"/></Shape>
                    <String CONTENT="αβγ"/>
                  </TextLine>
                  <TextLine ID="l2" BASEDIRECTION="ttb" BASELINE="12 58 188 56">
                    <Shape><Polygon POINTS="12,12 188,12 188,44 12,44"/></Shape>
                    <String CONTENT="δε"/>
                  </TextLine>
                </TextBlock>
              </PrintSpace></Page></Layout>
            </alto>
            """;
        var imported = AltoXml.Import(xml, new EntityId(EntityKind.Capture, "cap1"), SampleProject.Sum);
        Assert.Equal(200, imported.WidthPx);
        Assert.Equal(60, imported.HeightPx);
        var region = Assert.Single(imported.Regions);
        Assert.Equal(TextDirection.Ltr, region.Direction);
        Assert.Equal(["grc"], region.Languages);
        Assert.Equal(TextDirection.Ltr, imported.Lines[0].Direction);
        Assert.Equal(TextDirection.VerticalTopToBottom, imported.Lines[1].Direction);
        Assert.Equal([new PointD(10, 50), new PointD(190, 48)], imported.Lines[0].Baseline);

        var surface = new NativeSurface
        {
            Id = new EntityId(EntityKind.Surface, "s1"), Kind = "page", Captures = [],
            Regions = imported.Regions, Lines = imported.Lines, Revisions = imported.Revisions,
        };
        var capture = new NativeCapture
        {
            Id = new EntityId(EntityKind.Capture, "cap1"),
            Frame = new CoordinateFrame { ImageRef = new EntityId(EntityKind.Capture, "cap1"), ImageChecksum = SampleProject.Sum },
            ImageLocation = "x.png", WidthPx = 200, HeightPx = 60,
        };
        var exported = AltoXml.Export(surface, capture, imported.Revisions);
        Assert.Contains("BASEDIRECTION=\"ltr\"", exported);
        Assert.Contains("BASEDIRECTION=\"ttb\"", exported);
        Assert.Contains("BASELINE=\"10 50 190 48\"", exported);

        var reparsed = AltoXml.Import(exported, capture.Id, SampleProject.Sum);
        Assert.Equal(TextDirection.Ltr, Assert.Single(reparsed.Regions).Direction);
        Assert.Equal(TextDirection.VerticalTopToBottom, reparsed.Lines[1].Direction);
        Assert.Equal(imported.Lines[0].Baseline, reparsed.Lines[0].Baseline);
    }

    [Fact]
    public void Zero_dimensions_are_reported_as_unavailable()
    {
        var xml = """
            <alto xmlns="http://www.loc.gov/standards/alto/ns-v4#">
              <Layout><Page WIDTH="0" HEIGHT="0"><PrintSpace>
                <TextBlock ID="b1"><TextLine ID="l1">
                  <Shape><Polygon POINTS="0,0 10,0 10,10 0,10"/></Shape>
                </TextLine></TextBlock>
              </PrintSpace></Page></Layout>
            </alto>
            """;
        var imported = AltoXml.Import(xml, new EntityId(EntityKind.Capture, "cap1"), SampleProject.Sum);
        Assert.Null(imported.WidthPx);
        Assert.Null(imported.HeightPx);
    }

    [Fact]
    public void Explicit_reading_order_groups_are_preserved()
    {
        var xml = """
            <alto xmlns="http://www.loc.gov/standards/alto/ns-v4#">
              <ReadingOrder>
                <OrderedGroup ID="ro_total"><ElementRef REF="l1"/><ElementRef REF="l2"/></OrderedGroup>
                <UnorderedGroup ID="ug">
                  <OrderedGroup ID="ro_a"><ElementRef REF="l1"/></OrderedGroup>
                  <OrderedGroup ID="ro_b"><ElementRef REF="l2"/></OrderedGroup>
                </UnorderedGroup>
              </ReadingOrder>
              <Layout><Page WIDTH="100" HEIGHT="50"><PrintSpace>
                <TextBlock ID="b1"><TextLine ID="l1"><Shape><Polygon POINTS="0,0 10,0 10,10 0,10"/></Shape></TextLine>
                  <TextLine ID="l2"><Shape><Polygon POINTS="0,20 10,20 10,30 0,30"/></Shape></TextLine></TextBlock>
              </PrintSpace></Page></Layout>
            </alto>
            """;
        var imported = AltoXml.Import(xml, new EntityId(EntityKind.Capture, "cap1"), SampleProject.Sum);
        Assert.Equal(3, imported.ReadingOrders.Count);
        Assert.True(imported.ReadingOrders.Single(g => g.Id == "ro_total").IsTotal);
        Assert.False(imported.ReadingOrders.Single(g => g.Id == "ro_a").IsTotal);
        Assert.Equal(["l1"], imported.ReadingOrders.Single(g => g.Id == "ro_a").RegionRefs);
    }

    [Fact]
    public void Non_pixel_measurement_unit_and_empty_source_image_are_refused()
    {
        var badUnit = """
            <alto xmlns="http://www.loc.gov/standards/alto/ns-v4#">
              <Description><MeasurementUnit>mm</MeasurementUnit></Description>
              <Layout><Page WIDTH="100" HEIGHT="50"/></Layout>
            </alto>
            """;
        Assert.Throws<InvalidDataException>(() =>
            AltoXml.Import(badUnit, new EntityId(EntityKind.Capture, "cap1"), SampleProject.Sum));

        var emptyImage = """
            <alto xmlns="http://www.loc.gov/standards/alto/ns-v4#">
              <Description><MeasurementUnit>pixel</MeasurementUnit>
                <sourceImageInformation><fileName></fileName></sourceImageInformation>
              </Description>
              <Layout><Page WIDTH="100" HEIGHT="50"/></Layout>
            </alto>
            """;
        Assert.Throws<InvalidDataException>(() =>
            AltoXml.Import(emptyImage, new EntityId(EntityKind.Capture, "cap1"), SampleProject.Sum));
    }

    [Fact]
    public void Invalid_declared_dimensions_are_refused()
    {
        var xml = """
            <alto xmlns="http://www.loc.gov/standards/alto/ns-v4#">
              <Layout><Page WIDTH="foo" HEIGHT="bar"/></Layout>
            </alto>
            """;
        Assert.Throws<InvalidDataException>(() =>
            AltoXml.Import(xml, new EntityId(EntityKind.Capture, "cap1"), SampleProject.Sum));
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
