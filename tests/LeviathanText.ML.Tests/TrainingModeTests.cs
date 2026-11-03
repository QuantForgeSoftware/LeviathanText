using LeviathanText.Recognition;
using LeviathanText.Training;
using Xunit;
using static TorchSharp.torch;

namespace LeviathanText.ML.Tests;

public class TrainingModeTests
{
    private static readonly string[] Vocabulary = ["α", "β"];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Failed_training_restores_the_callers_mode(bool training)
    {
        var labels = new LabelSet(Vocabulary);
        using var model = new CrnnModel(labels.NumClasses, hiddenSize: 8, dropout: .8);
        var trainer = new CtcTrainer(model, labels, new TrainingConfig());
        model.train(training);
        Assert.Throws<ArgumentException>(() => trainer.TrainEpoch([new("α", new LineImage(8, 8, new float[64]))], 2));
        Assert.Equal(training, model.training);
    }

    [Fact]
    public void Evaluate_disables_dropout_and_restores_training_mode_even_after_error()
    {
        set_num_threads(1);
        manual_seed(41);
        var labels = new LabelSet(Vocabulary);
        var lines = SyntheticLineDataset.Generate(3, Vocabulary, seed: 91);
        using var model = new CrnnModel(labels.NumClasses, hiddenSize: 8, dropout: .8);
        var trainer = new CtcTrainer(model, labels, new TrainingConfig { BatchSize = 2 });
        model.eval();
        var expected = trainer.Evaluate(lines);
        Assert.False(model.training);
        model.train();
        Assert.Equal(expected, trainer.Evaluate(lines));
        Assert.Equal(expected, trainer.Evaluate(lines));
        Assert.True(model.training);
        Assert.Throws<ArgumentException>(() => trainer.Evaluate([new("α", new LineImage(8, 8, new float[64]))]));
        Assert.True(model.training);
        model.eval();
        Assert.Throws<ArgumentException>(() => trainer.Evaluate([new("α", new LineImage(8, 8, new float[64]))]));
        Assert.False(model.training);
    }

    [Fact]
    public void Resumed_training_uses_training_mode_even_if_caller_left_model_in_eval()
    {
        set_num_threads(1);
        var directory = Path.Combine(Path.GetTempPath(), "lt-mode", Guid.NewGuid().ToString("N"));
        var labels = new LabelSet(Vocabulary);
        var lines = SyntheticLineDataset.Generate(3, Vocabulary, seed: 92);
        var config = new TrainingConfig { BatchSize = 2, HiddenSize = 8, ModelName = "mode", CheckpointDirectory = directory };
        try
        {
            manual_seed(81);
            using var first = new CrnnModel(labels.NumClasses, hiddenSize: 8, dropout: .8);
            var trainer = new CtcTrainer(first, labels, config);
            trainer.TrainEpoch(lines, 1);
            trainer.SaveCheckpoint(isBest: true);
            using var second = new CrnnModel(labels.NumClasses, hiddenSize: 8, dropout: .8);
            var resumed = new CtcTrainer(second, labels, config);
            resumed.LoadCheckpoint();
            first.train();
            second.eval();
            manual_seed(72);
            double expected = trainer.TrainEpoch(lines, 2);
            manual_seed(72);
            double actual = resumed.TrainEpoch(lines, 2);
            Assert.Equal(expected, actual);
            Assert.True(first.training);
            Assert.False(second.training);
            var recognizer1 = new TorchLineRecognizer(first, labels);
            var recognizer2 = new TorchLineRecognizer(second, labels);
            var firstResult = recognizer1.Recognize([lines[0].Image])[0];
            var secondResult = recognizer2.Recognize([lines[0].Image])[0];
            Assert.Equal(firstResult.Text, secondResult.Text);
            Assert.Equal(firstResult.RawIndices, secondResult.RawIndices);
            Assert.Equal(firstResult.Graphemes, secondResult.Graphemes);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
