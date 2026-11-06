using LeviathanText.TestSupport;
using LeviathanText.Contracts;
using LeviathanText.Recognition;
using LeviathanText.Training;
using Xunit;
using static TorchSharp.torch;

namespace LeviathanText.ML.Tests;

public class TensorLifetimeTests
{
    private static readonly LabelSet Labels = new(["α", "β"]);
    private static readonly LabeledLine[] Lines = [new("α", new LineImage(16, 32, new float[512]))];

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    [Fact]
    public void Repeated_recognition_releases_intermediates_without_collecting_or_disposing_caller_resources()
    {
        set_num_threads(1);
        using var outer = NewDisposeScope();
        using var model = new CrnnModel(Labels.NumClasses, hiddenSize: 8);
        using var callerTensor = ones(2);
        var recognizer = new TorchLineRecognizer(model, Labels);
        Collect();
        long baseline = Tensor.TotalCount;
        var expected = recognizer.Recognize([Lines[0].Image])[0];
        Assert.Equal(baseline, Tensor.TotalCount);
        for (int i = 0; i < 30; i++)
        {
            var actual = recognizer.Recognize([Lines[0].Image])[0];
            Assert.Equal(expected.Text, actual.Text);
            Assert.Equal(expected.RawIndices, actual.RawIndices);
            Assert.Equal(expected.Graphemes, actual.Graphemes);
            Assert.Equal(baseline, Tensor.TotalCount);
        }
        using var sum = callerTensor.sum();
        Assert.Equal(2, sum.item<float>());
        Assert.True(model.training);
    }

    [Fact]
    public void Evaluation_releases_each_batch_and_restores_the_callers_mode()
    {
        set_num_threads(1);
        using var outer = NewDisposeScope();
        using var model = new CrnnModel(Labels.NumClasses, hiddenSize: 8);
        var trainer = new CtcTrainer(model, Labels, new TrainingConfig { BatchSize = 1 });
        Collect();
        long baseline = Tensor.TotalCount;
        var expected = trainer.EvaluateDetailed(Lines);
        Assert.Equal(baseline, Tensor.TotalCount);
        for (int i = 0; i < 5; i++)
        {
            var result = trainer.EvaluateDetailed([Lines[0], Lines[0]]);
            Assert.Equal(expected.Loss, result.Loss);
            Assert.Equal(expected.Cer, result.Cer);
            Assert.Equal(baseline, Tensor.TotalCount);
            Assert.True(model.training);
        }
    }

    [CapabilityTheory(TestCapabilities.Fitting)]
    [InlineData(false)]
    [InlineData(true)]
    public void Collation_failure_releases_already_allocated_inputs_and_restores_mode(bool training)
    {
        set_num_threads(1);
        using var outer = NewDisposeScope();
        using var model = new CrnnModel(Labels.NumClasses, hiddenSize: 8);
        var trainer = new CtcTrainer(model, Labels, new TrainingConfig());
        model.train(training);
        Collect();
        long baseline = Tensor.TotalCount;
        var unknown = new[] { Lines[0] with { Text = "γ" } };
        Assert.Throws<LeviathanException>(() => trainer.TrainEpoch(unknown, 1));
        Assert.Equal(baseline, Tensor.TotalCount);
        Assert.Equal(training, model.training);
        Assert.Throws<LeviathanException>(() => trainer.EvaluateDetailed(unknown));
        Assert.Equal(baseline, Tensor.TotalCount);
        Assert.Equal(training, model.training);
        Assert.True(double.IsFinite(trainer.Evaluate(Lines).Loss));
    }

    [CapabilityFact(TestCapabilities.Fitting)]
    public void Scoped_training_has_stable_live_aliases_and_preserves_reference_adam_and_resume_parameters()
    {
        set_num_threads(1);
        using var outer = NewDisposeScope();
        string directory = Path.Combine(Path.GetTempPath(), "lt-lifetime", Guid.NewGuid().ToString("N"));
        var config = new TrainingConfig { BatchSize = 1, LearningRate = .003, HiddenSize = 8,
            ModelName = "lifetime", CheckpointDirectory = directory };
        try
        {
            manual_seed(171);
            using var model = new CrnnModel(Labels.NumClasses, hiddenSize: 8);
            var trainer = new CtcTrainer(model, Labels, config);
            var losses = new List<double> { trainer.TrainEpoch(Lines, 1) };
            Collect();
            long warmed = Tensor.TotalCount;
            for (int i = 2; i <= 3; i++)
            {
                losses.Add(trainer.TrainEpoch(Lines, i));
                Assert.Equal(warmed, Tensor.TotalCount);
            }
            trainer.SaveCheckpoint(isBest: false);
            using var resumedModel = new CrnnModel(Labels.NumClasses, hiddenSize: 8);
            var resumed = new CtcTrainer(resumedModel, Labels, config);
            resumed.LoadCheckpoint();
            Assert.Equal(trainer.TrainEpoch(Lines, 4), resumed.TrainEpoch(Lines, 4));
            Assert.Equal(Weights(model), Weights(resumedModel));

            manual_seed(171);
            using var reference = new CrnnModel(Labels.NumClasses, hiddenSize: 8);
            using var adam = optim.Adam(reference.parameters(), lr: config.LearningRate);
            for (int i = 0; i < 4; i++)
            {
                // Direct loss/backward/Adam reference protects the training arithmetic.
                using var scope = NewDisposeScope();
                var batch = VariableWidthCollator.Collate(Lines, Labels, reference.WidthDownsampleFactor, reference.InputHeightPixels);
                adam.zero_grad();
                var logits = reference.forward(batch.Images);
                var loss = nn.functional.ctc_loss(logits, batch.Targets, batch.InputLengths, batch.TargetLengths,
                    blank: LabelSet.BlankIndex, zero_infinity: true);
                loss.backward();
                adam.step();
                if (i < losses.Count) Assert.Equal(losses[i], loss.item<float>());
            }
            Assert.Equal(Weights(reference), Weights(model));
            Assert.Equal(4, trainer.GlobalStep);
            Assert.True(model.training);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static float[] Weights(CrnnModel model) => model.parameters().SelectMany(p => p.data<float>().ToArray()).ToArray();
}
