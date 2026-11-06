using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Formats.Tests;

public class HocrFormatTests
{
    [Theory]
    [InlineData(TextDirection.Ltr, false, "bbox 10 58 110 58")]
    [InlineData(TextDirection.Ltr, true, "bbox 10 32 110 32")]
    [InlineData(TextDirection.Ltr, null, "bbox 10 45 110 45")]
    [InlineData(TextDirection.Rtl, false, "bbox 10 58 110 58")]
    [InlineData(TextDirection.Rtl, true, "bbox 10 32 110 32")]
    [InlineData(TextDirection.Rtl, null, "bbox 10 45 110 45")]
    [InlineData(TextDirection.VerticalTopToBottom, false, "bbox 35 20 35 70")]
    [InlineData(TextDirection.VerticalTopToBottom, true, "bbox 85 20 85 70")]
    [InlineData(TextDirection.VerticalTopToBottom, null, "bbox 60 20 60 70")]
    [InlineData(TextDirection.VerticalBottomToTop, false, "bbox 85 20 85 70")]
    [InlineData(TextDirection.VerticalBottomToTop, true, "bbox 35 20 35 70")]
    [InlineData(TextDirection.VerticalBottomToTop, null, "bbox 60 20 60 70")]
    public void Export_uses_baseline_extents_when_boundary_is_empty_without_changing_direction_or_input(
        TextDirection direction, bool? topline, string expectedBox)
    {
        var (surface, capture) = Sample();
        var baseline = LineGeometry.Baseline(new(10, 20, 110, 70), direction, topline).ToArray();
        var before = baseline.ToArray();
        var line = surface.Lines[0] with { Polygon = [], Baseline = baseline, Direction = direction };
        surface = surface with { Lines = [line] };

        var document = System.Xml.Linq.XDocument.Parse(HocrFormat.Export(surface, capture, surface.Revisions));
        var exported = document.Descendants().Single(element => (string?)element.Attribute("id") == "line_ln1");
        var title = (string)exported.Attribute("title")!;

        Assert.StartsWith(expectedBox + "; baseline 0 ", title);
        Assert.Equal(expectedBox, (string?)Assert.Single(exported.Elements()).Attribute("title"));
        if (direction is TextDirection.Ltr or TextDirection.Rtl)
            Assert.Equal(direction == TextDirection.Rtl ? "rtl" : "ltr", (string?)exported.Attribute("dir"));
        else
        {
            Assert.Contains("ocr_vertical", (string)exported.Attribute("class")!);
            Assert.EndsWith(direction == TextDirection.VerticalBottomToTop ? "; textangle -90" : "; textangle 90", title);
        }
        Assert.Empty(line.Polygon);
        Assert.Equal(before, line.Baseline);
        Assert.Same(baseline, line.Baseline);
    }

    [Fact]
    public void Export_prefers_boundary_extents_and_preserves_existing_midpoint_rounding()
    {
        var (surface, capture) = Sample();
        PointD[] polygon = [new(10.5, 20.5), new(110.5, 20.5), new(110.5, 70.5), new(10.5, 70.5)];
        PointD[] baseline = [new(-50, -60), new(250, 260)];
        var line = surface.Lines[0] with { Polygon = polygon, Baseline = baseline };
        var originalPolygon = polygon.ToArray();
        var originalBaseline = baseline.ToArray();
        surface = surface with { Lines = [line] };

        var html = HocrFormat.Export(surface, capture, surface.Revisions);

        Assert.Contains("bbox 10 20 110 70; baseline 0 320", html);
        Assert.Equal(originalPolygon, line.Polygon);
        Assert.Equal(originalBaseline, line.Baseline);
    }

    [Theory]
    [InlineData(true, "bbox 5 5 5 5")]
    [InlineData(false, "bbox 0 0 0 0")]
    public void Export_preserves_degenerate_or_missing_geometry_without_inventing_area(bool baselinePresent, string expectedBox)
    {
        var (surface, capture) = Sample();
        var line = surface.Lines[0] with
        {
            Polygon = [], Baseline = baselinePresent ? LineGeometry.Baseline(new(5, 5, 5, 5)) : [],
        };
        surface = surface with { Lines = [line] };

        var html = HocrFormat.Export(surface, capture, surface.Revisions);

        Assert.Contains(expectedBox + "; baseline 0 0", html);
        Assert.Empty(line.Polygon);
        Assert.Equal(baselinePresent ? 2 : 0, line.Baseline.Count);
    }

    private static (NativeSurface Surface, NativeCapture Capture) Sample()
    {
        var captureId = new EntityId(EntityKind.Capture, "cap1");
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
                        Id = new EntityId(EntityKind.Region, "reg1"),
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
                        RegionId = new EntityId(EntityKind.Region, "reg1"),
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
                ImageLocation = "fixture page.png",
                WidthPx = 200,
                HeightPx = 60,
            });
    }

    [Fact]
    public void Export_writes_ocr_page_and_line_spans_with_text()
    {
        var (surface, capture) = Sample();
        var html = HocrFormat.Export(surface, capture, surface.Revisions);

        Assert.Contains("class=\"ocr_page\"", html);
        Assert.Contains("class=\"ocr_line\"", html);
        Assert.Contains("bbox 10 10 190 40", html);
        Assert.Contains("α\u0305λ\u0305α\u0305νθρωπ", html);
        Assert.Contains("fixture page.png", html);
    }

    [Fact]
    public void Export_import_roundtrip_preserves_text_and_bounds()
    {
        var (surface, capture) = Sample();
        var html = HocrFormat.Export(surface, capture, surface.Revisions);
        var imported = HocrFormat.Import(html, capture.Id, SampleProject.Sum);

        var revision = Assert.Single(imported.Revisions);
        Assert.Equal("α\u0305λ\u0305α\u0305νθρωπ", revision.Text);
        var line = Assert.Single(imported.Lines);
        Assert.Equal(new PointD(10, 10), line.Polygon[0]);
        Assert.Equal(new PointD(190, 40), line.Polygon[2]);
        Assert.Empty(imported.Unsupported);
    }

    [Fact]
    public void Unsupported_line_children_are_reported()
    {
        var html = """
            <html xmlns="http://www.w3.org/1999/xhtml"><body>
              <div class="ocr_page"><div class="ocr_carea"><p class="ocr_par">
                <span class="ocr_line" id="l1" title="bbox 0 0 90 20">
                  <span class="ocrx_word" title="bbox 0 0 90 20">abc</span>
                  <span class="ocr_caption">caption text</span>
                </span>
              </p></div></div>
            </body></html>
            """;
        var imported = HocrFormat.Import(html, new EntityId(EntityKind.Capture, "c1"), SampleProject.Sum);
        Assert.Equal("abc caption text", Assert.Single(imported.Revisions).Text.Trim());
        Assert.Contains(imported.Unsupported, i => i.Element == "ocr_caption");
    }

    [Fact]
    public void Direction_and_vertical_class_roundtrip()
    {
        var (surface, capture) = Sample();
        var rtl = surface with
        {
            Lines = [surface.Lines[0] with { Direction = TextDirection.Rtl }],
        };
        var html = HocrFormat.Export(rtl, capture, rtl.Revisions);
        Assert.Contains("dir=\"rtl\"", html);
        var imported = HocrFormat.Import(html, capture.Id, SampleProject.Sum);
        Assert.Equal(TextDirection.Rtl, Assert.Single(imported.Lines).Direction);

        var vertical = surface with
        {
            Lines = [surface.Lines[0] with { Direction = TextDirection.VerticalBottomToTop }],
        };
        var verticalHtml = HocrFormat.Export(vertical, capture, vertical.Revisions);
        Assert.Contains("ocr_vertical", verticalHtml);
        Assert.Contains("textangle -90", verticalHtml);
        var reimported = HocrFormat.Import(verticalHtml, capture.Id, SampleProject.Sum);
        Assert.Equal(TextDirection.VerticalBottomToTop, Assert.Single(reimported.Lines).Direction);
    }

    [Fact]
    public void Non_hocr_document_is_refused()
    {
        Assert.Throws<InvalidDataException>(() => HocrFormat.Import(
            "<html xmlns=\"http://www.w3.org/1999/xhtml\"><body/></html>",
            new EntityId(EntityKind.Capture, "c1"), SampleProject.Sum));
        Assert.Throws<InvalidDataException>(() => HocrFormat.Import(
            "<div>not html</div>", new EntityId(EntityKind.Capture, "c1"), SampleProject.Sum));
    }

    [Fact]
    public void External_entities_are_refused()
    {
        var html = "<!DOCTYPE html [<!ENTITY x SYSTEM \"file:///etc/passwd\">]>" +
                   "<html xmlns=\"http://www.w3.org/1999/xhtml\"><body><div class=\"ocr_page\"/></body></html>";
        Assert.Throws<InvalidDataException>(() => HocrFormat.Import(
            html, new EntityId(EntityKind.Capture, "c1"), SampleProject.Sum));
    }
}
