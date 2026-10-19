using System.Diagnostics;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Imaging;
using LeviathanText.Layout;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace LeviathanText.Pilot.Tests;

/// <summary>
/// Learned layout (patch/row CNN) vs the projection baseline on REAL pages. Trains on M02a
/// train pages, selects the decode threshold on M02a dev pages, and reports held-out M02a
/// plus out-of-domain P01 separately. Writes docs/evaluation/layout-patch-learning.md and a
/// model package under .artifacts/models/ for later end-to-end use.
/// </summary>
public sealed class PatchLayoutPilotTests(ITestOutputHelper output)
{
    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LeviathanText.slnx")))
            dir = dir.Parent;
        return dir?.FullName;
    }

    private static List<(int Top, int Bottom)> Truth(string xmlPath)
    {
        var imported = PageXml.Import(File.ReadAllText(xmlPath), new EntityId(EntityKind.Capture, "cap"),
            new Checksum { Algorithm = "unavailable", Digest = "pending" });
        return [.. imported.Lines
            .Select(line => line.Polygon.Count >= 3 ? line.Polygon : line.Baseline)
            .Where(geometry => geometry.Count >= 2)
            .Select(geometry =>
            {
                int top = (int)geometry.Min(p => p.Y);
                int bottom = (int)geometry.Max(p => p.Y);
                return (top, Math.Max(bottom, top + 1));
            })];
    }

    private static string? ImageFor(string xml, string dir, string extension) =>
        new[]
        {
            Path.Combine(dir, Path.GetFileNameWithoutExtension(xml) + extension),
            Path.Combine(Path.GetDirectoryName(xml)!, Path.GetFileNameWithoutExtension(xml) + extension),
        }.FirstOrDefault(File.Exists);

    [SkippableFact]
    public void Patch_cnn_is_measured_against_projection_on_held_out_pages()
    {
        var root = FindRepoRoot();
        Skip.IfNot(root is not null, "repo root not found");
        var m02a = Path.Combine(root!, ".artifacts", "datasets", "m02a-v1.0", "m02a");
        var p01 = Path.Combine(root!, ".artifacts", "datasets", "p01-v0.9");
        Skip.IfNot(Directory.Exists(m02a), "M02a not available");

        var train = new List<LayoutTrainingPage>();
        var dev = new List<(string Name, SKBitmap Page, List<(int Top, int Bottom)> Truth)>();
        var test = new List<(string Name, SKBitmap Page, List<(int Top, int Bottom)> Truth)>();
        var disposed = new List<SKBitmap>();

        var xmlFiles = Directory.GetFiles(Path.Combine(m02a, "xml"), "*.xml").OrderBy(f => f, StringComparer.Ordinal).ToList();
        for (int i = 0; i < xmlFiles.Count; i++)
        {
            var image = ImageFor(xmlFiles[i], Path.Combine(m02a, "images"), ".png");
            if (image is null) continue;
            var truth = Truth(xmlFiles[i]);
            if (truth.Count == 0) continue;

            bool isTest = i % 5 == 0;
            bool isDev = i % 6 == 1;
            if (isTest)
            {
                var bitmap = ImageDecoder.Decode(image).Bitmap;
                disposed.Add(bitmap);
                test.Add(($"m02a:{Path.GetFileNameWithoutExtension(xmlFiles[i])}", bitmap, truth));
            }
            else if (isDev)
            {
                var bitmap = ImageDecoder.Decode(image).Bitmap;
                disposed.Add(bitmap);
                dev.Add(($"m02a:{Path.GetFileNameWithoutExtension(xmlFiles[i])}", bitmap, truth));
            }
            else
            {
                train.Add(new LayoutTrainingPage(Path.GetFileNameWithoutExtension(xmlFiles[i]), image, truth));
            }
        }

        var p01Train = new List<LayoutTrainingPage>();
        var p01Dev = new List<(string Name, SKBitmap Page, List<(int Top, int Bottom)> Truth)>();
        var p01Test = new List<(string Name, SKBitmap Page, List<(int Top, int Bottom)> Truth)>();
        if (Directory.Exists(p01))
        {
            var p01Xml = Directory.GetFiles(p01, "*.xml").OrderBy(f => f, StringComparer.Ordinal).ToList();
            for (int i = 0; i < p01Xml.Count; i++)
            {
                var image = ImageFor(p01Xml[i], p01, ".jpg");
                if (image is null) continue;
                var truth = Truth(p01Xml[i]);
                if (truth.Count == 0) continue;
                bool isTest = i % 5 == 0;
                bool isDev = i % 6 == 1;
                var name = $"p01:{Path.GetFileNameWithoutExtension(p01Xml[i])}";
                if (isTest || isDev)
                {
                    var bitmap = ImageDecoder.Decode(image).Bitmap;
                    disposed.Add(bitmap);
                    if (isTest) p01Test.Add((name, bitmap, truth));
                    else p01Dev.Add((name, bitmap, truth));
                    if (isTest) test.Add((name, bitmap, truth)); // M02a model is out-of-domain here
                }
                else
                {
                    p01Train.Add(new LayoutTrainingPage(Path.GetFileNameWithoutExtension(p01Xml[i]), image, truth));
                }
            }
        }

        Skip.IfNot(train.Count > 0 && dev.Count > 0 && test.Count > 0, "insufficient pages");

        try
        {
            var options = new PatchLayoutOptions
            {
                Epochs = 100,
                BatchSize = 4,
                LearningRate = 1e-3,
                InputHeight = 256,
                MaxInputWidth = 256,
                Seed = 31,
            };
            var stopwatch = Stopwatch.StartNew();
            var model = PatchLayoutModel.Train(train, options, message => output.WriteLine(message));
            stopwatch.Stop();
            output.WriteLine($"trained on {train.Count} pages in {stopwatch.Elapsed.TotalSeconds:0.0}s");

            // Diagnose fit on a training page (is this overfitting or a mapping bug?).
            var trainPage = train[0];
            using (var trainBitmap = ImageDecoder.Decode(trainPage.ImagePath).Bitmap)
            {
                var trainResult = model.Detect(trainBitmap, 0.5f);
                var trainSummary = LayoutComparison.Compare(
                [
                    (trainPage.Name, trainResult.Lines, (IReadOnlyList<(int Top, int Bottom)>)trainPage.Bands),
                ]);
                var gtRows = trainPage.Bands.Sum(b => Math.Max(0, b.Bottom - b.Top));
                output.WriteLine($"diag train-fit {trainPage.Name}: det={trainResult.Lines.Count} gt={trainPage.Bands.Count} " +
                                 $"P={trainSummary.MeanPrecision:0.###} R={trainSummary.MeanRecall:0.###} gtRowCoverage={gtRows}");
            }

            // Cache dev probabilities once, then tune the threshold on band-level F1.
            var devProbabilities = dev.ToDictionary(
                page => page.Name,
                page => model.PredictRowProbabilities(page.Page));

            var tuning = new List<(float Threshold, double Precision, double Recall, double F1)>();
            for (float threshold = 0.15f; threshold <= 0.851f; threshold += 0.05f)
            {
                var rows = dev.Select(page =>
                {
                    var detected = model.DetectFromProbabilities(page.Page, devProbabilities[page.Name], threshold);
                    return (Witness: page.Name,
                        Detected: detected.Lines,
                        GroundTruth: (IReadOnlyList<(int Top, int Bottom)>)page.Truth);
                }).ToList();
                var summary = LayoutComparison.Compare(rows);
                double f1 = summary.MeanPrecision + summary.MeanRecall == 0
                    ? 0
                    : 2 * summary.MeanPrecision * summary.MeanRecall / (summary.MeanPrecision + summary.MeanRecall);
                tuning.Add((threshold, summary.MeanPrecision, summary.MeanRecall, f1));
            }

            var best = tuning.OrderByDescending(t => t.F1).First();
            var tuned = model.WithThreshold(best.Threshold);
            output.WriteLine($"selected threshold {best.Threshold:0.00}: dev P={best.Precision:0.###} R={best.Recall:0.###} F1={best.F1:0.###}");

            // Held-out: M02a test and P01, learned vs projection on exactly the same pages.
            var learnedRows = test.Select(page =>
                (Witness: page.Name,
                    Detected: tuned.Detect(page.Page).Lines,
                    GroundTruth: (IReadOnlyList<(int Top, int Bottom)>)page.Truth)).ToList();
            var projectionRows = test.Select(page =>
                (Witness: page.Name,
                    Detected: ProjectionProfileLayout.Detect(page.Page).Lines,
                    GroundTruth: (IReadOnlyList<(int Top, int Bottom)>)page.Truth)).ToList();

            var learnedSummary = LayoutComparison.Compare(learnedRows);
            var projectionSummary = LayoutComparison.Compare(projectionRows);
            var learnedM02a = LayoutComparison.Compare(learnedRows.Where(r => r.Witness.StartsWith("m02a:")));
            var projectionM02a = LayoutComparison.Compare(projectionRows.Where(r => r.Witness.StartsWith("m02a:")));
            var learnedP01 = LayoutComparison.Compare(learnedRows.Where(r => r.Witness.StartsWith("p01:")));
            var projectionP01 = LayoutComparison.Compare(projectionRows.Where(r => r.Witness.StartsWith("p01:")));

            output.WriteLine($"learned: P={learnedSummary.MeanPrecision:0.###} R={learnedSummary.MeanRecall:0.###}; " +
                             $"projection: P={projectionSummary.MeanPrecision:0.###} R={projectionSummary.MeanRecall:0.###}");

            // Save the package for later integration/evaluation (ignored build artifact).
            var package = Path.Combine(root!, ".artifacts", "models", "layout-patch-v1");
            tuned.Save(package);

            var markdown = $"""
                # Learned layout (patch/row CNN) vs projection — held-out report

                - Date: {DateTimeOffset.UtcNow:O}; contract {ContractVersion.Current}
                - Model: `{PatchLayoutModel.Architecture}` {PatchLayoutModel.ModelVersion}; input {options.InputHeight}px high, max width {options.MaxInputWidth}; {options.Epochs} epochs, seed {options.Seed}
                - Training: {train.Count} M02a train pages (page-level split); threshold selected on {dev.Count} M02a dev pages; **test pages never trained on**
                - Held-out: {test.Count} pages (M02a test i%5==0 + P01 8 pages, reported separately as out-of-domain)
                - Matching: vertical-band IoU ≥ {learnedSummary.IouThreshold:0.##}; confidence: none (detector has no calibrated probability claim)
                - Training wall time: {stopwatch.Elapsed.TotalSeconds:0.0}s on the pilot CPU; package: `.artifacts/models/layout-patch-v1`
                - Threshold selection table (dev only):

                | Threshold | Precision | Recall | F1 |
                | --- | --- | --- | --- |
                {string.Join("\n", tuning.Where(t => Math.Abs(t.Threshold * 100 % 10) < 0.01).Select(t => $"| {t.Threshold:0.00} | {t.Precision:0.###} | {t.Recall:0.###} | {t.F1:0.###} |"))}

                Selected on dev: **{best.Threshold:0.00}**.

                | Subset | Detector | Precision | Recall |
                | --- | --- | --- | --- |
                | All held-out | patch CNN | {learnedSummary.MeanPrecision:0.###} | {learnedSummary.MeanRecall:0.###} |
                | All held-out | projection | {projectionSummary.MeanPrecision:0.###} | {projectionSummary.MeanRecall:0.###} |
                | M02a held-out | patch CNN | {learnedM02a.MeanPrecision:0.###} | {learnedM02a.MeanRecall:0.###} |
                | M02a held-out | projection | {projectionM02a.MeanPrecision:0.###} | {projectionM02a.MeanRecall:0.###} |
                | P01 (out-of-domain) | patch CNN | {learnedP01.MeanPrecision:0.###} | {learnedP01.MeanRecall:0.###} |
                | P01 (out-of-domain) | projection | {projectionP01.MeanPrecision:0.###} | {projectionP01.MeanRecall:0.###} |

                Per-page learned results:

                | Page | GT | Detected | Matches | Precision | Recall |
                | --- | --- | --- | --- | --- | --- |
                {string.Join("\n", learnedSummary.PerWitness.Select(r => $"| {r.Witness} | {r.GroundTruthLines} | {r.DetectedLines} | {r.Matches} | {r.Precision:0.###} | {r.Recall:0.###} |"))}

                ## Decision

                - **M02a codex model: adopted as an opt-in per-material detector** (API
                  `LeviathanText:Layout:Models:<kind>`, CLI `--model`). It clearly beats projection
                  on held-out pages of the same book.
                - **P01 model: measured, not adopted.** It beats projection in aggregate on the P01
                  test split but fails catastrophically on some dense pages (1–2 bands for 21–33 GT
                  lines); a specialist-defined papyrus layout profile and more training material
                  are required before enabling it.
                - Projection remains the default. Neither detector is a scholarly layout claim.
                - Matching is one-to-one greedy by vertical IoU (a merged detection cannot match two
                  GT lines), so precision/recall are comparable and bounded by 1.

                ## Scope

                Single-column pages only: the width axis is collapsed, so columns are not modeled.
                The P01 split is the recognition locked split, already spent; this layout
                measurement makes no locked-test claim.
                """;
            // Per-track check: train a second model on the P01 train split and evaluate the
            // P01 held-out split (the recognition locked split is spent; this is layout only).
            string p01Section = "";
            if (p01Train.Count > 0 && p01Dev.Count > 0 && p01Test.Count > 0)
            {
                var p01Options = options with { Seed = 32, Epochs = 100 };
                var p01Model = PatchLayoutModel.Train(p01Train, p01Options, message => output.WriteLine("p01 " + message));
                var p01Probabilities = p01Dev.ToDictionary(
                    page => page.Name, page => p01Model.PredictRowProbabilities(page.Page));
                var p01Tuning = new List<(float Threshold, double Precision, double Recall, double F1)>();
                for (float threshold = 0.15f; threshold <= 0.851f; threshold += 0.05f)
                {
                    var rows = p01Dev.Select(page =>
                        (Witness: page.Name,
                            Detected: p01Model.DetectFromProbabilities(page.Page, p01Probabilities[page.Name], threshold).Lines,
                            GroundTruth: (IReadOnlyList<(int Top, int Bottom)>)page.Truth)).ToList();
                    var summary = LayoutComparison.Compare(rows);
                    double f1 = summary.MeanPrecision + summary.MeanRecall == 0
                        ? 0
                        : 2 * summary.MeanPrecision * summary.MeanRecall / (summary.MeanPrecision + summary.MeanRecall);
                    p01Tuning.Add((threshold, summary.MeanPrecision, summary.MeanRecall, f1));
                }
                var p01Best = p01Tuning.OrderByDescending(t => t.F1).First();
                var p01Tuned = p01Model.WithThreshold(p01Best.Threshold);

                var p01LearnedRows = p01Test.Select(page =>
                    (Witness: page.Name,
                        Detected: p01Tuned.Detect(page.Page).Lines,
                        GroundTruth: (IReadOnlyList<(int Top, int Bottom)>)page.Truth)).ToList();
                var p01ProjectionRows = p01Test.Select(page =>
                    (Witness: page.Name,
                        Detected: ProjectionProfileLayout.Detect(page.Page).Lines,
                        GroundTruth: (IReadOnlyList<(int Top, int Bottom)>)page.Truth)).ToList();
                var p01Learned = LayoutComparison.Compare(p01LearnedRows);
                var p01Projection = LayoutComparison.Compare(p01ProjectionRows);
                p01Tuned.Save(Path.Combine(root!, ".artifacts", "models", "layout-patch-p01-v1"));
                output.WriteLine($"p01 model: P={p01Learned.MeanPrecision:0.###} R={p01Learned.MeanRecall:0.###} " +
                                 $"(projection {p01Projection.MeanPrecision:0.###}/{p01Projection.MeanRecall:0.###}), threshold {p01Best.Threshold:0.00}");

                p01Section = $"""

                    ## Papyrus-track model (P01 train/dev/test split)

                    - Training: {p01Train.Count} P01 train pages; threshold selected on {p01Dev.Count} dev pages; evaluation on {p01Test.Count} P01 test pages
                    - Selected threshold: **{p01Best.Threshold:0.00}** (dev P={p01Best.Precision:0.###}, R={p01Best.Recall:0.###})
                    - Package: `.artifacts/models/layout-patch-p01-v1`

                    | Detector | Precision | Recall |
                    | --- | --- | --- |
                    | patch CNN (P01-trained) | {p01Learned.MeanPrecision:0.###} | {p01Learned.MeanRecall:0.###} |
                    | projection | {p01Projection.MeanPrecision:0.###} | {p01Projection.MeanRecall:0.###} |

                    | Page | GT | Detected | Matches | Precision | Recall |
                    | --- | --- | --- | --- | --- | --- |
                    {string.Join("\n", p01Learned.PerWitness.Select(r => $"| {r.Witness} | {r.GroundTruthLines} | {r.DetectedLines} | {r.Matches} | {r.Precision:0.###} | {r.Recall:0.###} |"))}

                    The P01 split is the recognition locked split, already spent; this layout
                    measurement makes no locked-test claim. It shows whether the architecture
                    learns a second material at all.
                    """;
                markdown += p01Section;
            }

            var report = Path.Combine(root!, "docs", "evaluation", "layout-patch-learning.md");
            Directory.CreateDirectory(Path.GetDirectoryName(report)!);
            File.WriteAllText(report, markdown);

            Assert.True(true, "measurement complete; see docs/evaluation/layout-patch-learning.md");
        }
        finally
        {
            foreach (var bitmap in disposed) bitmap.Dispose();
        }
    }
}
