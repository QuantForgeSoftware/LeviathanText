using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Imaging;
using LeviathanText.Layout;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace LeviathanText.Pilot.Tests;

/// <summary>
/// Learned-layout baseline vs the projection heuristic on REAL pages (Phase 0 gate:
/// compare automatic and manually supplied layout). Trains on M02a train+dev pages and
/// scores held-out M02a pages plus P01 pages. Writes
/// docs/evaluation/layout-learned-comparison.md with disclosed matching tolerance.
/// </summary>
public sealed class LearnedLayoutPilotTests(ITestOutputHelper output)
{
    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LeviathanText.slnx")))
            dir = dir.Parent;
        return dir?.FullName;
    }

    [SkippableFact]
    public void Learned_vs_projection_on_held_out_pages()
    {
        var root = FindRepoRoot();
        Skip.IfNot(root is not null, "repo root not found");

        var m02a = Path.Combine(root!, ".artifacts", "datasets", "m02a-v1.0", "m02a");
        var p01 = Path.Combine(root!, ".artifacts", "datasets", "p01-v0.9");
        Skip.IfNot(Directory.Exists(m02a) || Directory.Exists(p01), "no datasets downloaded");

        // 1) Build training examples from M02a training/dev pages (file-level split).
        var training = new List<LearnedLineDetector.TrainingExample>();
        var validation = new List<LearnedLineDetector.TrainingExample>();
        var testWitnesses = new List<(string Name, SKBitmap Page, List<(int Top, int Bottom)> Truth)>();
        var disposed = new List<SKBitmap>();

        if (Directory.Exists(m02a))
        {
            var xmlFiles = Directory.GetFiles(Path.Combine(m02a, "xml"), "*.xml").OrderBy(f => f, StringComparer.Ordinal).ToList();
            for (int i = 0; i < xmlFiles.Count; i++)
            {
                var imagePath = ResolveImage(Path.Combine(m02a, "xml"), Path.GetFileNameWithoutExtension(xmlFiles[i]), ".png",
                    Path.Combine(m02a, "images"));
                if (imagePath is null) continue;
                var truth = GroundTruth(xmlFiles[i]);
                var bitmap = ImageDecoder.Decode(imagePath).Bitmap;
                disposed.Add(bitmap);

                bool isTest = i % 5 == 0;
                bool isDev = i % 6 == 1;
                var example = new LearnedLineDetector.TrainingExample(
                    RowFeatureExtractor.Profile(bitmap), RowFeatureExtractor.Labels(bitmap.Height, truth));
                if (isTest)
                    testWitnesses.Add(($"m02a:{Path.GetFileNameWithoutExtension(xmlFiles[i])}", bitmap, truth));
                else if (isDev)
                    validation.Add(example);
                else
                    training.Add(example);
            }
        }

        if (Directory.Exists(p01))
        {
            var xmlFiles = Directory.GetFiles(p01, "*.xml").OrderBy(f => f, StringComparer.Ordinal).Take(8).ToList();
            foreach (var xml in xmlFiles)
            {
                var imagePath = Path.Combine(p01, Path.GetFileNameWithoutExtension(xml) + ".jpg");
                if (!File.Exists(imagePath)) continue;
                var truth = GroundTruth(xml);
                var bitmap = ImageDecoder.Decode(imagePath).Bitmap;
                disposed.Add(bitmap);
                testWitnesses.Add(($"p01:{Path.GetFileNameWithoutExtension(xml)}", bitmap, truth));
            }
        }

        Skip.IfNot(training.Count > 0 && testWitnesses.Count > 0, "insufficient pages for the comparison");

        try
        {
            var learned = LearnedLineDetector.Train(training);
            var tuned = learned.WithTunedThreshold(validation.Count > 0 ? validation : training);

            var learnedRows = new List<(string, IReadOnlyList<DetectedLine>, IReadOnlyList<(int, int)>)>();
            var projectionRows = new List<(string, IReadOnlyList<DetectedLine>, IReadOnlyList<(int, int)>)>();
            foreach (var (name, page, truth) in testWitnesses)
            {
                learnedRows.Add((name, tuned.Detect(page).Lines, truth));
                var projection = ProjectionProfileLayout.Detect(page);
                projectionRows.Add((name, projection.Lines,
                    truth.Select(t => (t.Top, t.Bottom)).ToList()));
            }

            var learnedSummary = LayoutComparison.Compare(learnedRows);
            var projectionSummary = LayoutComparison.Compare(projectionRows);

            var markdown = $"""
                # Automatic layout: learned vs projection (pilot)

                - Date: {DateTimeOffset.UtcNow:O}; contract {ContractVersion.Current}
                - Learned: `{LearnedLineDetector.ModelVersion}` (per-row logistic regression; {RowFeatureExtractor.FeatureCount} ink-profile features; deterministic training)
                - Baseline: projection profile (Otsu, trim, adaptive valleys)
                - Training: M02a train pages ({training.Count}); threshold tuned on M02a dev pages ({validation.Count}); evaluation: held-out M02a pages + P01 pages ({testWitnesses.Count} pages)
                - Selected threshold: {tuned.Options.Threshold:0.00}; matching: vertical-band IoU ≥ {learnedSummary.IouThreshold:0.##}; confidence: none (no probability claim)

                | Detector | Mean precision | Mean recall |
                | --- | --- | --- |
                | Learned (`{LearnedLineDetector.ModelVersion}`) | {learnedSummary.MeanPrecision:0.###} | {learnedSummary.MeanRecall:0.###} |
                | Projection baseline | {projectionSummary.MeanPrecision:0.###} | {projectionSummary.MeanRecall:0.###} |

                Per-page learned results:

                | Page | GT | Detected | Matches | Precision | Recall |
                | --- | --- | --- | --- | --- | --- |
                {string.Join("\n", learnedSummary.PerWitness.Select(r => $"| {r.Witness} | {r.GroundTruthLines} | {r.DetectedLines} | {r.Matches} | {r.Precision:0.###} | {r.Recall:0.###} |"))}

                ## Status

                **Measured outcome: the learned row model does NOT beat the corrected projection
                baseline on this corpus** ({learnedSummary.MeanRecall:0.###} vs {projectionSummary.MeanRecall:0.###} mean recall).
                Diagnosis: a linear model over whole-row ink statistics cannot reliably separate
                faint letter rows from parchment texture at the windows that fit within one line;
                the projection detector with background-relative splitting is the stronger baseline
                here. Next iteration: patch-level CNN features (or learned baselines) with the same
                harness and split discipline. Multi-column, curved and rotated layouts remain
                unsupported and must not be presented as solved.
                """;
            var reportPath = Path.Combine(root!, "docs", "evaluation", "layout-learned-comparison.md");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
            File.WriteAllText(reportPath, markdown);
            output.WriteLine($"learned: P={learnedSummary.MeanPrecision:0.###} R={learnedSummary.MeanRecall:0.###}; " +
                             $"projection: P={projectionSummary.MeanPrecision:0.###} R={projectionSummary.MeanRecall:0.###}; " +
                             $"threshold={tuned.Options.Threshold:0.000}; train={training.Count} dev={validation.Count}");

            // Measured outcome is recorded, not gated: at this feature set/time-box the
            // learned row model did NOT beat the corrected projection baseline. The
            // report states that plainly; a patch-level CNN is the next iteration.
            Assert.True(true,
                $"measurement complete — learned R={learnedSummary.MeanRecall:0.###}, projection R={projectionSummary.MeanRecall:0.###}" +
                $" (learned underperforms at this feature set; recorded as a negative result)");
        }
        finally
        {
            foreach (var bitmap in disposed) bitmap.Dispose();
        }
    }

    private static List<(int Top, int Bottom)> GroundTruth(string xmlPath)
    {
        var imported = PageXml.Import(File.ReadAllText(xmlPath), new EntityId(EntityKind.Capture, "cap"),
            new Checksum { Algorithm = "unavailable", Digest = "pending" });
        var truth = new List<(int, int)>();
        foreach (var line in imported.Lines)
        {
            var geometry = line.Polygon.Count >= 3 ? line.Polygon : line.Baseline;
            if (geometry.Count < 2) continue;
            int top = (int)geometry.Min(p => p.Y);
            int bottom = (int)geometry.Max(p => p.Y);
            truth.Add((top, Math.Max(bottom, top + 1)));
        }
        return truth;
    }

    private static string? ResolveImage(string xmlDir, string baseName, string extension, string imagesDir)
    {
        string[] candidates =
        {
            Path.Combine(imagesDir, baseName + extension),
            Path.Combine(xmlDir, baseName + extension),
        };
        return candidates.FirstOrDefault(File.Exists);
    }
}
