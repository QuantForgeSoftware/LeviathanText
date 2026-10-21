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
}
