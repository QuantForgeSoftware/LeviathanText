using System.Text.Json;
using LeviathanText.Recognition;
using LeviathanText.Training;
using Xunit;
using static TorchSharp.torch;

namespace LeviathanText.ML.Tests;

public class RecognitionModeTests
{
    [Theory]
    [InlineData(CtcDecodeMode.Greedy)]
    [InlineData(CtcDecodeMode.PrefixBeam)]
    public void Inference_disables_dropout_and_restores_training_mode(CtcDecodeMode mode)
    {
        manual_seed(4242);
        var labels = new LabelSet(["α", "β"]);
        using var model = new CrnnModel(labels.NumClasses, hiddenSize: 16, dropout: 0.8);
        EnsureNonblankOutput(model);
        var recognizer = new TorchLineRecognizer(model, labels, mode, beamWidth: 4);
        var image = SyntheticGraphemeRenderer.Render("αβα");

        model.eval();
        string expected = Snapshot(Assert.Single(recognizer.Recognize([image])));
        model.train();

        for (int i = 0; i < 3; i++)
        {
            var actual = Assert.Single(recognizer.Recognize([image]));
            Assert.NotEmpty(actual.Graphemes);
            Assert.Equal(expected, Snapshot(actual));
            Assert.True(model.training);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Inference_preserves_mode_on_empty_input_and_forward_failure(bool training)
    {
        var labels = new LabelSet(["α", "β"]);
        using var model = new CrnnModel(labels.NumClasses, hiddenSize: 16, dropout: 0.5);
        var recognizer = new TorchLineRecognizer(model, labels);
        model.train(training);

        Assert.Empty(recognizer.Recognize([]));
        Assert.Equal(training, model.training);

        // Width one reaches forward(), then cannot survive the model's 2x pooling.
        var invalid = new LineImage(CrnnModel.InputHeight, 1, new float[CrnnModel.InputHeight]);
        Assert.ThrowsAny<Exception>(() => recognizer.Recognize([invalid]));
        Assert.Equal(training, model.training);
    }

    [Fact]
    public void Zero_dropout_predictions_and_confidences_are_unchanged_and_eval_mode_is_preserved()
    {
        manual_seed(4242);
        var labels = new LabelSet(["α", "β"]);
        using var model = new CrnnModel(labels.NumClasses, hiddenSize: 16);
        EnsureNonblankOutput(model);
        var recognizer = new TorchLineRecognizer(model, labels);
        var image = SyntheticGraphemeRenderer.Render("αβα");
        model.train();
        string trainingResult = Snapshot(Assert.Single(recognizer.Recognize([image])));
        Assert.True(model.training);

        model.eval();
        Assert.Equal(trainingResult, Snapshot(Assert.Single(recognizer.Recognize([image]))));
        Assert.False(model.training);
    }

    private static string Snapshot(RecognitionResult result) => JsonSerializer.Serialize(result);

    private static void EnsureNonblankOutput(CrnnModel model)
    {
        using var scope = no_grad();
        // Bias one label without removing dropout-sensitive feature weights. This makes
        // confidence equality meaningful even if random initialization favors blank.
        var bias = model.named_parameters().Single(p => p.name == "_fc.bias").parameter;
        bias.fill_(-3);
        using var labelBias = bias[1];
        labelBias.fill_(3);
    }
}
