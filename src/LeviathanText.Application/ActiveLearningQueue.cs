using System.Text;
using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application;

/// <summary>
/// Transparent active-learning priority for review. It combines missing/empty L0 output,
/// low model confidence, unreviewed lines and reviewer disagreement. This is a ranking
/// heuristic, not a calibrated risk model; reasons are returned with every score.
/// </summary>
public static class ActiveLearningQueue
{
    public sealed record Entry(
        EntityId LineId,
        EntityId SurfaceId,
        double Priority,
        double? MeanConfidence,
        bool MissingL0,
        bool EmptyOutput,
        bool Reviewed,
        bool Disputed,
        string Reasons);

    public static IReadOnlyList<Entry> Build(NativeProject project, int limit = 100)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (limit < 1) throw new ArgumentOutOfRangeException(nameof(limit));

        var entries = new List<Entry>();
        foreach (var surface in project.Objects.SelectMany(o => o.Surfaces))
        {
            foreach (var line in surface.Lines)
            {
                var l0 = surface.Revisions
                    .Where(r => r.ScopeId == line.Id && r.Layer == TextLayer.L0RawRecognition)
                    .OrderByDescending(r => r.CreatedAt)
                    .FirstOrDefault();
                var reviews = surface.ReviewDecisions.Where(d => d.LineId == line.Id).ToList();
                var confidences = l0?.GraphemeConfidences
                    .Where(c => !c.Confidence.IsNone)
                    .Select(c => c.Confidence.Value)
                    .ToList() ?? [];
                if (l0 is not null && confidences.Count == 0 && !l0.Confidence.IsNone)
                    confidences.Add(l0.Confidence.Value);
                double? mean = confidences.Count == 0 ? null : confidences.Average();
                bool missing = l0 is null;
                bool empty = l0 is not null && string.IsNullOrWhiteSpace(l0.Text);
                bool reviewed = reviews.Count > 0;
                bool disputed = reviews.Any(r => r.Outcome == ReviewOutcome.Dispute)
                    || reviews.Select(r => r.Outcome).Distinct().Count() > 1;

                double priority = 0;
                var reasons = new List<string>();
                if (missing) { priority += 0.35; reasons.Add("no L0"); }
                if (empty) { priority += 0.25; reasons.Add("empty L0"); }
                if (mean is { } value) { priority += (1 - value) * 0.35; if (value < 0.6) reasons.Add("low confidence"); }
                else if (!missing) { priority += 0.15; reasons.Add("no confidence"); }
                if (!reviewed) { priority += 0.10; reasons.Add("unreviewed"); }
                if (disputed) { priority += 0.20; reasons.Add("review disagreement"); }

                entries.Add(new Entry(line.Id, surface.Id, Math.Round(Math.Clamp(priority, 0, 1), 6),
                    mean is null ? null : Math.Round(mean.Value, 6), missing, empty, reviewed, disputed,
                    reasons.Count == 0 ? "trusted/low priority" : string.Join("; ", reasons)));
            }
        }

        return [.. entries
            .OrderByDescending(e => e.Priority)
            .ThenBy(e => e.LineId.Suffix, StringComparer.Ordinal)
            .Take(limit)];
    }

    public static string ToCsv(IReadOnlyList<Entry> entries)
    {
        var builder = new StringBuilder();
        builder.AppendLine("line_id,surface_id,priority,mean_confidence,missing_l0,empty_output,reviewed,disputed,reasons");
        foreach (var entry in entries)
            builder.Append(Escape(entry.LineId.ToString())).Append(',')
                .Append(Escape(entry.SurfaceId.ToString())).Append(',')
                .Append(entry.Priority.ToString("0.######")).Append(',')
                .Append(entry.MeanConfidence?.ToString("0.######") ?? "").Append(',')
                .Append(entry.MissingL0 ? "true" : "false").Append(',')
                .Append(entry.EmptyOutput ? "true" : "false").Append(',')
                .Append(entry.Reviewed ? "true" : "false").Append(',')
                .Append(entry.Disputed ? "true" : "false").Append(',')
                .Append(Escape(entry.Reasons)).AppendLine();
        return builder.ToString();
    }

    private static string Escape(string value) =>
        value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r')
            ? '"' + value.Replace("\"", "\"\"") + '"'
            : value;
}
