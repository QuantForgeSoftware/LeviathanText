using LeviathanText.Contracts;
using LeviathanText.Core.Unicode;
using LeviathanText.Evaluation;
using LeviathanText.Recognition;
using LeviathanText.Training;
using Xunit;
using Xunit.Abstractions;

namespace LeviathanText.Pilot.Tests;

/// <summary>
/// Phase 0 gate increment: C# CTC training on REAL Greek papyrus lines (P01 v0.9).
/// Proves train → best-checkpoint → reload → decode on real diplomatic labels.
///
/// Honest scope: pilot-scale (321 lines from one documentary hand), labels not yet
/// expert-adjudicated. Numbers are pilot measurements, not release thresholds.
/// Skipped when the artifact store is absent.
/// </summary>
public sealed class P01RealTrainingTests(ITestOutputHelper output)
{
    [SkippableFact]
    public void TrainsOnRealLines_ThenReloadsAndDecodes()
    {
        var dataDir = FindDataDir();
        Skip.IfNot(dataDir != "", "P01 dataset not downloaded; skipping real-data gate increment.");

        var snapshot = P01LineDataset.Load(dataDir);
        Assert.True(snapshot.TotalLines > 200, $"expected pilot-scale train data, got {snapshot.TotalLines}");

        // Label set from train+dev only: test graphemes stay unseen (no leakage).
        var vocabulary = snapshot.Train.Concat(snapshot.Dev)
            .SelectMany(l => Graphemes.Enumerate(l.Text).Select(g => g.Text))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var labels = new LabelSet(vocabulary);

        var checkpointDir = Path.Combine(Path.GetTempPath(), "lt-p01-pilot", Guid.NewGuid().ToString("N"));
        var config = new TrainingConfig
        {
            Epochs = 300,
            BatchSize = 8,
            LearningRate = 3e-3,
            Seed = 42,
            EarlyStoppingPatience = 300,
            HiddenSize = 64,
            ModelName = "p01-pilot-crnn",
            CheckpointDirectory = checkpointDir,
        };

        var model = new CrnnModel(labels.NumClasses, config.HiddenSize);
        var trainer = new CtcTrainer(model, labels, config);
        var run = trainer.Fit(snapshot.Train, snapshot.Dev);

        // 1) Training progresses on real data (loss decreases).
        Assert.True(run.History[^1].TrainLoss < run.History[0].TrainLoss, "real-data training loss should decrease");

        // 2) The trained model beats the empty-output baseline (CER 1.0) on REAL labels:
        //    any value strictly below 1.0 proves real letters are being read.
        Assert.True(run.BestValidationCer < 1.0,
            $"validation CER {run.BestValidationCer} must beat the empty baseline; " +
            $"trainLoss[0]={run.History[0].TrainLoss:0.####}, trainLoss[-1]={run.History[^1].TrainLoss:0.####}, " +
            $"valLoss[0]={run.History[0].ValidationLoss:0.####}, valLoss[-1]={run.History[^1].ValidationLoss:0.####}, " +
            $"vocab={vocabulary.Count}");

        // 3) Reload parity: a fresh model loads the .best checkpoint and reproduces the
        //    validation score within a small tolerance.
        var fresh = new CrnnModel(labels.NumClasses, config.HiddenSize);
        var freshTrainer = new CtcTrainer(fresh, labels, config);
        freshTrainer.LoadCheckpoint(".best");
        var (_, reloadedCer) = freshTrainer.Evaluate(snapshot.Dev);
        Assert.True(Math.Abs(reloadedCer - run.BestValidationCer) < 0.05,
            $"reload parity failed: best {run.BestValidationCer} vs reloaded {reloadedCer}");

        // Record the pilot numbers (human evidence; not a threshold).
        var summary = $"P01 pilot v0.9: train={snapshot.Train.Count} dev={snapshot.Dev.Count} test={snapshot.TestLocked.Count} (locked); " +
                      $"epochs={run.History.Count}; trainLoss {run.History[0].TrainLoss:0.###}->{run.History[^1].TrainLoss:0.###}; " +
                      $"best dev CER={run.BestValidationCer:0.###}; reloaded dev CER={reloadedCer:0.###}; vocab={vocabulary.Count}";
        Directory.CreateDirectory(".artifacts/pilot");
        File.WriteAllText(".artifacts/pilot/p01-training-summary.txt", summary);
        output.WriteLine(summary);
    }

    private static string FindDataDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".artifacts", "datasets", "p01-v0.9")))
            dir = dir.Parent;
        return dir is null ? "" : Path.Combine(dir.FullName, ".artifacts", "datasets", "p01-v0.9");
    }
}
