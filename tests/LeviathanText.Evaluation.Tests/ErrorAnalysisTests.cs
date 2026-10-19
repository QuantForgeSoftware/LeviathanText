using LeviathanText.Evaluation;
using Xunit;

namespace LeviathanText.Evaluation.Tests;

/// <summary>Ranked confusion analysis with separate mark-error accounting.</summary>
public class ErrorAnalysisTests
{
    [Fact]
    public void Classifies_substitutions_insertions_and_deletions()
    {
        var report = ErrorAnalysis.Analyze("αβ", "αγ");

        Assert.Equal(1, report.TotalSubstitutions);
        Assert.Equal(0, report.TotalInsertions);
        Assert.Equal(0, report.TotalDeletions);
        var pair = Assert.Single(report.TopSubstitutions);
        Assert.Equal("β", pair.Expected);
        Assert.Equal("γ", pair.Produced);

        var insertion = ErrorAnalysis.Analyze("α", "αβ");
        Assert.Equal(1, insertion.TotalInsertions);
        Assert.Equal("β", Assert.Single(insertion.TopInsertions).Produced);

        var deletion = ErrorAnalysis.Analyze("αβ", "α");
        Assert.Equal(1, deletion.TotalDeletions);
        Assert.Equal("β", Assert.Single(deletion.TopDeletions).Expected);
    }

    [Fact]
    public void Counts_mark_insertions_separately_from_base_edits()
    {
        var report = ErrorAnalysis.Analyze("α", "α\u0301");

        Assert.Equal(0, report.TotalSubstitutions);
        Assert.Equal(1, report.TotalInsertions);
        Assert.Equal(1, report.MarkInsertions);
        Assert.Equal("\u0301", Assert.Single(report.TopInsertions).Produced);
    }

    [Fact]
    public void Ranks_confusions_by_frequency_then_lexically()
    {
        var report = ErrorAnalysis.Analyze("αβ", "αγγβ");

        Assert.Equal(2, report.TotalInsertions);
        var pairs = report.TopInsertions;
        Assert.All(pairs, p => Assert.Equal("γ", p.Produced));
        Assert.Equal(2, pairs[0].Count);
    }

    [Fact]
    public void Aggregate_sums_counts_and_recomputes_the_ranking()
    {
        var first = ErrorAnalysis.Analyze("αβ", "αγ");   // β→γ
        var second = ErrorAnalysis.Analyze("αβ", "αγ");  // β→γ
        var third = ErrorAnalysis.Analyze("αβ", "αδ");   // β→δ

        var aggregate = ErrorAnalysis.Aggregate([first, second, third], top: 1);

        Assert.Equal(3, aggregate.TotalSubstitutions);
        var top = Assert.Single(aggregate.TopSubstitutions);
        Assert.Equal("β", top.Expected);
        Assert.Equal("γ", top.Produced);
        Assert.Equal(2, top.Count);
    }

    [Fact]
    public void Aggregate_preserves_mark_error_totals()
    {
        var aggregate = ErrorAnalysis.Aggregate([ErrorAnalysis.Analyze("α", "α\u0301"), ErrorAnalysis.Analyze("ε", "ε\u0300")]);

        Assert.Equal(2, aggregate.MarkInsertions);
    }

    [Fact]
    public void Invalid_top_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ErrorAnalysis.Analyze("α", "α", top: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ErrorAnalysis.Aggregate([], top: -1));
    }
}
