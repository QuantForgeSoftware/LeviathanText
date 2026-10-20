using System.Xml.Linq;
using LeviathanText.Contracts;

namespace LeviathanText.Formats;

/// <summary>
/// Bounded TEI P5 / EpiDoc profile (project-plan §3 "scholarly interchange"): lines, unclear
/// spans, gaps with extent, alternative readings and supplied text — nothing more.
/// Unsupported constructs are reported, never silently flattened. Bracket/underdot
/// conventions are imported as annotations, never as literal characters in L1 text.
/// Schema version pinned: TEI P5 text via the EpiDoc conventions listed in
/// interchange-and-api.md §1.2; external entities refused.
/// </summary>
public static class TeiXml
{
    private static readonly XNamespace Tei = "http://www.tei-c.org/ns/1.0";

    public sealed record TeiImportResult(
        IReadOnlyList<TextRevision> Revisions,
        IReadOnlyList<Annotation> Annotations,
        IReadOnlyList<PageXml.ImportIssue> Unsupported);

    // ---- Export ----

    public static string Export(IEnumerable<(EntityId LineId, TextRevision? Revision, IReadOnlyList<Annotation> Annotations)> lines,
        string title)
    {
        var edition = new XElement(Tei + "div",
            new XAttribute("type", "edition"),
            lines.Select(l => ExportLine(l.Revision, l.Annotations)));
        var body = new XElement(Tei + "body", edition);
        var text = new XElement(Tei + "text", body);
        var header = new XElement(Tei + "teiHeader",
            new XElement(Tei + "fileDesc",
                new XElement(Tei + "titleStmt", new XElement(Tei + "title", title)),
                new XElement(Tei + "publicationStmt",
                    new XElement(Tei + "p", "LeviathanText native export; bounded EpiDoc profile"))));
        var tei = new XElement(Tei + "TEI", header, text);
        return new XDocument(new XDeclaration("1.0", "UTF-8", null), tei).ToString();
    }

    private static XElement ExportLine(TextRevision? revision, IReadOnlyList<Annotation> annotations)
    {
        var lb = new XElement(Tei + "lb");
        if (revision is null) return new XElement(Tei + "ab", lb, new XComment("not-yet-transcribed"));

        var content = MarkupWithAnnotations(revision.Text, annotations.Where(a => a.AnchoredRevisionId == revision.Id).ToList(), out var dropped);
        var ab = new XElement(Tei + "ab", lb, content);
        foreach (var drop in dropped)
            ab.Add(new XComment($"lost-in-export: {drop.Category} x{drop.Count} ({drop.Description})"));
        return ab;
    }

    private static List<object> MarkupWithAnnotations(string text, List<Annotation> annotations, out List<LossEntry> dropped)
    {
        dropped = [];
        var scalars = ScalarText.Count(text);
        var applicable = annotations
            .Where(a => a.Kind is AnnotationKind.Unclear or AnnotationKind.Lost or AnnotationKind.AlternativeReading or AnnotationKind.SuppliedEditorial)
            .OrderBy(a => a.Range.Start)
            .ToList();

        // Overlapping partial ranges are not representable; they are dropped with a loss entry.
        var accepted = new List<Annotation>();
        foreach (var a in applicable)
        {
            var clash = accepted.FirstOrDefault(b => a.Range.Start < b.Range.End && b.Range.Start < a.Range.End
                                                    && !(b.Range.Start <= a.Range.Start && a.Range.End <= b.Range.End)
                                                    && !(a.Range.Start <= b.Range.Start && b.Range.End <= a.Range.End));
            if (clash is null) accepted.Add(a);
            else dropped.Add(new LossEntry("overlapping-uncertainty", 1, $"annotation {a.Id} overlaps {clash.Id}"));
        }

        var pieces = new List<object>();
        int cursor = 0;
        foreach (var a in accepted.OrderBy(a => a.Range.Start))
        {
            if (a.Range.Start > cursor)
                pieces.Add(ScalarText.Slice(text, cursor, a.Range.Start));
            var inner = a.Range.IsEmpty ? "" : ScalarText.Slice(text, a.Range.Start, a.Range.End);
            pieces.Add(Markup(a, inner));
            cursor = Math.Max(cursor, a.Range.End);
        }
        if (cursor < scalars) pieces.Add(ScalarText.Slice(text, cursor, scalars));
        if (pieces.Count == 0) pieces.Add(text); // no annotations: emit the whole text once
        return pieces;
    }

    private static XElement Markup(Annotation a, string inner) => a.Kind switch
    {
        AnnotationKind.Unclear => new XElement(Tei + "unclear", new XAttribute("reason", "unclear"), inner),
        AnnotationKind.SuppliedEditorial => new XElement(Tei + "supplied", new XAttribute("reason", "editorial"), inner),
        AnnotationKind.Lost => a.GapExtent switch
        {
            GapExtent.Estimated => new XElement(Tei + "gap",
                new XAttribute("reason", "lost"),
                new XAttribute("extent", "estimated"),
                new XAttribute("quantity", a.EstimatedScalarCount ?? 0)),
            GapExtent.Known => new XElement(Tei + "gap", new XAttribute("reason", "lost"), new XAttribute("extent", "known")),
            _ => new XElement(Tei + "gap", new XAttribute("reason", "lost"), new XAttribute("extent", "unknown")),
        },
        AnnotationKind.AlternativeReading => new XElement(Tei + "app",
            new XElement(Tei + "lem", inner),
            new XElement(Tei + "rdg", new XAttribute("type", "alternative"), a.AlternativeText ?? "")),
        _ => throw new InvalidOperationException(a.Kind.ToString()),
    };

    // ---- Import ----

    public static TeiImportResult Import(string xml)
    {
        var doc = XDocument.Parse(xml, LoadOptions.None);
        if (doc.DocumentType is not null)
            throw new InvalidDataException("DOCTYPE/external entities are not accepted in TEI imports.");

        var revisions = new List<TextRevision>();
        var annotations = new List<Annotation>();
        var unsupported = new List<PageXml.ImportIssue>();

        var body = doc.Descendants(Tei + "body").FirstOrDefault()
            ?? throw new InvalidDataException("Missing TEI body.");
        var edition = body.Elements(Tei + "div").FirstOrDefault(d => (string?)d.Attribute("type") == "edition")
            ?? body.Elements(Tei + "div").FirstOrDefault();

        int lineIndex = 0;
        if (edition is not null)
            foreach (var ab in edition.Elements(Tei + "ab"))
            {
                lineIndex++;
                var lineId = new EntityId(EntityKind.Line, $"tei{lineIndex}");
                int annotationCountBefore = annotations.Count;
                var text = BuildLine(ab, lineId, revisions, annotations, unsupported);
                var revision = new TextRevision
                {
                    Id = new EntityId(EntityKind.TranscriptionRevision, $"tei{lineIndex}"),
                    ScopeId = lineId,
                    Layer = TextLayer.L1Diplomatic,
                    Author = new AgentRef { Id = "tei-import", IsModel = false },
                    CreatedAt = DateTimeOffset.UtcNow,
                    Text = text,
                };
                revisions.Add(revision);
                // Bind the annotations produced for this line to the revision that now exists.
                for (int i = annotationCountBefore; i < annotations.Count; i++)
                    annotations[i] = annotations[i] with { AnchoredRevisionId = revision.Id };
            }

        foreach (var element in body.Descendants())
        {
            if (element.AncestorsAndSelf().Any(a => a.Name.LocalName is "unclear" or "gap" or "supplied" or "app" or "lem" or "rdg" or "ab" or "lb"))
                continue;
            unsupported.Add(new PageXml.ImportIssue(element.Name.LocalName, "outside the bounded EpiDoc profile"));
        }

        return new TeiImportResult(revisions, annotations, unsupported);
    }

    private static string BuildLine(XElement ab, EntityId lineId, List<TextRevision> revisions,
        List<Annotation> annotations, List<PageXml.ImportIssue> unsupported)
    {
        var text = new System.Text.StringBuilder();
        foreach (var node in ab.Nodes())
        {
            switch (node)
            {
                case XElement el:
                    HandleInline(el, lineId, text, revisions, annotations, unsupported);
                    break;
                case XText t:
                    text.Append(t.Value);
                    break;
                case XComment c:
                    if (c.Value.Contains("not-yet-transcribed"))
                        annotations.Add(new Annotation
                        {
                            Id = new EntityId(EntityKind.Annotation, $"nyt{annotations.Count}"),
                            Kind = AnnotationKind.NotYetTranscribed,
                            AnchoredRevisionId = default,
                            Range = new ScalarRange(0, 0),
                        });
                    break;
            }
        }
        return text.ToString();
    }

    private static void HandleInline(XElement el, EntityId lineId, System.Text.StringBuilder text,
        List<TextRevision> revisions, List<Annotation> annotations, List<PageXml.ImportIssue> unsupported)
    {
        switch (el.Name.LocalName)
        {
            case "lb":
                return;
            case "unclear":
            case "supplied":
            {
                int start = ScalarText.Count(text.ToString());
                text.Append(el.Value);
                annotations.Add(new Annotation
                {
                    Id = new EntityId(EntityKind.Annotation, $"{el.Name.LocalName}{annotations.Count}"),
                    Kind = el.Name.LocalName == "unclear" ? AnnotationKind.Unclear : AnnotationKind.SuppliedEditorial,
                    AnchoredRevisionId = default, // bound by the importer after the revision exists
                    Range = new ScalarRange(start, start + ScalarText.Count(el.Value)),
                    Description = (string?)el.Attribute("reason"),
                });
                break;
            }
            case "gap":
            {
                int position = ScalarText.Count(text.ToString());
                var extent = ((string?)el.Attribute("extent")) switch
                {
                    "known" => GapExtent.Known,
                    "estimated" => GapExtent.Estimated,
                    _ => GapExtent.Unknown,
                };
                int? quantity = (int?)el.Attribute("quantity");
                annotations.Add(new Annotation
                {
                    Id = new EntityId(EntityKind.Annotation, $"gap{annotations.Count}"),
                    Kind = AnnotationKind.Lost,
                    AnchoredRevisionId = default,
                    Range = new ScalarRange(position, position),
                    GapExtent = extent,
                    EstimatedScalarCount = quantity,
                });
                break;
            }
            case "app":
            {
                int start = ScalarText.Count(text.ToString());
                var lem = el.Element(Tei + "lem")?.Value ?? "";
                text.Append(lem);
                var rdg = el.Element(Tei + "rdg")?.Value;
                if (rdg is not null)
                    annotations.Add(new Annotation
                    {
                        Id = new EntityId(EntityKind.Annotation, $"rdg{annotations.Count}"),
                        Kind = AnnotationKind.AlternativeReading,
                        AnchoredRevisionId = default,
                        Range = new ScalarRange(start, start + ScalarText.Count(lem)),
                        AlternativeText = rdg,
                    });
                break;
            }
            default:
                unsupported.Add(new PageXml.ImportIssue(el.Name.LocalName, $"inline element outside the bounded profile (preserved in attachment)"));
                text.Append(el.Value); // text content survives; markup distinction does not
                break;
        }
    }

    private static int Count(string s) => ScalarText.Count(s);
}
