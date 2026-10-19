using LeviathanText.Layout;


public class LayoutComparisonOneToOneTests
{
    private static DetectedLine Line(int top, int bottom) => new()
    {
        Id = new LeviathanText.Contracts.EntityId(LeviathanText.Contracts.EntityKind.Region, $"r{top}"),
        Top = top,
        Bottom = bottom,
        Left = 0,
        Right = 100,
    };

    [Fact]
    public void One_detection_cannot_match_two_ground_truth_lines()
    {
        // A single merged band covering both GT lines has IoU >= 0.5 with each of them.
        var summary = LayoutComparison.Compare(
        [
            ("merged", (IReadOnlyList<DetectedLine>)[Line(0, 100)],
                (IReadOnlyList<(int Top, int Bottom)>)[(0, 50), (50, 100)]),
        ]);

        var record = Assert.Single(summary.PerWitness);
        Assert.Equal(1, record.Matches);
        Assert.Equal(1.0, record.Precision);
        Assert.Equal(0.5, record.Recall);
    }

    [Fact]
    public void Perfect_one_to_one_matching_scores_one()
    {
        var summary = LayoutComparison.Compare(
        [
            ("clean", (IReadOnlyList<DetectedLine>)[Line(0, 40), Line(60, 100)],
                (IReadOnlyList<(int Top, int Bottom)>)[(0, 40), (60, 100)]),
        ]);
        var record = Assert.Single(summary.PerWitness);
        Assert.Equal(2, record.Matches);
        Assert.Equal(1.0, record.Precision);
        Assert.Equal(1.0, record.Recall);
    }

    [Fact]
    public void Precision_never_exceeds_one()
    {
        var summary = LayoutComparison.Compare(
        [
            ("dense", (IReadOnlyList<DetectedLine>)[Line(0, 100), Line(0, 100)],
                (IReadOnlyList<(int Top, int Bottom)>)[(0, 50), (50, 100)]),
        ]);
        Assert.True(summary.MeanPrecision <= 1.0);
        Assert.Equal(2, summary.PerWitness[0].Matches);
        Assert.Equal(1.0, summary.PerWitness[0].Precision);
        Assert.Equal(1.0, summary.PerWitness[0].Recall);
    }
}
