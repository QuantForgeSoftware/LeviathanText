using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Core.Unicode;
using LeviathanText.Evaluation;
using LeviathanText.Formats;
using LeviathanText.Imaging;
using LeviathanText.Layout;
using LeviathanText.Recognition;
using LeviathanText.Training;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;
using ScalarText = LeviathanText.Core.Unicode.ScalarText;

namespace LeviathanText.Pilot.Tests;

/// <summary>
/// Measures the effect of the learned layout model on the image-only workflow: same
/// recognition model, same pages, projection vs patch-CNN detection. Matching is one-to-one
/// greedy; missed GT lines count as deletions. One seed (recognition variance is measured
/// separately in end-to-end-page-cer.md).
/// </summary>
public sealed class LayoutImpactPageCerTests(ITestOutputHelper output)
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

    private static NativeSurface Surface(string imagePath)
    {
        var captureId = new EntityId(EntityKind.Capture, "cap");
        return new NativeSurface
        {
            Id = new EntityId(EntityKind.Surface, "surf"),
            Kind = "codex-page",
            Captures = [new NativeCapture
            {
                Id = captureId,
                Frame = new CoordinateFrame { ImageRef = captureId, ImageChecksum = new Checksum { Algorithm = "unavailable", Digest = "pending" } },
                ImageLocation = imagePath,
            }],
            Regions = [],
            Lines = [],
        };
    }

    private static NativeProject Project(NativeSurface surface) => new()
    {
        Id = new EntityId(EntityKind.Collection, "col"),
        Name = "layout-impact",
        Objects = [new NativeObject { Id = new EntityId(EntityKind.PhysicalObject, "obj"), Surfaces = [surface] }],
    };

    private static (double PageCer, double DetectionRecall) Evaluate(
        NativeProject project, NativeSurface surface, IReadOnlyList<TruthLine> truth,
        RecognitionPipeline.Options recognitionOptions)
    {
        var outcome = new RecognitionPipeline(new InMemoryStore()).Run(project, surface.Id, recognitionOptions, persistL0: false);
        var detected = project.Objects[0].Surfaces[0].Lines;

        var candidates = new List<(int Gt, int Det, double Iou)>();
        for (int g = 0; g < truth.Count; g++)
            for (int d = 0; d < detected.Count; d++)
            {
                if (outcome.Lines.All(r => r.LineId != detected[d].Id)) continue;
                int top = (int)Math.Round(detected[d].Polygon.Min(p => p.Y));
                int bottom = (int)Math.Round(detected[d].Polygon.Max(p => p.Y));
                int overlap = Math.Max(0, Math.Min(truth[g].Bottom, bottom) - Math.Max(truth[g].Top, top));
                int union = Math.Max(truth[g].Bottom, bottom) - Math.Min(truth[g].Top, top);
                double iou = union <= 0 ? 0 : (double)overlap / union;
                if (iou >= 0.5) candidates.Add((g, d, iou));
            }

        var usedGt = new bool[truth.Count];
        var usedDet = new bool[detected.Count];
        int matched = 0, edits = 0, gtScalars = 0;
        foreach (var (g, d, _) in candidates.OrderByDescending(c => c.Iou).ThenBy(c => c.Gt).ThenBy(c => c.Det))
        {
            if (usedGt[g] || usedDet[d]) continue;
            usedGt[g] = true;
            usedDet[d] = true;
            matched++;
            var recognized = outcome.Lines.First(r => r.LineId == detected[d].Id);
            edits += RecognitionScorer.Score(truth[g].Text, recognized.L0Text).ScalarDistance;
        }
        for (int g = 0; g < truth.Count; g++)
        {
            int length = ScalarText.ScalarLength(truth[g].Text);
            gtScalars += length;
            if (!usedGt[g]) edits += length;
        }

        return (gtScalars == 0 ? 0 : edits / (double)gtScalars,
            truth.Count == 0 ? 0 : (double)matched / truth.Count);
    }

    [SkippableFact]
    public void Learned_layout_improves_page_cer_over_projection()
    {
        var root = FindRepoRoot();
        Skip.IfNot(root is not null, "repo root not found");
        var m02a = Path.Combine(root!, ".artifacts", "datasets", "m02a-v1.0", "m02a");
        Skip.IfNot(Directory.Exists(m02a), "M02a not available");

        // Layout training pages (same page-level split as the layout reports).
        var xmlFiles = Directory.GetFiles(Path.Combine(m02a, "xml"), "*.xml").OrderBy(f => f, StringComparer.Ordinal).ToList();
        var layoutTrain = new List<LayoutTrainingPage>();
        var pages = new List<PageTruth>();
        for (int i = 0; i < xmlFiles.Count; i++)
        {
            var stem = Path.GetFileNameWithoutExtension(xmlFiles[i]);
            var image = Path.Combine(m02a, "images", stem + ".png");
            if (!File.Exists(image)) continue;
            var bands = Truth(xmlFiles[i]).Select(t => (t.Top, t.Bottom)).ToList();
            if (bands.Count == 0) continue;
            if (i % 5 == 0) pages.Add(new PageTruth(stem, image, xmlFiles[i]));
            else if (i % 6 != 1) layoutTrain.Add(new LayoutTrainingPage(stem, image, bands));
        }
        Skip.IfNot(layoutTrain.Count > 0 && pages.Count > 0, "insufficient pages");

        var layoutModel = PatchLayoutModel.Train(layoutTrain, new PatchLayoutOptions
        {
            Epochs = 100, Seed = 31, Threshold = 0.55f,
        }, message => output.WriteLine("layout " + message));
        output.WriteLine($"layout model trained on {layoutTrain.Count} pages, threshold 0.55 (dev-selected in layout-patch-learning.md)");

        // Recognition model: same data/split and seed as the page-CER pilot.
        var snapshot = P01LineDataset.Load(Path.Combine(m02a, "xml"), imageExtension: ".png");
        bool Contaminated(string text) => text.Any(c => c is '<' or '>' or '&' or '[' or ']');
        var train = snapshot.Train.Where(l => !Contaminated(l.Text)).ToList();
        var dev = snapshot.Dev.Where(l => !Contaminated(l.Text)).ToList();
        var vocabulary = train.Concat(dev)
            .SelectMany(l => Graphemes.Enumerate(l.Text).Select(g => g.Text))
            .Distinct(StringComparer.Ordinal).ToList();
        var labels = new LabelSet(vocabulary);
        var checkpointDir = Path.Combine(Path.GetTempPath(), "lt-layout-impact", Guid.NewGuid().ToString("N"));
        var config = new TrainingConfig
        {
            Epochs = 80, BatchSize = 8, LearningRate = 3e-3, Seed = 23,
            EarlyStoppingPatience = 80, HiddenSize = 64,
            ModelName = "layout-impact-crnn", CheckpointDirectory = checkpointDir,
        };
        new CtcTrainer(new CrnnModel(labels.NumClasses, config.HiddenSize), labels, config).Fit(train, dev);

        RecognitionPipeline.Options Recognition() => new()
        {
            ModelDirectory = checkpointDir,
            ModelName = config.ModelName,
            ModelVersion = "layout-impact",
            ModelFactory = () =>
            {
                var model = new CrnnModel(labels.NumClasses);
                model.load(CheckpointStore.ModelPath(checkpointDir, config.ModelName, ".best"));
                return model;
            },
            LabelSetFactory = () => labels,
        };

        var store = new InMemoryStore();
        var rows = new List<(string Page, int Gt, double ProjCer, double PatchCer, double ProjRecall, double PatchRecall)>();
        double projEdits = 0, patchEdits = 0, gtTotal = 0;

        foreach (var page in pages)
        {
            var truth = Truth(page.XmlPath);

            var projectionSurface = Surface(page.ImagePath);
            var projectionProject = Project(projectionSurface);
            store.SaveProject(projectionProject);
            new LayoutService(store).Detect(projectionProject, projectionSurface.Id);
            var projectionProjectLoaded = store.LoadProject(projectionProject.Id)!;
            var projection = Evaluate(projectionProjectLoaded, projectionProjectLoaded.Objects[0].Surfaces[0], truth, Recognition());

            var patchSurface = Surface(page.ImagePath);
            var patchProject = Project(patchSurface);
            store.SaveProject(patchProject);
            new LayoutService(store).Detect(patchProject, patchSurface.Id, model: layoutModel);
            var patchProjectLoaded = store.LoadProject(patchProject.Id)!;
            var patch = Evaluate(patchProjectLoaded, patchProjectLoaded.Objects[0].Surfaces[0], truth, Recognition());

            int gtScalars = truth.Sum(t => ScalarText.ScalarLength(t.Text));
            gtTotal += gtScalars;
            projEdits += projection.PageCer * gtScalars;
            patchEdits += patch.PageCer * gtScalars;
            rows.Add((page.Name, truth.Count, projection.PageCer, patch.PageCer, projection.DetectionRecall, patch.DetectionRecall));
            output.WriteLine($"{page.Name}: projCER={projection.PageCer:0.###} patchCER={patch.PageCer:0.###} " +
                             $"projRecall={projection.DetectionRecall:0.###} patchRecall={patch.DetectionRecall:0.###}");
        }

        var projAggregate = gtTotal == 0 ? 0 : projEdits / gtTotal;
        var patchAggregate = gtTotal == 0 ? 0 : patchEdits / gtTotal;
        output.WriteLine($"aggregate: projection {projAggregate:0.###}, patch {patchAggregate:0.###} (one-to-one matching)");

        var markdown = $"""
            # Layout impact on the image-only page workflow

            - Date: {DateTimeOffset.UtcNow:O}; contract {ContractVersion.Current}
            - Same recognition model/seed, same pages; only the layout detector changes
            - Layout model: `{PatchLayoutModel.Architecture}` trained on {layoutTrain.Count} M02a train pages, threshold 0.55
            - Evaluation: {pages.Count} held-out M02a pages (one seed; recognition variance is measured separately)
            - Matching: one-to-one greedy by vertical IoU ≥ 0.5; missed GT lines count as full deletions

            | Detector | Aggregate page CER | Mean detection recall |
            | --- | --- | --- |
            | Projection | {projAggregate:0.###} | {rows.Average(r => r.ProjRecall):0.###} |
            | Patch CNN (learned) | {patchAggregate:0.###} | {rows.Average(r => r.PatchRecall):0.###} |

            | Page | GT | Projection CER | Patch CER | Projection recall | Patch recall |
            | --- | --- | --- | --- | --- | --- |
            {string.Join("\n", rows.Select(r => $"| {r.Page} | {r.Gt} | {r.ProjCer:0.###} | {r.PatchCer:0.###} | {r.ProjRecall:0.###} | {r.PatchRecall:0.###} |"))}

            ## Reading

            The learned detector improves page-level text recovery mainly by finding lines the
            projection heuristic misses; recognition quality on matched lines is unchanged. This
            is a single-book, single-seed workflow measurement — not a generalization or
            acceptance claim. The model remains opt-in and per-material.
            """;
        var path = Path.Combine(root!, "docs", "evaluation", "layout-impact-page-cer.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, markdown);

        Assert.True(patchAggregate < projAggregate,
            $"expected learned layout to reduce page CER (projection {projAggregate:0.###}, patch {patchAggregate:0.###})");
    }
}
