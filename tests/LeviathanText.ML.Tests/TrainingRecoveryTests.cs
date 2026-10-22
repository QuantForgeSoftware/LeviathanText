using LeviathanText.Recognition;
using LeviathanText.Training;
using Xunit;

namespace LeviathanText.ML.Tests;

/// <summary>
/// A failed epoch must leave a recoverable checkpoint behind without overwriting the
/// selected best model and without swallowing the original exception.
/// </summary>
public class TrainingRecoveryTests
{
    private static readonly string[] Vocabulary = ["α", "β", "γ", "δ"];

    [Fact]
    public void Failure_during_validation_writes_reloadable_failure_checkpoint()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lt-training-recovery", Guid.NewGuid().ToString("N"));
        var labels = new LabelSet(Vocabulary);
        var train = SyntheticLineDataset.Generate(8, Vocabulary, seed: 12);

        // Height 8 cannot be collated into the model's 16-pixel input, so the first
        // validation pass fails deterministically after the training epoch has run.
        var invalidValidation = new[]
        {
            new LabeledLine("αβ", new LineImage(8, 12, new float[8 * 12])),
        };
        var config = new TrainingConfig
        {
            Epochs = 3,
            BatchSize = 4,
            Seed = 4,
            HiddenSize = 8,
            ModelName = "recovery-crnn",
            CheckpointDirectory = directory,
        };

        var model = new CrnnModel(labels.NumClasses, config.HiddenSize);
        var trainer = new CtcTrainer(model, labels, config);

        var ex = Assert.Throws<ArgumentException>(() => trainer.Fit(train, invalidValidation));
        Assert.Contains("input height", ex.Message, StringComparison.OrdinalIgnoreCase);

        Assert.Null(trainer.LastEmergencyCheckpointError);
        Assert.NotNull(trainer.LastEmergencyCheckpointPath);
        Assert.EndsWith(".failure.model", trainer.LastEmergencyCheckpointPath);
        Assert.True(File.Exists(trainer.LastEmergencyCheckpointPath));
        Assert.True(File.Exists(CheckpointStore.OptimizerPath(directory, config.ModelName, ".failure")));
        Assert.True(File.Exists(CheckpointStore.MetadataPath(directory, config.ModelName, ".failure")));
        Assert.False(File.Exists(CheckpointStore.ModelPath(directory, config.ModelName, ".best")));

        var recovered = new CtcTrainer(
            new CrnnModel(labels.NumClasses, config.HiddenSize), labels, config);
        var metadata = recovered.LoadCheckpoint(".failure");
        Assert.Equal(0, metadata.Epoch);
        Assert.Equal(CheckpointStore.LabelSetHash(labels), metadata.LabelSetHash);
    }

    [Fact]
    public void Cancellation_writes_failure_checkpoint_and_rethrows()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lt-training-recovery", Guid.NewGuid().ToString("N"));
        var labels = new LabelSet(Vocabulary);
        var train = SyntheticLineDataset.Generate(4, Vocabulary, seed: 13);
        var config = new TrainingConfig
        {
            Epochs = 3,
            BatchSize = 4,
            Seed = 5,
            HiddenSize = 8,
            ModelName = "cancelled-crnn",
            CheckpointDirectory = directory,
        };

        var trainer = new CtcTrainer(new CrnnModel(labels.NumClasses, config.HiddenSize), labels, config);
        Assert.Throws<OperationCanceledException>(() => trainer.Fit(train, new CancellingList()));

        Assert.Null(trainer.LastEmergencyCheckpointError);
        Assert.NotNull(trainer.LastEmergencyCheckpointPath);
        Assert.True(File.Exists(trainer.LastEmergencyCheckpointPath));
    }

    private sealed class CancellingList : IReadOnlyList<LabeledLine>
    {
        public int Count => 1;
        public LabeledLine this[int index] => throw new OperationCanceledException();
        public IEnumerator<LabeledLine> GetEnumerator() => throw new OperationCanceledException();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
