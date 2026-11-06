using LeviathanText.TestSupport;
using System.Globalization;
using System.Text.Json;
using LeviathanText.Recognition;
using LeviathanText.Training;
using Xunit;
using static TorchSharp.torch;

namespace LeviathanText.ML.Tests;

public sealed class TrainingTargetTests
{
    private static CrnnModel ZeroModel(LabelSet labels)
    {
        var model = new CrnnModel(labels.NumClasses, hiddenSize: 8);
        using var scope = no_grad();
        foreach (var parameter in model.parameters()) parameter.zero_();
        return model;
    }

    private static LabeledLine[] Lines() =>
    [
        new("α", new LineImage(16, 16, new float[256])),
        new("β", new LineImage(16, 16, new float[256])),
    ];

    [CapabilityFact(TestCapabilities.Fitting)]
    public void Reaching_control_target_stops_at_first_epoch_and_records_final_diagnostics()
    {
        set_num_threads(1);
        string directory = Path.Combine(Path.GetTempPath(), "lt-target", Guid.NewGuid().ToString("N"));
        try
        {
            var labels = new LabelSet(["α", "β"]);
            using var model = ZeroModel(labels);
            var config = new TrainingConfig { Epochs = 5, BatchSize = 2, HiddenSize = 8, LearningRate = 0,
                EarlyStoppingPatience = 10, TargetValidationCer = 1, DiagnosticsEveryNEpochs = 4,
                ModelName = "target", CheckpointDirectory = directory };
            var trainer = new CtcTrainer(model, labels, config);
            var lines = Lines();
            var run = trainer.Fit(lines, lines);
            Assert.Equal(1, run.EpochsCompleted);
            Assert.Equal(1, run.BestValidationCer);
            var epoch = Assert.Single(run.History);
            Assert.Equal(1, epoch.GlobalStep);
            Assert.NotNull(epoch.TrainingEvaluation);
            Assert.NotNull(epoch.ValidationEvaluation);
            Assert.Equal(1, epoch.ValidationEvaluation.Cer);
            Assert.Equal(8, epoch.ValidationEvaluation.Diagnostics.ValidFrames);
            var metadata = trainer.RestoreBest();
            Assert.Equal(1, metadata.Epoch);
            Assert.Equal(1, metadata.GlobalStep);
            Assert.Equal(config.ComputeHash(), metadata.ConfigHash);
            var manifest = TrainingRunManifestWriter.Create(model, labels, config, run, 2, 2, 0, "split");
            var path = TrainingRunManifestWriter.Write(directory, manifest);
            Assert.Equal(1, TrainingRunManifestWriter.Read(path).TargetValidationCer);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [CapabilityTheory(TestCapabilities.Fitting)]
    [InlineData(null)]
    [InlineData(.5)]
    public void Missing_or_unreached_target_keeps_the_fixed_epoch_budget(double? target)
    {
        set_num_threads(1);
        string directory = Path.Combine(Path.GetTempPath(), "lt-target", Guid.NewGuid().ToString("N"));
        try
        {
            var labels = new LabelSet(["α", "β"]);
            using var model = ZeroModel(labels);
            var config = new TrainingConfig { Epochs = 3, BatchSize = 2, HiddenSize = 8, LearningRate = 0,
                EarlyStoppingPatience = 10, TargetValidationCer = target,
                ModelName = "budget", CheckpointDirectory = directory };
            var lines = Lines();
            var run = new CtcTrainer(model, labels, config).Fit(lines, lines);
            Assert.Equal(3, run.EpochsCompleted);
            Assert.Equal(3, run.History.Count);
            Assert.Equal(new long[] { 1, 2, 3 }, run.History.Select(e => e.GlobalStep));
            if (target is null)
            {
                var manifest = TrainingRunManifestWriter.Create(model, labels, config, run, 2, 2, 0, "split");
                var path = TrainingRunManifestWriter.Write(directory, manifest);
                using var json = JsonDocument.Parse(File.ReadAllText(path));
                Assert.False(json.RootElement.TryGetProperty("TargetValidationCer", out _));
                Assert.Null(TrainingRunManifestWriter.Read(path).TargetValidationCer);
            }
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-.01)]
    public void Invalid_target_is_rejected_by_constructor_and_hash(double target)
    {
        var labels = new LabelSet(["α", "β"]);
        using var model = ZeroModel(labels);
        var config = new TrainingConfig { TargetValidationCer = target };
        Assert.Throws<ArgumentOutOfRangeException>(() => new CtcTrainer(model, labels, config));
        Assert.Throws<ArgumentOutOfRangeException>(() => config.ComputeHash());
    }

    [Fact]
    public void Null_target_preserves_legacy_hash_and_explicit_target_changes_it()
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var legacy = new TrainingConfig();
            Assert.Equal("f07af4db69e67adf60f91e60fda613f965b8d3474e52478d0590111e5c25c13e", legacy.ComputeHash());
            Assert.Equal(legacy.ComputeHash(), (legacy with { TargetValidationCer = null }).ComputeHash());
            Assert.NotEqual(legacy.ComputeHash(), (legacy with { TargetValidationCer = .02 }).ComputeHash());
        }
        finally { CultureInfo.CurrentCulture = culture; }
    }

    [CapabilityTheory(TestCapabilities.Fitting)]
    [InlineData(null)]
    [InlineData(1.0)]
    public void First_target_crossing_is_selected_even_inside_the_ordinary_improvement_tolerance(double? target)
    {
        set_num_threads(1);
        string directory = Path.Combine(Path.GetTempPath(), "lt-target-crossing", Guid.NewGuid().ToString("N"));
        try
        {
            var labels = new LabelSet(["α", "β"]);
            using var model = ZeroModel(labels);
            var initial = new TrainingConfig { Epochs = 1, BatchSize = 2, HiddenSize = 8, LearningRate = 0,
                EarlyStoppingPatience = 10, TargetValidationCer = target, DiagnosticsEveryNEpochs = 10,
                ModelName = "crossing", CheckpointDirectory = directory };
            var lines = Lines();
            new CtcTrainer(model, labels, initial).Fit(lines, lines);
            var resumedConfig = initial with { Epochs = 3 };

            // Represent a preceding score just above the target. Such a sub-tolerance
            // CER step can occur with a large reference set; no huge native fixture is needed.
            const double priorBest = 1.0000005;
            var metadata = CheckpointStore.ReadMetadata(directory, initial.ModelName) with
            {
                BestValidationCer = priorBest,
                ConfigHash = resumedConfig.ComputeHash(),
            };
            CheckpointStore.WriteMetadata(directory, initial.ModelName, metadata);
            CheckpointStore.WriteMetadata(directory, initial.ModelName, metadata, ".best");
            var trainer = new CtcTrainer(model, labels, resumedConfig);
            trainer.LoadCheckpoint();
            var run = trainer.Fit(lines, lines);
            var selected = trainer.RestoreBest();

            if (target is not null)
            {
                Assert.Equal(2, run.EpochsCompleted);
                Assert.Equal(1, run.BestValidationCer);
                var epoch = Assert.Single(run.History);
                Assert.Equal(2, epoch.GlobalStep);
                Assert.Equal(1, epoch.ValidationEvaluation!.Cer);
                Assert.Equal(2, selected.Epoch);
                Assert.Equal(2, selected.GlobalStep);
                Assert.Equal(1, selected.BestValidationCer);
            }
            else
            {
                Assert.Equal(3, run.EpochsCompleted);
                Assert.Equal(2, run.History.Count);
                Assert.Equal(priorBest, run.BestValidationCer);
                Assert.Equal(1, selected.Epoch);
                Assert.Equal(priorBest, selected.BestValidationCer);
            }
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
