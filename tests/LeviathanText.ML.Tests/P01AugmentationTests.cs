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
/// Does on-the-fly augmentation help the data-limited P01 pilot? Same architecture
/// (height 32, width downsample 4 = the best measured base), same seed, dev-only scoring.
/// Negative results are recorded too; the locked test split is untouched.
/// </summary>
public sealed class P01AugmentationTests(ITestOutputHelper output)
{
    [SkippableFact]
    public void Compare_training_with_and_without_augmentation()
    {
        var dataDir = FindDataDir();
        Skip.IfNot(dataDir != "", "P01 dataset not downloaded; skipping augmentation A/B.");

        var snapshot = P01LineDataset.Load(dataDir, targetHeight: 16);
        var snapshot32 = P01LineDataset.Load(dataDir, targetHeight: 32);
        var vocabulary = snapshot.Train.Concat(snapshot.Dev)
            .SelectMany(l => Graphemes.Enumerate(l.Text).Select(g => g.Text))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var labels = new LabelSet(vocabulary);

        var artifactDir = Path.Combine(FindRepoRoot() ?? ".", ".artifacts", "pilot");
        Directory.CreateDirectory(artifactDir);
        var splitHash = TrainingRunManifestWriter.SplitHash(snapshot.WitnessSplitMap.Select(kv => $"{kv.Key}={kv.Value}"));

        var baseline = TrainAndEvaluate(snapshot, labels, augment: false);
        var augmented = TrainAndEvaluate(snapshot, labels, augment: true,
            packageDirectory: Path.Combine(artifactDir, "model-p01-h16-aug"),
            splitHash: splitHash,
            testLockedLines: snapshot.TestLocked.Count);
        var augmented32 = TrainAndEvaluate(snapshot32, labels, augment: true);

        var summary = new StringBuilder();
        summary.AppendLine("P01 v0.9 training-augmentation A/B, dev split (pilot-scale, non-adjudicated labels)");
        summary.AppendLine($"train={snapshot.Train.Count} dev={snapshot.Dev.Count}; height 16 vs 32, width downsample 4 (post IMG padding fix), greedy decoding");
        summary.AppendLine();
        summary.AppendLine("| Input height | Training | Dev scalar CER | Best dev CER during fit | Perfect lines | s/line |");
        summary.AppendLine("| --- | --- | --- | --- | --- | --- |");
        summary.AppendLine($"| 16 | plain | {baseline.Cer:0.####} | {baseline.BestValidationCer:0.####} | {baseline.Perfect}/{baseline.Lines} | {baseline.SecondsPerLine:0.0000} |");
        summary.AppendLine($"| 16 | augmented | {augmented.Cer:0.####} | {augmented.BestValidationCer:0.####} | {augmented.Perfect}/{augmented.Lines} | {augmented.SecondsPerLine:0.0000} |");
        summary.AppendLine($"| 32 | augmented | {augmented32.Cer:0.####} | {augmented32.BestValidationCer:0.####} | {augmented32.Perfect}/{augmented32.Lines} | {augmented32.SecondsPerLine:0.0000} |");
        summary.AppendLine();
        summary.AppendLine("Augmentation = stretch ±10%, vertical shift ±1 px, 1% salt/pepper, training only.");
        summary.AppendLine();
        summary.AppendLine($"Packaged candidate: `{Path.Combine(artifactDir, "model-p01-h16-aug")}` (weights, metadata, manifest, model card).");
        summary.AppendLine();
        summary.AppendLine("Augmentation is training-only; validation images are never transformed. The comparison is");
        summary.AppendLine("dev-only, so either direction is a selection input, not a release claim.");

        var text = summary.ToString();
        output.WriteLine(text);

        File.WriteAllText(Path.Combine(artifactDir, "p01-augmentation.txt"), text);

        Assert.All(new[] { baseline, augmented, augmented32 }, r => Assert.True(r.Cer < 1.0, "every run must beat the empty baseline"));
    }

    private static Result TrainAndEvaluate(
        P01LineDataset.Snapshot snapshot,
        LabelSet labels,
        bool augment,
        string? packageDirectory = null,
        string? splitHash = null,
        int testLockedLines = 0)
    {
        var checkpointDir = Path.Combine(Path.GetTempPath(), $"lt-aug-{augment}", Guid.NewGuid().ToString("N"));
        var config = new TrainingConfig
        {
            Epochs = 300,
            BatchSize = 8,
            LearningRate = 3e-3,
            Seed = 42,
            EarlyStoppingPatience = 300,
            HiddenSize = 64,
            ModelName = augment ? $"h{snapshot.Dev[0].Image.Height}-aug" : $"h{snapshot.Dev[0].Image.Height}-plain",
            CheckpointDirectory = checkpointDir,
            AugmentTraining = augment,
            MaxHorizontalStretch = 0.1,
            MaxVerticalShiftPx = 1,
            SaltPepperFraction = 0.01,
        };

        var model = new CrnnModel(labels.NumClasses, config.HiddenSize, widthDownsampleFactor: 4, inputHeight: snapshot.Dev[0].Image.Height);
        var trainer = new CtcTrainer(model, labels, config);
        var run = trainer.Fit(snapshot.Train, snapshot.Dev);
        trainer.RestoreBest(); // measure the selected checkpoint, not the last epoch

        if (packageDirectory is not null)
        {
            var manifest = TrainingRunManifestWriter.Create(
                model, labels, config, run,
                trainLines: snapshot.Train.Count,
                devLines: snapshot.Dev.Count,
                testLockedLines: testLockedLines,
                splitHash: splitHash ?? string.Empty);
            ModelPackageWriter.Write(
                packageDirectory, checkpointDir, config.ModelName, manifest,
                "Best measured P01 dev configuration (height 16, width 4, augmentation on). Pilot-scale; labels not adjudicated; not a release model.");
        }

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

        return new Result(
            length == 0 ? 1.0 : (double)distance / length,
            run.BestValidationCer,
            perfect,
            images.Length,
            clock.Elapsed.TotalSeconds / Math.Max(1, images.Length));
    }

    private sealed record Result(double Cer, double BestValidationCer, int Perfect, int Lines, double SecondsPerLine);

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
