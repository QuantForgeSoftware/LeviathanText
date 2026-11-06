using LeviathanText.Contracts;

namespace LeviathanText.Formats.Tests;

/// <summary>Invented ASCII fixtures; physical source structure, not Greek adjudication.</summary>
public class TeiPhysicalLineTests
{
    private static string Document(string edition) =>
        "<TEI xmlns=\"http://www.tei-c.org/ns/1.0\"><text><body><div type=\"edition\">"
        + edition + "</div></body></text></TEI>";

    [Fact]
    public void Nested_word_milestone_starts_physical_row_even_when_token_continues()
    {
        var result = TeiXml.Import(Document("""
            <ab><lb xml:id="row-one" n="1"/><w>A<lb xml:id="row-two" n="2" break="no"/>B</w></ab>
            """));
        Assert.Equal(new[] { "A", "B" }, result.Revisions
            .Where(r => r.Layer == TextLayer.L1Diplomatic).Select(r => r.Text));
        Assert.Equal(new int?[] { 1, 2 }, result.Document!.Lines.Select(l => l.Number));
        Assert.Equal(new[] { "row-one", "row-two" }, result.Document.Lines.Select(l => l.XmlId));
    }

    [Fact]
    public void Verse_container_boundary_does_not_invent_physical_row()
    {
        var result = TeiXml.Import(Document("""
            <lb xml:id="row-one" n="1"/><ab n="V1">A</ab><ab n="V2">B<lb xml:id="row-two" n="2"/>C</ab>
            """));
        Assert.Equal(new[] { "AB", "C" }, result.Revisions
            .Where(r => r.Layer == TextLayer.L1Diplomatic).Select(r => r.Text));
        Assert.Equal(new int?[] { 1, 2 }, result.Document!.Lines.Select(l => l.Number));
        Assert.Equal(new[] { "row-one", "row-two" }, result.Document.Lines.Select(l => l.XmlId));
    }

    [Theory]
    [InlineData("no")]
    [InlineData("yes")]
    [InlineData("maybe")]
    public void Direct_milestone_break_attribute_is_token_metadata(string continuity)
    {
        var result = TeiXml.Import(Document($"<ab><lb n='1'/>A<lb n='2' break='{continuity}'/>B</ab>"));
        Assert.Equal(new[] { "A", "B" }, result.Revisions.Select(r => r.Text));
        Assert.Contains("tei-lb-break=" + continuity, result.Document!.Lines[1].Tags);
    }

    [Theory]
    [InlineData("<choice><orig>A<lb/>B</orig><reg>C</reg></choice>")]
    [InlineData("<app><rdg type='orig'>A<lb/>B</rdg><rdg type='corr'>C</rdg></app>")]
    [InlineData("<choice><orig>A<pb n='2'/>B</orig><reg>C</reg></choice>")]
    public void Cross_line_alternative_refuses_before_returning_partial_import(string alternative)
    {
        var error = Assert.Throws<InvalidDataException>(() => TeiXml.Import(Document("<ab><lb/>" + alternative + "</ab>")));
        Assert.Contains("alternative", error.Message);
    }

    [Fact]
    public void Inline_annotation_fragments_and_page_context_follow_physical_rows()
    {
        var result = TeiXml.Import(Document("""
            <pb n="1r"/><ab><lb xml:id="a" n="1"/><unclear>A<lb xml:id="b" n="2"/>B</unclear><pb n="1v"/><lb xml:id="c" n="3"/><supplied>C</supplied></ab>
            """));
        Assert.Equal(new[] { "A", "B", "" }, result.Revisions.Where(r => r.Layer == TextLayer.L1Diplomatic).Select(r => r.Text));
        Assert.Equal("C", Assert.Single(result.Revisions, r => r.Layer == TextLayer.L2Editorial).Text);
        Assert.Equal(new[] { "1r", "1r", "1v" }, result.Document!.Lines.Select(l => l.PageNumber));
        Assert.Equal(new[] { 1, 1, 2 }, result.Document.Lines.Select(l => l.PageIndex));
        Assert.Equal(2, result.Annotations.Count(a => a.Kind == AnnotationKind.Unclear));
        Assert.All(result.Annotations, a => Assert.Equal(new ScalarRange(0, 1), a.Range));
    }

    [Fact]
    public void Declared_empty_rows_are_retained_and_container_ends_do_not_add_rows()
    {
        var result = TeiXml.Import(Document("<ab><lb xml:id='empty' n='1'/><lb n='2'/>A<lb/></ab>"));
        Assert.Equal(new[] { "", "A" }, result.Revisions.Select(r => r.Text));
        Assert.Equal(new int?[] { 1, 2 }, result.Document!.Lines.Select(l => l.Number));
    }

    [Fact]
    public void Cross_verse_context_is_whitespace_safe_and_roundtrips_without_reinterpretation()
    {
        var result = TeiXml.Import(Document("<lb xml:id='a' n='1'/><ab n='Verse one'>A</ab><ab n='Verse two'>B</ab>"));
        var info = Assert.Single(result.Document!.Lines);
        var context = Assert.Single(info.Tags, t => t.StartsWith("tei-source-context="));
        var encoded = context.Split('=', 2)[1];
        Assert.DoesNotContain(' ', encoded);
        var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        Assert.Contains("Verse one", decoded);
        Assert.Contains("Verse two", decoded);
        var exported = TeiXml.ExportDocument([new(info.LineId, Assert.Single(result.Revisions), null, [], info)], "contexts");
        var imported = TeiXml.Import(exported.Xml);
        Assert.Contains(context, Assert.Single(imported.Document!.Lines).Tags);
        Assert.Equal("AB", Assert.Single(imported.Revisions).Text);
        Assert.Equal(info.LineId, Assert.Single(imported.Document.Lines).LineId);
    }

    [Fact]
    public void No_milestones_retains_logical_block_fallback()
    {
        var result = TeiXml.Import(Document("<ab xml:id='a'>A</ab><ab xml:id='b'>B</ab>"));
        Assert.Equal(new[] { "A", "B" }, result.Revisions.Select(r => r.Text));
        Assert.Equal(new[] { "a", "b" }, result.Document!.Lines.Select(l => l.XmlId));
    }

    [Fact]
    public void Leading_body_milestone_identity_is_retained_for_selected_book_prefix()
    {
        var xml = "<TEI xmlns='http://www.tei-c.org/ns/1.0'><text><body><pb n='1r'/><lb xml:id='first' n='1'/><div type='book'><ab>A<lb xml:id='second' n='2'/>B</ab></div></body></text></TEI>";
        var result = TeiXml.Import(xml);
        Assert.Equal(new[] { "A", "B" }, result.Revisions.Select(r => r.Text));
        Assert.Equal(new[] { "first", "second" }, result.Document!.Lines.Select(l => l.XmlId));
        Assert.Equal(new int?[] { 1, 2 }, result.Document.Lines.Select(l => l.Number));
        Assert.All(result.Document.Lines, l => Assert.Equal("1r", l.PageNumber));
    }

    [Fact]
    public void Later_nested_milestone_does_not_reuse_verse_container_identity()
    {
        var result = TeiXml.Import(Document("<ab xml:id='verse'><lb/>A<w><lb/>B</w></ab>"));
        Assert.Equal("verse", result.Document!.Lines[0].XmlId);
        Assert.Null(result.Document.Lines[1].XmlId);
        Assert.Equal(2, result.Document.Lines.Select(l => l.LineId).Distinct().Count());
    }

    [Fact]
    public void Oversized_source_context_refuses_instead_of_truncating()
    {
        var source = "<ab n='" + new string('x', 50000) + "'><lb/>A</ab>";
        var error = Assert.Throws<InvalidDataException>(() => TeiXml.Import(Document(source)));
        Assert.Contains("64-KiB", error.Message);
    }

    [Theory]
    [InlineData("oversized")]
    [InlineData("base64")]
    [InlineData("json")]
    [InlineData("nonobject")]
    [InlineData("whitespace")]
    public void Supplied_source_context_is_bounded_validated_untrusted_metadata(string kind)
    {
        var value = kind switch
        {
            "oversized" => new string('x', 65537),
            "base64" => "!",
            "json" => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("not JSON")),
            "nonobject" => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("[]")),
            _ => "e3 0=",
        };
        Assert.Throws<InvalidDataException>(() => TeiXml.Import(Document("<ab data-tei-source-context='" + value + "'><lb/>A</ab>")));
    }

    [Fact]
    public void Different_supplied_contexts_on_one_row_refuse_instead_of_first_value_export()
    {
        var first = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"source\":1}"));
        var second = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"source\":2}"));
        Assert.Throws<InvalidDataException>(() => TeiXml.Import(Document($"<lb/><ab data-tei-source-context='{first}'>A</ab><ab data-tei-source-context='{second}'>B</ab>")));
    }

    [Fact]
    public void Physical_row_retains_all_explicit_contributing_languages_without_script_inference()
    {
        var result = TeiXml.Import(Document("<lb/><ab xml:lang='grc'>A</ab><ab xml:lang='la'>B</ab>"));
        var row = Assert.Single(result.Document!.Lines);
        Assert.Equal(new[] { "grc", "la" }, row.Languages);
        Assert.Null(row.Direction);
        Assert.Null(row.Script);
        var exported = TeiXml.ExportDocument([new(row.LineId, Assert.Single(result.Revisions), null, [], row)], "languages");
        Assert.Contains(exported.Report.Entries, entry => entry.Category == "line-languages");
        Assert.Contains("la", System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(
            row.Tags.Single(t => t.StartsWith("tei-source-context=")).Split('=', 2)[1])));
    }

    [Fact]
    public void Unmarked_physical_prefix_is_retained_with_explicit_note()
    {
        var result = TeiXml.Import(Document("<ab>A<lb n='2'/>B</ab>"));
        Assert.Equal(new[] { "A", "B" }, result.Revisions.Select(r => r.Text));
        Assert.Contains(result.Unsupported, issue => issue.Element == "physical-line-without-milestone"
            && issue.Severity == PageXml.IssueSeverity.Note);
    }

    [Fact]
    public void Logical_fallback_also_rejects_invalid_supplied_source_context()
    {
        Assert.Throws<InvalidDataException>(() => TeiXml.Import(Document("<ab data-tei-source-context='!'>A</ab>")));
    }

    [Fact]
    public void Valid_supplied_context_is_retained_but_never_governs_source_identity()
    {
        var forged = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"MilestoneXmlId\":\"forged\"}"));
        var result = TeiXml.Import(Document($"<ab xml:id='actual' data-tei-source-context='{forged}'><lb/>A</ab>"));
        var row = Assert.Single(result.Document!.Lines);
        Assert.Equal("actual", row.XmlId);
        Assert.Equal("actual", row.LineId.Suffix);
        Assert.Contains("tei-source-context=" + forged, row.Tags);
    }
}
