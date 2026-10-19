using LeviathanText.Evaluation;
using Xunit;

namespace LeviathanText.Evaluation.Tests;

/// <summary>Grapheme-level confidence pairing for calibration on real recognizer output.</summary>
public class GraphemeCalibrationTests
{
    private static GraphemeSpan Span(int start, int end, double confidence) => new(start, end, confidence);

    [Fact]
    public void Pairs_confidence_with_match_and_substitution()
    {
        var samples = GraphemeCalibration.Samples("αβ", "αγ", [Span(0, 1, 0.9), Span(1, 2, 0.2)]);

        Assert.Equal(2, samples.Count);
        Assert.True(samples[0].Correct);
        Assert.Equal(0.9, samples[0].Confidence);
        Assert.False(samples[1].Correct);
        Assert.Equal(0.2, samples[1].Confidence);
    }

    [Fact]
    public void Inserted_grapheme_is_incorrect_and_keeps_its_confidence()
    {
        var samples = GraphemeCalibration.Samples("αβ", "αβγ", [Span(0, 1, 0.8), Span(1, 2, 0.7), Span(2, 3, 0.55)]);

        Assert.Equal(3, samples.Count);
        Assert.False(samples[2].Correct);
        Assert.Equal(0.55, samples[2].Confidence, 10);
    }

    [Fact]
    public void Deleted_reference_grapheme_is_skipped_not_guessed()
    {
        var samples = GraphemeCalibration.Samples("αβ", "α", [Span(0, 1, 0.8)]);

        var sample = Assert.Single(samples);
        Assert.True(sample.Correct);
    }

    [Fact]
    public void Multi_scalar_grapheme_is_one_sample()
    {
        // α + combining acute is one extended grapheme cluster covering two scalars.
        var samples = GraphemeCalibration.Samples("α\u0301", "α\u0301", [Span(0, 2, 0.6)]);

        var sample = Assert.Single(samples);
        Assert.True(sample.Correct);
        Assert.Equal(0.6, sample.Confidence, 10);
    }

    [Fact]
    public void Calibration_report_consumes_the_samples()
    {
        var samples = GraphemeCalibration.Samples("αβ", "αγ", [Span(0, 1, 0.9), Span(1, 2, 0.2)]);

        var report = SelectivePredictionScorer.Calibration(samples, bins: 10);

        Assert.True(report.ExpectedCalibrationError > 0);
        Assert.Equal(2, report.Bins.Sum(b => b.Count));
    }

    [Fact]
    public void Invalid_spans_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => GraphemeCalibration.Samples("α", "α", [Span(0, 1, 1.5)]));
        Assert.Throws<ArgumentException>(() => GraphemeCalibration.Samples("α", "α", [Span(0, 5, 0.5)]));
        Assert.Throws<ArgumentException>(() => GraphemeCalibration.Samples("α", "α", [])); // produced grapheme without a confidence
    }
}
