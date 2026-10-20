using LeviathanText.Contracts;
using LeviathanText.Contracts.Serialization;
using LeviathanText.Formats;

namespace LeviathanText.Application;

/// <summary>
/// Training-data export (todo Phase 2: "Export corrected training data using visible-text
/// labels and explicit annotation status; do not train visual recognition on supplied
/// restorations").
///
/// Policy, per line:
/// - The label is the **current L1 diplomatic** text, never L2 editorial text.
/// - Lines containing any editorial supply (L2-linked or annotated as supplied) are
///   **excluded** outright: a restoration must never become a visual label.
/// - Uncertain spans (unclear/illegible/lost/alternative readings) stay in the text —
///   they are visible evidence — but are listed so scorers can stratify exact vs.
///   uncertainty spans; exact spans are the complement.
/// - Not-yet-transcribed and empty lines are excluded with a reason.
/// </summary>
public static class TrainingDataExport
{
    public sealed record LineExport(
        EntityId LineId,
        EntityId? RevisionId,
        string Text,
        string Status, // clean | uncertain | excluded
        string? ExclusionReason,
        IReadOnlyList<ScalarRange> UncertainSpans,
        IReadOnlyList<ScalarRange> ExactSpans,
        IReadOnlyList<string> AnnotationKinds);

    public sealed record ExportDocument(
        string Format,
        string ContractVersion,
        string Policy,
        int CleanLines,
        int UncertainLines,
        int ExcludedLines,
        IReadOnlyList<LineExport> Lines);

    public static ExportDocument Export(NativeProject project)
    {
        var lines = new List<LineExport>();
        int clean = 0, uncertain = 0, excluded = 0;

        foreach (var surface in project.Objects.SelectMany(o => o.Surfaces))
        {
            var revisionById = surface.Revisions.ToDictionary(r => r.Id);

            foreach (var line in surface.Lines)
            {
                var revision = line.DiplomaticRevisionId is { } id && revisionById.TryGetValue(id, out var r) ? r : null;
                var retracted = surface.AnnotationRetractions.Select(r => r.AnnotationId).ToHashSet();
                var annotations = surface.Annotations
                    .Where(a => a.AnchoredRevisionId == (revision?.Id ?? default) && !retracted.Contains(a.Id))
                    .ToList();

                if (revision is null || string.IsNullOrWhiteSpace(revision.Text))
                {
                    lines.Add(new LineExport(line.Id, null, "", "excluded",
                        "not-yet-transcribed", [], [], []));
                    excluded++;
                    continue;
                }

                if (annotations.Any(a => a.Kind == AnnotationKind.SuppliedEditorial))
                {
                    lines.Add(new LineExport(line.Id, revision.Id, revision.Text, "excluded",
                        "editorial-supply-present", [], [], annotationKinds(annotations)));
                    excluded++;
                    continue;
                }

                var uncertainSpans = MergeRanges(annotations
                    .Where(a => a.Kind is AnnotationKind.Unclear or AnnotationKind.Illegible
                        or AnnotationKind.Lost or AnnotationKind.AlternativeReading)
                    .Select(a => a.Range));
                var exactSpans = Complement(uncertainSpans, ScalarLength(revision.Text));

                var status = uncertainSpans.Count == 0 ? "clean" : "uncertain";
                if (status == "clean") clean++; else uncertain++;
                lines.Add(new LineExport(line.Id, revision.Id, revision.Text, status, null,
                    uncertainSpans, exactSpans, annotationKinds(annotations)));
            }
        }

        return new ExportDocument(
            "leviathantext-training-lines",
            ContractVersion.Current,
            "L1-visible-text-only;no-restorations;uncertainty-spans-reported",
            clean, uncertain, excluded, lines);
    }

    public static string ToJson(ExportDocument document) => ContractJson.ToJson(document);

    private static List<string> annotationKinds(List<Annotation> annotations) =>
        [.. annotations.Select(a => a.Kind.ToString()).Distinct().OrderBy(k => k)];

    private static int ScalarLength(string text) => Core.Unicode.ScalarText.ScalarLength(text);

    public static IReadOnlyList<ScalarRange> MergeRanges(IEnumerable<ScalarRange> ranges)
    {
        var sorted = ranges.Where(r => !r.IsEmpty).OrderBy(r => r.Start).ThenBy(r => r.End).ToList();
        var merged = new List<ScalarRange>();
        foreach (var range in sorted)
        {
            if (merged.Count > 0 && range.Start <= merged[^1].End)
                merged[^1] = new ScalarRange(merged[^1].Start, Math.Max(merged[^1].End, range.End));
            else
                merged.Add(range);
        }
        return merged;
    }

    public static IReadOnlyList<ScalarRange> Complement(IReadOnlyList<ScalarRange> spans, int length)
    {
        var gaps = new List<ScalarRange>();
        int cursor = 0;
        foreach (var span in spans)
        {
            if (span.Start > cursor) gaps.Add(new ScalarRange(cursor, span.Start));
            cursor = Math.Max(cursor, span.End);
        }
        if (cursor < length) gaps.Add(new ScalarRange(cursor, length));
        return gaps;
    }
}
