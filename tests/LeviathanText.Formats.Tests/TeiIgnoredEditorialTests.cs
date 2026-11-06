using LeviathanText.Contracts;
using LeviathanText.Formats;
using Xunit;
namespace LeviathanText.Formats.Tests;
// Generated ASCII cases from the independent physical-line review.
public class TeiIgnoredEditorialTests
{
    private static string Doc(string source) => "<TEI xmlns='http://www.tei-c.org/ns/1.0'><text><body><div type='edition'>"+source+"</div></body></text></TEI>";
    private static string[] Texts(TeiXml.TeiImportResult result) => result.Revisions.Where(r=>r.Layer==TextLayer.L1Diplomatic).Select(r=>r.Text).ToArray();
    [Theory]
    [InlineData("note")][InlineData("bibl")][InlineData("ref")]
    public void Hidden_editorial_line_break_does_not_split_witness(string name)
    {
        var result=TeiXml.Import(Doc($"<ab><lb/>A<{name}>X<lb xml:id='hidden'/>Y</{name}>B</ab>"));
        Assert.Equal(new[]{"AB"},Texts(result));
        Assert.Null(Assert.Single(result.Document!.Lines).XmlId);
    }
    [Fact]
    public void Hidden_page_break_does_not_split_or_relabel_witness()
    {
        var result=TeiXml.Import(Doc("<pb n='1r'/><ab><lb/>A<note><pb n='note-page'/></note>B<lb/>C</ab>"));
        Assert.Equal(new[]{"AB","C"},Texts(result));
        Assert.All(result.Document!.Lines,line=>Assert.Equal("1r",line.PageNumber));
        Assert.Single(result.Document.PageBreaks!);
    }
    [Fact]
    public void Hidden_line_break_does_not_activate_physical_mode_in_a_logical_document()
    {
        var result=TeiXml.Import(Doc("<ab xml:id='first'>A<note><lb xml:id='hidden'/>N</note></ab><ab xml:id='second'>B</ab>"));
        Assert.Equal(new[]{"A","B"},Texts(result));
        Assert.Equal(new[]{"first","second"},result.Document!.Lines.Select(line=>line.XmlId));
    }
    [Fact]
    public void Hidden_alternative_milestone_does_not_refuse_visible_witness()
    {
        var result=TeiXml.Import(Doc("<ab><lb/>A<note><choice><orig>X<lb/>Y</orig><reg>Z</reg></choice></note>B</ab>"));
        Assert.Equal(new[]{"AB"},Texts(result));
    }
    [Fact]
    public void Hidden_leading_marker_does_not_supply_witness_identity()
    {
        var xml="<TEI xmlns='http://www.tei-c.org/ns/1.0'><text><body><pb n='1r'/><note><lb xml:id='hidden' n='99'/>N</note><div type='edition'><ab>A<lb xml:id='second' n='2'/>B</ab></div></body></text></TEI>";
        var result=TeiXml.Import(xml);
        Assert.Equal(new[]{"A","B"},Texts(result));
        Assert.Null(result.Document!.Lines[0].XmlId);
        Assert.Null(result.Document.Lines[0].Number);
    }
    [Fact]
    public void Ignored_subtree_context_is_not_candidate_witness_metadata()
    {
        var result=TeiXml.Import(Doc("<ab><lb/>A<note data-tei-source-context='!'>N</note>B</ab>"));
        Assert.Equal(new[]{"AB"},Texts(result));
    }
    [Fact]
    public void Ignored_logical_ab_is_not_a_witness_line()
    {
        var result=TeiXml.Import(Doc("<note><ab xml:id='note'>N</ab></note><ab xml:id='real'>A</ab>"));
        Assert.Equal(new[]{"A"},Texts(result));
        Assert.Equal("real",Assert.Single(result.Document!.Lines).XmlId);
    }
    [Fact]
    public void Real_nested_word_break_remains_a_physical_boundary()
    {
        Assert.Equal(new[]{"A","B"},Texts(TeiXml.Import(Doc("<ab><lb/><w>A<lb/>B</w></ab>"))));
    }
    [Fact]
    public void Real_page_break_remains_a_physical_boundary()
    {
        var result=TeiXml.Import(Doc("<pb n='1r'/><ab><lb/>A<pb n='1v'/><lb/>B</ab>"));
        Assert.Equal(new[]{"A","B"},Texts(result));
        Assert.Equal(new[]{"1r","1v"},result.Document!.Lines.Select(line=>line.PageNumber));
    }
    [Fact]
    public void Ordinary_skipped_note_does_not_add_visible_text()
    {
        Assert.Equal(new[]{"AB"},Texts(TeiXml.Import(Doc("<ab><lb/>A<note>N</note>B</ab>"))));
    }

    [Theory]
    [InlineData("<w><note>N</note></w><ab><lb/>A</ab>")]
    [InlineData("<ab><w><note>N</note></w></ab><ab>A</ab>")]
    public void Editorial_only_wrappers_do_not_create_an_empty_witness_row(string source)
    {
        Assert.Equal(new[] { "A" }, Texts(TeiXml.Import(Doc(source))));
    }

    [Fact]
    public void Ignored_only_block_does_not_contribute_fictitious_source_context()
    {
        var result = TeiXml.Import(Doc("<lb/><ab n='editorial'><w><note>N</note></w></ab><ab n='actual'>A</ab>"));
        var row = Assert.Single(result.Document!.Lines);
        var encoded = row.Tags.Single(t => t.StartsWith("tei-source-context=")).Split('=', 2)[1];
        var context = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        Assert.Contains("actual", context);
        Assert.DoesNotContain("editorial", context);
        Assert.Contains(result.Unsupported, issue => issue.Element == "note" && issue.Severity == PageXml.IssueSeverity.Note);
    }

    [Theory]
    [InlineData("note")]
    [InlineData("bibl")]
    [InlineData("ref")]
    public void Hidden_primary_edition_does_not_override_the_visible_edition(string editorial)
    {
        var xml = "<TEI xmlns='http://www.tei-c.org/ns/1.0'><text><body><" + editorial
            + "><div type='edition' subtype='primary'><ab>N</ab></div></" + editorial
            + "><div type='edition'><ab><lb/>A</ab></div></body></text></TEI>";
        Assert.Equal(new[] { "A" }, Texts(TeiXml.Import(xml)));
    }

    [Theory]
    [InlineData("edition")]
    [InlineData("book")]
    public void Hidden_nonprimary_division_does_not_override_visible_book_fallback(string hiddenType)
    {
        var xml = "<TEI xmlns='http://www.tei-c.org/ns/1.0'><text><body><note><div type='" + hiddenType
            + "'><ab>N</ab></div></note><div type='book'><ab>A</ab></div></body></text></TEI>";
        Assert.Equal(new[] { "A" }, Texts(TeiXml.Import(xml)));
    }

    [Fact]
    public void Visible_primary_edition_still_takes_precedence_over_visible_fallbacks()
    {
        var xml = "<TEI xmlns='http://www.tei-c.org/ns/1.0'><text><body><div type='book'><ab>BOOK</ab></div><div type='edition'><ab>OTHER</ab></div><div type='edition' subtype='primary'><ab>A</ab></div></body></text></TEI>";
        Assert.Equal(new[] { "A" }, Texts(TeiXml.Import(xml)));
    }
}
