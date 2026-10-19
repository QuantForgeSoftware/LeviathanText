using LeviathanText.Evaluation;
using LeviathanText.Recognition;
using LeviathanText.Training;
using Xunit;

namespace LeviathanText.ML.Tests;

/// <summary>
/// Proves the Phase 0 C# feasibility path end to end on synthetic data:
/// train with variable-width CTC → select/reload a checkpoint → recognize,
/// plus optimizer-state resume. Synthetic images are not corpus counts.
/// </summary>
public class TrainingEndToEndTests
{
    private static readonly string[] Vocabulary = { "α", "β", "γ", "δ" };

    private static string NewDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lt-ml-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void Train_SelectsBestCheckpoint_ThenReloadsAndRecognizes()
    {
        var dir = NewDirectory();
        var labels = new LabelSet(Vocabulary);
        var train = SyntheticLineDataset.Generate(24, Vocabulary, seed: 1);
        var validation = SyntheticLineDataset.Generate(8, Vocabulary, seed: 2);

        var config = new TrainingConfig
        {
            Epochs = 60,
            BatchSize = 4,
            LearningRate = 1e-2,
            Seed = 7,
            EarlyStoppingPatience = 60,
            HiddenSize = 64,
            ModelName = "test-crnn",
            CheckpointDirectory = dir,
        };

        var model = new CrnnModel(labels.NumClasses, config.HiddenSize);
        var trainer = new CtcTrainer(model, labels, config);
        var result = trainer.Fit(train, validation);

        Assert.NotEmpty(result.History);
        Assert.True(result.History[^1].TrainLoss < result.History[0].TrainLoss, "training loss should decrease");
        Assert.True(result.BestValidationCer < 0.02, $"expected near-perfect validation CER, got {result.BestValidationCer}");

        var metadata = CheckpointStore.ReadMetadata(dir, config.ModelName, ".best");
        Assert.True(metadata.Epoch > 0);
        Assert.Equal(config.ComputeHash(), metadata.ConfigHash);
        Assert.True(File.Exists(CheckpointStore.ModelPath(dir, config.ModelName, ".best")));
        Assert.True(File.Exists(CheckpointStore.OptimizerPath(dir, config.ModelName, ".best")));

        // Fresh model + optimizer, reload the best checkpoint, then recognize.
        var reloadedModel = new CrnnModel(labels.NumClasses, config.HiddenSize);
        var reloadedTrainer = new CtcTrainer(reloadedModel, labels, config);
        reloadedTrainer.LoadCheckpoint(".best");

        var recognizer = new TorchLineRecognizer(reloadedModel, labels);
        var results = recognizer.Recognize([.. train.Select(t => t.Image)]);

        int distance = 0, length = 0;
        for (int i = 0; i < train.Count; i++)
        {
            var score = RecognitionScorer.Score(train[i].Text, results[i].Text);
            distance += score.ScalarDistance;
            length += score.ReferenceScalarLength;
        }
        double cer = (double)distance / length;
        Assert.True(cer < 0.02, $"reloaded model training CER = {cer}");
    }

    [Fact]
    public void Fit_ResumesFromCheckpoint_ContinuingTheSchedule()
    {
        var dir = NewDirectory();
        var labels = new LabelSet(Vocabulary);
        var train = SyntheticLineDataset.Generate(16, Vocabulary, seed: 3);
        var validation = SyntheticLineDataset.Generate(6, Vocabulary, seed: 4);

        var config = new TrainingConfig
        {
            Epochs = 6,
            BatchSize = 4,
            LearningRate = 1e-2,
            Seed = 11,
            EarlyStoppingPatience = 6,
            ModelName = "resume-crnn",
            CheckpointDirectory = dir,
        };

        var trainer = new CtcTrainer(new CrnnModel(labels.NumClasses, config.HiddenSize), labels, config);
        var first = trainer.Fit(train, validation);
        Assert.Equal(6, first.EpochsCompleted);

        // Extend the schedule and resume on a fresh model/optimizer pair.
        var resumeConfig = config with { Epochs = 10 };
        var trainer2 = new CtcTrainer(new CrnnModel(labels.NumClasses, resumeConfig.HiddenSize), labels, resumeConfig);
        var metadata = trainer2.LoadCheckpoint();
        Assert.Equal(6, metadata.Epoch);

        var second = trainer2.Fit(train, validation);
        Assert.Equal(6, second.StartEpoch);
        Assert.Equal(10, second.EpochsCompleted);
    }
}
