using System.Diagnostics;
using System.Text;
using LeviathanText.Core.Unicode;
using LeviathanText.Evaluation;
using LeviathanText.Recognition;
using LeviathanText.Training;
using Xunit;
using Xunit.Abstractions;

namespace LeviathanText.ML.Tests;

/// <summary>
/// Learning-rate A/B on the best measured P01 configuration (height 16, width 4,
/// augmentation on, weight decay 0, dropout 0). Dev-only; locked test untouched.
/// </summary>
public sealed class P01LearningRateTests(ITestOutputHelper output)
{
    [SkippableFact]
    public void Compare_learning_rates()
    {
        var dataDir = FindDataDir();
        Skip.IfNot(dataDir != "", "P01 dataset not downloaded; skipping learning-rate A/B.");

        var snapshot = P01LineDataset.Load(dataDir, targetHeight: 16);
        var vocabulary = snapshot.Train.Concat(snapshot.Dev)
            .SelectMany(l => Graphemes.Enumerate(l.Text).Select(g => g.Text))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var labels = new LabelSet(vocabulary);

        var results = new[] { 1e-3, 3e-3, 5e-3 }
            .Select(learningRate => TrainAndEvaluate(snapshot, labels, learningRate))
            .ToArray();

        var summary = new StringBuilder();
        summary.AppendLine("P01 v0.9 learning-rate A/B, dev split (pilot-scale, non-adjudicated labels)");
        summary.AppendLine($"train={snapshot.Train.Count} dev={snapshot.Dev.Count}; height 16, width 4, augmentation on, wd 0, dropout 0, greedy decoding");
        summary.AppendLine();
        summary.AppendLine("| Learning rate | Dev scalar CER | Best dev CER during fit | Perfect lines | s/line |");
        summary.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var result in results)
            summary.AppendLine($"| {result.LearningRate:0.####} | {result.Cer:0.####} | {result.BestValidationCer:0.####} | {result.Perfect}/{result.Lines} | {result.SecondsPerLine:0.0000} |");
        summary.AppendLine();
        summary.AppendLine("Adam, 300 epochs, seed 42, same augmentation. Dev-only selection input.");

        var text = summary.ToString();
        output.WriteLine(text);

        var artifactDir = Path.Combine(FindRepoRoot() ?? ".", ".artifacts", "pilot");
        Directory.CreateDirectory(artifactDir);
        File.WriteAllText(Path.Combine(artifactDir, "p01-learning-rate.txt"), text);

        Assert.All(results, r => Assert.True(r.Cer < 1.0, "every run must beat the empty baseline"));
    }

    private static Result TrainAndEvaluate(P01LineDataset.Snapshot snapshot, LabelSet labels, double learningRate)
    {
        var checkpointDir = Path.Combine(Path.GetTempPath(), $"lt-lr-{learningRate}", Guid.NewGuid().ToString("N"));
        var config = new TrainingConfig
        {
            Epochs = 300,
            BatchSize = 8,
            LearningRate = learningRate,
            Seed = 42,
            EarlyStoppingPatience = 300,
            HiddenSize = 64,
            ModelName = $"lr-{learningRate}",
            CheckpointDirectory = checkpointDir,
            AugmentTraining = true,
            MaxHorizontalStretch = 0.1,
            MaxVerticalShiftPx = 1,
            SaltPepperFraction = 0.01,
        };

        var model = new CrnnModel(labels.NumClasses, config.HiddenSize, widthDownsampleFactor: 4, inputHeight: 16);
        var trainer = new CtcTrainer(model, labels, config);
        var run = trainer.Fit(snapshot.Train, snapshot.Dev);
        trainer.RestoreBest();

        var images = snapshot.Dev.Select(l => l.Image).ToArray();
        var recognizer = new TorchLineRecognizer(model, labels);
        var clock = Stopwatch.StartNew();
        var recognized = recognizer.Recognize(images);
        clock.Stop();

        int distance = 0, length = 0, perfect = 0;
        for (var i = 0; i < images.Length; i++)
        {
            var score = RecognitionScorer.Score(snapshot.Dev[i].Text, recognized[i].Text);
            distance += score.ScalarDistance;
            length += score.ReferenceScalarLength;
            if (score.ScalarDistance == 0) perfect++;
        }

        return new Result(learningRate, length == 0 ? 1.0 : (double)distance / length, run.BestValidationCer, perfect, images.Length, clock.Elapsed.TotalSeconds / Math.Max(1, images.Length));
    }

    private sealed record Result(double LearningRate, double Cer, double BestValidationCer, int Perfect, int Lines, double SecondsPerLine);

    private static string FindDataDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".artifacts", "datasets", "p01-v0.9")))
            dir = dir.Parent;
        return dir is null ? "" : Path.Combine(dir.FullName, ".artifacts", "datasets", "p01-v0.9");
    }

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LeviathanText.slnx")))
            dir = dir.Parent;
        return dir?.FullName;
    }
}
