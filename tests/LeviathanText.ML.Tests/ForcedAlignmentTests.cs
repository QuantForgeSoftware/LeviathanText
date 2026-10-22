using LeviathanText.Contracts;
using LeviathanText.Recognition;
using LeviathanText.Training;
using Xunit;

namespace LeviathanText.ML.Tests;

public class ForcedAlignmentTests
{
    private static readonly string[] Vocabulary = ["α", "β"];

    [Fact]
    public void Known_text_gets_ordered_time_spans_and_confidence()
    {
        var labels = new LabelSet(Vocabulary);
        var model = new CrnnModel(labels.NumClasses, hiddenSize: 8);
        var image = SyntheticGraphemeRenderer.Render("αβα");

        var result = CtcForcedAligner.Align(model, labels, image, "αβα");

        Assert.Equal("αβα", result.Text);
        Assert.Equal(3, result.Graphemes.Count);
        Assert.True(result.TimeSteps > 0);
        Assert.All(result.Graphemes, grapheme =>
        {
            Assert.InRange(grapheme.StartTimeStep, 0, result.TimeSteps - 1);
            Assert.InRange(grapheme.EndTimeStep, grapheme.StartTimeStep, result.TimeSteps - 1);
            Assert.InRange(grapheme.Confidence.Value, 0, 1);
        });
        Assert.True(result.Graphemes[0].StartTimeStep <= result.Graphemes[1].StartTimeStep);
        Assert.True(result.Graphemes[1].StartTimeStep <= result.Graphemes[2].StartTimeStep);
    }

    [Fact]
    public void Unencodable_target_is_reported_as_unknown_label()
    {
        var labels = new LabelSet(Vocabulary);
        var model = new CrnnModel(labels.NumClasses, hiddenSize: 8);
        var image = SyntheticGraphemeRenderer.Render("αβ");

        var ex = Assert.Throws<LeviathanException>(() =>
            CtcForcedAligner.Align(model, labels, image, "γ"));
        Assert.Equal(ErrorCode.UnknownLabel, ex.Error.Code);
    }

    [Fact]
    public void Target_longer_than_the_time_budget_is_rejected()
    {
        var labels = new LabelSet(Vocabulary);
        var model = new CrnnModel(labels.NumClasses, hiddenSize: 8);
        var image = SyntheticGraphemeRenderer.Render("αβ");

        var ex = Assert.Throws<LeviathanException>(() =>
            CtcForcedAligner.Align(model, labels, image, new string('α', 200)));
        Assert.Equal(ErrorCode.StageContractViolation, ex.Error.Code);
    }

    [Fact]
    public void Empty_target_returns_empty_alignment()
    {
        var labels = new LabelSet(Vocabulary);
        var model = new CrnnModel(labels.NumClasses, hiddenSize: 8);
        var image = SyntheticGraphemeRenderer.Render("αβ");

        var result = CtcForcedAligner.Align(model, labels, image, "");
        Assert.Empty(result.Graphemes);
        Assert.Equal(0, result.TimeSteps);
    }
}
