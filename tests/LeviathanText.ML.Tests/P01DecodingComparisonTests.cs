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
/// Decoding-strategy measurement (todo §5: greedy vs beam is chosen by measurement, not
/// assumption). Trains the pilot CRNN on the P01 train split, then scores the **dev**
/// split with greedy and several beam widths. The locked test split is never touched.
///
/// Pilot-scale and non-adjudicated: this selects a decoder, it does not claim a threshold.
/// Skipped when the artifact store is absent.
/// </summary>
public sealed class P01DecodingComparisonTests(ITestOutputHelper output)
{
    [SkippableFact]
    public void Compare_greedy_and_beam_on_the_dev_split()
    {
        var dataDir = FindDataDir();
        Skip.IfNot(dataDir != "", "P01 dataset not downloaded; skipping decoding comparison.");

        var snapshot = P01LineDataset.Load(dataDir);
        Assert.True(snapshot.Dev.Count > 0, "dev split must exist");

        var vocabulary = snapshot.Train.Concat(snapshot.Dev)
            .SelectMany(l => Graphemes.Enumerate(l.Text).Select(g => g.Text))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var labels = new LabelSet(vocabulary);

        var checkpointDir = Path.Combine(Path.GetTempPath(), "lt-decode", Guid.NewGuid().ToString("N"));
        var config = new TrainingConfig
        {
            Epochs = 300,
            BatchSize = 8,
            LearningRate = 3e-3,
            Seed = 42,
            EarlyStoppingPatience = 300,
            HiddenSize = 64,
            ModelName = "decode-comparison-crnn",
            CheckpointDirectory = checkpointDir,
        };

        var model = new CrnnModel(labels.NumClasses, config.HiddenSize);
        var trainer = new CtcTrainer(model, labels, config);
        var run = trainer.Fit(snapshot.Train, snapshot.Dev);

        var images = snapshot.Dev.Select(l => l.Image).ToArray();
        var references = snapshot.Dev.Select(l => l.Text).ToArray();

        var candidateWidths = new[] { 2, 4, 8 };
        var results = new List<(string Mode, double Cer, double SecondsPerLine, int Perfect)>
        {
            Measure("greedy", new TorchLineRecognizer(model, labels), images, references),
        };
        foreach (var width in candidateWidths)
            results.Add(Measure($"beam-{width}", new TorchLineRecognizer(model, labels, CtcDecodeMode.PrefixBeam, width), images, references));

        var summary = new StringBuilder();
        summary.AppendLine("P01 v0.9 decoding comparison, dev split (pilot-scale, non-adjudicated labels)");
        summary.AppendLine($"train={snapshot.Train.Count} dev={snapshot.Dev.Count}; trainLoss {run.History[0].TrainLoss:0.###}->{run.History[^1].TrainLoss:0.###}; best dev CER={run.BestValidationCer:0.###}; vocab={vocabulary.Count}");
        summary.AppendLine();
        summary.AppendLine("| Decoder | Dev scalar CER | Perfect lines | s/line |");
        summary.AppendLine("| --- | --- | --- | --- |");
        foreach (var result in results)
            summary.AppendLine($"| {result.Mode} | {result.Cer:0.####} | {result.Perfect}/{references.Length} | {result.SecondsPerLine:0.####} |");

        var best = results.OrderBy(r => r.Cer).ThenBy(r => r.SecondsPerLine).First();
        summary.AppendLine();
        summary.AppendLine($"Best measured dev CER: {best.Mode} at {best.Cer:0.####}. Selection is dev-only; the locked test split stays untouched.");

        // Selective prediction on the default (greedy) decoder: line confidence is the
        // minimum per-grapheme probability, so a single shaky grapheme lowers the line.
        var greedyResults = new TorchLineRecognizer(model, labels).Recognize(images);
        var selectiveSamples = new List<SelectiveSample>(greedyResults.Count);
        for (var i = 0; i < greedyResults.Count; i++)
        {
            var score = RecognitionScorer.Score(references[i], greedyResults[i].Text);
            var confidence = greedyResults[i].Graphemes.Count == 0
                ? 0.0
                : greedyResults[i].Graphemes.Min(g => g.Confidence.Value);
            selectiveSamples.Add(new SelectiveSample(confidence, score.ReferenceScalarLength, score.ScalarDistance));
        }

        var selective = SelectivePredictionScorer.Selective(selectiveSamples);
        summary.AppendLine();
        summary.AppendLine("## Selective prediction (greedy, min per-grapheme line confidence)");
        summary.AppendLine();
        summary.AppendLine($"- Full-coverage dev CER: {selective.FullCoverageCer:0.####}");
        summary.AppendLine($"- Area under the risk-coverage curve (rectangle rule, lower is better): {selective.AreaUnderRiskCoverageCurve:0.####}");
        summary.AppendLine($"- Text coverage with CER <= 0.80: {selective.CoverageForCerBelow(0.80):0.###}");
        summary.AppendLine($"- Text coverage with CER <= 0.75: {selective.CoverageForCerBelow(0.75):0.###}");
        summary.AppendLine("- Pilot-scale: confidence is a raw model probability, not a scholarly judgment; no threshold is set here.");

        var text = summary.ToString();
        output.WriteLine(text);

        var artifactDir = Path.Combine(FindRepoRoot() ?? ".", ".artifacts", "pilot");
        Directory.CreateDirectory(artifactDir);
        File.WriteAllText(Path.Combine(artifactDir, "p01-decoding-comparison.txt"), text);

        Assert.All(results, r => Assert.True(r.Cer < 1.0, $"{r.Mode} CER {r.Cer:0.###} must beat the empty baseline"));
        Assert.Contains(results, r => r.Mode == "greedy");
    }

    private static (string Mode, double Cer, double SecondsPerLine, int Perfect) Measure(
        string mode,
        TorchLineRecognizer recognizer,
        LineImage[] images,
        string[] references)
    {
        var clock = Stopwatch.StartNew();
        var recognized = recognizer.Recognize(images);
        clock.Stop();

        int distance = 0, length = 0, perfect = 0;
        for (var i = 0; i < references.Length; i++)
        {
            var score = RecognitionScorer.Score(references[i], recognized[i].Text);
            distance += score.ScalarDistance;
            length += score.ReferenceScalarLength;
            if (score.ScalarDistance == 0) perfect++;
        }

        var cer = length == 0 ? 1.0 : (double)distance / length;
        return (mode, cer, clock.Elapsed.TotalSeconds / Math.Max(1, images.Length), perfect);
    }

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
