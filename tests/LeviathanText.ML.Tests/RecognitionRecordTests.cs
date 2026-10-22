using LeviathanText.Contracts;
using LeviathanText.Recognition;
using Xunit;

namespace LeviathanText.ML.Tests;

public class RecognitionRecordTests
{
    private static RecognitionRecord Record(bool rtl = false, bool display = false) => new(
        "l1",
        "αβ",
        [
            new GraphemeConfidence(0, 1, Confidence.None),
            new GraphemeConfidence(1, 2, Confidence.None),
        ],
        rtl,
        display);

    [Fact]
    public void Ltr_record_order_is_identity()
    {
        var record = Record();
        Assert.Same(record, record.DisplayOrder());
        Assert.Same(record, record.LogicalOrder());
    }

    [Fact]
    public void Rtl_logical_to_display_reverses_graphemes_and_rebases_offsets()
    {
        var logical = Record(rtl: true);
        var display = logical.DisplayOrder();

        Assert.True(display.IsDisplayOrder);
        Assert.Equal("βα", display.Text);
        Assert.Equal([0, 1], display.Graphemes.Select(g => g.StartScalar).ToArray());
        Assert.Equal([1, 2], display.Graphemes.Select(g => g.EndScalar).ToArray());
        Assert.Equal("αβ", display.LogicalOrder().Text);
    }

    [Fact]
    public void Rtl_display_to_logical_is_reversible()
    {
        var display = Record(rtl: true) with
        {
            Text = "βα",
            IsDisplayOrder = true,
            Graphemes =
            [
                new GraphemeConfidence(0, 1, Confidence.None),
                new GraphemeConfidence(1, 2, Confidence.None),
            ],
        };
        var logical = display.LogicalOrder();
        Assert.False(logical.IsDisplayOrder);
        Assert.Equal("αβ", logical.Text);
        Assert.Equal("βα", logical.DisplayOrder().Text);
    }

    [Fact]
    public void Bbox_baseline_region_and_slice_are_preserved()
    {
        var record = new RecognitionRecord("l1", "αβγ",
        [
            new GraphemeConfidence(0, 1, Confidence.Of(0.9, ConfidenceScale.RawModelProbability)),
            new GraphemeConfidence(1, 2, Confidence.Of(0.8, ConfidenceScale.RawModelProbability)),
            new GraphemeConfidence(2, 3, Confidence.Of(0.7, ConfidenceScale.RawModelProbability)),
        ])
        {
            RegionId = new EntityId(EntityKind.Region, "r1"),
            Bbox = (10, 20, 110, 40),
            Baseline = [(10, 38), (110, 36)],
        };

        var slice = record.Slice(1, 3);
        Assert.Equal("βγ", slice.Text);
        Assert.Equal(10 + 100.0 / 3, slice.Bbox!.Value.Left, 6);
        Assert.Equal(110, slice.Bbox!.Value.Right, 6);
        Assert.Equal(0.75, slice.Confidence.Value, 6);
        Assert.Equal(new EntityId(EntityKind.Region, "r1"), record.RegionId);
        Assert.Equal(2, record.Baseline.Count);
    }

    [Fact]
    public void Slice_supports_step_and_rejects_invalid_ranges()
    {
        var record = new RecognitionRecord("l1", "αβγ",
        [
            new GraphemeConfidence(0, 1, Confidence.None),
            new GraphemeConfidence(1, 2, Confidence.None),
            new GraphemeConfidence(2, 3, Confidence.None),
        ]) { Bbox = (0, 0, 90, 10) };

        Assert.Equal("αγ", record.Slice(0, 3, 2).Text);
        Assert.Throws<ArgumentOutOfRangeException>(() => record.Slice(1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => record.Slice(-1, 2));
    }

    [Fact]
    public void Mismatched_grapheme_and_confidence_counts_fail()
    {
        var record = new RecognitionRecord("l1", "αβ", [new GraphemeConfidence(0, 1, Confidence.None)], Rtl: true);
        Assert.Throws<InvalidOperationException>(() => record.DisplayOrder());
    }
}
