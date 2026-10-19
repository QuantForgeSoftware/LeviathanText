using System.Text;
using LeviathanText.Contracts;

namespace LeviathanText.Application;

/// <summary>
/// Review queue for active learning: orders recognized lines by review need — empty output
/// first, then lowest mean raw-model confidence (absent confidence sorts first among the
/// rest because no confidence semantics means "needs a human"). This is a review aid, not a
/// calibrated risk estimate; the model card governs confidence semantics.
/// </summary>
public static class ReviewQueue
{
    public sealed record Entry(
        EntityId LineId,
        double? MeanConfidence,
        int GraphemeCount,
        bool EmptyOutput,
        string Text);

    public static IReadOnlyList<Entry> Build(IReadOnlyList<RecognitionPipeline.LineRecognition> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        return [.. lines
            .Select(line =>
            {
                var values = line.Confidence
                    .Where(c => !c.Confidence.IsNone)
                    .Select(c => c.Confidence.Value)
                    .ToList();
                return new Entry(
                    line.LineId,
                    values.Count == 0 ? null : values.Average(),
                    line.Confidence.Count,
                    string.IsNullOrWhiteSpace(line.L0Text),
                    line.L0Text);
            })
            .OrderBy(e => e.EmptyOutput ? 0 : 1)
            .ThenBy(e => e.MeanConfidence ?? double.NegativeInfinity)
            .ThenBy(e => e.Text.Length)];
    }

    public static string ToCsv(IReadOnlyList<Entry> entries)
    {
        var builder = new StringBuilder();
        builder.AppendLine("line_id,mean_confidence,graphemes,empty_output,text");
        foreach (var entry in entries)
        {
            var confidence = entry.MeanConfidence is { } value ? value.ToString("0.######") : "";
            builder.Append(entry.LineId).Append(',')
                .Append(confidence).Append(',')
                .Append(entry.GraphemeCount).Append(',')
                .Append(entry.EmptyOutput ? "true" : "false").Append(',')
                .Append(Escape(entry.Text))
                .AppendLine();
        }
        return builder.ToString();
    }

    private static string Escape(string value) =>
        value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r')
            ? '"' + value.Replace("\"", "\"\"") + '"'
            : value;
}
