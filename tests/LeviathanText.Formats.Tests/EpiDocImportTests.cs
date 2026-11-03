using LeviathanText.Formats;

namespace LeviathanText.Formats.Tests;

public class EpiDocImportTests
{
    private const string Sample = """
        <TEI xmlns="http://www.tei-c.org/ns/1.0">
          <facsimile><graphic n="screen" url="ISic001151_tiled.tif"/></facsimile>
          <text><body>
            <div type="edition" subtype="primary">
              <ab>
                <lb n="1"/>Ὀπετρηία Λητώι <note>editorial note</note> χαῖρε
                <lb n="2" break="no"/>χρηστὴ καὶ εὐσεβής <gap reason="lost" quantity="2"/>
              </ab>
            </div>
          </body></text>
        </TEI>
        """;

    [Fact]
    public void Splits_edition_text_at_lb_and_reports_flattened_markup()
    {
        var result = EpiDocImport.Parse(Sample);

        Assert.Equal(2, result.Lines.Count);
        Assert.Equal("Ὀπετρηία Λητώι χαῖρε", result.Lines[0].Text);
        Assert.Equal("χρηστὴ καὶ εὐσεβής", result.Lines[1].Text);
        Assert.Equal(1, result.Lines[0].Number);
        Assert.Equal(2, result.Lines[1].Number);
        Assert.Equal("ISic001151_tiled.tif", result.ImageUrl);
        Assert.Contains(result.Notes, n => n.Element == "note");
        Assert.Contains(result.Notes, n => n.Element == "gap");
        Assert.Contains(result.Notes, n => n.Element == "edition"); // not a diplomatic reading
    }

    [Fact]
    public void Apparatus_and_choice_pairs_do_not_concatenate_their_readings()
    {
        const string xml = """
            <TEI xmlns="http://www.tei-c.org/ns/1.0"><text><body>
              <div type="edition" subtype="primary"><ab>
                <lb n="1"/>κ<choice><abbr>ϲ</abbr><expan>υρίου</expan></choice>
                <choice><orig>ϲ</orig><reg>σ</reg></choice>οφος
                <app><lem>ο</lem><rdg>ω</rdg><rdg>ου</rdg></app>
              </ab></div>
            </body></text></TEI>
            """;

        var result = EpiDocImport.Parse(xml);

        var line = Assert.Single(result.Lines);
        Assert.Equal("κϲ ϲοφος ο", line.Text);
        Assert.DoesNotContain("υρίου", line.Text);
        Assert.DoesNotContain("ου", line.Text);
        Assert.Contains(result.Notes, n => n.Element == "choice");
        Assert.Contains(result.Notes, n => n.Element == "app");
    }

    [Fact]
    public void Missing_edition_is_rejected()
    {
        Assert.Throws<InvalidDataException>(() => EpiDocImport.Parse(
            """<TEI xmlns="http://www.tei-c.org/ns/1.0"><text><body><p>x</p></body></text></TEI>"""));
    }

    [Fact]
    public void Doctype_is_rejected()
    {
        var xml = "<!DOCTYPE TEI [<!ENTITY x SYSTEM \"file:///etc/passwd\">]>" +
                  "<TEI xmlns=\"http://www.tei-c.org/ns/1.0\"><text><body><div type=\"edition\"><ab>α</ab></div></body></text></TEI>";
        Assert.Throws<InvalidDataException>(() => EpiDocImport.Parse(xml));
    }
}
