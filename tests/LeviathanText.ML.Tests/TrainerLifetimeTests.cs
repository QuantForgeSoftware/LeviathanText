using LeviathanText.TestSupport;
using LeviathanText.Recognition;
using LeviathanText.Training;
using Xunit;
using static TorchSharp.torch;

namespace LeviathanText.ML.Tests;

public sealed class TrainerLifetimeTests
{
    private static readonly LabelSet Labels = new(["α", "β"]);
    private static readonly LabeledLine[] Lines = [new("α", new LineImage(16, 32, new float[512]))];

    // This also runs against the pre-fix public API: the missing lifetime route is
    // observed as retained optimizer state, rather than an unrelated compile error.
    private static void DisposeTrainer(CtcTrainer trainer)
    {
        if ((object)trainer is IDisposable disposable) disposable.Dispose();
    }

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    [CapabilityFact(TestCapabilities.Fitting)]
    public void Dispose_releases_optimizer_state_immediately_and_preserves_caller_resources()
    {
        set_num_threads(1);
        using var outer = NewDisposeScope();
        using var model = new CrnnModel(Labels.NumClasses, hiddenSize: 8);
        using var callerTensor = ones(2);
        model.eval();
        Collect();
        long baseline = Tensor.TotalCount;
        var trainer = new CtcTrainer(model, Labels, new TrainingConfig { BatchSize = 1 });
        trainer.TrainEpoch(Lines, 41);
        trainer.TrainEpoch(Lines, 42);
        Collect();
        Assert.True(Tensor.TotalCount > baseline); // Adam owns warmed moving averages.
        DisposeTrainer(trainer);
        Assert.Equal(baseline, Tensor.TotalCount); // No forced GC after disposal.
        DisposeTrainer(trainer);
        Assert.Equal(baseline, Tensor.TotalCount);
        Assert.False(model.training);
        var result = new TorchLineRecognizer(model, Labels).Recognize([Lines[0].Image]);
        Assert.Single(result);
        using var sum = callerTensor.sum();
        Assert.Equal(2, sum.item<float>());
        Assert.Equal(2, trainer.GlobalStep);
        GC.KeepAlive(trainer);
    }

    [CapabilityTheory(TestCapabilities.Fitting)]
    [InlineData("TrainEpoch")]
    [InlineData("Evaluate")]
    [InlineData("EvaluateDetailed")]
    [InlineData("Fit")]
    [InlineData("SaveCheckpoint")]
    [InlineData("LoadCheckpoint")]
    [InlineData("RestoreBest")]
    public void Disposed_operations_fail_before_native_state_or_checkpoint_mutation(string operation)
    {
        set_num_threads(1);
        string directory = Path.Combine(Path.GetTempPath(), "lt-trainer-lifetime", Guid.NewGuid().ToString("N"));
        using var outer = NewDisposeScope();
        using var model = new CrnnModel(Labels.NumClasses, hiddenSize: 8);
        var config = new TrainingConfig { Epochs = 1, BatchSize = 1, CheckpointDirectory = directory, ModelName = "lifetime" };
        var trainer = new CtcTrainer(model, Labels, config);
        try
        {
            trainer.Fit(Lines, Lines);
            model.eval();
            var checkpointBytes = Directory.GetFiles(directory).ToDictionary(p => Path.GetFileName(p)!, File.ReadAllBytes);
            long step = trainer.GlobalStep;
            DisposeTrainer(trainer);
            Action action = operation switch
            {
                "TrainEpoch" => () => trainer.TrainEpoch(Lines, 99),
                "Evaluate" => () => trainer.Evaluate(Lines),
                "EvaluateDetailed" => () => trainer.EvaluateDetailed(Lines),
                "Fit" => () => trainer.Fit(Lines, Lines),
                "SaveCheckpoint" => () => trainer.SaveCheckpoint(isBest: true),
                "LoadCheckpoint" => () => trainer.LoadCheckpoint(),
                _ => () => trainer.RestoreBest(),
            };
            Assert.Throws<ObjectDisposedException>(action);
            Assert.False(model.training);
            Assert.Equal(step, trainer.GlobalStep);
            Assert.Equal(checkpointBytes.Keys.Order(), Directory.GetFiles(directory).Select(Path.GetFileName).Order());
            foreach (var (file, bytes) in checkpointBytes) Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(directory, file!)));
            Assert.Single(new TorchLineRecognizer(model, Labels).Recognize([Lines[0].Image]));
        }
        finally
        {
            DisposeTrainer(trainer);
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [CapabilityFact(TestCapabilities.Fitting)]
    public void Disposed_Fit_does_not_create_its_output_directory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "lt-trainer-lifetime", Guid.NewGuid().ToString("N"));
        using var model = new CrnnModel(Labels.NumClasses, hiddenSize: 8);
        var trainer = new CtcTrainer(model, Labels, new TrainingConfig { Epochs = 0, CheckpointDirectory = directory });
        try
        {
            DisposeTrainer(trainer);
            Assert.Throws<ObjectDisposedException>(() => trainer.Fit([], []));
            Assert.False(Directory.Exists(directory));
        }
        finally
        {
            DisposeTrainer(trainer);
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [CapabilityFact(TestCapabilities.Fitting)]
    public void A_new_trainer_can_resume_on_the_callers_model_after_old_trainer_disposal()
    {
        set_num_threads(1);
        string directory = Path.Combine(Path.GetTempPath(), "lt-trainer-lifetime", Guid.NewGuid().ToString("N"));
        using var outer = NewDisposeScope();
        manual_seed(1729);
        using var model = new CrnnModel(Labels.NumClasses, hiddenSize: 8);
        var config = new TrainingConfig { Epochs = 1, BatchSize = 1, CheckpointDirectory = directory, ModelName = "resume" };
        var initial = new CtcTrainer(model, Labels, config);
        try
        {
            initial.Fit(Lines, Lines);
            using var referenceModel = new CrnnModel(Labels.NumClasses, hiddenSize: 8);
            var reference = new CtcTrainer(referenceModel, Labels, config);
            try
            {
                reference.LoadCheckpoint();
                double expectedLoss = reference.TrainEpoch(Lines, 102);
                var expectedWeights = referenceModel.parameters().SelectMany(p => p.data<float>().ToArray()).ToArray();
                DisposeTrainer(initial);
                var resumed = new CtcTrainer(model, Labels, config);
                try
                {
                    resumed.LoadCheckpoint();
                    Assert.Equal(expectedLoss, resumed.TrainEpoch(Lines, 102));
                    Assert.Equal(expectedWeights, model.parameters().SelectMany(p => p.data<float>().ToArray()).ToArray());
                    Assert.Equal(reference.GlobalStep, resumed.GlobalStep);
                }
                finally { DisposeTrainer(resumed); }
            }
            finally { DisposeTrainer(reference); }
        }
        finally
        {
            DisposeTrainer(initial);
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
