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
using ScalarText = LeviathanText.Core.Unicode.ScalarText;
using Xunit.Abstractions;

namespace LeviathanText.Pilot.Tests;

/// <summary>
/// End-to-end page measurement: image-only page -> automatic layout -> recognition, with a
/// PAGE-box control. Detected lines are matched to PAGE lines by vertical IoU (>= 0.5);
/// missed GT lines count as full deletions. CPU training is not bit-reproducible across
/// runs, so this reports several seeds and the spread rather than one lucky number.
/// </summary>
public sealed class PageEndToEndPilotTests(ITestOutputHelper output)
{
    private static readonly int[] Seeds = [23, 24, 25];

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
    private sealed record TruthLine(int Left, int Top, int Right, int Bottom, string Text);

    private static List<TruthLine> Truth(string xmlPath)
    {
        var imported = PageXml.Import(File.ReadAllText(xmlPath), new EntityId(EntityKind.Capture, "cap"),
            new Checksum { Algorithm = "unavailable", Digest = "pending" });
        var texts = imported.Revisions.ToDictionary(r => r.Id, r => r.Text);
        var lines = new List<TruthLine>();
        foreach (var line in imported.Lines)
        {
            var geometry = line.Polygon.Count >= 3 ? line.Polygon : line.Baseline;
            if (geometry.Count < 2) continue;
            var text = line.DiplomaticRevisionId is { } id && texts.TryGetValue(id, out var t) ? t : "";
            lines.Add(new TruthLine(
                (int)geometry.Min(p => p.X), (int)geometry.Min(p => p.Y),
                (int)geometry.Max(p => p.X), (int)geometry.Max(p => p.Y), text));
        }
        return lines;
    }

    private sealed record PageEvaluation(int Gt, int Detected, int Matched, double MatchedCer, double PageCer, double BoxCer);

    private static PageEvaluation Evaluate(
        NativeProject detected, NativeSurface surface, IReadOnlyList<TruthLine> truth,
        string imagePath, RecognitionPipeline.Options options)
    {
        var outcome = new RecognitionPipeline(new InMemoryStore()).Run(detected, surface.Id, options, persistL0: false);
        var surfaceAfter = detected.Objects[0].Surfaces[0];

        int matched = 0, gtScalars = 0, edits = 0;
        double matchedEdits = 0, matchedGtScalars = 0;

        foreach (var line in truth)
        {
            int length = ScalarText.ScalarLength(line.Text);
            gtScalars += length;
            var best = surfaceAfter.Lines
                .Select(l =>
                {
                    int top = (int)Math.Round(l.Polygon.Min(p => p.Y));
                    int bottom = (int)Math.Round(l.Polygon.Max(p => p.Y));
                    int overlap = Math.Max(0, Math.Min(line.Bottom, bottom) - Math.Max(line.Top, top));
                    int union = Math.Max(line.Bottom, bottom) - Math.Min(line.Top, top);
                    double iou = union <= 0 ? 0 : (double)overlap / union;
                    return (l.Id, Iou: iou, Recognized: outcome.Lines.FirstOrDefault(o => o.LineId == l.Id));
                })
                .Where(c => c.Recognized is not null && c.Iou >= 0.5)
                .OrderByDescending(c => c.Iou)
                .FirstOrDefault();

            if (best.Recognized is null)
            {
                edits += length;
                continue;
            }

            matched++;
            var score = RecognitionScorer.Score(line.Text, best.Recognized.L0Text);
            edits += score.ScalarDistance;
            matchedEdits += score.ScalarDistance;
            matchedGtScalars += score.ReferenceScalarLength;
        }

        // Control: the same model on PAGE boxes (perfect layout).
        var controlSurface = BuildControlSurface(surface, truth, imagePath);
        var controlProject = new NativeProject
        {
            Id = new EntityId(EntityKind.Collection, "col-control"),
            Name = "control",
            Objects = [new NativeObject { Id = new EntityId(EntityKind.PhysicalObject, "obj-control"), Surfaces = [controlSurface] }],
        };
        var controlOutcome = new RecognitionPipeline(new InMemoryStore()).Run(controlProject, controlSurface.Id, options, persistL0: false);
        double boxEdits = 0, boxScalars = 0;
        for (int i = 0; i < truth.Count; i++)
        {
            var recognized = controlOutcome.Lines.FirstOrDefault(l => l.LineId == new EntityId(EntityKind.Line, $"gt-{i}"));
            if (recognized is null) continue;
            var score = RecognitionScorer.Score(truth[i].Text, recognized.L0Text);
            boxEdits += score.ScalarDistance;
            boxScalars += score.ReferenceScalarLength;
        }

        return new PageEvaluation(
            truth.Count,
            surfaceAfter.Lines.Count,
            matched,
            matchedGtScalars == 0 ? 0 : matchedEdits / matchedGtScalars,
            gtScalars == 0 ? 0 : edits / (double)gtScalars,
            boxScalars == 0 ? 0 : boxEdits / boxScalars);
    }

    private static NativeSurface BuildControlSurface(NativeSurface source, IReadOnlyList<TruthLine> truth, string imagePath)
    {
        var captureId = new EntityId(EntityKind.Capture, "cap-control");
        var regionId = new EntityId(EntityKind.Region, "reg-control");
        return new NativeSurface
        {
            Id = new EntityId(EntityKind.Surface, "surf-control"),
            Kind = source.Kind,
            Captures = [new NativeCapture
            {
                Id = captureId,
                Frame = new CoordinateFrame { ImageRef = captureId, ImageChecksum = new Checksum { Algorithm = "unavailable", Digest = "pending" } },
                ImageLocation = imagePath,
            }],
            Regions = [new NativeRegion
            {
                Id = regionId,
                FrameRef = captureId,
                Polygon = [new(0, 0), new(1000, 0), new(1000, 1000), new(0, 1000)],
                LayoutSource = LayoutSource.Imported,
                ReadingOrderIndex = 0,
            }],
            Lines = truth.Select((t, index) => new NativeLine
            {
                Id = new EntityId(EntityKind.Line, $"gt-{index}"),
                RegionId = regionId,
                FrameRef = captureId,
                Polygon = [new(t.Left, t.Top), new(t.Right, t.Top), new(t.Right, t.Bottom), new(t.Left, t.Bottom)],
                Baseline = [new(t.Left, t.Bottom), new(t.Right, t.Bottom)],
            }).ToList(),
        };
    }

    [SkippableFact]
    public void Page_cer_includes_layout_misses_across_seeds()
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

        // Detect layout once (independent of the seed), then evaluate several trained models.
        var store = new InMemoryStore();
        var prepared = new List<(PageTruth Page, NativeProject Project, NativeSurface Surface, List<TruthLine> Truth)>();
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
            prepared.Add((page, detected, detected.Objects[0].Surfaces[0], Truth(page.XmlPath)));
            output.WriteLine($"{page.Name}: layout {layout.LineCount} lines");
        }

        var seedResults = new List<(int Seed, double PageCer, double BoxCer, double DetRecall)>();
        foreach (var seed in Seeds)
        {
            var checkpointDir = Path.Combine(Path.GetTempPath(), "lt-e2e-page", Guid.NewGuid().ToString("N"));
            var config = new TrainingConfig
            {
                Epochs = 80, BatchSize = 8, LearningRate = 3e-3, Seed = seed,
                EarlyStoppingPatience = 80, HiddenSize = 64,
                ModelName = "e2e-page-crnn", CheckpointDirectory = checkpointDir,
            };
            new CtcTrainer(new CrnnModel(labels.NumClasses, config.HiddenSize), labels, config).Fit(train, dev);

            double gtTotal = 0, pageEdits = 0, boxTotal = 0, boxEdits = 0;
            int gtLines = 0, matched = 0;

            foreach (var (page, project, surface, truth) in prepared)
            {
                var evaluation = Evaluate(project, surface, truth, page.ImagePath, new RecognitionPipeline.Options
                {
                    ModelDirectory = checkpointDir,
                    ModelName = config.ModelName,
                    ModelVersion = $"seed-{seed}",
                    ModelFactory = () =>
                    {
                        var model = new CrnnModel(labels.NumClasses);
                        model.load(CheckpointStore.ModelPath(checkpointDir, config.ModelName, ".best"));
                        return model;
                    },
                    LabelSetFactory = () => labels,
                });

                gtLines += evaluation.Gt;
                matched += evaluation.Matched;
                gtTotal += evaluation.Gt;
                pageEdits += evaluation.PageCer * evaluation.Gt;
                boxTotal += evaluation.Gt;
                boxEdits += evaluation.BoxCer * evaluation.Gt;
            }

            var pageCer = gtTotal == 0 ? 0 : pageEdits / gtTotal;
            var boxCer = boxTotal == 0 ? 0 : boxEdits / boxTotal;
            var detRecall = gtLines == 0 ? 0 : (double)matched / gtLines;
            seedResults.Add((seed, pageCer, boxCer, detRecall));
            output.WriteLine($"seed {seed}: pageCER={pageCer:0.###} boxCER={boxCer:0.###} detRecall={detRecall:0.###}");
        }

        var meanPage = seedResults.Average(r => r.PageCer);
        var meanBox = seedResults.Average(r => r.BoxCer);
        var meanRecall = seedResults.Average(r => r.DetRecall);

        var markdown = $"""
            # End-to-end page CER (layout + recognition), held-out manuscript pages

            - Date: {DateTimeOffset.UtcNow:O}; contract {ContractVersion.Current}
            - Pipeline: image-only surface → automatic layout → recognition; control = same model on PAGE boxes
            - Matching: detected↔PAGE line vertical IoU ≥ 0.5; **missed GT lines count as full deletions**
            - Evaluation pages: {pages.Count} held-out M02a pages (file split i%5==0); *same books as training — workflow measurement, not a generalization claim*
            - Seeds: {string.Join(", ", Seeds)} (CPU training is not bit-reproducible across runs; report the spread, not one run)

            | Seed | Page CER (incl. misses) | Control CER (PAGE boxes) | Detection recall |
            | --- | --- | --- | --- |
            {string.Join("\n", seedResults.Select(r => $"| {r.Seed} | {r.PageCer:0.###} | {r.BoxCer:0.###} | {r.DetRecall:0.###} |"))}
            | **mean** | **{meanPage:0.###}** | **{meanBox:0.###}** | **{meanRecall:0.###}** |

            Single-run variance is material (see the control column, which varies from
            {seedResults.Min(r => r.BoxCer):0.###} to {seedResults.Max(r => r.BoxCer):0.###} with identical code and data).
            The earlier single-seed note of 0.938/0.881 was one favorable draw from this
            distribution and should not be quoted as a stable result.

            ## Reading

            Page CER includes layout misses and false lines; it is the honest number for the
            image-only workflow. With this model quality the mean page-vs-control difference
            ({meanPage - meanBox:+0.###;-0.###;0}) is comparable to the seed spread, so this run **cannot cleanly
            separate layout cost from training variance**. Detection recall is stable
            ({meanRecall:0.###}) and caps the achievable page CER regardless of recognition quality:
            improving recognition alone cannot recover lost lines. A stronger, more stable model
            and more seeds are required before attributing page-level differences to layout.
            Multi-column/curved layouts remain unsupported.
            """;
        var path = Path.Combine(root!, "docs", "evaluation", "end-to-end-page-cer.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, markdown);
        output.WriteLine($"mean page CER {meanPage:0.###}; mean box control {meanBox:0.###}");
    }
}
