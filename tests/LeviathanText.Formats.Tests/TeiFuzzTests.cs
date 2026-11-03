using LeviathanText.Contracts;

namespace LeviathanText.Formats.Tests;

/// <summary>
/// Deterministic structural fuzz over the supported inline profile. The invariant is modest
/// but important: a supported document must import, export and reimport without changing the
/// visible (L1) text. It does not claim feature-level equivalence for combinations the bounded
/// profile reports as nested/ambiguous.
/// </summary>
public class TeiFuzzTests
{
    private const string Prefix = """<TEI xmlns="http://www.tei-c.org/ns/1.0"><text><body><div type="edition"><ab><lb/>""";
    private const string Suffix = """</ab></div></body></text></TEI>""";

    [Fact]
    public void Supported_inline_combinations_keep_visible_text_through_export()
    {
        var random = new Random(20261103);
        for (int iteration = 0; iteration < 150; iteration++)
        {
            var content = string.Concat(Enumerable.Range(0, random.Next(3, 9))
                .Select(_ => Token(random)));
            var xml = Prefix + content + Suffix;
            var imported = TeiXml.Import(xml);
            var l1 = Assert.Single(imported.Revisions, r => r.Layer == TextLayer.L1Diplomatic);
            var l2 = imported.Revisions.SingleOrDefault(r => r.Layer == TextLayer.L2Editorial);
            var line = Assert.Single(imported.Document!.Lines);

            var exported = TeiXml.ExportDocument(
                [new TeiXml.TeiExportLine(line.LineId, l1, l2, imported.Annotations, line)],
                "fuzz", new TeiXml.TeiExportContext(Graphics: [new TeiXml.TeiGraphic("synthetic.png")]));
            var reparsed = TeiXml.Import(exported.Xml);
            var reparsedL1 = Assert.Single(reparsed.Revisions, r => r.Layer == TextLayer.L1Diplomatic);

            Assert.True(l1.Text == reparsedL1.Text,
                $"iteration {iteration}: L1 changed\ninput: {content}\nL1: {l1.Text}\nreparsed: {reparsedL1.Text}\nxml: {exported.Xml}");
        }
    }

    private static string Token(Random random) => random.Next(0, 8) switch
    {
        0 => Greek(random),
        1 => $"<unclear>{Greek(random)}</unclear>",
        2 => $"<supplied>{Greek(random)}</supplied>",
        3 => $"<choice><abbr>{Greek(random)}</abbr><expan>{Greek(random)}</expan></choice>",
        4 => $"<app><lem>{Greek(random)}</lem><rdg>{Greek(random)}</rdg></app>",
        5 => $"<add>{Greek(random)}</add>",
        6 => $"<del>{Greek(random)}</del>",
        _ => $"<hi rend=\"overline\">{Greek(random)}</hi>",
    };

    private static string Greek(Random random)
    {
        const string alphabet = "αβγδεζηθικλμνξοπρστυφχψω";
        return new string(Enumerable.Range(0, random.Next(1, 5))
            .Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray());
    }
}
