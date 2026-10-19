using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Core.Unicode;
using LeviathanText.Evaluation;
using LeviathanText.Formats;
using LeviathanText.Imaging;
using LeviathanText.Recognition;
using LeviathanText.Training;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace LeviathanText.Pilot.Tests;

/// <summary>
/// End-to-end page measurement: image-only page -> automatic layout -> recognition.
/// Detected lines are matched to PAGE ground-truth lines by vertical IoU (≥ 0.5); missed
/// GT lines count as full deletions, so the reported page CER includes layout losses
/// (unlike line-crop CER, which silently assumes perfect detection). Training pages are
/// excluded; the evaluation pages are held out (same books, so this is not a
/// generalization claim — it measures the workflow).
/// </summary>
public sealed class PageEndToEndPilotTests(ITestOutputHelper output)
{
    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LeviathanText.slnx")))
            dir = dir.Parent;
        return dir?.FullName;
    }

    private sealed class InMemoryStore : IProjectStore
    {
        private readonly Dictionary<string, string> _projects = [];
        public void SaveProject(NativeProject project) => _projects[project.Id.ToString()] = NativeJson.Save(project);
        public NativeProject? LoadProject(EntityId id) =>
            _projects.TryGetValue(id.ToString(), out var json) ? NativeJson.Load(json) : null;
        public IReadOnlyList<(EntityId Id, string Name)> ListProjects() =>
            [.. _projects.Select(kv => (EntityId.Parse(kv.Key), NativeJson.Load(kv.Value).Name))];
    }

    private sealed record PageTruth(string Name, string ImagePath, string XmlPath);

    private static List<(int Left, int Top, int Right, int Bottom, string Text)> Truth(string xmlPath)
    {
        var imported = PageXml.Import(File.ReadAllText(xmlPath), new EntityId(EntityKind.Capture, "cap"),
            new Checksum { Algorithm = "unavailable", Digest = "pending" });
        var texts = imported.Revisions.ToDictionary(r => r.Id, r => r.Text);
        var lines = new List<(int, int, int, int, string)>();
        foreach (var line in imported.Lines)
        {
            var geometry = line.Polygon.Count >= 3 ? line.Polygon : line.Baseline;
            if (geometry.Count < 2) continue;
            int left = (int)geometry.Min(p => p.X);
            int right = (int)geometry.Max(p => p.X);
            int top = (int)geometry.Min(p => p.Y);
            int bottom = (int)geometry.Max(p => p.Y);
            var text = line.DiplomaticRevisionId is { } id && texts.TryGetValue(id, out var t) ? t : "";
            lines.Add((left, top, Math.Max(right, left + 1), Math.Max(bottom, top + 1), text));
        }
        return lines;
    }

    [SkippableFact]
    public void Page_cer_includes_layout_misses_on_held_out_pages()
    {
        var root = FindRepoRoot();
        Skip.IfNot(root is not null, "repo root not found");
        var m02a = Path.Combine(root!, ".artifacts", "datasets", "m02a-v1.0", "m02a");
        Skip.IfNot(Directory.Exists(m02a), "M02a not available");

        var snapshot = P01LineDataset.Load(Path.Combine(m02a, "xml"), imageExtension: ".png");
        bool Contaminated(string text) => text.Any(c => c is '<' or '>' or '&' or '[' or ']');
        var train = snapshot.Train.Where(l => !Contaminated(l.Text)).ToList();
        var dev = snapshot.Dev.Where(l => !Contaminated(l.Text)).ToList();
        var vocabulary = train.Concat(dev)
            .SelectMany(l => Graphemes.Enumerate(l.Text).Select(g => g.Text))
            .Distinct(StringComparer.Ordinal).ToList();
        var labels = new LabelSet(vocabulary);
        var checkpointDir = Path.Combine(Path.GetTempPath(), "lt-e2e-page", Guid.NewGuid().ToString("N"));
        var config = new TrainingConfig
        {
            Epochs = 80, BatchSize = 8, LearningRate = 3e-3, Seed = 23,
            EarlyStoppingPatience = 80, HiddenSize = 64,
            ModelName = "e2e-page-crnn", CheckpointDirectory = checkpointDir,
        };
        new CtcTrainer(new CrnnModel(labels.NumClasses, config.HiddenSize), labels, config).Fit(train, dev);

        // Held-out pages (file split is the same rule used elsewhere).
        var xmlFiles = Directory.GetFiles(Path.Combine(m02a, "xml"), "*.xml").OrderBy(f => f, StringComparer.Ordinal).ToList();
        var pages = new List<PageTruth>();
        for (int i = 0; i < xmlFiles.Count && pages.Count < 6; i++)
        {
            if (i % 5 != 0) continue;
            var stem = Path.GetFileNameWithoutExtension(xmlFiles[i]);
            var image = Path.Combine(m02a, "images", stem + ".png");
            if (File.Exists(image)) pages.Add(new PageTruth(stem, image, xmlFiles[i]));
        }
        Skip.IfNot(pages.Count > 0, "no held-out pages with images");

        CrnnModel LoadModel()
        {
            var model = new CrnnModel(labels.NumClasses);
            model.load(CheckpointStore.ModelPath(checkpointDir, config.ModelName, ".best"));
            return model;
        }

        var store = new InMemoryStore();
        var rows = new List<(string Page, int Gt, int Detected, int Matched, double DetRecall, double MatchedCer, double PageCer, double BoxCer)>();
        long totalGtScalars = 0, totalEdits = 0, totalBoxScalars = 0, totalBoxEdits = 0;

        foreach (var page in pages)
        {
            var captureId = new EntityId(EntityKind.Capture, "cap");
            var surface = new NativeSurface
            {
                Id = new EntityId(EntityKind.Surface, "surf"),
                Kind = "codex-page",
                Captures = [new NativeCapture
                {
                    Id = captureId,
                    Frame = new CoordinateFrame { ImageRef = captureId, ImageChecksum = new Checksum { Algorithm = "unavailable", Digest = "pending" } },
                    ImageLocation = page.ImagePath,
                }],
                Regions = [],
                Lines = [],
            };
            var project = new NativeProject
            {
                Id = new EntityId(EntityKind.Collection, "col"),
                Name = "page-e2e",
                Objects = [new NativeObject { Id = new EntityId(EntityKind.PhysicalObject, "obj"), Surfaces = [surface] }],
            };
            store.SaveProject(project);

            var layout = new LayoutService(store).Detect(project, surface.Id);
            var detected = store.LoadProject(project.Id)!;
            var outcome = new RecognitionPipeline(store).Run(detected, surface.Id, new RecognitionPipeline.Options
            {
                ModelDirectory = checkpointDir,
                ModelName = config.ModelName,
                ModelVersion = "e2e-page",
                ModelFactory = () =>
                {
                    var model = new CrnnModel(labels.NumClasses);
                    model.load(CheckpointStore.ModelPath(checkpointDir, config.ModelName, ".best"));
                    return model;
                },
                LabelSetFactory = () => labels,
            }, persistL0: false);

            var truth = Truth(page.XmlPath);
            var surfaceAfter = detected.Objects[0].Surfaces[0];
            int matched = 0, gtScalars = 0, edits = 0;
            double matchedEdits = 0, matchedGtScalars = 0;

            foreach (var (_, top, _, bottom, text) in truth)
            {
                int length = Graphemes.Enumerate(text).Sum(g => g.Text.Length); // scalar length
                gtScalars += length;
                totalGtScalars += length;

                // Best vertical-IoU detected line that has a recognition result.
                var best = surfaceAfter.Lines
                    .Select(l =>
                    {
                        int lTop = (int)Math.Round(l.Polygon.Min(p => p.Y));
                        int lBottom = (int)Math.Round(l.Polygon.Max(p => p.Y));
                        int overlap = Math.Max(0, Math.Min(bottom, lBottom) - Math.Max(top, lTop));
                        int union = Math.Max(bottom, lBottom) - Math.Min(top, lTop);
                        double iou = union <= 0 ? 0 : (double)overlap / union;
                        var recognition = outcome.Lines.FirstOrDefault(o => o.LineId == l.Id);
                        return (Line: l, Iou: iou, Recognition: recognition);
                    })
                    .Where(c => c.Recognition is not null && c.Iou >= 0.5)
                    .OrderByDescending(c => c.Iou)
                    .FirstOrDefault();

                if (best.Line is null)
                {
                    edits += length; // detection miss = full deletion in page CER
                    totalEdits += length;
                    continue;
                }

                matched++;
                var score = RecognitionScorer.Score(text, best.Recognition!.L0Text);
                edits += score.ScalarDistance;
                totalEdits += score.ScalarDistance;
                matchedEdits += score.ScalarDistance;
                matchedGtScalars += score.ReferenceScalarLength;
            }

            // Control: the same model on the PAGE boxes (perfect layout). The gap between
            // this and the page CER is attributable to detection, not recognition.
            var controlCaptureId = new EntityId(EntityKind.Capture, "cap-control");
            var controlRegionId = new EntityId(EntityKind.Region, "reg-control");
            var controlSurface = new NativeSurface
            {
                Id = new EntityId(EntityKind.Surface, "surf-control"),
                Kind = surface.Kind,
                Captures = [new NativeCapture
                {
                    Id = controlCaptureId,
                    Frame = new CoordinateFrame { ImageRef = controlCaptureId, ImageChecksum = new Checksum { Algorithm = "unavailable", Digest = "pending" } },
                    ImageLocation = page.ImagePath,
                }],
                Regions =
                [
                    new NativeRegion
                    {
                        Id = controlRegionId,
                        FrameRef = controlCaptureId,
                        Polygon = [new(0, 0), new(1000, 0), new(1000, 1000), new(0, 1000)],
                        LayoutSource = LayoutSource.Imported,
                        ReadingOrderIndex = 0,
                    },
                ],
                Lines =
                [
                    .. truth.Select((t, index) => new NativeLine
                    {
                        Id = new EntityId(EntityKind.Line, $"gt-{index}"),
                        RegionId = controlRegionId,
                        FrameRef = controlCaptureId,
                        Polygon =
                        [
                            new(t.Left, t.Top), new(t.Right, t.Top),
                            new(t.Right, t.Bottom), new(t.Left, t.Bottom),
                        ],
                        Baseline = [new(t.Left, t.Bottom), new(t.Right, t.Bottom)],
                    }),
                ],
            };
            var controlProject = new NativeProject
            {
                Id = new EntityId(EntityKind.Collection, "col-control"),
                Name = "control",
                Objects = [new NativeObject { Id = new EntityId(EntityKind.PhysicalObject, "obj-control"), Surfaces = [controlSurface] }],
            };
            var controlOutcome = new RecognitionPipeline(store).Run(controlProject, controlSurface.Id, new RecognitionPipeline.Options
            {
                ModelDirectory = checkpointDir,
                ModelName = config.ModelName,
                ModelVersion = "control",
                ModelFactory = LoadModel,
                LabelSetFactory = () => labels,
            }, persistL0: false);

            double boxEdits = 0, boxScalars = 0;
            for (int i = 0; i < truth.Count; i++)
            {
                var lineId = new EntityId(EntityKind.Line, $"gt-{i}");
                var recognized = controlOutcome.Lines.FirstOrDefault(l => l.LineId == lineId);
                if (recognized is null) continue;
                var score = RecognitionScorer.Score(truth[i].Text, recognized.L0Text);
                boxEdits += score.ScalarDistance;
                boxScalars += score.ReferenceScalarLength;
            }
            totalBoxEdits += (long)boxEdits;
            totalBoxScalars += (long)boxScalars;
            var boxCer = boxScalars == 0 ? 0 : boxEdits / boxScalars;

            var pageCer = gtScalars == 0 ? 0 : edits / (double)gtScalars;
            var matchedCer = matchedGtScalars == 0 ? 0 : matchedEdits / matchedGtScalars;
            var detRecall = truth.Count == 0 ? 0 : matched / (double)truth.Count;
            rows.Add((page.Name, truth.Count, layout.LineCount, matched, detRecall, matchedCer, pageCer, boxCer));
            output.WriteLine($"{page.Name}: gt={truth.Count} det={layout.LineCount} matched={matched} " +
                             $"detRecall={detRecall:0.###} matchedCER={matchedCer:0.###} pageCER={pageCer:0.###} boxCER={boxCer:0.###}");
        }

        var aggregate = totalGtScalars == 0 ? 0 : totalEdits / (double)totalGtScalars;
        var boxAggregate = totalBoxScalars == 0 ? 0 : totalBoxEdits / (double)totalBoxScalars;
        var markdown = $"""
            # End-to-end page CER (layout + recognition), held-out manuscript pages

            - Date: {DateTimeOffset.UtcNow:O}; contract {ContractVersion.Current}
            - Model: `{config.ModelName}` (CRNN h=64, {config.Epochs} epochs max, trained on M02a train/dev lines only)
            - Pipeline: image-only surface → automatic layout → recognition; no imported layout
            - Matching: detected↔PAGE line vertical IoU ≥ 0.5; **missed GT lines count as full deletions**
            - Evaluation pages: {pages.Count} held-out M02a pages (file split i%5==0); *same books as training — workflow measurement, not a generalization claim*
            - Aggregate page CER (layout + recognition, misses as deletions): **{aggregate:0.###}** over {totalGtScalars} scalars
            - Aggregate control CER (same model, PAGE boxes = perfect layout): **{boxAggregate:0.###}** over {totalBoxScalars} scalars
            - Layout-induced CER increase: **{aggregate - boxAggregate:+0.###;-0.###;0}** absolute

            | Page | GT lines | Detected | Matched | Detection recall | CER (matched auto boxes) | Page CER (incl. misses) | Control CER (PAGE boxes) |
            | --- | --- | --- | --- | --- | --- | --- | --- |
            {string.Join("\n", rows.Select(r => $"| {r.Page} | {r.Gt} | {r.Detected} | {r.Matched} | {r.DetRecall:0.###} | {r.MatchedCer:0.###} | {r.PageCer:0.###} | {r.BoxCer:0.###} |"))}

            ## Reading

            Page CER is substantially worse than line-crop CER because layout misses (and false
            lines) are counted. That is the honest number for the image-only workflow: improving
            recognition alone cannot fix lost lines. Multi-column/curved layouts remain
            unsupported; layout quality is the current workflow bottleneck alongside label noise.
            """;
        var path = Path.Combine(root!, "docs", "evaluation", "end-to-end-page-cer.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, markdown);
        output.WriteLine($"aggregate page CER {aggregate:0.###}; box control CER {boxAggregate:0.###}");
    }
}
