using System.Globalization;
using System.Text;
using System.Text.Json;
using LeviathanText.Evaluation;

namespace LeviathanText.Application;

/// <summary>
/// Line-level error analysis for evaluation outputs. This is deliberately independent of a
/// particular model: callers supply aligned reference/hypothesis pairs, and the report keeps
/// scalar/grapheme scores plus mark-specific and confusion evidence per line. Empty hypotheses
/// are scored as full-line deletions, never dropped.
/// </summary>
public static class LineEvaluationReport
{
    public sealed record Row(
        int Index,
        string Reference,
        string Hypothesis,
        int ReferenceScalars,
        int HypothesisScalars,
        int ScalarDistance,
        double ScalarCer,
        int BaseErrors,
        int MarkErrors,
        ErrorAnalysisReport Confusions);

    public sealed record Summary(
        int Lines,
        int ReferenceScalars,
        int ScalarDistance,
        double ScalarCer,
        int MarkErrors,
        IReadOnlyList<ConfusionPair> TopSubstitutions,
        IReadOnlyList<ConfusionPair> TopInsertions,
        IReadOnlyList<ConfusionPair> TopDeletions);

    public static (IReadOnlyList<Row> Rows, Summary Summary) Build(
        IEnumerable<(string Reference, string? Hypothesis)> pairs, int top = 10)
    {
        var rows = new List<Row>();
        var analyses = new List<ErrorAnalysisReport>();
        int index = 0, referenceScalars = 0, distance = 0, markErrors = 0;

        foreach (var (reference, hypothesis) in pairs)
        {
            var produced = hypothesis ?? string.Empty;
            var score = RecognitionScorer.Score(reference, produced);
            var analysis = ErrorAnalysis.Analyze(reference, produced, top);
            rows.Add(new Row(index, reference, produced,
                score.ReferenceScalarLength, score.HypothesisScalarLength, score.ScalarDistance,
                score.ScalarCer,
                score.BaseErrors.Total, score.MarkErrors.Total, analysis));
            analyses.Add(analysis);
            referenceScalars += score.ReferenceScalarLength;
            distance += score.ScalarDistance;
            markErrors += score.MarkErrors.Total;
            index++;
        }

        var aggregate = ErrorAnalysis.Aggregate(analyses, top);
        double cer = referenceScalars == 0 ? (distance == 0 ? 0 : 1) : (double)distance / referenceScalars;
        var summary = new Summary(rows.Count, referenceScalars, distance, cer, markErrors,
            aggregate.TopSubstitutions, aggregate.TopInsertions, aggregate.TopDeletions);
        return (rows, summary);
    }

    public static string ToCsv(IReadOnlyList<Row> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("index,reference,hypothesis,reference_scalars,hypothesis_scalars,scalar_distance,scalar_cer,base_errors,mark_errors,top_substitutions,top_insertions,top_deletions");
        foreach (var row in rows)
        {
            sb.Append(row.Index).Append(',')
                .Append(Escape(row.Reference)).Append(',')
                .Append(Escape(row.Hypothesis)).Append(',')
                .Append(row.ReferenceScalars).Append(',')
                .Append(row.HypothesisScalars).Append(',')
                .Append(row.ScalarDistance).Append(',')
                .Append(row.ScalarCer.ToString("0.######", CultureInfo.InvariantCulture)).Append(',')
                .Append(row.BaseErrors).Append(',')
                .Append(row.MarkErrors).Append(',')
                .Append(Escape(Pairs(row.Confusions.TopSubstitutions))).Append(',')
                .Append(Escape(Pairs(row.Confusions.TopInsertions))).Append(',')
                .Append(Escape(Pairs(row.Confusions.TopDeletions))).AppendLine();
        }
        return sb.ToString();
    }

    public static string ToJson(Summary summary, IReadOnlyDictionary<string, string> context)
    {
        var payload = new
        {
            generatedAt = DateTimeOffset.UtcNow,
            context,
            summary.Lines,
            summary.ReferenceScalars,
            summary.ScalarDistance,
            summary.ScalarCer,
            summary.MarkErrors,
            topSubstitutions = summary.TopSubstitutions,
            topInsertions = summary.TopInsertions,
            topDeletions = summary.TopDeletions,
            note = "Line-level error analysis; labels are not necessarily specialist-adjudicated. Locked acceptance sets must not be used for tuning.",
        };
        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
    }

    private static string Pairs(IEnumerable<ConfusionPair> pairs) =>
        string.Join(" | ", pairs.Select(p => $"{p.Expected}->{p.Produced}×{p.Count}"));

    private static string Escape(string value)
    {
        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0) return value;
        return '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
    }
}
