using LeviathanText.Evaluation;
using Xunit;

namespace LeviathanText.Evaluation.Tests;

/// <summary>
/// Reliability and selective-prediction scoring: calibration error, risk–coverage curve
/// and the coverage achievable at a target CER (acceptance-plan.md §3).
/// </summary>
public class SelectivePredictionTests
{
    [Fact]
    public void Calibration_error_matches_hand_computation()
    {
        var report = SelectivePredictionScorer.Calibration(
        [
            new GraphemeSample(0.9, true),
            new GraphemeSample(0.9, true),
            new GraphemeSample(0.1, false),
            new GraphemeSample(0.1, true),
        ], bins: 10);

        Assert.Equal(0.25, report.ExpectedCalibrationError, 10);   // (0.5-0.1)*0.5 + (1.0-0.9)*0.5
        Assert.Equal(0.4, report.MaximumCalibrationError, 10);
        Assert.Equal(4, report.Bins.Sum(b => b.Count));
        Assert.Equal(0.5, report.Bins[1].Accuracy, 10);
        Assert.Equal(1.0, report.Bins[9].Accuracy, 10);
    }

    [Fact]
    public void Confidence_of_exactly_one_lands_in_the_last_bin()
    {
        var report = SelectivePredictionScorer.Calibration([new GraphemeSample(1.0, true)], bins: 10);

        Assert.Equal(1, report.Bins[^1].Count);
        Assert.Equal(1.0, report.Bins[^1].MeanConfidence, 10);
    }

    [Fact]
    public void Risk_coverage_curve_and_area_are_text_weighted()
    {
        var report = SelectivePredictionScorer.Selective(
        [
            new SelectiveSample(0.9, 10, 0),
            new SelectiveSample(0.8, 10, 1),
            new SelectiveSample(0.5, 10, 5),
            new SelectiveSample(0.2, 10, 9),
        ]);

        Assert.Equal(0.375, report.FullCoverageCer, 10);
        Assert.Equal(4, report.Curve.Count);
        Assert.Equal(0.25, report.Curve[0].Coverage, 10);
        Assert.Equal(0.0, report.Curve[0].RiskCer, 10);
        Assert.Equal(1.0, report.Curve[^1].Coverage, 10);
        // Rectangle rule: 0*0.25 + 0.05*0.25 + 0.20*0.25 + 0.375*0.25 = 0.15625
        Assert.Equal(0.15625, report.AreaUnderRiskCoverageCurve, 10);
    }

    [Fact]
    public void Coverage_for_a_target_cer_uses_the_best_retained_prefix()
    {
        var report = SelectivePredictionScorer.Selective(
        [
            new SelectiveSample(0.9, 10, 0),
            new SelectiveSample(0.8, 10, 1),
            new SelectiveSample(0.5, 10, 5),
            new SelectiveSample(0.2, 10, 9),
        ]);

        Assert.Equal(0.5, report.CoverageForCerBelow(0.1), 10);
        Assert.Equal(0.75, report.CoverageForCerBelow(0.2), 10);
        Assert.Equal(0.0, report.CoverageForCerBelow(-0.01), 10);
    }

    [Fact]
    public void Scoring_rejects_invalid_input()
    {
        Assert.Throws<ArgumentException>(() => SelectivePredictionScorer.Calibration([]));
        Assert.Throws<ArgumentException>(() => SelectivePredictionScorer.Calibration([new GraphemeSample(double.NaN, true)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => SelectivePredictionScorer.Calibration([new GraphemeSample(0.5, true)], bins: 0));
        Assert.Throws<ArgumentException>(() => SelectivePredictionScorer.Selective([]));
        Assert.Throws<ArgumentException>(() => SelectivePredictionScorer.Selective([new SelectiveSample(0.5, 0, 0)]));
        Assert.Throws<ArgumentException>(() => SelectivePredictionScorer.Selective([new SelectiveSample(0.5, 10, -1)]));
    }
}
