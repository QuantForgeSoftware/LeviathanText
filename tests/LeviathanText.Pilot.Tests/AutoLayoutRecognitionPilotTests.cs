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
/// Full workflow proof (I1): image-only surface -> automatic layout -> C# CTC recognition.
/// A quick model is trained on P01 dev lines; the target page is a P01 photograph without
/// imported layout. No accuracy claim: this proves the stage wiring on real data.
/// </summary>
public sealed class AutoLayoutRecognitionPilotTests(ITestOutputHelper output)
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
    public void Image_only_page_gets_layout_then_recognition()
    {
        var root = FindRepoRoot();
        Skip.IfNot(root is not null, "repo root not found");
        var dataDir = Path.Combine(root!, ".artifacts", "datasets", "p01-v0.9");
        var image = Path.Combine(dataDir, "P.Cair.Zen.II.59263r.jpg");
        Skip.IfNot(Directory.Exists(dataDir) && File.Exists(image), "P01 artifacts not available");

        // 1) Train a small model on P01 training/dev lines (same hand; mechanics only).
        var snapshot = P01LineDataset.Load(dataDir);
        var vocabulary = snapshot.Train.Concat(snapshot.Dev)
            .SelectMany(l => Graphemes.Enumerate(l.Text).Select(g => g.Text))
            .Distinct(StringComparer.Ordinal).ToList();
        var labels = new LabelSet(vocabulary);
        var checkpointDir = Path.Combine(Path.GetTempPath(), "lt-auto-e2e", Guid.NewGuid().ToString("N"));
        var config = new TrainingConfig
        {
            Epochs = 60, BatchSize = 8, LearningRate = 3e-3, Seed = 7,
            EarlyStoppingPatience = 60, HiddenSize = 64,
            ModelName = "auto-e2e-crnn", CheckpointDirectory = checkpointDir,
        };
        var model = new CrnnModel(labels.NumClasses, config.HiddenSize);
        new CtcTrainer(model, labels, config).Fit(snapshot.Train, snapshot.Dev);

        // 2) Image-only surface.
        var captureId = new EntityId(EntityKind.Capture, "cap-e2e");
        var surface = new NativeSurface
        {
            Id = new EntityId(EntityKind.Surface, "surf-e2e"),
            Kind = "papyrus-side",
            Captures = [new NativeCapture
            {
                Id = captureId,
                Frame = new CoordinateFrame { ImageRef = captureId, ImageChecksum = new Checksum { Algorithm = "unavailable", Digest = "pending" } },
                ImageLocation = image,
            }],
            Regions = [],
            Lines = [],
        };
        var project = new NativeProject
        {
            Id = new EntityId(EntityKind.Collection, "col-e2e"),
            Name = "auto-recognition",
            Objects = [new NativeObject { Id = new EntityId(EntityKind.PhysicalObject, "obj-e2e"), Surfaces = [surface] }],
        };
        var store = new InMemoryStore();
        store.SaveProject(project);

        // 3) Auto layout.
        var layout = new LayoutService(store).Detect(project, surface.Id);
        Assert.True(layout.LineCount > 0);
        var detected = store.LoadProject(project.Id)!;

        // 4) Recognition through the Application pipeline with the trained checkpoint.
        var pipeline = new RecognitionPipeline(store);
        var outcome = pipeline.Run(detected, surface.Id, new RecognitionPipeline.Options
        {
            ModelDirectory = checkpointDir,
            ModelName = config.ModelName,
            ModelVersion = "pilot-e2e",
            ModelFactory = () =>
            {
                var fresh = new CrnnModel(labels.NumClasses);
                fresh.load(CheckpointStore.ModelPath(checkpointDir, config.ModelName, ".best"));
                return fresh;
            },
            LabelSetFactory = () => labels,
        });

        Assert.Equal(layout.LineCount, outcome.Lines.Count);
        Assert.Empty(outcome.SkippedLines); // auto-detected lines always carry geometry

        // L0 recognition is persisted as raw revisions with model provenance.
        var reloaded = store.LoadProject(project.Id)!;
        var l0 = reloaded.Objects[0].Surfaces[0].Revisions
            .Where(r => r.Layer == TextLayer.L0RawRecognition)
            .ToList();
        Assert.Equal(layout.LineCount, l0.Count);
        Assert.All(l0, r => Assert.True(r.Author.IsModel));
        Assert.All(l0, r => Assert.Contains(config.ModelName, r.Author.ModelIdentity));
        var stage = Assert.Single(outcome.Stages);
        Assert.Equal(StageName.Recognition, stage.Stage);
        Assert.Equal(config.ModelName, stage.Model!.PackageId);
        output.WriteLine($"auto-layout {layout.LineCount} lines -> L0 outputs {outcome.Lines.Count}; " +
                         $"non-empty {outcome.Lines.Count(l => !string.IsNullOrEmpty(l.L0Text))}; vocab {vocabulary.Count}");
    }
}
