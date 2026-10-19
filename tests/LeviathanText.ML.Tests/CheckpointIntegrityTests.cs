using LeviathanText.Contracts;
using LeviathanText.Recognition;
using LeviathanText.Training;
using Xunit;

namespace LeviathanText.ML.Tests;

/// <summary>
/// A checkpoint is a complete model package: loading with a different label set must fail
/// loudly even when the class count happens to match (domain-and-pipeline.md §3).
/// </summary>
public class CheckpointIntegrityTests
{
    [Fact]
    public void Loading_with_a_different_label_set_fails_as_model_incompatible()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lt-checkpoint-integrity", Guid.NewGuid().ToString("N"));
        var vocabulary = new[] { "α", "β" };
        var labels = new LabelSet(vocabulary);
        var train = SyntheticLineDataset.Generate(8, vocabulary, seed: 5);
        var config = new TrainingConfig
        {
            Epochs = 2,
            BatchSize = 4,
            Seed = 3,
            HiddenSize = 16,
            ModelName = "integrity-crnn",
            CheckpointDirectory = dir,
        };

        var model = new CrnnModel(labels.NumClasses, config.HiddenSize);
        new CtcTrainer(model, labels, config).Fit(train, train);

        var sameLabels = new CtcTrainer(new CrnnModel(labels.NumClasses, config.HiddenSize), new LabelSet(vocabulary), config);
        var metadata = sameLabels.LoadCheckpoint(".best");
        Assert.Equal(CheckpointStore.LabelSetHash(labels), metadata.LabelSetHash);

        var otherLabels = new LabelSet(new[] { "α", "γ" }); // same class count, different meaning
        var mismatched = new CtcTrainer(new CrnnModel(otherLabels.NumClasses, config.HiddenSize), otherLabels, config);

        var ex = Assert.Throws<LeviathanException>(() => mismatched.LoadCheckpoint(".best"));
        Assert.Equal(ErrorCode.ModelIncompatible, ex.Error.Code);
        Assert.Equal(RemedyHint.MapUnknownLabel, ex.Error.RemedyHint);
    }
}
