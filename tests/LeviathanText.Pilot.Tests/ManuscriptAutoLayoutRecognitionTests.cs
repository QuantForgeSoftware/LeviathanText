using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Core.Unicode;
using LeviathanText.Formats;
using LeviathanText.Recognition;
using LeviathanText.Training;
using Xunit;
using Xunit.Abstractions;

namespace LeviathanText.Pilot.Tests;

/// <summary>
/// Manuscript-track workflow proof: a page with no imported layout gets automatic
/// layout, then a model trained on M02a lines recognizes it. Mechanics only — the page
/// is in the training books' domain, and no accuracy claim is made from one page.
/// </summary>
public sealed class ManuscriptAutoLayoutRecognitionTests(ITestOutputHelper output)
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

    [SkippableFact]
    public void Manuscript_page_gets_layout_then_recognition()
    {
        var root = FindRepoRoot();
        Skip.IfNot(root is not null, "repo root not found");
        var m02a = Path.Combine(root!, ".artifacts", "datasets", "m02a-v1.0", "m02a");
        Skip.IfNot(Directory.Exists(m02a), "M02a artifacts not available");

        // Train on M02a train/dev lines (markup-contaminated labels excluded).
        var snapshot = P01LineDataset.Load(Path.Combine(m02a, "xml"), imageExtension: ".png");
        bool Contaminated(string text) => text.Any(c => c is '<' or '>' or '&' or '[' or ']');
        var train = snapshot.Train.Where(l => !Contaminated(l.Text)).ToList();
        var dev = snapshot.Dev.Where(l => !Contaminated(l.Text)).ToList();
        var vocabulary = train.Concat(dev)
            .SelectMany(l => Graphemes.Enumerate(l.Text).Select(g => g.Text))
            .Distinct(StringComparer.Ordinal).ToList();
        var labels = new LabelSet(vocabulary);
        var checkpointDir = Path.Combine(Path.GetTempPath(), "lt-manuscript-e2e", Guid.NewGuid().ToString("N"));
        var config = new TrainingConfig
        {
            Epochs = 80, BatchSize = 8, LearningRate = 3e-3, Seed = 11,
            EarlyStoppingPatience = 80, HiddenSize = 64,
            ModelName = "mss-e2e-crnn", CheckpointDirectory = checkpointDir,
        };
        new CtcTrainer(new CrnnModel(labels.NumClasses, config.HiddenSize), labels, config).Fit(train, dev);

        // A held-out page, image-only (its PAGE layout is NOT imported here).
        var pageStem = Path.GetFileNameWithoutExtension(
            Directory.GetFiles(Path.Combine(m02a, "xml"), "*.xml").OrderBy(f => f, StringComparer.Ordinal).First(f => f.Contains("_0016_")));
        var pageImage = Path.Combine(m02a, "images", pageStem + ".png");
        Skip.IfNot(File.Exists(pageImage), "page image missing");

        var captureId = new EntityId(EntityKind.Capture, "cap-mss");
        var surface = new NativeSurface
        {
            Id = new EntityId(EntityKind.Surface, "surf-mss"),
            Kind = "codex-page",
            Captures = [new NativeCapture
            {
                Id = captureId,
                Frame = new CoordinateFrame { ImageRef = captureId, ImageChecksum = new Checksum { Algorithm = "unavailable", Digest = "pending" } },
                ImageLocation = pageImage,
            }],
            Regions = [],
            Lines = [],
        };
        var project = new NativeProject
        {
            Id = new EntityId(EntityKind.Collection, "col-mss"),
            Name = "mss-auto",
            Objects = [new NativeObject { Id = new EntityId(EntityKind.PhysicalObject, "obj-mss"), Surfaces = [surface] }],
        };
        var store = new InMemoryStore();
        store.SaveProject(project);

        var layout = new LayoutService(store).Detect(project, surface.Id);
        Assert.True(layout.LineCount > 0, "manuscript page should yield detected lines");
        var detected = store.LoadProject(project.Id)!;

        var outcome = new RecognitionPipeline(store).Run(detected, surface.Id, new RecognitionPipeline.Options
        {
            ModelName = config.ModelName,
            ModelVersion = "pilot-mss-e2e",
            ModelFactory = () =>
            {
                var fresh = new CrnnModel(labels.NumClasses);
                fresh.load(CheckpointStore.ModelPath(checkpointDir, config.ModelName, ".best"));
                return fresh;
            },
            LabelSetFactory = () => labels,
        });

        Assert.Equal(layout.LineCount, outcome.Lines.Count);
        Assert.Empty(outcome.SkippedLines);
        output.WriteLine($"manuscript page: layout {layout.LineCount} lines, L0 non-empty " +
                         $"{outcome.Lines.Count(l => !string.IsNullOrEmpty(l.L0Text))}; vocab {vocabulary.Count}");
    }
}
