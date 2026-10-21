using LeviathanText.Layout;
using Xunit;

namespace LeviathanText.Layout.Tests;

/// <summary>Behavioral parity scenarios adapted from Kraken test_segmentation_metrics.py.</summary>
public class DetectionMetricsParityTests
{
    private static DetectionMetrics.Interval Line(double y) => new(y, y);

    [Fact]
    public void Empty_cases_have_explicit_precision_recall_semantics()
    {
        var both = DetectionMetrics.Compute([], [], 1);
        Assert.Equal((1.0, 1.0, 1.0, 0, 0), (both.Precision, both.Recall, both.F1, both.NumPred, both.NumGt));

        var noPred = DetectionMetrics.Compute([], [Line(0)], 1);
        Assert.Equal((0.0, 0.0, 0.0), (noPred.Precision, noPred.Recall, noPred.F1));

        var noGt = DetectionMetrics.Compute([Line(0)], [], 1);
        Assert.Equal((0.0, 0.0, 0.0), (noGt.Precision, noGt.Recall, noGt.F1));
    }

    [Fact]
    public void Perfect_match_scores_one_and_extra_prediction_penalizes_precision()
    {
        var perfect = DetectionMetrics.Compute([Line(0)], [Line(0)], 1);
        Assert.Equal((1.0, 1.0, 1.0), (perfect.Precision, perfect.Recall, perfect.F1));

        var extra = DetectionMetrics.Compute([Line(0), Line(50)], [Line(0)], 1);
        Assert.Equal(0.5, extra.Precision);
        Assert.Equal(1.0, extra.Recall);
        Assert.Equal(2.0 / 3.0, extra.F1, 6);
    }

    [Fact]
    public void Better_match_is_monotonic_and_aggregate_is_weighted()
    {
        var good = DetectionMetrics.Compute([Line(0)], [Line(0)], 1);
        var bad = DetectionMetrics.Compute([Line(2.5)], [Line(0)], 1);
        Assert.True(good.Precision > bad.Precision);
        Assert.True(good.Recall > bad.Recall);

        var aggregate = DetectionMetrics.Aggregate(
        [
            new DetectionMetrics.Result(1.0, 0.5, 2.0 / 3.0, 1, 2),
            new DetectionMetrics.Result(0.0, 0.0, 0.0, 1, 1),
        ]);
        Assert.Equal(0.5, aggregate.Precision, 6);
        Assert.Equal(0.25, aggregate.Recall, 6);
        Assert.Equal(1.0 / 3.0, aggregate.F1, 6);
    }
}
