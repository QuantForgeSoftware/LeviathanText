using LeviathanText.Contracts;

namespace LeviathanText.Formats.Tests;

/// <summary>
/// Bounded TEI/EpiDoc profile behaviour: line splitting, layered readings, image links,
/// stable identifiers, corrections/provenance and mandatory loss reporting.
/// Fixtures are invented unless explicitly marked otherwise.
/// </summary>
public class TeiProfileTests
{
    private const string Prefix = """<TEI xmlns="http://www.tei-c.org/ns/1.0">""";
    private const string Suffix = """</TEI>""";

    private static string Document(string body, string header = "<teiHeader><fileDesc><titleStmt><title>T</title></titleStmt><publicationStmt><p>p</p></publicationStmt></fileDesc></teiHeader>") =>
        Prefix + header + "<text><body>" + body + "</body></text>" + Suffix;

    private static TextRevision Revision(TeiXml.TeiImportResult result, TextLayer layer) =>
        Assert.Single(result.Revisions, r => r.Layer == layer);

    [Fact]
    public void Edition_lines_split_at_lb_with_numbers_ids_and_break_no_continuation()
    {
        var xml = Document("""
            <div type="edition" subtype="primary"><div type="textpart">
              <ab xml:id="line-a"><lb n="1"/>αβ<lb n="2" break="no"/>γδ<lb n="3"/>εζ</ab>
            </div></div>
            """);

        var result = TeiXml.Import(xml);

        var l1 = result.Revisions.Where(r => r.Layer == TextLayer.L1Diplomatic).ToList();
        Assert.Equal(2, l1.Count);
        Assert.Equal("αβγδ", l1[0].Text);
        Assert.Equal("εζ", l1[1].Text);
        Assert.Equal(new int?[] { 1, 3 }, result.Document!.Lines.Select(l => l.Number));
        Assert.Equal("line-a", result.Document.Lines[0].XmlId);
        Assert.Equal(l1[0].ScopeId, result.Document.Lines[0].LineId);
    }

    [Fact]
    public void Supplied_and_expansion_text_live_in_l2_and_links_roundtrip()
    {
        var xml = Document("""
            <div type="edition"><ab><lb/>κ<supplied reason="editorial">α</supplied><choice><abbr>ϲ</abbr><expan>ου</expan></choice></ab></div>
            """);

        var result = TeiXml.Import(xml);
        var l1 = Revision(result, TextLayer.L1Diplomatic);
        var l2 = Revision(result, TextLayer.L2Editorial);
        Assert.Equal("κϲ", l1.Text);
        Assert.Equal("καου", l2.Text);

        var supplied = Assert.Single(result.Annotations, a => a.Kind == AnnotationKind.SuppliedEditorial);
        Assert.Equal(l2.Id, supplied.AnchoredRevisionId);
        Assert.Equal(new ScalarRange(1, 2), supplied.Range);

        var abbreviation = Assert.Single(result.Annotations, a => a.Kind == AnnotationKind.Abbreviation);
        Assert.Equal(l1.Id, abbreviation.AnchoredRevisionId);
        Assert.Equal("ου", abbreviation.ExpansionText);
        Assert.Equal(l2.Id, abbreviation.ExpansionRevisionId);
        Assert.Equal(new ScalarRange(2, 4), abbreviation.ExpansionRange);

        var exported = TeiXml.ExportDocument(
            [new TeiXml.TeiExportLine(l1.ScopeId, l1, l2, result.Annotations)], "links");
        var reparsed = TeiXml.Import(exported.Xml);
        Assert.Equal("κϲ", Revision(reparsed, TextLayer.L1Diplomatic).Text);
        Assert.Equal("καου", Revision(reparsed, TextLayer.L2Editorial).Text);
        var reAbbr = Assert.Single(reparsed.Annotations, a => a.Kind == AnnotationKind.Abbreviation);
        Assert.Equal("ου", reAbbr.ExpansionText);
        Assert.NotNull(reAbbr.ExpansionRevisionId);
    }

    [Fact]
    public void Facsimile_image_link_and_provenance_roundtrip()
    {
        var lineId = new EntityId(EntityKind.Line, "ln-roundtrip");
        var l1 = new TextRevision
        {
            Id = new EntityId(EntityKind.TranscriptionRevision, "rev-roundtrip"),
            ScopeId = lineId,
            Layer = TextLayer.L1Diplomatic,
            Author = new AgentRef { Id = "author", IsModel = false },
            CreatedAt = DateTimeOffset.UtcNow,
            Text = "αβ",
        };
        var context = new TeiXml.TeiExportContext(
            SurfaceId: "surf-roundtrip",
            Graphics: [new TeiXml.TeiGraphic("ISic001151_w800.jpg", "screen", 800, 600)],
            // Real EpiDoc may write "5520px"; the import path tolerates the unit.
            Idnos: [new TeiXml.TeiIdno("TM", "12345")],
            Licence: "CC BY 4.0",
            Responsibilities: [new TeiXml.TeiResponsibility("transcriber", "A Reviewer", "2026-11-02")],
            SourceHash: "abcdef");

        var exported = TeiXml.ExportDocument(
            [new TeiXml.TeiExportLine(lineId, l1, null, [],
                new TeiXml.TeiLineInfo(lineId, 7, "line-z", null, null, ["grc"], []))],
            "linked", context);

        Assert.Contains("<facsimile>", exported.Xml);
        Assert.Contains("ISic001151_w800.jpg", exported.Xml);
        Assert.Contains("type=\"TM\"", exported.Xml);
        Assert.Contains("CC BY 4.0", exported.Xml);
        Assert.DoesNotContain(exported.Report.Entries, e => e.Category == "image-link");

        var reparsed = TeiXml.Import(exported.Xml);
        Assert.Equal("linked", reparsed.Document!.Title);
        Assert.Equal("surf-roundtrip", reparsed.Document.SurfaceId);
        Assert.Equal("ISic001151_w800.jpg", Assert.Single(reparsed.Document.Graphics).Url);
        Assert.Equal("12345", Assert.Single(reparsed.Document.Idnos).Value);
        Assert.Equal("CC BY 4.0", reparsed.Document.Licence);
        Assert.Contains(reparsed.Document.Responsibilities, r => r.Name == "A Reviewer");
        Assert.Equal(7, Assert.Single(reparsed.Document.Lines).Number);
        Assert.NotNull(reparsed.OriginalMarkup);
    }

    [Fact]
    public void Availability_html_in_cdata_is_reduced_to_readable_licence_text()
    {
        var xml = Prefix + """
            <teiHeader><fileDesc><publicationStmt><availability><p><![CDATA[
              This work is licensed under a <a rel="license" href="https://creativecommons.org/licenses/by/4.0/">Creative Commons Attribution 4.0</a>.
            ]]></p></availability></publicationStmt></fileDesc></teiHeader>
            <text><body><div type="edition"><ab><lb/>α</ab></div></body></text>
            """ + Suffix;

        var licence = TeiXml.Import(xml).Document!.Licence;
        Assert.Equal("This work is licensed under a Creative Commons Attribution 4.0.", licence);
    }

    [Fact]
    public void Ntvmr_style_verse_markup_maps_orig_corr_overlines_and_non_numeric_n()
    {
        var xml = Prefix + """
            <text xml:lang="grc"><body>
              <pb n="081v" type="folio"/><cb n="1"/><lb/><foo>block</foo>
              <div type="book" n="B09"><div type="chapter" n="B09K1">
                <ab n="B09K1V10" part="F">
                  <w>ε<unclear>ι</unclear></w> <w>ετι</w> <w><abbr type="nomSac"><hi rend="overline">χρυ</hi></abbr></w>
                  <lb/><w>ουκ</w>
                </ab>
                <ab n="B09K1V11"><app>
                  <rdg type="orig" hand="firsthand"><w>ιουδασμω</w></rdg>
                  <rdg type="corr" hand="corrector"><w>ιουδαισμω</w></rdg>
                </app></ab>
              </div></div>
            </body></text>
            """ + Suffix;

        var result = TeiXml.Import(xml);

        Assert.Equal("081v", Assert.Single(result.Document!.PageBreaks!).Number);
        Assert.Equal(3, result.Document.Lines.Count);
        Assert.Equal("B09K1V10", result.Document.Lines[0].Tags.Single(t => t.StartsWith("n="))[2..]);
        Assert.Contains("part=F", result.Document.Lines[0].Tags);

        var l1 = result.Revisions.Where(r => r.Layer == TextLayer.L1Diplomatic).ToList();
        var l2 = result.Revisions.Where(r => r.Layer == TextLayer.L2Editorial).ToList();
        Assert.Equal("ει ετι χρυ", l1[0].Text);
        Assert.Equal("ουκ", l1[1].Text);
        Assert.Equal("ιουδασμω", l1[2].Text);
        Assert.Equal("ιουδαισμω", Assert.Single(l2).Text);

        Assert.Contains(result.Annotations, a => a.Kind == AnnotationKind.Unclear);
        Assert.Contains(result.Annotations, a => a.Kind == AnnotationKind.Sign && a.Description == "overline");
        var correction = Assert.Single(result.Annotations,
            a => a.Kind == AnnotationKind.AlternativeReading && a.Description == "app/orig-corr");
        Assert.Equal("ιουδαισμω", correction.AlternativeText);
        Assert.Equal("corrector", correction.HandLabel);
        Assert.Contains(result.Unsupported, i => i.Element == "abbr-without-expan");
        Assert.Contains(result.Unsupported, i => i.Element == "cb" && i.Severity == PageXml.IssueSeverity.Note);
        Assert.Contains(result.Unsupported, i => i.Element == "foo" && i.Severity == PageXml.IssueSeverity.Loss);

        var lines = result.Document.Lines.Select(info => new TeiXml.TeiExportLine(
            info.LineId,
            l1.FirstOrDefault(r => r.ScopeId == info.LineId),
            l2.FirstOrDefault(r => r.ScopeId == info.LineId),
            result.Annotations,
            info)).ToList();
        var exported = TeiXml.ExportDocument(lines, "ntvmr",
            new TeiXml.TeiExportContext(Folio: "081v"));
        Assert.Contains("<pb n=\"081v\" type=\"folio\" />", exported.Xml);
        Assert.Contains("n=\"B09K1V10\"", exported.Xml);
        Assert.Contains("<hi rend=\"overline\">", exported.Xml);
        Assert.Contains("type=\"orig\"", exported.Xml);
        Assert.Contains("type=\"corr\"", exported.Xml);

        var reparsed = TeiXml.Import(exported.Xml);
        Assert.Contains("ιουδαισμω", reparsed.Revisions
            .Where(r => r.Layer == TextLayer.L2Editorial).Select(r => r.Text));
        Assert.Contains(reparsed.Annotations, a => a.Kind == AnnotationKind.Sign);
    }

    [Fact]
    public void Reimporting_the_same_record_produces_stable_ids_when_the_source_declares_them()
    {
        var xml = Document("""
            <div type="edition"><ab xml:id="source-line"><lb n="1"/>α<unclear>β</unclear></ab></div>
            """);
        var first = TeiXml.Import(xml);
        var second = TeiXml.Import(xml);

        Assert.Equal(first.Revisions.Select(r => r.Id), second.Revisions.Select(r => r.Id));
        Assert.Equal(first.Annotations.Select(a => a.Id), second.Annotations.Select(a => a.Id));
        Assert.All(first.Revisions, r => Assert.Equal("source-line", r.ScopeId.Suffix));
    }

    [Fact]
    public void Lines_are_associated_with_the_preceding_page_break()
    {
        var xml = Prefix + """
            <text><body>
              <div type="edition">
                <pb n="001r"/><ab><lb/>α</ab>
                <pb n="001v"/><ab><lb/>β</ab>
              </div>
            </body></text>
            """ + Suffix;

        var lines = TeiXml.Import(xml).Document!.Lines;

        Assert.Equal("001r", lines[0].PageNumber);
        Assert.Equal(1, lines[0].PageIndex);
        Assert.Equal("001v", lines[1].PageNumber);
        Assert.Equal(2, lines[1].PageIndex);
    }

    [Fact]
    public void Repeated_spans_use_surrounding_context_and_refuse_true_ambiguity()
    {
        var lineId = new EntityId(EntityKind.Line, "ln-context");
        var l1 = new TextRevision
        {
            Id = new EntityId(EntityKind.TranscriptionRevision, "rev-context-l1"),
            ScopeId = lineId,
            Layer = TextLayer.L1Diplomatic,
            Author = new AgentRef { Id = "author", IsModel = false },
            CreatedAt = DateTimeOffset.UtcNow,
            Text = "αβα",
        };
        var l2 = l1 with
        {
            Id = new EntityId(EntityKind.TranscriptionRevision, "rev-context-l2"),
            Layer = TextLayer.L2Editorial,
            Text = "αβαδ",
        };
        var unclear = new Annotation
        {
            Id = new EntityId(EntityKind.Annotation, "ann-context"),
            Kind = AnnotationKind.Unclear,
            AnchoredRevisionId = l1.Id,
            Range = new ScalarRange(2, 3),
        };

        var exported = TeiXml.ExportDocument(
            [new TeiXml.TeiExportLine(lineId, l1, l2, [unclear])], "context");
        var reparsed = TeiXml.Import(exported.Xml);
        var mapped = Assert.Single(reparsed.Annotations, a => a.Kind == AnnotationKind.Unclear);
        Assert.Equal(new ScalarRange(2, 3), mapped.Range);

        var ambiguous = l1 with { Text = "αα" };
        var ambiguousL2 = l2 with { Text = "αδα" };
        var ambiguousAnnotation = unclear with { Range = new ScalarRange(0, 1) };
        var ambiguousExport = TeiXml.ExportDocument(
            [new TeiXml.TeiExportLine(lineId, ambiguous, ambiguousL2, [ambiguousAnnotation])], "ambiguous");
        Assert.Contains(ambiguousExport.Report.Entries, e => e.Category == "annotation-mapping");
    }

    [Fact]
    public void Optional_xsd_validation_accepts_the_pinned_schema_and_reports_errors()
    {
        var schema = Path.Combine(Path.GetTempPath(), $"lt-tei-{Guid.NewGuid():N}.xsd");
        File.WriteAllText(schema, """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
                       targetNamespace="http://www.tei-c.org/ns/1.0"
                       xmlns:t="http://www.tei-c.org/ns/1.0" elementFormDefault="qualified">
              <xs:element name="TEI">
                <xs:complexType><xs:sequence>
                  <xs:element name="text" minOccurs="1" maxOccurs="1"/>
                </xs:sequence></xs:complexType>
              </xs:element>
            </xs:schema>
            """);
        try
        {
            var valid = """<TEI xmlns="http://www.tei-c.org/ns/1.0"><text/></TEI>""";
            var invalid = """<TEI xmlns="http://www.tei-c.org/ns/1.0"/>""";

            var (validIssues, hash) = TeiXml.ValidateAgainstSchema(valid, schema);
            Assert.Equal(64, hash.Length);
            Assert.DoesNotContain(validIssues, issue => issue.Severity == PageXml.IssueSeverity.Loss);

            var (invalidIssues, _) = TeiXml.ValidateAgainstSchema(invalid, schema);
            Assert.Contains(invalidIssues, issue => issue.Severity == PageXml.IssueSeverity.Loss);
        }
        finally
        {
            File.Delete(schema);
        }
    }

    [Fact]
    public void Profile_lint_reports_duplicate_ids_and_malformed_structures()
    {
        var xml = Prefix + """
            <text><body>
              <pb/><div type="edition">
                <ab xml:id="dup"><lb/>α<gap/></ab>
                <ab xml:id="dup"><lb/>β<app/><choice><seg>γ</seg></choice></ab>
              </div>
            </body></text>
            """ + Suffix;

        var result = TeiXml.Import(xml);
        Assert.Contains(result.Unsupported, i => i.Element == "duplicate-xml-id"
            && i.Severity == PageXml.IssueSeverity.Loss);
        Assert.Contains(result.Unsupported, i => i.Element == "page-break-without-number"
            && i.Severity == PageXml.IssueSeverity.Note);
        Assert.Contains(result.Unsupported, i => i.Element == "gap-without-extent");
        Assert.Contains(result.Unsupported, i => i.Element == "app-without-reading");
        Assert.Contains(result.Unsupported, i => i.Element == "choice-without-reading");
    }

    [Fact]
    public void Facsimile_graphics_tolerate_pixel_units_seen_in_real_epidoc()
    {
        var xml = Prefix
            + """<facsimile><surface xml:id="s"><graphic url="a.jpg" width="5520px" height="3720px"/></surface></facsimile>"""
            + "<text><body><div type=\"edition\"><ab><lb/>α</ab></div></body></text>"
            + Suffix;

        var graphic = Assert.Single(TeiXml.Import(xml).Document!.Graphics);
        Assert.Equal(5520, graphic.Width);
        Assert.Equal(3720, graphic.Height);
    }

    [Fact]
    public void Addition_deletion_and_hand_shift_roundtrip()
    {
        var xml = Document("""
            <div type="edition"><ab><lb/>α<add place="above">β</add><del>γ</del><handShift new="hand2"/>δ</ab></div>
            """);

        var result = TeiXml.Import(xml);
        var l1 = Revision(result, TextLayer.L1Diplomatic);
        var l2 = Revision(result, TextLayer.L2Editorial);
        Assert.Equal("αβδ", l1.Text);
        Assert.Equal("αβγδ", l2.Text);
        Assert.Contains(result.Annotations, a => a.Kind == AnnotationKind.Added && a.AnchoredRevisionId == l1.Id);
        Assert.Contains(result.Annotations, a => a.Kind == AnnotationKind.Erased && a.AnchoredRevisionId == l2.Id);
        Assert.Equal("hand2", Assert.Single(result.Annotations, a => a.Kind == AnnotationKind.DifferentHand).HandLabel);

        var exported = TeiXml.ExportDocument(
            [new TeiXml.TeiExportLine(l1.ScopeId, l1, l2, result.Annotations)], "corrections");
        Assert.Contains("<add", exported.Xml);
        Assert.Contains("<del", exported.Xml);
        Assert.Contains("handShift", exported.Xml);

        var reparsed = TeiXml.Import(exported.Xml);
        Assert.Equal("αβδ", Revision(reparsed, TextLayer.L1Diplomatic).Text);
        Assert.Equal("αβγδ", Revision(reparsed, TextLayer.L2Editorial).Text);
        foreach (var kind in new[] { AnnotationKind.Added, AnnotationKind.Erased, AnnotationKind.DifferentHand })
            Assert.Contains(reparsed.Annotations, a => a.Kind == kind);
    }

    [Fact]
    public void Original_and_regularized_readings_roundtrip_without_concatenation()
    {
        var xml = Document("""
            <div type="edition"><ab><lb/><choice><orig>ϲ</orig><reg>σ</reg></choice>οφος</ab></div>
            """);

        var result = TeiXml.Import(xml);
        Assert.Equal("ϲοφος", Revision(result, TextLayer.L1Diplomatic).Text);
        Assert.Equal("σοφος", Revision(result, TextLayer.L2Editorial).Text);
        var alternative = Assert.Single(result.Annotations, a => a.Kind == AnnotationKind.AlternativeReading);
        Assert.Equal("choice/orig-reg", alternative.Description);

        var exported = TeiXml.ExportDocument(
            [new TeiXml.TeiExportLine(result.Revisions[0].ScopeId,
                Revision(result, TextLayer.L1Diplomatic), Revision(result, TextLayer.L2Editorial), result.Annotations)],
            "orig-reg");
        Assert.Contains("<orig>ϲ</orig>", exported.Xml);
        Assert.Contains("<reg>σ</reg>", exported.Xml);

        var reparsed = TeiXml.Import(exported.Xml);
        Assert.Equal("ϲοφος", Revision(reparsed, TextLayer.L1Diplomatic).Text);
        Assert.Equal("σοφος", Revision(reparsed, TextLayer.L2Editorial).Text);
    }

    [Fact]
    public void Unsupported_markup_is_aggregated_and_the_original_is_returned_for_attachment()
    {
        var xml = Document("""
            <div type="edition"><ab><lb/>α<foo x="1">β</foo><foo>γ</foo><note>editorial</note></ab></div>
            """);

        var result = TeiXml.Import(xml);
        var foo = Assert.Single(result.Unsupported, i => i.Element == "foo");
        Assert.Contains("2 occurrences", foo.Description);
        Assert.Equal(PageXml.IssueSeverity.Loss, foo.Severity);
        var note = Assert.Single(result.Unsupported, i => i.Element == "note");
        Assert.Equal(PageXml.IssueSeverity.Note, note.Severity);
        Assert.Equal("αβγ", Revision(result, TextLayer.L1Diplomatic).Text);
        Assert.Equal(xml, result.OriginalMarkup);
    }

    [Fact]
    public void Export_reports_missing_image_dropped_geometry_and_unmappable_annotations()
    {
        var lineId = new EntityId(EntityKind.Line, "ln-loss");
        var l1 = new TextRevision
        {
            Id = new EntityId(EntityKind.TranscriptionRevision, "rev-loss"),
            ScopeId = lineId,
            Layer = TextLayer.L1Diplomatic,
            Author = new AgentRef { Id = "author", IsModel = false },
            CreatedAt = DateTimeOffset.UtcNow,
            Text = "αβ",
        };
        var glyph = new Annotation
        {
            Id = new EntityId(EntityKind.Annotation, "ann-glyph"),
            Kind = AnnotationKind.GlyphForm,
            AnchoredRevisionId = l1.Id,
            Range = new ScalarRange(0, 1),
            Description = "lunate sigma",
        };

        var exported = TeiXml.ExportDocument(
            [new TeiXml.TeiExportLine(lineId, l1, null, [glyph])], "lossy",
            new TeiXml.TeiExportContext(GeometryCount: 3));

        Assert.Contains(exported.Report.Entries, e => e.Category == "image-link");
        Assert.Contains(exported.Report.Entries, e => e.Category == "geometry" && e.Count == 3);
        Assert.Contains(exported.Report.Entries, e => e.Category == "annotation-kind");

        var multiPage = TeiXml.ExportDocument(
            [new TeiXml.TeiExportLine(lineId, l1, null, [])], "pages",
            new TeiXml.TeiExportContext(PageBreakCount: 3));
        Assert.Contains(multiPage.Report.Entries, e => e.Category == "page-breaks" && e.Count == 2);
        Assert.Contains("loss-report:", exported.Xml);
    }
    private static TeiXml.TeiExportResult RoundtripExport(TeiXml.TeiImportResult imported) =>
        TeiXml.ExportDocument(imported.Document!.Lines.Select(info => new TeiXml.TeiExportLine(
            info.LineId,
            imported.Revisions.SingleOrDefault(r => r.ScopeId == info.LineId && r.Layer == TextLayer.L1Diplomatic),
            imported.Revisions.SingleOrDefault(r => r.ScopeId == info.LineId && r.Layer == TextLayer.L2Editorial),
            imported.Annotations, info)).ToList(), "regression",
            new TeiXml.TeiExportContext(Graphics: [new TeiXml.TeiGraphic("synthetic.png")]));

    [Theory]
    [InlineData("α <unclear>β</unclear> γ", "α β γ", 2, 3)]
    [InlineData("α<unclear> β  γ </unclear>δ", "α β γ δ", 1, 6)]
    public void Inline_text_boundary_spaces_and_annotation_offsets_survive(string content, string expected, int start, int end)
    {
        var imported = TeiXml.Import(Document("<div type=\"edition\"><ab><lb/>" + content + "</ab></div>"));
        Assert.Equal(expected, Revision(imported, TextLayer.L1Diplomatic).Text);
        Assert.Equal(new ScalarRange(start, end), imported.Annotations.Single().Range);
        var exported = RoundtripExport(imported);
        var reparsed = TeiXml.Import(exported.Xml);
        Assert.Equal(expected, Revision(reparsed, TextLayer.L1Diplomatic).Text);
        Assert.Equal(imported.Annotations.Single().Range, reparsed.Annotations.Single().Range);
        Assert.True(exported.Report.IsLossless, exported.Report.ToString());
    }

    [Theory]
    [InlineData("<hi rend=\"overline\">α<unclear>β</unclear>γ</hi>", "αβγ")]
    [InlineData("<hi rend=\"overline\"><unclear>αβ</unclear></hi>", "αβ")]
    [InlineData("<unclear>α</unclear><unclear>β</unclear>", "αβ")]
    public void Nested_equal_and_adjacent_annotations_preserve_text_and_ranges(string content, string expected)
    {
        var imported = TeiXml.Import(Document("<div type=\"edition\"><ab><lb/>" + content + "</ab></div>"));
        var exported = RoundtripExport(imported);
        var reparsed = TeiXml.Import(exported.Xml);
        Assert.Equal(expected, Revision(reparsed, TextLayer.L1Diplomatic).Text);
        Assert.Equal(imported.Annotations.OrderBy(a => a.Kind).Select(a => (a.Kind, a.Range)),
            reparsed.Annotations.OrderBy(a => a.Kind).Select(a => (a.Kind, a.Range)));
        Assert.True(exported.Report.IsLossless, exported.Report.ToString());
    }

    [Theory]
    [InlineData("αβ<handShift new=\"B\"/>γδ", 2)]
    [InlineData("αβ<addSpan spanTo=\"end\"/>γδ", 2)]
    [InlineData("αβ<delSpan spanTo=\"end\"/>γδ", 2)]
    [InlineData("αβ<handShift new=\"B\"/>", 2)]
    [InlineData("<hi rend=\"overline\">α<handShift new=\"B\"/>β</hi>", 1)]
    [InlineData("α<del>β</del><handShift new=\"B\"/>γ", 2)]
    [InlineData("α<supplied>β</supplied><handShift new=\"B\"/>γ", 2)]
    public void Milestones_keep_their_scalar_position(string content, int position)
    {
        var imported = TeiXml.Import(Document("<div type=\"edition\"><ab><lb/>" + content + "</ab></div>"));
        var exported = RoundtripExport(imported);
        var reparsed = TeiXml.Import(exported.Xml);
        Assert.Equal(Revision(imported, TextLayer.L1Diplomatic).Text, Revision(reparsed, TextLayer.L1Diplomatic).Text);
        Assert.Equal(new ScalarRange(position, position), reparsed.Annotations.Single(a => a.Range.IsEmpty).Range);
        Assert.True(exported.Report.IsLossless, exported.Report.ToString());
    }

    [Fact]
    public void Ambiguous_cross_layer_spans_are_reported_instead_of_attached_to_the_first_match()
    {
        var imported = TeiXml.Import(Document("<div type=\"edition\"><ab><lb/>αβ<unclear>α</unclear><supplied>δ</supplied></ab></div>"));
        var exported = RoundtripExport(imported);
        var reparsed = TeiXml.Import(exported.Xml);
        Assert.Equal("αβα", Revision(reparsed, TextLayer.L1Diplomatic).Text);
        Assert.Equal("αβαδ", Revision(reparsed, TextLayer.L2Editorial).Text);
        var uncertainty = reparsed.Annotations.SingleOrDefault(a => a.Kind == AnnotationKind.Unclear);
        if (uncertainty is null)
            Assert.Contains(exported.Report.Entries, e => e.Category == "annotation-mapping");
        else
            Assert.Equal(new ScalarRange(2, 3), uncertainty.Range);
    }

    [Fact]
    public void Identical_layers_keep_repeated_span_coordinates()
    {
        var imported = TeiXml.Import(Document("<div type=\"edition\"><ab><lb/>αβ<unclear>α</unclear><choice><orig>δ</orig><reg>δ</reg></choice></ab></div>"));
        var exported = RoundtripExport(imported);
        var reparsed = TeiXml.Import(exported.Xml);
        Assert.Equal("αβαδ", Revision(reparsed, TextLayer.L1Diplomatic).Text);
        Assert.Equal(new ScalarRange(2, 3), reparsed.Annotations.Single(a => a.Kind == AnnotationKind.Unclear).Range);
        Assert.True(exported.Report.IsLossless, exported.Report.ToString());
    }

    [Fact]
    public void Repeated_editorial_readings_report_ambiguous_mapping()
    {
        var imported = TeiXml.Import(Document("<div type=\"edition\"><ab><lb/>σ<choice><orig>ϲ</orig><reg>σ</reg></choice></ab></div>"));
        var exported = RoundtripExport(imported);
        var reparsed = TeiXml.Import(exported.Xml);
        Assert.Equal("σσ", reparsed.Revisions.Last().Text);
        Assert.DoesNotContain(reparsed.Annotations, a => a.Kind == AnnotationKind.AlternativeReading);
        Assert.Contains(exported.Report.Entries, e => e.Category == "annotation-mapping");
    }

    [Theory]
    [InlineData("<choice><abbr>αβ</abbr><expan>α</expan></choice><supplied>β</supplied>")]
    [InlineData("<choice><orig>αβ</orig><reg>α</reg></choice><supplied>β</supplied>")]
    public void Equal_layer_text_does_not_override_explicit_editorial_span(string content)
    {
        var imported = TeiXml.Import(Document("<div type=\"edition\"><ab><lb/>" + content + "</ab></div>"));
        var exported = RoundtripExport(imported);
        var reparsed = TeiXml.Import(exported.Xml);
        Assert.Equal("αβ", Revision(reparsed, TextLayer.L1Diplomatic).Text);
        Assert.Equal("αβ", Revision(reparsed, TextLayer.L2Editorial).Text);
        Assert.Contains(reparsed.Annotations, a => a.Kind == AnnotationKind.SuppliedEditorial);
        Assert.True(exported.Report.IsLossless, exported.Report.ToString());
    }

    [Fact]
    public void Cross_layer_mapping_counts_supplementary_characters_as_one_scalar()
    {
        var imported = TeiXml.Import(Document("<div type=\"edition\"><ab><lb/><supplied>δ</supplied>𐅀<unclear>β</unclear>γ</ab></div>"));
        var exported = RoundtripExport(imported);
        var reparsed = TeiXml.Import(exported.Xml);
        Assert.Equal("𐅀βγ", Revision(reparsed, TextLayer.L1Diplomatic).Text);
        Assert.Equal("δ𐅀βγ", Revision(reparsed, TextLayer.L2Editorial).Text);
        Assert.Equal(new ScalarRange(1, 2), reparsed.Annotations.Single(a => a.Kind == AnnotationKind.Unclear).Range);
        Assert.True(exported.Report.IsLossless, exported.Report.ToString());
    }

    [Fact]
    public void Repeated_spans_with_distinct_context_keep_their_location()
    {
        var imported = TeiXml.Import(Document("<div type=\"edition\"><ab><lb/><supplied>δ</supplied>α<unclear>αα</unclear><supplied>δ</supplied></ab></div>"));
        var exported = RoundtripExport(imported);
        var reparsed = TeiXml.Import(exported.Xml);
        Assert.Equal("ααα", Revision(reparsed, TextLayer.L1Diplomatic).Text);
        Assert.Equal(new ScalarRange(1, 3),
            reparsed.Annotations.Single(a => a.Kind == AnnotationKind.Unclear).Range);
        Assert.DoesNotContain(exported.Report.Entries, e => e.Category == "annotation-mapping");
    }

}
