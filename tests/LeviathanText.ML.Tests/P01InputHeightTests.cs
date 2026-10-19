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
/// Input-height A/B for the P01 sequence-length blocker. The mandated vertical padding
/// doubles crop height, so resizing that padded crop to height 16 leaves the text band
/// only ~8 px tall and halves the width (and CTC frames). Re-cropping at height 32
/// restores both text resolution and time steps.
///
/// Dev-only; the locked test split is untouched. Skipped when the artifact store is absent.
/// </summary>
public sealed class P01InputHeightTests(ITestOutputHelper output)
{
    [SkippableFact]
    public void Height_32_restores_text_and_time_resolution()
    {
        var dataDir = FindDataDir();
        Skip.IfNot(dataDir != "", "P01 dataset not downloaded; skipping input-height A/B.");

        var snapshot16 = P01LineDataset.Load(dataDir, targetHeight: 16);
        var snapshot32 = P01LineDataset.Load(dataDir, targetHeight: 32);

        var vocabulary = snapshot16.Train.Concat(snapshot16.Dev)
            .SelectMany(l => Graphemes.Enumerate(l.Text).Select(g => g.Text))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var labels = new LabelSet(vocabulary);

        var results = new[]
        {
            TrainAndEvaluate(snapshot16, labels, inputHeight: 16, widthDownsampleFactor: 4),
            TrainAndEvaluate(snapshot32, labels, inputHeight: 32, widthDownsampleFactor: 4),
            TrainAndEvaluate(snapshot32, labels, inputHeight: 32, widthDownsampleFactor: 2),
        };

        var summary = new StringBuilder();
        summary.AppendLine("P01 v0.9 input-height / width-downsample A/B, dev split (pilot-scale, non-adjudicated labels)");
        summary.AppendLine($"train={snapshot16.Train.Count} dev={snapshot16.Dev.Count}; vocab={vocabulary.Count}; greedy decoding");
        summary.AppendLine();
        summary.AppendLine("| Input height | Width downsample | Dev scalar CER | Reachable lines | Min T | Median T | Median width | s/line |");
        summary.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");
        foreach (var result in results)
            summary.AppendLine($"| {result.InputHeight} | {result.Factor} | {result.Cer:0.####} | {result.Reachable}/{result.Lines} | {result.MinTimeSteps} | {result.MedianTimeSteps} | {result.MedianWidth} | {result.SecondsPerLine:0.0000} |");
        summary.AppendLine();
        summary.AppendLine("The height-16 crop includes the mandatory vertical padding in its 16 rows;");
        summary.AppendLine("height 32 gives the text band ~16 rows and doubles the width-derived CTC frames.");
        summary.AppendLine("Crop geometry (padding fraction) remains an IMG-side decision; this is the ML-side measurement.");

        var text = summary.ToString();
        output.WriteLine(text);

        var artifactDir = Path.Combine(FindRepoRoot() ?? ".", ".artifacts", "pilot");
        Directory.CreateDirectory(artifactDir);
        File.WriteAllText(Path.Combine(artifactDir, "p01-input-height.txt"), text);

        Assert.All(results, r => Assert.True(r.Cer < 1.0, $"{r.InputHeight}/{r.Factor} must beat the empty baseline"));
        Assert.True(results[1].Reachable >= results[0].Reachable, "height 32 must not reduce reachability");
    }

    private static Result TrainAndEvaluate(P01LineDataset.Snapshot snapshot, LabelSet labels, int inputHeight, int widthDownsampleFactor)
    {
        var checkpointDir = Path.Combine(Path.GetTempPath(), $"lt-h{inputHeight}-w{widthDownsampleFactor}", Guid.NewGuid().ToString("N"));
        var config = new TrainingConfig
        {
            Epochs = 300,
            BatchSize = 8,
            LearningRate = 3e-3,
            Seed = 42,
            EarlyStoppingPatience = 300,
            HiddenSize = 64,
            ModelName = $"h{inputHeight}-w{widthDownsampleFactor}",
            CheckpointDirectory = checkpointDir,
        };

        var model = new CrnnModel(labels.NumClasses, config.HiddenSize, widthDownsampleFactor, inputHeight);
        new CtcTrainer(model, labels, config).Fit(snapshot.Train, snapshot.Dev);

        var images = snapshot.Dev.Select(l => l.Image).ToArray();
        var recognizer = new TorchLineRecognizer(model, labels);
        var clock = Stopwatch.StartNew();
        var recognized = recognizer.Recognize(images);
        clock.Stop();

        int distance = 0, length = 0, reachable = 0;
        var timeSteps = new int[images.Length];
        for (var i = 0; i < images.Length; i++)
        {
            var score = RecognitionScorer.Score(snapshot.Dev[i].Text, recognized[i].Text);
            distance += score.ScalarDistance;
            length += score.ReferenceScalarLength;
            timeSteps[i] = model.TimeSteps(images[i].Width);
            if (timeSteps[i] >= score.ReferenceScalarLength) reachable++;
        }

        return new Result(
            inputHeight,
            widthDownsampleFactor,
            length == 0 ? 1.0 : (double)distance / length,
            reachable,
            images.Length,
            timeSteps.Min(),
            Median(timeSteps),
            Median(images.Select(i => i.Width).ToArray()),
            clock.Elapsed.TotalSeconds / Math.Max(1, images.Length));
    }

    private static int Median(int[] values)
    {
        var ordered = values.OrderBy(v => v).ToArray();
        return ordered[ordered.Length / 2];
    }

    private sealed record Result(
        int InputHeight,
        int Factor,
        double Cer,
        int Reachable,
        int Lines,
        int MinTimeSteps,
        int MedianTimeSteps,
        int MedianWidth,
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
