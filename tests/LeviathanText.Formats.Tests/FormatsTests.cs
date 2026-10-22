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

public class ExternalIdsTests
{
    [Fact]
    public void Sanitizes_external_ids_to_the_entity_suffix_alphabet()
    {
        Assert.Equal("region_1", ExternalIds.Sanitize("region 1"));
        Assert.Equal("line__1_", ExternalIds.Sanitize("line (1)"));
        Assert.Equal("id", ExternalIds.Sanitize(""));
        Assert.Equal(63, ExternalIds.Sanitize(new string('a', 100)).Length);
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
    public void Direction_and_script_roundtrip_without_becoming_a_display_default()
    {
        var xml = """
            <PcGts xmlns="http://schema.primaresearch.org/PAGE/gts/pagecontent/2019-07-15">
              <Page imageFilename="dir.jpg" imageWidth="200" imageHeight="60">
                <TextRegion id="r1" readingDirection="right-to-left" primaryScript="Grek">
                  <Coords points="0,0 200,0 200,60 0,60"/>
                  <TextLine id="l1" readingDirection="top-to-bottom" primaryScript="Grek">
                    <Coords points="10,10 190,10 190,40 10,40"/>
                    <Baseline points="10,38 190,38"/>
                    <TextEquiv><Unicode>αβγ</Unicode></TextEquiv>
                  </TextLine>
                </TextRegion>
              </Page>
            </PcGts>
            """;
        var imported = PageXml.Import(xml, new EntityId(EntityKind.Capture, "cap1"), SampleProject.Sum);
        Assert.Equal(TextDirection.Rtl, Assert.Single(imported.Regions).Direction);
        Assert.Equal("Grek", Assert.Single(imported.Regions).Script);
        Assert.Equal(TextDirection.VerticalTopToBottom, Assert.Single(imported.Lines).Direction);
        Assert.Equal("Grek", Assert.Single(imported.Lines).Script);

        var surface = new NativeSurface
        {
            Id = new EntityId(EntityKind.Surface, "s1"),
            Kind = "page",
            Captures = [new NativeCapture
            {
                Id = new EntityId(EntityKind.Capture, "cap1"),
                Frame = new CoordinateFrame { ImageRef = new EntityId(EntityKind.Capture, "cap1"), ImageChecksum = SampleProject.Sum },
                ImageLocation = "dir.jpg",
            }],
            Regions = imported.Regions,
            Lines = imported.Lines,
            Revisions = imported.Revisions,
        };
        var reparsed = PageXml.Import(
            PageXml.Export(surface, surface.Captures[0], imported.Revisions),
            new EntityId(EntityKind.Capture, "cap1"), SampleProject.Sum);
        Assert.Equal(TextDirection.Rtl, Assert.Single(reparsed.Regions).Direction);
        Assert.Equal("Grek", Assert.Single(reparsed.Regions).Script);
        Assert.Equal(TextDirection.VerticalTopToBottom, Assert.Single(reparsed.Lines).Direction);
        Assert.Equal("Grek", Assert.Single(reparsed.Lines).Script);
    }

    [Fact]
    public void Language_tags_and_split_inherit_and_roundtrip()
    {
        var xml = """
            <PcGts xmlns="http://schema.primaresearch.org/PAGE/gts/pagecontent/2019-07-15">
              <Page imageFilename="meta.jpg" custom="language {type: grc;}">
                <TextRegion id="r1" custom="language {type: deu;} split {type: train;} structure {type: heading;}">
                  <Coords points="0,0 200,0 200,60 0,60"/>
                  <TextLine id="l1" custom="language {type: eng;}">
                    <Coords points="10,10 190,10 190,40 10,40"/>
                    <TextEquiv><Unicode>αβγ</Unicode></TextEquiv>
                  </TextLine>
                </TextRegion>
              </Page>
            </PcGts>
            """;
        var imported = PageXml.Import(xml, new EntityId(EntityKind.Capture, "cap1"), SampleProject.Sum);
        var region = Assert.Single(imported.Regions);
        Assert.Equal(["grc", "deu"], region.Languages);
        Assert.Equal("train", region.Split);
        Assert.Contains("heading", region.Tags["type"]);
        var line = Assert.Single(imported.Lines);
        Assert.Equal(["grc", "deu", "eng"], line.Languages);
        Assert.Equal("train", line.Split);

        var surface = new NativeSurface
        {
            Id = new EntityId(EntityKind.Surface, "s1"), Kind = "page",
            Captures = [new NativeCapture
            {
                Id = new EntityId(EntityKind.Capture, "cap1"),
                Frame = new CoordinateFrame { ImageRef = new EntityId(EntityKind.Capture, "cap1"), ImageChecksum = SampleProject.Sum },
                ImageLocation = "meta.jpg",
            }],
            Regions = imported.Regions, Lines = imported.Lines, Revisions = imported.Revisions,
        };
        var reparsed = PageXml.Import(PageXml.Export(surface, surface.Captures[0], imported.Revisions),
            new EntityId(EntityKind.Capture, "cap1"), SampleProject.Sum);
        Assert.Equal(["grc", "deu"], Assert.Single(reparsed.Regions).Languages);
        Assert.Equal("train", Assert.Single(reparsed.Regions).Split);
        Assert.Equal(["grc", "deu", "eng"], Assert.Single(reparsed.Lines).Languages);
    }

    [Fact]
    public void Invalid_declared_page_dimensions_are_refused()
    {
        var xml = """
            <PcGts xmlns="http://schema.primaresearch.org/PAGE/gts/pagecontent/2019-07-15">
              <Page imageFilename="x.png" imageWidth="foo" imageHeight="bar"/>
            </PcGts>
            """;
        Assert.Throws<InvalidDataException>(() =>
            PageXml.Import(xml, new EntityId(EntityKind.Capture, "cap1"), SampleProject.Sum));
    }

    [Fact]
    public void Explicit_reading_order_groups_are_preserved_as_total_or_partial()
    {
        var xml = """
            <PcGts xmlns="http://schema.primaresearch.org/PAGE/gts/pagecontent/2019-07-15">
              <Page imageFilename="ro.jpg" imageWidth="200" imageHeight="100">
                <ReadingOrder>
                  <OrderedGroup id="ro_main" caption="Main text">
                    <RegionRefIndexed regionRef="r2" index="0"/>
                    <RegionRefIndexed regionRef="r1" index="1"/>
                  </OrderedGroup>
                </ReadingOrder>
              </Page>
            </PcGts>
            """;
        var imported = PageXml.Import(xml, new EntityId(EntityKind.Capture, "cap1"), SampleProject.Sum);
        var group = Assert.Single(imported.ReadingOrders);
        Assert.Equal("ro_main", group.Id);
        Assert.Equal("Main text", group.Caption);
        Assert.True(group.IsTotal);
        Assert.Equal(["r2", "r1"], group.RegionRefs);

        var partialXml = """
            <PcGts xmlns="http://schema.primaresearch.org/PAGE/gts/pagecontent/2019-07-15">
              <Page imageFilename="ro.jpg" imageWidth="200" imageHeight="100">
                <ReadingOrder>
                  <UnorderedGroup id="ug">
                    <OrderedGroup id="ro_main"><RegionRefIndexed regionRef="r1" index="0"/></OrderedGroup>
                    <OrderedGroup id="ro_margin"><RegionRefIndexed regionRef="r3" index="0"/></OrderedGroup>
                  </UnorderedGroup>
                </ReadingOrder>
              </Page>
            </PcGts>
            """;
        var partial = PageXml.Import(partialXml, new EntityId(EntityKind.Capture, "cap1"), SampleProject.Sum);
        Assert.Equal(2, partial.ReadingOrders.Count);
        Assert.All(partial.ReadingOrders, group => Assert.False(group.IsTotal));
        Assert.Equal(["r1"], partial.ReadingOrders.Single(g => g.Id == "ro_main").RegionRefs);
        Assert.Equal(["r3"], partial.ReadingOrders.Single(g => g.Id == "ro_margin").RegionRefs);
    }

    [Fact]
    public void Duplicate_page_element_ids_are_refused()
    {
        var xml = """
            <PcGts xmlns="http://schema.primaresearch.org/PAGE/gts/pagecontent/2019-07-15">
              <Page imageFilename="dup.jpg" imageWidth="100" imageHeight="50">
                <TextRegion id="r1"><Coords points="0,0 100,0 100,50 0,50"/>
                  <TextLine id="same"><Coords points="1,1 90,1 90,20 1,20"/></TextLine>
                  <TextLine id="same"><Coords points="1,25 90,25 90,45 1,45"/></TextLine>
                </TextRegion>
              </Page>
            </PcGts>
            """;
        Assert.Throws<InvalidDataException>(() =>
            PageXml.Import(xml, new EntityId(EntityKind.Capture, "cap1"), SampleProject.Sum));
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
    public void Abbreviation_export_uses_choice_abbr_expan_and_reimports_text()
    {
        var lineId = new EntityId(EntityKind.Line, "line-abbr");
        var revision = new TextRevision
        {
            Id = new EntityId(EntityKind.TranscriptionRevision, "rev-abbr"),
            ScopeId = lineId,
            Layer = TextLayer.L1Diplomatic,
            Author = SampleProject.Agent,
            CreatedAt = DateTimeOffset.UtcNow,
            Text = "κϲ",
        };
        var annotations = new[]
        {
            new Annotation
            {
                Id = new EntityId(EntityKind.Annotation, "abbr-1"),
                Kind = AnnotationKind.Abbreviation,
                AnchoredRevisionId = revision.Id,
                Range = new ScalarRange(0, 2),
                ExpansionText = "κυρίου",
            },
        };
        var xml = TeiXml.Export([(lineId, revision, annotations)], "abbr");
        Assert.Contains("<abbr>κϲ</abbr>", xml);
        Assert.Contains("<expan>κυρίου</expan>", xml);

        var reparsed = TeiXml.Import(xml);
        Assert.Equal("κϲ", reparsed.Revisions[0].Text);
        Assert.Contains(reparsed.Annotations, a => a.Kind == AnnotationKind.Abbreviation && a.ExpansionText == "κυρίου");
    }

    [Fact]
    public void Direction_and_script_roundtrip_in_the_bounded_tei_rend_attribute()
    {
        var lineId = new EntityId(EntityKind.Line, "line-1");
        var revision = new TextRevision
        {
            Id = new EntityId(EntityKind.TranscriptionRevision, "rev-1"),
            ScopeId = lineId,
            Layer = TextLayer.L1Diplomatic,
            Author = SampleProject.Agent,
            CreatedAt = DateTimeOffset.UtcNow,
            Text = "αβγ",
        };
        var orientations = new Dictionary<EntityId, TeiXml.TeiLineOrientation>
        {
            [lineId] = new(lineId, TextDirection.Rtl, "Grek"),
        };
        var xml = TeiXml.Export([(lineId, revision, (IReadOnlyList<Annotation>)[])], "orientation", orientations);
        Assert.Contains("rend=\"direction:right-to-left;script:Grek\"", xml);

        var reparsed = TeiXml.Import(xml);
        var orientation = Assert.Single(reparsed.Orientations!);
        Assert.Equal(TextDirection.Rtl, orientation.Direction);
        Assert.Equal("Grek", orientation.Script);
    }

    [Fact]
    public void External_entities_are_refused()
    {
        var xml = "<!DOCTYPE TEI [<!ENTITY x SYSTEM \"http://evil/x\">]><TEI xmlns=\"http://www.tei-c.org/ns/1.0\"><text/></TEI>";
        Assert.Throws<InvalidDataException>(() => TeiXml.Import(xml));
    }
}

public class PageExportOrderingTests
{
    [Fact]
    public void Export_orders_regions_and_lines_by_reading_order_and_records_dimensions()
    {
        var captureId = new EntityId(EntityKind.Capture, "cap1");
        var regionA = new NativeRegion
        {
            Id = new EntityId(EntityKind.Region, "regA"), FrameRef = captureId,
            Polygon = [new(0, 0), new(100, 0), new(100, 50), new(0, 50)],
            LayoutSource = LayoutSource.Manual, ReadingOrderIndex = 1,
        };
        var regionB = new NativeRegion
        {
            Id = new EntityId(EntityKind.Region, "regB"), FrameRef = captureId,
            Polygon = [new(0, 60), new(100, 60), new(100, 110), new(0, 110)],
            LayoutSource = LayoutSource.Manual, ReadingOrderIndex = 0,
        };
        NativeLine Line(string id, EntityId region, int top) => new()
        {
            Id = new EntityId(EntityKind.Line, id), RegionId = region, FrameRef = captureId,
            Polygon = [new(0, top), new(100, top), new(100, top + 20), new(0, top + 20)],
            Baseline = [new(0, top + 18), new(100, top + 18)],
        };

        var surface = new NativeSurface
        {
            Id = new EntityId(EntityKind.Surface, "s1"), Kind = "page",
            Captures = [new NativeCapture
            {
                Id = captureId,
                Frame = new CoordinateFrame { ImageRef = captureId, ImageChecksum = SampleProject.Sum },
                ImageLocation = "page.png", WidthPx = 800, HeightPx = 1000,
            }],
            Regions = [regionA, regionB],
            // Deliberately out of order: region A's line first, then B's.
            Lines = [Line("a1", regionA.Id, 10), Line("b1", regionB.Id, 70)],
        };

        var xml = PageXml.Export(surface, surface.Captures[0], []);
        var document = System.Xml.Linq.XDocument.Parse(xml);
        var page = document.Root!.Elements().First(e => e.Name.LocalName == "Page");
        Assert.Equal("800", (string?)page.Attribute("imageWidth"));
        Assert.Equal("1000", (string?)page.Attribute("imageHeight"));

        var regionIds = page.Elements().Where(e => e.Name.LocalName == "TextRegion")
            .Select(e => (string?)e.Attribute("id")).ToList();
        Assert.Equal(["regB", "regA"], regionIds);
        var readingOrder = page.Elements().First(e => e.Name.LocalName == "ReadingOrder")
            .Elements().Select(e => (string?)e.Attribute("regionRef")).ToList();
        Assert.Equal(["regB", "regA"], readingOrder);
        Assert.Equal("b1", (string?)page.Elements().First(e => (string?)e.Attribute("id") == "regB")
            .Elements().First(e => e.Name.LocalName == "TextLine").Attribute("id"));
    }
}
