using LeviathanText.Application;
using LeviathanText.Contracts;

namespace LeviathanText.Application.Tests;

public sealed class RegressionSuiteTests
{
    private static RunRecord Run(params (string Key, double Value)[] metrics) => new()
    {
        Id = "run",
        Kind = "recognition",
        Name = "run",
        CreatedAt = DateTimeOffset.UtcNow,
        Metrics = metrics.ToDictionary(pair => pair.Key, pair => pair.Value),
        Config = new Dictionary<string, string> { ["profile"] = "greek-diplomatic" },
    };

    [Fact]
    public void Criteria_are_evaluated_with_direction_and_tolerance()
    {
        var baseline = Run(("cer", 0.2), ("devF1", 0.8));
        var candidate = Run(("cer", 0.21), ("devF1", 0.79));
        var report = RegressionSuite.Evaluate(baseline, candidate,
        [
            new RegressionCriterion("cer", MaxRegression: 0.02),
            new RegressionCriterion("devF1", MaxRegression: 0.02, HigherIsBetter: true),
        ]);

        Assert.True(report.Passed);
        Assert.Equal(2, report.Checks.Count);
    }

    [Fact]
    public void Regression_beyond_tolerance_and_missing_metrics_fail()
    {
        var baseline = Run(("cer", 0.2));
        var candidate = Run(("cer", 0.3));
        var report = RegressionSuite.Evaluate(baseline, candidate,
        [
            new RegressionCriterion("cer", MaxRegression: 0.01),
            new RegressionCriterion("missing"),
        ]);

        Assert.False(report.Passed);
        Assert.Contains(report.Checks, check => check.Metric == "cer" && !check.Passed);
        Assert.Contains(report.Checks, check => check.Reason == "baseline metric missing");
    }

    [Fact]
    public void Profile_specific_criteria_are_ignored_for_other_profiles()
    {
        var baseline = Run(("cer", 0.2));
        var candidate = Run(("cer", 0.2));
        var report = RegressionSuite.Evaluate(baseline, candidate,
        [
            new RegressionCriterion("cer", MaxRegression: 0, Profile: "other-profile"),
        ]);

        Assert.False(report.Passed);
        Assert.Contains(report.Checks, check => check.Reason?.Contains("no criterion matched") == true);
    }
}
