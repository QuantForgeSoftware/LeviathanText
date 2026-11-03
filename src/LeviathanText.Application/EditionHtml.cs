using System.Net;
using System.Text;
using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application;

/// <summary>
/// Bounded presentation export for a single surface: diplomatic text, the linked editorial
/// reading and a line-by-line critical apparatus rendered as HTML. It is a reading/printing
/// aid, not a scholarly interchange format; native/TEI records remain authoritative.
/// </summary>
public static class EditionHtml
{
    public sealed record Result(string Html, LossReport Report);

    public static Result Export(NativeProject project, EntityId surfaceId, string title, string? imageSource = null)
    {
        var surface = project.Objects.SelectMany(o => o.Surfaces).FirstOrDefault(s => s.Id == surfaceId)
            ?? throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = surfaceId.ToString(),
                Message = "Surface not found in the project.",
            });
        var capture = surface.Captures.FirstOrDefault();
        var revisions = surface.Revisions.GroupBy(r => r.Id).ToDictionary(g => g.Key, g => g.First());
        var annotations = surface.Annotations
            .Where(a => !surface.AnnotationRetractions.Any(r => r.AnnotationId == a.Id))
            .ToList();

        var html = new StringBuilder();
        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"grc\">");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine($"<title>{Encode(title)}</title>");
        html.AppendLine("<style>");
        html.AppendLine("body{font-family:Georgia,serif;max-width:62rem;margin:2rem auto;line-height:1.55;color:#222}");
        html.AppendLine(".surface{display:grid;grid-template-columns:minmax(0,1fr) minmax(18rem,24rem);gap:2rem;align-items:start}");
        html.AppendLine("figure{margin:0}img{max-width:100%;height:auto;border:1px solid #bbb}");
        html.AppendLine("ol.lines{padding-left:0;list-style:none}.line{margin:0 0 1.2rem 0;padding-bottom:.8rem;border-bottom:1px solid #ddd}");
        html.AppendLine(".ln{color:#667;font-size:.8rem;margin-right:.5rem}.greek{font-size:1.25rem}.editorial{display:block;color:#345;margin-top:.25rem}");
        html.AppendLine(".app{font-size:.82rem;color:#444;margin:.35rem 0 0 1.2rem}.app li{margin:.1rem 0}.kind{font-weight:bold}");
        html.AppendLine("@media print{body{margin:0}.surface{grid-template-columns:1fr}figure{page-break-after:always}}");
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine($"<h1>{Encode(title)}</h1>");
        html.AppendLine("<div class=\"surface\"><figure>");
        if (capture is not null)
            html.AppendLine($"<img src=\"{Encode(imageSource ?? capture.ImageLocation)}\" alt=\"source capture\">");
        else
            html.AppendLine("<p class=\"editorial\">No capture attached.</p>");
        html.AppendLine("</figure><main><ol class=\"lines\">");

        int unplaced = 0;
        int apparatusEntries = 0;
        var regionOrder = surface.Regions
            .GroupBy(r => r.Id)
            .ToDictionary(g => g.Key, g => g.First().ReadingOrderIndex ?? int.MaxValue);
        foreach (var line in surface.Lines
                     .OrderBy(l => regionOrder.GetValueOrDefault(l.RegionId, int.MaxValue))
                     .ThenBy(l => l.LineNumber ?? int.MaxValue)
                     .ThenBy(l => l.Id.ToString(), StringComparer.Ordinal))
        {
            var lineRevisions = revisions.Values.Where(r => r.ScopeId == line.Id).ToList();
            var l1 = line.DiplomaticRevisionId is { } pointer && revisions.TryGetValue(pointer, out var pointed)
                ? pointed
                : lineRevisions.Where(r => r.Layer == TextLayer.L1Diplomatic)
                    .OrderByDescending(r => r.CreatedAt).FirstOrDefault();
            var l2 = lineRevisions.Where(r => r.Layer == TextLayer.L2Editorial)
                .OrderByDescending(r => r.CreatedAt).FirstOrDefault();
            if (l1 is null)
            {
                unplaced++;
                continue;
            }
            var lineAnnotations = annotations
                .Where(a => a.AnchoredRevisionId == l1.Id || (l2 is not null && a.AnchoredRevisionId == l2.Id))
                .ToList();
            apparatusEntries += lineAnnotations.Count;
            html.AppendLine("<li class=\"line\">");
            html.AppendLine($"<span class=\"ln\">{(line.LineNumber is { } n ? n.ToString() : "·")}</span>");
            var direction = line.Direction == TextDirection.Rtl ? " dir=\"rtl\"" : "";
            html.AppendLine($"<span class=\"greek\"{direction}>{Encode(l1.Text)}</span>");
            if (l2 is not null)
                html.AppendLine($"<span class=\"editorial\"{direction}>{Encode(l2.Text)}</span>");
            if (lineAnnotations.Count > 0)
            {
                html.AppendLine("<ul class=\"app\">");
                foreach (var annotation in lineAnnotations
                             .OrderBy(a => a.Range.Start).ThenBy(a => a.Kind))
                    html.AppendLine($"<li><span class=\"kind\">{Encode(annotation.Kind.ToString())}</span> " +
                                    $"[{annotation.Range.Start}, {annotation.Range.End}) — {Encode(Apparatus(annotation))}</li>");
                html.AppendLine("</ul>");
            }
            html.AppendLine("</li>");
        }
        html.AppendLine("</ol></main></div>");
        html.AppendLine("</body></html>");

        var entries = new List<LossEntry>
        {
            new("presentation-only", 1,
                "HTML edition is a reading/printing aid; use TEI/native JSON for interchange"),
            new("unplaced-lines", unplaced,
                "lines without a diplomatic revision are omitted"),
            new("apparatus-entries", apparatusEntries,
                "apparatus text is escaped plain text; structured links remain in the native/TEI records"),
        };
        var report = new LossReport
        {
            Format = "edition-html",
            ContractVersion = ContractVersion.Current,
            Entries = [.. entries.Where(e => e.Count > 0)],
        };
        return new Result(html.ToString(), report);
    }

    /// <summary>Render every surface as one printable edition document.</summary>
    public static Result ExportProject(NativeProject project, string title, Func<NativeSurface, string?>? imageSource = null)
    {
        var sections = new List<Result>();
        foreach (var surface in project.Objects.SelectMany(o => o.Surfaces))
            sections.Add(Export(project, surface.Id, $"{title} — {surface.Id}", imageSource?.Invoke(surface)));
        if (sections.Count == 0)
        {
            var empty = new LossReport
            {
                Format = "edition-html",
                ContractVersion = ContractVersion.Current,
                Entries = [new LossEntry("presentation-only", 1,
                    "HTML edition is a reading/printing aid; no surfaces were present")],
            };
            return new Result($"<!doctype html><html><head><meta charset=\"utf-8\"><title>{Encode(title)}</title></head>" +
                              $"<body><h1>{Encode(title)}</h1><p>No surfaces in this project.</p></body></html>", empty);
        }
        var prefix = sections[0].Html[..(sections[0].Html.IndexOf("<body>", StringComparison.Ordinal) + 6)];
        var suffix = sections[0].Html[sections[0].Html.LastIndexOf("</body>", StringComparison.Ordinal)..];
        var body = new StringBuilder();
        foreach (var section in sections)
        {
            var start = section.Html.IndexOf("<body>", StringComparison.Ordinal) + 6;
            var end = section.Html.LastIndexOf("</body>", StringComparison.Ordinal);
            body.AppendLine("<section class=\"surface-section\">");
            body.AppendLine(section.Html[start..end]);
            body.AppendLine("</section>");
        }
        var entries = sections.SelectMany(s => s.Report.Entries)
            .GroupBy(e => (e.Category, e.Description))
            .Select(g => new LossEntry(g.Key.Category, g.Sum(e => e.Count), g.Key.Description))
            .ToList();
        return new Result(prefix + body.ToString() + suffix, new LossReport
        {
            Format = "edition-html",
            ContractVersion = ContractVersion.Current,
            Entries = entries,
        });
    }

    private static string Apparatus(Annotation annotation) => annotation.Kind switch
    {
        AnnotationKind.AlternativeReading => annotation.AlternativeText ?? annotation.Description ?? "",
        AnnotationKind.Abbreviation => annotation.ExpansionText ?? annotation.Description ?? "",
        AnnotationKind.DifferentHand => annotation.HandLabel ?? annotation.Description ?? "",
        _ => annotation.Description ?? annotation.HandLabel ?? "",
    };

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
