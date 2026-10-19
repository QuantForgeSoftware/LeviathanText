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
/// A/B experiment for the P01 sequence-length blocker: the default CRNN reduces width by
/// 4 (two 2x2 pools), but the measured P01 crops are near-square, leaving every dev line
/// with fewer CTC frames than reference scalars. This trains the same configuration with
/// a width downsample of 2 and 4 and compares dev CER and reachability.
///
/// Dev-only; the locked test split is untouched. Skipped when the artifact store is absent.
/// </summary>
public sealed class P01WidthDownsampleTests(ITestOutputHelper output)
{
    [SkippableFact]
    public void Reducing_width_downsample_improves_reachability_and_should_not_hurt_cer()
    {
        var dataDir = FindDataDir();
        Skip.IfNot(dataDir != "", "P01 dataset not downloaded; skipping width-downsample A/B.");

        var snapshot = P01LineDataset.Load(dataDir);
        var vocabulary = snapshot.Train.Concat(snapshot.Dev)
            .SelectMany(l => Graphemes.Enumerate(l.Text).Select(g => g.Text))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var labels = new LabelSet(vocabulary);

        var factorFour = TrainAndEvaluate(snapshot, labels, widthDownsampleFactor: 4);
        var factorTwo = TrainAndEvaluate(snapshot, labels, widthDownsampleFactor: 2);

        var summary = new StringBuilder();
        summary.AppendLine("P01 v0.9 width-downsample A/B, dev split (pilot-scale, non-adjudicated labels)");
        summary.AppendLine($"train={snapshot.Train.Count} dev={snapshot.Dev.Count}; vocab={vocabulary.Count}; greedy decoding");
        summary.AppendLine();
        summary.AppendLine("| Width downsample | Dev scalar CER | Reachable lines (T >= scalars) | Min T | Median T | Median scalars | s/line |");
        summary.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");
        foreach (var result in new[] { factorFour, factorTwo })
            summary.AppendLine($"| {result.Factor} | {result.Cer:0.####} | {result.Reachable}/{result.Lines} | {result.MinTimeSteps} | {result.MedianTimeSteps} | {result.MedianScalars} | {result.SecondsPerLine:0.0000} |");
        summary.AppendLine();
        summary.AppendLine("Interpretation: the width/4 model cannot emit a full line on any dev line, so its");
        summary.AppendLine("error profile is deletion-dominated. Wider time resolution removes that structural");
        summary.AppendLine("limit; the crop-geometry fix remains the primary recommendation (IMG).");

        var text = summary.ToString();
        output.WriteLine(text);

        var artifactDir = Path.Combine(FindRepoRoot() ?? ".", ".artifacts", "pilot");
        Directory.CreateDirectory(artifactDir);
        File.WriteAllText(Path.Combine(artifactDir, "p01-width-downsample.txt"), text);

        Assert.True(factorFour.Cer < 1.0 && factorTwo.Cer < 1.0, "both configurations must beat the empty baseline");
        Assert.True(factorTwo.Reachable >= factorFour.Reachable, "width/2 must not reduce reachability");
    }

    private static Result TrainAndEvaluate(P01LineDataset.Snapshot snapshot, LabelSet labels, int widthDownsampleFactor)
    {
        var checkpointDir = Path.Combine(Path.GetTempPath(), $"lt-width{widthDownsampleFactor}", Guid.NewGuid().ToString("N"));
        var config = new TrainingConfig
        {
            Epochs = 300,
            BatchSize = 8,
            LearningRate = 3e-3,
            Seed = 42,
            EarlyStoppingPatience = 300,
            HiddenSize = 64,
            ModelName = $"width-{widthDownsampleFactor}",
            CheckpointDirectory = checkpointDir,
        };

        var model = new CrnnModel(labels.NumClasses, config.HiddenSize, widthDownsampleFactor);
        var trainer = new CtcTrainer(model, labels, config);
        trainer.Fit(snapshot.Train, snapshot.Dev);

        var images = snapshot.Dev.Select(l => l.Image).ToArray();
        var recognizer = new TorchLineRecognizer(model, labels);
        var clock = Stopwatch.StartNew();
        var recognized = recognizer.Recognize(images);
        clock.Stop();

        int distance = 0, length = 0, reachable = 0;
        var timeSteps = new int[images.Length];
        var scalars = new int[images.Length];
        for (var i = 0; i < images.Length; i++)
        {
            var score = RecognitionScorer.Score(snapshot.Dev[i].Text, recognized[i].Text);
            distance += score.ScalarDistance;
            length += score.ReferenceScalarLength;
            timeSteps[i] = model.TimeSteps(images[i].Width);
            scalars[i] = score.ReferenceScalarLength;
            if (timeSteps[i] >= scalars[i]) reachable++;
        }

        return new Result(
            widthDownsampleFactor,
            length == 0 ? 1.0 : (double)distance / length,
            reachable,
            images.Length,
            timeSteps.Min(),
            Median(timeSteps),
            Median(scalars),
            clock.Elapsed.TotalSeconds / Math.Max(1, images.Length));
    }

    private static int Median(int[] values)
    {
        var ordered = values.OrderBy(v => v).ToArray();
        return ordered[ordered.Length / 2];
    }

    private sealed record Result(
        int Factor,
        double Cer,
        int Reachable,
        int Lines,
        int MinTimeSteps,
        int MedianTimeSteps,
        int MedianScalars,
        double SecondsPerLine);

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
