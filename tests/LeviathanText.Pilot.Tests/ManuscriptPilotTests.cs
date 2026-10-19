using LeviathanText.Contracts;
using LeviathanText.Core.Unicode;
using LeviathanText.Evaluation;
using LeviathanText.Recognition;
using LeviathanText.Training;
using Xunit;
using Xunit.Abstractions;

namespace LeviathanText.Pilot.Tests;

/// <summary>
/// MAN-02a pilot: the manuscript track's first real-data feasibility increment.
/// M02a (Stavronikita 53, Zenodo 5595669 v1.0, CC BY 4.0) — 54 pages, 1038 real lines
/// (text assembled from the Transkribus Word layer where the line has no own TextEquiv).
///
/// Label-policy audit findings (recorded in data/manifests/manuscripts/m02a.json):
/// some labels carry literal editorial markup (<+>) or apparent expansion-lite forms
/// (ἰῦ, χῦ) that require Greek-specialist review. Filtered here: any label containing
/// markup characters is excluded from training labels — editorial interventions must
/// not become visible-text labels. Split is page-level within the single codex
/// (cross-hand generalization unknown; publisher's line-level .lst splits NOT used).
/// </summary>
public sealed class ManuscriptPilotTests(ITestOutputHelper output)
{
    private static string FindDataDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".artifacts", "datasets", "m02a-v1.0", "m02a")))
            dir = dir.Parent;
        return dir is null ? "" : Path.Combine(dir.FullName, ".artifacts", "datasets", "m02a-v1.0", "m02a");
    }

    private static bool MarkupContaminated(string text) =>
        text.Contains('<') || text.Contains('>') || text.Contains('&') || text.Contains('[') || text.Contains(']');

    [SkippableFact]
    public void Ingest_Train_AndScore_OnPageHoldout()
    {
        var dataDir = FindDataDir();
        Skip.IfNot(dataDir != "", "M02a dataset not downloaded.");

        var xmlDir = Path.Combine(dataDir, "xml");
        var snapshot = P01LineDataset.Load(xmlDir, imageExtension: ".png");

        // Filter markup-contaminated labels out of ALL splits (they stay excluded, not hidden).
        var train = snapshot.Train.Where(l => !MarkupContaminated(l.Text)).ToList();
        var dev = snapshot.Dev.Where(l => !MarkupContaminated(l.Text)).ToList();
        var test = snapshot.TestLocked.Where(l => !MarkupContaminated(l.Text)).ToList();
        int excluded = snapshot.TotalLines - train.Count - dev.Count - test.Count;

        Assert.True(train.Count > 300, $"expected manuscript-scale training units, got {train.Count}");

        var vocabulary = train.Concat(dev)
            .SelectMany(l => Graphemes.Enumerate(l.Text).Select(g => g.Text))
            .Distinct(StringComparer.Ordinal).ToList();
        var labels = new LabelSet(vocabulary);

        var checkpointDir = Path.Combine(Path.GetTempPath(), "lt-m02a", Guid.NewGuid().ToString("N"));
        var config = new TrainingConfig
        {
            Epochs = 150, BatchSize = 8, LearningRate = 3e-3, Seed = 42,
            EarlyStoppingPatience = 150, HiddenSize = 64,
            ModelName = "m02a-pilot-crnn", CheckpointDirectory = checkpointDir,
        };

        var model = new CrnnModel(labels.NumClasses, config.HiddenSize);
        var trainer = new CtcTrainer(model, labels, config);
        var run = trainer.Fit(train, dev);

        // One-shot scoring on the page-holdout units (imported layout).
        var results = new TorchLineRecognizer(model, labels).Recognize([.. test.Select(l => l.Image)]);
        int distance = 0, referenceLength = 0;
        for (int i = 0; i < test.Count; i++)
        {
            var score = RecognitionScorer.Score(test[i].Text, results[i].Text);
            distance += score.ScalarDistance;
            referenceLength += score.ReferenceScalarLength;
        }
        double cer = referenceLength == 0 ? 1.0 : (double)distance / referenceLength;

        output.WriteLine($"M02a pilot: lines={snapshot.TotalLines} (157-ish markup-excluded={excluded}), " +
                         $"train={train.Count} dev={dev.Count} test={test.Count}; best dev CER={run.BestValidationCer:0.###}; " +
                         $"page-holdout CER={cer:0.###}; vocab={vocabulary.Count}");

        Assert.True(run.History[^1].TrainLoss < run.History[0].TrainLoss, "manuscript training loss should decrease");
        Assert.True(cer < 1.0, $"page-holdout CER {cer:0.###} must beat the empty baseline");
    }
}
