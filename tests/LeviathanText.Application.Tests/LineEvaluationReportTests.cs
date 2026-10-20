using LeviathanText.Application;
using Xunit;

namespace LeviathanText.Application.Tests;

public class LineEvaluationReportTests
{
    [Fact]
    public void Scores_lines_and_keeps_mark_errors_and_confusions_separate()
    {
        var (rows, summary) = LineEvaluationReport.Build(
        [
            ("α\u0301βγ", "αβγ"),        // missing accent = mark deletion, no base error
            ("abc", ""),                  // full-line deletion
            ("δ", "δδ"),                  // insertion
        ]);

        Assert.Equal(3, rows.Count);
        Assert.Equal(3, summary.Lines);
        Assert.True(rows[0].MarkErrors > 0);
        Assert.Equal(0, rows[0].BaseErrors);
        Assert.True(rows[1].ScalarDistance > 0);
        Assert.True(summary.ScalarCer > 0);
        Assert.NotEmpty(summary.TopDeletions);
        Assert.Contains(rows, r => r.Confusions.TopInsertions.Count > 0);
    }

    [Fact]
    public void Csv_escapes_commas_quotes_and_newlines_and_json_records_context()
    {
        var (rows, summary) = LineEvaluationReport.Build([("a,b\"c\nd", "a,b\"c\nd")]);
        var csv = LineEvaluationReport.ToCsv(rows);
        Assert.Contains("\"a,b\"\"c\nd\"", csv);
        var json = LineEvaluationReport.ToJson(summary, new Dictionary<string, string> { ["split"] = "dev" });
        Assert.Contains("\"split\": \"dev\"", json);
        Assert.Contains("\"scalarCer\": 0", json);
    }

    [Fact]
    public void Markdown_report_contains_metrics_caveat_and_worst_lines()
    {
        var (rows, summary) = LineEvaluationReport.Build([("αβ", "α"), ("γ", "γ")]);
        var md = LineEvaluationReport.ToMarkdown(rows, summary, new Dictionary<string, string> { ["split"] = "dev" });
        Assert.Contains("# Evaluation error report", md);
        Assert.Contains("Scalar CER", md);
        Assert.Contains("Worst lines", md);
        Assert.Contains("not an acceptance claim", md);
        Assert.Contains("**split**: dev", md);
    }
}
