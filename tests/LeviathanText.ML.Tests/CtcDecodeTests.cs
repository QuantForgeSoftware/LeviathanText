using LeviathanText.Recognition;
using Xunit;

namespace LeviathanText.ML.Tests;

/// <summary>
/// CTC decoding behavior: greedy collapse, prefix beam search over summed alignment
/// probability, forced alignment for per-label confidence, and input validation.
/// </summary>
public class CtcDecodeTests
{
    [Fact]
    public void Greedy_from_probabilities_takes_per_frame_argmax_then_collapses()
    {
        var frames = new List<IReadOnlyList<double>>
        {
            new[] { 0.1, 0.7, 0.2 },
            new[] { 0.6, 0.2, 0.2 },   // blank between the two label-1 frames keeps both
            new[] { 0.1, 0.8, 0.1 },
            new[] { 0.7, 0.1, 0.2 },
        };

        Assert.Equal(new[] { 1, 1 }, CtcBeamDecoder.Greedy(frames));
    }

    [Fact]
    public void Beam_search_recovers_label_mass_that_greedy_misses()
    {
        // Neither frame argmaxes a label, but the alignments for "1" sum to 0.56 while
        // the blank-only path is 0.25.
        var frames = new List<IReadOnlyList<double>>
        {
            new[] { 0.5, 0.4, 0.1 },
            new[] { 0.5, 0.4, 0.1 },
        };

        Assert.Empty(CtcBeamDecoder.Greedy(frames));
        Assert.Equal(new[] { 1 }, CtcBeamDecoder.PrefixBeamSearch(frames, beamWidth: 8));
    }

    [Fact]
    public void Beam_search_keeps_a_repeated_label_separated_by_a_blank()
    {
        var frames = new List<IReadOnlyList<double>>
        {
            new[] { 0.05, 0.9, 0.05 },
            new[] { 0.9, 0.05, 0.05 },
            new[] { 0.05, 0.9, 0.05 },
        };

        Assert.Equal(new[] { 1, 1 }, CtcBeamDecoder.PrefixBeamSearch(frames, beamWidth: 4));
    }

    [Fact]
    public void Best_alignment_returns_a_probability_per_label()
    {
        var frames = new List<IReadOnlyList<double>>
        {
            new[] { 0.05, 0.9, 0.05 },
            new[] { 0.9, 0.05, 0.05 },
            new[] { 0.05, 0.9, 0.05 },
        };

        var alignment = CtcBeamDecoder.BestAlignment(frames, new[] { 1, 1 });

        Assert.Equal(2, alignment.Count);
        Assert.Equal((0, 0.9), alignment[0]);
        Assert.Equal((2, 0.9), alignment[1]);
    }

    [Fact]
    public void Best_alignment_of_a_single_label_picks_the_higher_probability_frame()
    {
        var alignment = CtcBeamDecoder.BestAlignment(
            [new[] { 0.5, 0.4, 0.1 }, new[] { 0.5, 0.3, 0.2 }],
            new[] { 1 });

        var (timeStep, probability) = Assert.Single(alignment);
        Assert.Equal(0, timeStep);
        Assert.Equal(0.4, probability);
    }

    [Fact]
    public void Decoders_validate_input()
    {
        Assert.Throws<ArgumentException>(() => CtcBeamDecoder.PrefixBeamSearch([]));
        Assert.Throws<ArgumentOutOfRangeException>(() => CtcBeamDecoder.PrefixBeamSearch([new[] { 0.5, 0.5 }], beamWidth: 0));
        Assert.Throws<ArgumentException>(() => CtcBeamDecoder.Greedy([new[] { double.NaN, 0.5 }]));
        Assert.Throws<ArgumentException>(() => CtcBeamDecoder.PrefixBeamSearch([new[] { 0.5 }]));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(2)]
    [InlineData(1)]
    public void Model_output_time_steps_match_the_configured_width_downsample(int factor)
    {
        var labels = new LabelSet(new[] { "α", "β", "γ" });
        var model = new CrnnModel(labels.NumClasses, hiddenSize: 64, widthDownsampleFactor: factor);
        var image = Training.SyntheticGraphemeRenderer.Render("αβγ");

        var (images, lengths) = LineBatcher.ToTensor([image], factor);
        var logProbs = model.forward(images);

        Assert.Equal(model.TimeSteps(image.Width), (int)logProbs.shape[0]);
        Assert.Equal(model.TimeSteps(image.Width), lengths[0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => new CrnnModel(3, 64, widthDownsampleFactor: 3));
    }

    [Fact]
    public void Dropout_is_active_in_training_and_bypassed_in_evaluation()
    {
        var labels = new LabelSet(new[] { "α", "β", "γ" });
        var model = new CrnnModel(labels.NumClasses, hiddenSize: 64, dropout: 0.5);
        var (images, _) = LineBatcher.ToTensor([Training.SyntheticGraphemeRenderer.Render("αβγ")]);

        model.eval();
        var first = model.forward(images).data<float>().ToArray();
        var second = model.forward(images).data<float>().ToArray();
        Assert.Equal(first, second);

        model.train();
        var third = model.forward(images).data<float>().ToArray();
        var fourth = model.forward(images).data<float>().ToArray();
        Assert.NotEqual(third, fourth);

        Assert.Throws<ArgumentOutOfRangeException>(() => new CrnnModel(labels.NumClasses, dropout: 1.0));
    }

    [Fact]
    public void Recognizer_defaults_to_greedy_and_accepts_beam_mode()
    {
        var labels = new LabelSet(new[] { "α", "β", "γ" });
        var model = new CrnnModel(labels.NumClasses);
        var image = Training.SyntheticGraphemeRenderer.Render("αβ");

        var greedy = new TorchLineRecognizer(model, labels);
        var beam = new TorchLineRecognizer(model, labels, CtcDecodeMode.PrefixBeam, beamWidth: 4);

        Assert.Equal(CtcDecodeMode.Greedy, greedy.Mode);
        Assert.Equal(CtcDecodeMode.PrefixBeam, beam.Mode);
        Assert.Single(greedy.Recognize([image]));
        Assert.Single(beam.Recognize([image]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TorchLineRecognizer(model, labels, CtcDecodeMode.PrefixBeam, beamWidth: 0));
    }
}
