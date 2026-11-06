using LeviathanText.Contracts;
using Xunit;

namespace LeviathanText.Formats.Tests;

public sealed class TeiLanguageMetadataTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Visible_textless_atoms_keep_their_original_language_scope(bool physical)
    {
        var marker = physical ? "<lb/>" : "";
        var xml = "<TEI xmlns='http://www.tei-c.org/ns/1.0'><text><body><div type='edition'><ab xml:lang='grc'>" + marker + "<gap xml:lang='la' quantity='1' unit='character'/></ab></div></body></text></TEI>";
        Assert.Equal(new[] { "la" }, Assert.Single(TeiXml.Import(xml).Document!.Lines).Languages);
    }

    [Theory]
    [InlineData("<w xml:lang='la'><lb/>α</w>", "la")]
    [InlineData("<w xml:lang=''><lb/>α</w>", "")]
    [InlineData("<lb/><w><hi xml:lang='la'>α</hi></w>", "la")]
    public void Original_visible_inline_text_scope_overrides_its_ab(string content, string expected)
    {
        var xml = "<TEI xmlns='http://www.tei-c.org/ns/1.0'><text><body><div type='edition'><ab xml:lang='grc'>" + content + "</ab></div></body></text></TEI>";
        Assert.Equal(expected.Length == 0 ? Array.Empty<string>() : new[] { expected }, Assert.Single(TeiXml.Import(xml).Document!.Lines).Languages);
    }

    [Theory]
    [InlineData("<w xml:lang='la'>α</w>", "la")]
    [InlineData("<w xml:lang=''>α</w>", "")]
    [InlineData("<w><hi xml:lang='la'>α</hi></w>", "la")]
    public void Logical_rows_also_retain_original_visible_inline_scope(string content, string expected)
    {
        var xml = "<TEI xmlns='http://www.tei-c.org/ns/1.0'><text><body><div type='edition'><ab xml:lang='grc'>" + content + "</ab></div></body></text></TEI>";
        Assert.Equal(expected.Length == 0 ? Array.Empty<string>() : new[] { expected }, Assert.Single(TeiXml.Import(xml).Document!.Lines).Languages);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Language_union_retains_choice_and_supplied_scopes_but_ignores_editorial_descendants(bool physical)
    {
        var xml = "<TEI xmlns='http://www.tei-c.org/ns/1.0'><text><body><div type='edition'><ab xml:lang='grc'><lb/><w><choice><orig>α</orig><reg xml:lang='la'>a</reg></choice><supplied xml:lang='cop'>β</supplied><note xml:lang='de'>hidden</note><bibl xml:lang='fr'>hidden</bibl><ref xml:lang='en'>hidden</ref></w></ab></div></body></text></TEI>";
        if (!physical) xml = xml.Replace("<lb/>", "", StringComparison.Ordinal);
        Assert.Equal(new[] { "grc", "la", "cop" }, Assert.Single(TeiXml.Import(xml).Document!.Lines).Languages);
    }

    [Theory]
    [InlineData("<lb/>α", "grc")]
    [InlineData("<w xml:lang='la'><lb/>α</w>", "la")]
    [InlineData("<w xml:lang=''><lb/>α</w>", "")]
    [InlineData("<w xml:lang='la'><lb n='1'/></w>", "la")]
    public void Physical_rows_without_ab_retain_actual_scope_or_empty_milestone_scope(string content, string expected)
    {
        var xml = "<TEI xmlns='http://www.tei-c.org/ns/1.0'><text xml:lang='grc'><body><div type='edition'>" + content + "</div></body></text></TEI>";
        var line = Assert.Single(TeiXml.Import(xml).Document!.Lines);
        Assert.Equal(expected.Length == 0 ? Array.Empty<string>() : new[] { expected }, line.Languages);
    }

    [Theory]
    [InlineData("text")]
    [InlineData("body")]
    [InlineData("div")]
    public void Physical_line_inherits_nearest_explicit_ancestor_language(string element)
    {
        string Attr(string name) => element == name ? " xml:lang='grc'" : "";
        var xml = $"<TEI xmlns='http://www.tei-c.org/ns/1.0'><text{Attr("text")}><body{Attr("body")}><div type='edition'{Attr("div")}><ab><lb/>α</ab></div></body></text></TEI>";
        var imported = TeiXml.Import(xml);
        Assert.Equal(new[] { "grc" }, Assert.Single(imported.Document!.Lines).Languages);
    }

    [Fact]
    public void Closest_override_and_explicit_empty_reset_are_distinct_from_header_declarations()
    {
        var xml = "<TEI xmlns='http://www.tei-c.org/ns/1.0'><teiHeader><profileDesc><langUsage><language ident='grc'/><language ident='la'/></langUsage></profileDesc></teiHeader><text xml:lang='grc'><body xml:lang='la'><div type='edition'><ab><lb/>α</ab><ab xml:lang=''><lb/>β</ab><ab xml:lang='cop'><lb/>γ</ab></div></body></text></TEI>";
        var lines = TeiXml.Import(xml).Document!.Lines;
        Assert.Equal(new[] { "la" }, lines[0].Languages);
        Assert.Empty(lines[1].Languages);
        Assert.Equal(new[] { "cop" }, lines[2].Languages);
    }

    [Fact]
    public void Header_languages_are_document_metadata_and_do_not_guess_line_languages()
    {
        var xml = "<TEI xmlns='http://www.tei-c.org/ns/1.0'><teiHeader><profileDesc><langUsage><language ident='grc'/><language ident='la'/></langUsage></profileDesc></teiHeader><text><body><div type='edition'><ab><lb/>α</ab></div></body></text></TEI>";
        var document = TeiXml.Import(xml).Document!;
        Assert.Equal(new[] { "grc", "la" }, document.Languages);
        Assert.Empty(Assert.Single(document.Lines).Languages);
    }

    [Fact]
    public void Only_direct_header_language_declarations_count_and_ignored_editorial_scopes_do_not_leak()
    {
        var xml = "<TEI xmlns='http://www.tei-c.org/ns/1.0'><teiHeader><fileDesc><sourceDesc><profileDesc><langUsage><language ident='cop'/></langUsage></profileDesc></sourceDesc></fileDesc><profileDesc><langUsage><language ident='grc'/><language ident='grc'/></langUsage></profileDesc></teiHeader><text xml:lang='grc'><body><note xml:lang='la'><teiHeader><profileDesc><langUsage><language ident='la'/></langUsage></profileDesc></teiHeader></note><div type='edition'><ab><lb/>α<note xml:lang='cop'>editorial</note>β</ab></div></body></text></TEI>";
        var imported = TeiXml.Import(xml);
        Assert.Equal(new[] { "grc" }, imported.Document!.DeclaredLanguages);
        Assert.Equal(new[] { "grc" }, imported.Document.Languages);
        Assert.Equal(new[] { "grc" }, Assert.Single(imported.Document.Lines).Languages);
        Assert.Equal("αβ", Assert.Single(imported.Revisions).Text);
    }

    [Fact]
    public void Empty_reset_at_a_container_stops_inheritance_for_descendant_rows()
    {
        var xml = "<TEI xmlns='http://www.tei-c.org/ns/1.0' xml:lang='grc'><text><body><div type='edition' xml:lang=''><ab><lb/>α</ab></div></body></text></TEI>";
        Assert.Empty(Assert.Single(TeiXml.Import(xml).Document!.Lines).Languages);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("empty")]
    [InlineData("long")]
    [InlineData("count")]
    public void Invalid_or_oversized_declared_header_metadata_is_refused(string kind)
    {
        var declarations = kind switch
        {
            "missing" => "<language/>",
            "empty" => "<language ident=''/>",
            "long" => "<language ident='" + new string('x', 257) + "'/>",
            _ => string.Concat(Enumerable.Range(0, 129).Select(i => $"<language ident='x-{i}'/>")),
        };
        var xml = "<TEI xmlns='http://www.tei-c.org/ns/1.0'><teiHeader><profileDesc><langUsage>" + declarations + "</langUsage></profileDesc></teiHeader><text><body><div type='edition'><ab><lb/>α</ab></div></body></text></TEI>";
        Assert.Throws<InvalidDataException>(() => TeiXml.Import(xml));
    }

    [Fact]
    public void Header_language_export_preserves_exact_codes_order_and_bounded_schema_validity()
    {
        var result = TeiXml.ExportDocument([], "declared", new TeiXml.TeiExportContext(DeclaredLanguages: ["grc", "x-Private", "grc"]));
        var document = TeiXml.Import(result.Xml).Document!;
        Assert.Equal(new[] { "grc", "x-Private" }, document.DeclaredLanguages);
        Assert.Equal(new[] { "grc", "x-Private" }, document.Languages);
        Assert.Empty(document.Lines);
        var schema = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../docs/contracts/tei-bounded-profile.xsd"));
        var (issues, _) = TeiXml.ValidateAgainstSchema(result.Xml, schema);
        Assert.DoesNotContain(issues, issue => issue.Severity == PageXml.IssueSeverity.Loss);
    }

    [Fact]
    public void Invalid_XML_characters_in_declared_export_metadata_are_typed_profile_refusals()
    {
        Assert.Throws<InvalidDataException>(() => TeiXml.ExportDocument([], "invalid", new TeiXml.TeiExportContext(DeclaredLanguages: ["grc\0"])));
    }
}
