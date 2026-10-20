using LeviathanText.Contracts;
using LeviathanText.Core.Unicode;
using LeviathanText.Evaluation;
using LeviathanText.Recognition;
using LeviathanText.Training;
using Xunit;
using Xunit.Abstractions;

namespace LeviathanText.Pilot.Tests;

/// <summary>
/// EVAL-02 (pilot form), **opt-in only**: scores the trained pilot model on the locked
/// papyrus test split and writes the candidate report. The split is SPENT (multiple runs);
/// routine test suites must not re-score it. Set LEVIATHANTEXT_RUN_LOCKED=1 deliberately.
/// Pilot-scale, non-adjudicated labels, single track; no pass/fail threshold is claimed.
/// </summary>
public sealed class LockedTestEvaluationTests(ITestOutputHelper output)
{
    [SkippableFact]
    public void OneShot_ScoreLockedTest_AndPublishReport()
    {
        Skip.IfNot(Environment.GetEnvironmentVariable("LEVIATHANTEXT_RUN_LOCKED") == "1",
            "locked-test evaluation is opt-in (LEVIATHANTEXT_RUN_LOCKED=1); the P01 split is spent " +
            "and must not be re-scored by routine suites.");
        var dataDir = FindDataDir();
        Skip.IfNot(dataDir != "", "P01 dataset not downloaded.");

        var snapshot = P01LineDataset.Load(dataDir);
        Assert.True(snapshot.TestLocked.Count > 0, "locked test split must exist");

        // Label set from train+dev only (no leakage into the locked set).
        var vocabulary = snapshot.Train.Concat(snapshot.Dev)
            .SelectMany(l => Graphemes.Enumerate(l.Text).Select(g => g.Text))
            .Distinct(StringComparer.Ordinal).ToList();
        var labels = new LabelSet(vocabulary);

        var checkpointDir = Path.Combine(Path.GetTempPath(), "lt-eval", Guid.NewGuid().ToString("N"));
        var config = new TrainingConfig
        {
            Epochs = 300, BatchSize = 8, LearningRate = 3e-3, Seed = 42,
            EarlyStoppingPatience = 300, HiddenSize = 64,
            ModelName = "eval-pilot-crnn", CheckpointDirectory = checkpointDir,
        };

        var model = new CrnnModel(labels.NumClasses, config.HiddenSize);
        var trainer = new CtcTrainer(model, labels, config);
        var trainingClock = System.Diagnostics.Stopwatch.StartNew();
        var run = trainer.Fit(snapshot.Train, snapshot.Dev);
        trainingClock.Stop();
        var trainingWallTime = trainingClock.Elapsed;
        var trainingSecondsPerLine = trainingWallTime.TotalSeconds / Math.Max(1, snapshot.Train.Count);

        // EVAL-04: measured operating limits on this machine (pilot form).
        // PeakWorkingSet64 is not reliably supported on macOS; allocations are precise.
        var allocationMb = GC.GetTotalAllocatedBytes(precise: true) / 1024.0 / 1024.0;

        // ONE-SHOT locked-test scoring, raw model output only (no assistance).
        var results = new TorchLineRecognizer(model, labels).Recognize([.. snapshot.TestLocked.Select(l => l.Image)]);

        int distance = 0, referenceLength = 0, markInsertions = 0, markDeletions = 0, markSubstitutions = 0;
        int perfectLines = 0;
        for (int i = 0; i < snapshot.TestLocked.Count; i++)
        {
            var score = RecognitionScorer.Score(snapshot.TestLocked[i].Text, results[i].Text);
            distance += score.ScalarDistance;
            referenceLength += score.ReferenceScalarLength;
            markInsertions += score.MarkErrors.Insertions;
            markDeletions += score.MarkErrors.Deletions;
            markSubstitutions += score.MarkErrors.Substitutions;
            if (score.ScalarDistance == 0) perfectLines++;
        }
        double cer = referenceLength == 0 ? 1.0 : (double)distance / referenceLength;

        // Split hash: sha256 of the sorted witness→split assignment (leakage-check currency, corpus-manifest §5).
        var assignment = string.Join('\n', snapshot.WitnessSplitMap.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"));
        var splitHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(assignment))).ToLowerInvariant();

        // Publish the report; the locked split is now spent for this pilot and any reuse must be disclosed.
        var report = $"""
            # P01 pilot evaluation (pilot-scale; locked set used multiple times)

            - Date: {DateTimeOffset.UtcNow:O}; contract {LeviathanText.Contracts.ContractVersion.Current}
            - **Locked-set status:** this split has been scored multiple times (pre-fix and post-fix re-baselines); it is spent for threshold purposes. See `docs/evaluation/locked-set-reuse.md`.
            - Dataset: P01 Zenon Papyri HTR v0.9 (CC BY 4.0), md5-verified, 27 pairs, {snapshot.TotalLines} lines
            - Split: witness-level (file = unit); train {snapshot.Train.Count} / dev {snapshot.Dev.Count} / **test {snapshot.TestLocked.Count} (locked)**
            - Split hash (sha256 of sorted assignment): `{splitHash}`
            - Model: {config.ModelName} CRNN(h={config.HiddenSize}), {run.History.Count} epochs, lr {config.LearningRate}, seed {config.Seed}; train loss {run.History[0].TrainLoss:0.###} → {run.History[^1].TrainLoss:0.###}
            - Labels: Transkribus-prepared diplomatic text, grapheme-level label set ({vocabulary.Count} classes), NOT yet expert-adjudicated (DATA-03 pending)
            - Layout: imported (PAGE geometry) — this is recognition-on-manual-layout, never advertised as fully automatic

            ## Raw-model results (no AI assistance)

            | Metric | Value |
            | --- | --- |
            | Scalar CER (canonical-equivalent, case-sensitive) | {cer:0.###} |
            | Perfect lines | {perfectLines}/{snapshot.TestLocked.Count} |
            | Mark insertions (wrongly supplied marks) | {markInsertions} |
            | Mark deletions (missing marks) | {markDeletions} |
            | Mark substitutions | {markSubstitutions} |

            ## Operating limits measured (EVAL-04, pilot machine: macOS arm64, CPU inference/training)

            | Entry | Value |
            | --- | --- |
            | Training wall time | {trainingWallTime.TotalMinutes:0.#} min |
            | Training seconds per line | {trainingSecondsPerLine:0.###} s |
            | Peak RAM (process) | unmeasured on macOS (platform limitation); working set at report time {Environment.WorkingSet / 1024 / 1024:0} MB |
            | Total allocations | {allocationMb:0} MB |
            | Backend | TorchSharp CPU (libtorch-cpu-osx-arm64 2.10.0) |

            ## Status and caveats

            - **Pilot-scale**: generalization is data-limited (176 training lines); {cer:0.###} is a measured baseline, not a threshold or a pass/fail result.
            - The locked split has been used multiple times; under acceptance-plan §1 it is spent for threshold purposes and any further claim requires fresh held-out witnesses and disclosure.
            - Missing tracks: manuscripts and inscriptions are not evaluated here; the combined initial scope is not complete (acceptance-plan §9).
            """;
        var reportPath = FindRepoRoot() is { } root ? Path.Combine(root, "docs", "evaluation", "pilot-p01-report.md") : "pilot-p01-report.md";
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllText(reportPath, report);
        output.WriteLine($"wrote {reportPath}");

        Assert.True(cer < 1.0, $"locked-test CER {cer:0.###} must beat the empty baseline");
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
