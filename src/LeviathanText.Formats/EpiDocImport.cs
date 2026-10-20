using System.Text;
using System.Xml.Linq;

namespace LeviathanText.Formats;

/// <summary>
/// Bootstrap importer for ISicily-style EpiDoc records (todo INS-01). It extracts the
/// *published edition* text split at &lt;lb/&gt; boundaries and the declared image URL. The
/// edition is normalized (expanded words, editorial accents), so it is NOT a diplomatic
/// reading: callers must import it as an L2 reference and leave L1 empty until a human
/// transcribes the visible text. Markup (notes, gaps, apparatus) is flattened and reported,
/// never silently presented as visible evidence.
/// </summary>
public static class EpiDocImport
{
    private static readonly XNamespace Tei = "http://www.tei-c.org/ns/1.0";

    public sealed record Line(int Number, string Text);

    public sealed record Result(
        IReadOnlyList<Line> Lines,
        string? ImageUrl,
        IReadOnlyList<PageXml.ImportIssue> Notes);

    public static Result Parse(string xml)
    {
        var doc = XDocument.Parse(xml, LoadOptions.None);
        if (doc.DocumentType is not null)
            throw new InvalidDataException("DOCTYPE/external entities are not accepted in EpiDoc imports.");

        var body = doc.Descendants(Tei + "body").FirstOrDefault()
            ?? throw new InvalidDataException("Missing TEI body.");
        var edition = body.Descendants(Tei + "div")
            .FirstOrDefault(d => (string?)d.Attribute("type") == "edition" && (string?)d.Attribute("subtype") == "primary")
            ?? body.Descendants(Tei + "div").FirstOrDefault(d => (string?)d.Attribute("type") == "edition")
            ?? throw new InvalidDataException("No edition div found.");

        var notes = new List<PageXml.ImportIssue>
        {
            new("edition", "EpiDoc edition text is normalized (expansions/accents/word division); " +
                           "imported as L2 reference, not as L1 diplomatic text.", PageXml.IssueSeverity.Note),
        };

        var rawLines = new List<(int Number, string Text)>();
        int lbCounter = 0;
        bool skippedNote = false, skippedGap = false;

        void Flush(StringBuilder current, int number)
        {
            var text = Normalize(current.ToString());
            if (text.Length > 0) rawLines.Add((number, text));
            current.Clear();
        }

        void Walk(XElement element, StringBuilder current, ref int number)
        {
            foreach (var node in element.Nodes())
            {
                switch (node)
                {
                    case XText text:
                        current.Append(text.Value);
                        break;
                    case XElement child:
                        var local = child.Name.LocalName;
                        if (local == "lb")
                        {
                            Flush(current, number);
                            lbCounter++;
                            number = int.TryParse((string?)child.Attribute("n"), out var n) ? n : lbCounter;
                        }
                        else if (local is "note" or "bibl" or "ref")
                        {
                            skippedNote = true;
                        }
                        else if (local == "gap")
                        {
                            skippedGap = true; // gaps are not visible text; geometry/uncertainty is annotated by a human
                        }
                        else
                        {
                            Walk(child, current, ref number);
                        }
                        break;
                }
            }
        }

        int currentNumber = 1;
        foreach (var ab in edition.Descendants(Tei + "ab"))
        {
            var buffer = new StringBuilder();
            Walk(ab, buffer, ref currentNumber);
            Flush(buffer, currentNumber);
            currentNumber++;
        }

        if (rawLines.Count == 0)
            throw new InvalidDataException("The edition contains no text lines.");

        if (skippedNote)
            notes.Add(new PageXml.ImportIssue("note", "Editorial notes were skipped (not visible text).", PageXml.IssueSeverity.Note));
        if (skippedGap)
            notes.Add(new PageXml.ImportIssue("gap", "Gap markers were skipped; annotate damage/geometry manually.", PageXml.IssueSeverity.Note));

        var graphic = doc.Descendants(Tei + "graphic")
            .FirstOrDefault(g => (string?)g.Attribute("n") == "screen")
            ?? doc.Descendants(Tei + "graphic").FirstOrDefault();
        var imageUrl = (string?)graphic?.Attribute("url");
        if (string.IsNullOrWhiteSpace(imageUrl))
            notes.Add(new PageXml.ImportIssue("graphic", "No image URL declared; supply an image path.", PageXml.IssueSeverity.Loss));

        return new Result(
            [.. rawLines.Select(l => new Line(l.Number, l.Text))],
            imageUrl,
            notes);
    }

    private static string Normalize(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
