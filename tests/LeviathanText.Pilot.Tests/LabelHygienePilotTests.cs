using LeviathanText.Core.Unicode;
using LeviathanText.Formats;
using LeviathanText.Recognition;
using LeviathanText.Training;
using Xunit;
using Xunit.Abstractions;

namespace LeviathanText.Pilot.Tests;

/// <summary>
/// Label-hygiene experiment: does excluding lines whose characters fall outside the Greek
/// transcription profile (e.g. PAGE markup leaked into labels) change dev CER? Same
/// architecture, seed, label set and epochs; only the training list differs. Report only —
/// no default is changed on the strength of one run.
/// </summary>
public sealed class LabelHygienePilotTests(ITestOutputHelper output)
{
    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LeviathanText.slnx")))
            dir = dir.Parent;
        return dir?.FullName;
    }

    private static bool OutsideProfile(string text) =>
        GreekTextProfile.Inspect(text).Issues.Any(i => i.Category == GreekTextProfile.Category.Other);

    [SkippableFact]
    public void Excluding_out_of_profile_lines_is_measured_on_dev()
    {
        var root = FindRepoRoot();
        Skip.IfNot(root is not null, "repo root not found");
        var dataDir = Path.Combine(root!, ".artifacts", "datasets", "p01-v0.9");
        Skip.IfNot(Directory.Exists(dataDir), "P01 artifacts not available");

        var snapshot = P01LineDataset.Load(dataDir);
        var excluded = snapshot.Train.Count(l => OutsideProfile(l.Text));
        var cleanTrain = snapshot.Train.Where(l => !OutsideProfile(l.Text)).ToList();
        var cleanDev = snapshot.Dev.Where(l => !OutsideProfile(l.Text)).ToList();

        // One label set for both runs (fair comparison).
        var vocabulary = snapshot.Train.Concat(snapshot.Dev)
            .SelectMany(l => Graphemes.Enumerate(l.Text).Select(g => g.Text))
            .Distinct(StringComparer.Ordinal).ToList();
        var labels = new LabelSet(vocabulary);

        double Train(string name, IReadOnlyList<LabeledLine> train, IReadOnlyList<LabeledLine> dev)
        {
            var checkpointDir = Path.Combine(Path.GetTempPath(), $"lt-hygiene-{Guid.NewGuid():N}");
            var config = new TrainingConfig
            {
                Epochs = 60, BatchSize = 8, LearningRate = 3e-3, Seed = 99,
                EarlyStoppingPatience = 60, HiddenSize = 64,
                ModelName = name, CheckpointDirectory = checkpointDir,
            };
            var run = new CtcTrainer(new CrnnModel(labels.NumClasses, config.HiddenSize), labels, config)
                .Fit(train, dev);
            return run.BestValidationCer;
        }

        var baseline = Train("hygiene-baseline", snapshot.Train, snapshot.Dev);
        var clean = Train("hygiene-clean", cleanTrain, cleanDev);
        output.WriteLine($"baseline dev CER {baseline:0.###} ({snapshot.Train.Count} lines, {excluded} out-of-profile); " +
                         $"clean dev CER {clean:0.###} ({cleanTrain.Count} lines)");

        var markdown = $"""
            # Label hygiene: out-of-profile training lines (P01 dev)

            - Date: {DateTimeOffset.UtcNow:O}; profile `{GreekTextProfile.ProfileId}@{GreekTextProfile.Version}`
            - Same architecture (CRNN h=64), seed 99, 60 epochs, identical label set; only the training list differs
            - Out-of-profile = any scalar classified `Other` (e.g. PAGE markup `<`, `>`, `&`, brackets leaked into labels)
            - Training lines: {snapshot.Train.Count} baseline vs {cleanTrain.Count} clean ({excluded} excluded); dev: {snapshot.Dev.Count} vs {cleanDev.Count}

            | Variant | Dev CER |
            | --- | --- |
            | All training lines | {baseline:0.###} |
            | Out-of-profile lines excluded | {clean:0.###} |

            ## Reading

            This is a single-seed, single-epoch-budget comparison on the P01 dev split; it is
            evidence for cleaning labels, not a tuned result. P01's locked split was spent
            earlier — no locked-test claim is made here. The production recommendation is to
            run profile validation (`leviathan validate text --strict`) before training and
            to exclude markup-contaminated labels by default once a second corpus confirms the
            direction.
            """;
        var path = Path.Combine(root!, "docs", "evaluation", "label-hygiene.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, markdown);
    }
}
