using LeviathanText.TestSupport;
using System.Text.Json;
using LeviathanText.Recognition;
using LeviathanText.Training;
using SkiaSharp;
using Xunit;
using static TorchSharp.torch;

namespace LeviathanText.ML.Tests;

public class TrainingDiagnosticsTests
{
    private static readonly string[] Vocabulary = ["α", "β"];

    private static CrnnModel ZeroModel(LabelSet labels)
    {
        var model = new CrnnModel(labels.NumClasses, hiddenSize: 8);
        using var scope = no_grad();
        foreach (var parameter in model.parameters()) parameter.zero_();
        return model;
    }

    private static LabeledLine Line(string text, int width) => new(text,
        new LineImage(16, width, Enumerable.Repeat(1f, 16 * width).ToArray(), backgroundValue: 1));

    [Fact]
    public void Detailed_evaluation_counts_canonical_scalars_and_only_valid_frames()
    {
        set_num_threads(1);
        var labels = new LabelSet(["α\u0301", "β"]);
        using var model = ZeroModel(labels);
        var trainer = new CtcTrainer(model, labels, new TrainingConfig { BatchSize = 2, DiagnosticsSampleCount = 1 });
        var result = trainer.EvaluateDetailed([Line("α\u0301", 32), Line("ββ", 48)]);
        var d = result.Diagnostics;
        Assert.Equal(2, d.Lines);
        Assert.Equal(3, d.ReferenceScalars); // NFC ά is one scalar, not two NFD scalars
        Assert.Equal(0, d.PredictionScalars);
        Assert.Equal(3, d.Deletions);
        Assert.Equal(0, d.Insertions);
        Assert.Equal(0, d.Substitutions);
        Assert.Equal(1, result.Cer);
        Assert.Equal(0, d.PredictionToReferenceRatio);
        Assert.Equal(20, d.ValidFrames); // 8 + 12, not 12 + 12 padded steps
        Assert.Equal(20, d.BlankArgmaxFrames);
        Assert.Equal(1, d.BlankArgmaxFraction);
        Assert.InRange(d.MeanBlankPosterior, 1d / 3 - 1e-6, 1d / 3 + 1e-6);
        var sample = Assert.Single(d.Samples);
        Assert.Equal(new TrainingPredictionSample(0, "α\u0301", ""), sample);
        Assert.Equal(trainer.Evaluate([Line("α\u0301", 32), Line("ββ", 48)]).Cer, result.Cer);
        Assert.True(model.training);
    }

    [Fact]
    public void Detailed_evaluation_attributes_substitutions_deletions_and_insertions()
    {
        var labels = new LabelSet(Vocabulary);
        using var model = ZeroModel(labels);
        using (var scope = no_grad())
        {
            var bias = model.named_parameters().Single(p => p.name == "_fc.bias").parameter;
            using var beta = bias[2];
            beta.fill_(10);
        }
        var trainer = new CtcTrainer(model, labels, new TrainingConfig { DiagnosticsSampleCount = 0 });
        var d = trainer.EvaluateDetailed([Line("α", 16), Line("ββ", 32), Line("", 16)]).Diagnostics;
        Assert.Equal(1, d.Substitutions);
        Assert.Equal(1, d.Deletions);
        Assert.Equal(1, d.Insertions);
        Assert.Equal(3, d.PredictionScalars);
        Assert.Equal(3, d.ScalarDistance);
        Assert.Empty(d.Samples);
        Assert.Equal(1, trainer.EvaluateDetailed([Line("", 16)]).Cer);
    }

    [Fact]
    public void Empty_diagnostics_are_finite_and_old_history_can_be_read()
    {
        var labels = new LabelSet(Vocabulary);
        using var model = ZeroModel(labels);
        var trainer = new CtcTrainer(model, labels, new TrainingConfig());
        var result = trainer.EvaluateDetailed([]);
        Assert.Equal(0, result.Loss);
        Assert.Equal(0, result.Cer);
        Assert.Equal(0, result.Diagnostics.BlankArgmaxFraction);
        Assert.Equal(0, result.Diagnostics.MeanBlankPosterior);
        Assert.Null(result.Diagnostics.PredictionToReferenceRatio);
        var old = JsonSerializer.Deserialize<EpochRecord>("{\"Epoch\":1,\"TrainLoss\":2,\"ValidationLoss\":3,\"ValidationCer\":1}")!;
        Assert.Null(old.TrainingEvaluation);
        Assert.Null(old.ValidationEvaluation);
    }

    [CapabilityFact(TestCapabilities.Fitting)]
    public void Interval_final_epoch_resume_and_manifest_preserve_diagnostics()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lt-diagnostics", Guid.NewGuid().ToString("N"));
        try
        {
            var labels = new LabelSet(Vocabulary);
            using var model = ZeroModel(labels);
            var config = new TrainingConfig { Epochs = 3, BatchSize = 2, LearningRate = 0, HiddenSize = 8,
                EarlyStoppingPatience = 10, DiagnosticsEveryNEpochs = 2, DiagnosticsSampleCount = 1,
                ModelName = "telemetry", CheckpointDirectory = directory };
            var lines = new[] { Line("α", 16), Line("ββ", 32) };
            var trainer = new CtcTrainer(model, labels, config);
            var run = trainer.Fit(lines, lines);
            Assert.Null(run.History[0].TrainingEvaluation);
            Assert.NotNull(run.History[1].TrainingEvaluation);
            Assert.NotNull(run.History[2].ValidationEvaluation); // final even off interval
            Assert.Equal(new long[] { 1, 2, 3 }, run.History.Select(h => h.GlobalStep));
            var manifest = TrainingRunManifestWriter.Create(model, labels, config, run, 2, 2, 0, "split");
            var path = TrainingRunManifestWriter.Write(directory, manifest);
            var loaded = TrainingRunManifestWriter.Read(path);
            Assert.Equal(2, loaded.DiagnosticsEveryNEpochs);
            Assert.Equal(1, loaded.DiagnosticsSampleCount);
            var evaluation = loaded.History[2].ValidationEvaluation!;
            Assert.Equal(3, evaluation.Diagnostics.Deletions);
            Assert.Equal(12, evaluation.Diagnostics.ValidFrames);
            Assert.Equal(new TrainingPredictionSample(0, "α", ""), Assert.Single(evaluation.Diagnostics.Samples));
            using var resumedModel = ZeroModel(labels);
            var resumed = new CtcTrainer(resumedModel, labels, config with { Epochs = 5 });
            resumed.LoadCheckpoint();
            var second = resumed.Fit(lines, lines);
            Assert.Equal(3, second.StartEpoch);
            Assert.Equal(new long[] { 4, 5 }, second.History.Select(h => h.GlobalStep));
            Assert.All(second.History, h => Assert.NotNull(h.TrainingEvaluation));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [CapabilityFact(TestCapabilities.Fitting)]
    public void Early_stopped_final_epoch_is_measured_even_off_interval()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lt-diagnostics", Guid.NewGuid().ToString("N"));
        try
        {
            var labels = new LabelSet(Vocabulary);
            using var model = ZeroModel(labels);
            var trainer = new CtcTrainer(model, labels, new TrainingConfig { Epochs = 10, LearningRate = 0,
                EarlyStoppingPatience = 1, DiagnosticsEveryNEpochs = 5, ModelName = "early", CheckpointDirectory = directory });
            var run = trainer.Fit([Line("α", 16)], [Line("β", 16)]);
            Assert.Equal(2, run.EpochsCompleted);
            Assert.Null(run.History[0].ValidationEvaluation);
            Assert.NotNull(run.History[1].ValidationEvaluation);
            Assert.NotNull(run.History[1].TrainingEvaluation);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [CapabilityFact(TestCapabilities.Fitting)]
    public void Enabling_telemetry_does_not_change_dropout_training_or_checkpoint_selection()
    {
        set_num_threads(1);
        var directory = Path.Combine(Path.GetTempPath(), "lt-diagnostics", Guid.NewGuid().ToString("N"));
        try
        {
            var labels = new LabelSet(Vocabulary);
            var lines = SyntheticLineDataset.Generate(4, Vocabulary, seed: 6);
            TrainingRunResult Run(int interval)
            {
                manual_seed(58);
                using var model = new CrnnModel(labels.NumClasses, hiddenSize: 8, dropout: .7);
                var config = new TrainingConfig { Epochs = 3, BatchSize = 2, DiagnosticsEveryNEpochs = interval,
                    ModelName = "toggle", CheckpointDirectory = Path.Combine(directory, interval.ToString()) };
                return new CtcTrainer(model, labels, config).Fit(lines, lines);
            }
            var off = Run(0);
            var on = Run(1);
            Assert.All(off.History, h => Assert.Null(h.ValidationEvaluation));
            Assert.Equal(off.History.Select(h => (h.TrainLoss, h.ValidationLoss, h.ValidationCer)),
                on.History.Select(h => (h.TrainLoss, h.ValidationLoss, h.ValidationCer)));
            Assert.Equal(off.BestValidationCer, on.BestValidationCer);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void Luminance_transform_declares_white_background_without_inverting_pixels()
    {
        using var bitmap = new SKBitmap(16, 16);
        bitmap.Erase(SKColors.White);
        bitmap.SetPixel(8, 8, SKColors.Black);
        var image = LineInputTransform.Apply(bitmap).ToLineImage();
        Assert.Equal(1, image.BackgroundValue);
        Assert.Equal(1, image[0, 0]);
        Assert.Equal(0, image[8, 8]);
    }

    [Fact]
    public void Invalid_telemetry_settings_fail_and_observability_does_not_change_model_hash()
    {
        var labels = new LabelSet(Vocabulary);
        using var model = ZeroModel(labels);
        Assert.Throws<ArgumentOutOfRangeException>(() => new CtcTrainer(model, labels, new() { DiagnosticsEveryNEpochs = -1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CtcTrainer(model, labels, new() { DiagnosticsSampleCount = 101 }));
        Assert.Equal(new TrainingConfig().ComputeHash(), new TrainingConfig { DiagnosticsEveryNEpochs = 2, DiagnosticsSampleCount = 0 }.ComputeHash());
    }
}
