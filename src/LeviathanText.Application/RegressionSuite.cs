using LeviathanText.Contracts;

namespace LeviathanText.Application;

/// <summary>One metric threshold in a regression suite. Missing metrics fail loudly.</summary>
public sealed record RegressionCriterion(
    string Metric,
    double MaxRegression = 0,
    bool HigherIsBetter = false,
    string? Profile = null);

public sealed record RegressionCheck(
    string Metric,
    string? Profile,
    double? Baseline,
    double? Candidate,
    double Regression,
    double MaxRegression,
    bool Passed,
    string? Reason);

public sealed record RegressionReport(bool Passed, IReadOnlyList<RegressionCheck> Checks);

/// <summary>
/// Declarative regression gate for recorded runs. This is an experiment guard, not an
/// accuracy claim: criteria and tolerances are supplied by the operator and may be
/// profile-specific via a <c>profile</c> config value.
/// </summary>
public static class RegressionSuite
{
    public static RegressionReport Evaluate(
        RunRecord baseline,
        RunRecord candidate,
        IReadOnlyList<RegressionCriterion> criteria)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(criteria);
        if (criteria.Count == 0)
            throw new ArgumentException("At least one regression criterion is required.", nameof(criteria));

        var profile = candidate.Config.GetValueOrDefault("profile")
            ?? baseline.Config.GetValueOrDefault("profile");
        var checks = new List<RegressionCheck>();
        foreach (var criterion in criteria)
        {
            if (!string.IsNullOrWhiteSpace(criterion.Profile)
                && !string.Equals(criterion.Profile, profile, StringComparison.Ordinal))
                continue;
            if (!baseline.Metrics.TryGetValue(criterion.Metric, out var before))
            {
                checks.Add(new RegressionCheck(criterion.Metric, criterion.Profile, null, null, 0,
                    criterion.MaxRegression, false, "baseline metric missing"));
                continue;
            }
            if (!candidate.Metrics.TryGetValue(criterion.Metric, out var after))
            {
                checks.Add(new RegressionCheck(criterion.Metric, criterion.Profile, before, null, 0,
                    criterion.MaxRegression, false, "candidate metric missing"));
                continue;
            }

            double regression = criterion.HigherIsBetter ? before - after : after - before;
            bool passed = regression <= criterion.MaxRegression + 1e-12;
            checks.Add(new RegressionCheck(criterion.Metric, criterion.Profile, before, after,
                regression, criterion.MaxRegression, passed, passed ? null : "regression exceeds tolerance"));
        }

        if (checks.Count == 0)
            checks.Add(new RegressionCheck("(suite)", profile, null, null, 0, 0, false,
                "no criterion matched the run profile"));
        return new RegressionReport(checks.All(c => c.Passed), checks);
    }
}
