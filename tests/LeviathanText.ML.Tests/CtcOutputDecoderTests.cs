using System.Text.Json;
using LeviathanText.Recognition;
using LeviathanText.Training;
using Xunit;
using static TorchSharp.torch;

namespace LeviathanText.ML.Tests;

public class CtcOutputDecoderTests
{
    [Theory]
    [InlineData(CtcDecodeMode.Greedy)]
    [InlineData(CtcDecodeMode.PrefixBeam)]
    public void Decoder_masks_padding_and_preserves_glyph_confidences(CtcDecodeMode mode)
    {
        var labels = new LabelSet(["α\u0301", "β"]);
        // T=4, N=2, C=3. The last two frames of sample zero are padding,
        // deliberately favoring beta so an unmasked decoder would add false text.
        using var probabilities = tensor(new float[] {
            .05f, .9f, .05f,  .1f, .1f, .8f,
            .05f, .9f, .05f,  .9f, .05f, .05f,
            .05f, .05f, .9f,  .05f, .05f, .9f,
            .05f, .05f, .9f,  .9f, .05f, .05f,
        }, [4, 2, 3]);
        using var logProbs = probabilities.log();

        var results = CtcOutputDecoder.Decode(logProbs, [2, 4], labels, mode);

        Assert.Equal(2, results.Count);
        Assert.Equal("α\u0301", results[0].Text);
        Assert.Equal(new[] { 1 }, results[0].RawIndices);
        var first = Assert.Single(results[0].Graphemes);
        Assert.Equal(0, first.StartScalar);
        Assert.Equal(2, first.EndScalar);
        Assert.InRange(first.Confidence.Value, .9 - 1e-6, .9 + 1e-6);
        Assert.Equal("ββ", results[1].Text);
        Assert.Equal(new[] { 2, 2 }, results[1].RawIndices);
        Assert.InRange(results[1].Graphemes[0].Confidence.Value, .8 - 1e-6, .8 + 1e-6);
        Assert.InRange(results[1].Graphemes[1].Confidence.Value, .9 - 1e-6, .9 + 1e-6);
        Assert.Equal(new long[] { 4, 2, 3 }, logProbs.shape); // caller still owns input
    }

    [Theory]
    [InlineData(CtcDecodeMode.Greedy)]
    [InlineData(CtcDecodeMode.PrefixBeam)]
    public void Zero_length_row_decodes_to_empty_without_consuming_padding(CtcDecodeMode mode)
    {
        using var logProbs = zeros(2, 1, 3);
        var result = Assert.Single(CtcOutputDecoder.Decode(logProbs, [0], new LabelSet(["α", "β"]), mode));
        Assert.Empty(result.Text);
        Assert.Empty(result.RawIndices);
        Assert.Empty(result.Graphemes);
    }

    [Fact]
    public void Decoder_validates_tensor_shape_lengths_and_options()
    {
        var labels = new LabelSet(["α", "β"]);
        using var invalidRank = zeros(2, 3);
        using var valid = zeros(2, 1, 3);
        using var invalidClasses = zeros(2, 1, 2);
        Assert.Throws<ArgumentException>(() => CtcOutputDecoder.Decode(invalidRank, [2], labels));
        Assert.Throws<ArgumentException>(() => CtcOutputDecoder.Decode(invalidClasses, [2], labels));
        Assert.Throws<ArgumentException>(() => CtcOutputDecoder.Decode(valid, [2, 2], labels));
        Assert.Throws<ArgumentOutOfRangeException>(() => CtcOutputDecoder.Decode(valid, [-1], labels));
        Assert.Throws<ArgumentOutOfRangeException>(() => CtcOutputDecoder.Decode(valid, [3], labels));
        Assert.Throws<ArgumentOutOfRangeException>(() => CtcOutputDecoder.Decode(valid, [2], labels, (CtcDecodeMode)42));
        Assert.Throws<ArgumentOutOfRangeException>(() => CtcOutputDecoder.Decode(valid, [2], labels, beamWidth: 0));
    }

    [Theory]
    [InlineData(CtcDecodeMode.Greedy)]
    [InlineData(CtcDecodeMode.PrefixBeam)]
    public void Direct_decoder_matches_recognizer_output_and_confidence(CtcDecodeMode mode)
    {
        var labels = new LabelSet(["α", "β"]);
        using var model = new CrnnModel(labels.NumClasses, hiddenSize: 8);
        model.eval();
        var lines = new[] { SyntheticGraphemeRenderer.Render("αβ"), SyntheticGraphemeRenderer.Render("βαβ") };
        var (images, lengths) = LineBatcher.ToTensor(lines);
        using var input = images;
        using var scope = no_grad();
        using var logProbs = model.forward(images);
        var direct = CtcOutputDecoder.Decode(logProbs, lengths, labels, mode);
        var recognized = new TorchLineRecognizer(model, labels, mode).Recognize(lines);
        Assert.Equal(JsonSerializer.Serialize(recognized), JsonSerializer.Serialize(direct));
    }
}
