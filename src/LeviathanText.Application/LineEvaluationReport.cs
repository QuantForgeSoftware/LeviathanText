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

    /// <summary>Caller-supplied thresholds; the tool never invents acceptance values.</summary>
    public sealed record AcceptanceCriteria(double? MaxCer = null, double? MaxMarkErrorRate = null, int? MinLines = null);

    public sealed record AcceptanceCheck(string Name, bool Passed, string Detail);

    public sealed record AcceptanceResult(bool Passed, IReadOnlyList<AcceptanceCheck> Checks);

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

    public static AcceptanceResult Check(Summary summary, AcceptanceCriteria criteria)
    {
        var checks = new List<AcceptanceCheck>();
        if (criteria.MinLines is { } minLines)
            checks.Add(new AcceptanceCheck("minLines", summary.Lines >= minLines,
                $"{summary.Lines} >= {minLines}"));
        if (criteria.MaxCer is { } maxCer)
            checks.Add(new AcceptanceCheck("maxCer", summary.ScalarCer <= maxCer,
                $"{summary.ScalarCer:0.######} <= {maxCer:0.######}"));
        if (criteria.MaxMarkErrorRate is { } maxMarkRate)
        {
            double rate = summary.ReferenceScalars == 0 ? 0 : (double)summary.MarkErrors / summary.ReferenceScalars;
            checks.Add(new AcceptanceCheck("maxMarkErrorRate", rate <= maxMarkRate,
                $"{rate:0.######} <= {maxMarkRate:0.######}"));
        }
        return new AcceptanceResult(checks.Count > 0 && checks.All(c => c.Passed), checks);
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

    public static string ToJson(Summary summary, IReadOnlyDictionary<string, string> context,
        AcceptanceResult? acceptance = null)
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
            acceptance,
            note = "Line-level error analysis; labels are not necessarily specialist-adjudicated. Locked acceptance sets must not be used for tuning.",
        };
        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
    }


    public static string ToMarkdown(IReadOnlyList<Row> rows, Summary summary,
        IReadOnlyDictionary<string, string> context, AcceptanceResult? acceptance = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Evaluation error report");
        sb.AppendLine();
        sb.AppendLine($"Generated: {DateTimeOffset.UtcNow:O}");
        foreach (var pair in context.OrderBy(p => p.Key, StringComparer.Ordinal))
            sb.AppendLine($"- **{pair.Key}**: {pair.Value}");
        sb.AppendLine();
        sb.AppendLine($"- Lines: {summary.Lines}");
        sb.AppendLine($"- Reference scalars: {summary.ReferenceScalars}");
        sb.AppendLine($"- Scalar CER: {summary.ScalarCer:0.######}");
        sb.AppendLine($"- Mark errors: {summary.MarkErrors}");
        sb.AppendLine();
        if (acceptance is not null)
        {
            sb.AppendLine($"## Acceptance: {(acceptance.Passed ? "PASS" : "FAIL")}");
            sb.AppendLine();
            foreach (var check in acceptance.Checks)
                sb.AppendLine($"- [{(check.Passed ? "x" : " ")}] `{check.Name}` {check.Detail}");
            sb.AppendLine();
        }
        sb.AppendLine("## Top confusions");
        AppendPairs(sb, "Substitutions", summary.TopSubstitutions);
        AppendPairs(sb, "Insertions", summary.TopInsertions);
        AppendPairs(sb, "Deletions", summary.TopDeletions);
        sb.AppendLine();
        sb.AppendLine("## Worst lines");
        sb.AppendLine();
        sb.AppendLine("| # | CER | Reference | Hypothesis |");
        sb.AppendLine("| ---: | ---: | --- | --- |");
        foreach (var row in rows.OrderByDescending(r => r.ScalarCer).ThenBy(r => r.Index).Take(10))
            sb.AppendLine($"| {row.Index} | {row.ScalarCer:0.###} | {MarkdownCell(row.Reference)} | {MarkdownCell(row.Hypothesis)} |");
        sb.AppendLine();
        sb.AppendLine("Labels are not necessarily specialist-adjudicated. Locked acceptance sets must not be used for tuning; this report is an analysis aid, not an acceptance claim.");
        return sb.ToString();
    }

    private static void AppendPairs(StringBuilder sb, string title, IEnumerable<ConfusionPair> pairs)
    {
        sb.AppendLine();
        sb.AppendLine($"### {title}");
        var any = false;
        foreach (var pair in pairs)
        {
            any = true;
            sb.AppendLine($"- `{pair.Expected}` → `{pair.Produced}` × {pair.Count}");
        }
        if (!any) sb.AppendLine("- none");
    }

    private static string MarkdownCell(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);

    private static string Pairs(IEnumerable<ConfusionPair> pairs) =>
        string.Join(" | ", pairs.Select(p => $"{p.Expected}->{p.Produced}×{p.Count}"));

    private static string Escape(string value)
    {
        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0) return value;
        return '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
    }
}
