using System.Text;
using LeviathanText.Formats;

namespace LeviathanText.Application;

/// <summary>
/// Annotation CSV for external review: one row per annotation with its scalar range and
/// structured fields. CSV is lossy (no geometry, no provenance chain); the native project
/// remains authoritative.
/// </summary>
public static class AnnotationCsv
{
    public static string Export(NativeProject project)
    {
        var builder = new StringBuilder();
        builder.AppendLine("annotation_id,surface_id,line_id,kind,start,end,gap_extent,alternative_text,hand_label,description,anchored_revision,retracted,retracted_by,retraction_reason,expansion_text,expansion_revision,expansion_start,expansion_end,sequence_id,sequence_index,order_basis");

        foreach (var surface in project.Objects.SelectMany(o => o.Surfaces))
        {
            var lineByRevision = surface.Revisions.ToDictionary(r => r.Id, r => r.ScopeId);
            var retractions = surface.AnnotationRetractions
                .GroupBy(r => r.AnnotationId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.At).First());
            foreach (var annotation in surface.Annotations.OrderBy(a => a.Id.Suffix, StringComparer.Ordinal))
            {
                var retraction = retractions.GetValueOrDefault(annotation.Id);
                var lineId = lineByRevision.GetValueOrDefault(annotation.AnchoredRevisionId);
                Append(builder, new[]
                {
                    annotation.Id.ToString(),
                    surface.Id.ToString(),
                    lineId.ToString(),
                    annotation.Kind.ToString(),
                    annotation.Range.Start.ToString(),
                    annotation.Range.End.ToString(),
                    annotation.GapExtent?.ToString() ?? "",
                    annotation.AlternativeText ?? "",
                    annotation.HandLabel ?? "",
                    annotation.Description ?? "",
                    annotation.AnchoredRevisionId.ToString(),
                    retraction is null ? "false" : "true",
                    retraction?.Reviewer ?? "",
                    retraction?.Reason ?? "",
                    annotation.ExpansionText ?? "",
                    annotation.ExpansionRevisionId?.ToString() ?? "",
                    annotation.ExpansionRange?.Start.ToString() ?? "",
                    annotation.ExpansionRange?.End.ToString() ?? "",
                    annotation.SequenceId ?? "",
                    annotation.SequenceIndex?.ToString() ?? "",
                    annotation.OrderBasis?.ToString() ?? "",
                });
            }
        }

        return builder.ToString();
    }

    private static void Append(StringBuilder builder, IReadOnlyList<string> fields)
    {
        for (int i = 0; i < fields.Count; i++)
        {
            if (i > 0) builder.Append(',');
            builder.Append(Escape(fields[i]));
        }
        builder.AppendLine();
    }

    private static string Escape(string value) =>
        value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r')
            ? '"' + value.Replace("\"", "\"\"") + '"'
            : value;
}
