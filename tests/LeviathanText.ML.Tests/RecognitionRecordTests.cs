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
    public void Mismatched_grapheme_and_confidence_counts_fail()
    {
        var record = new RecognitionRecord("l1", "αβ", [new GraphemeConfidence(0, 1, Confidence.None)], Rtl: true);
        Assert.Throws<InvalidOperationException>(() => record.DisplayOrder());
    }
}
