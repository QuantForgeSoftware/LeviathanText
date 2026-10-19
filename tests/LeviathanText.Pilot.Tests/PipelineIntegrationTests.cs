using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Core.Unicode;
using LeviathanText.Evaluation;
using LeviathanText.Formats;
using LeviathanText.Recognition;
using LeviathanText.Training;
using Xunit;
using Xunit.Abstractions;

namespace LeviathanText.Pilot.Tests;

/// <summary>
/// I1 integration proof: native persistence + imported geometry + C# recognition,
/// end to end on real data — train briefly on P01, run the Application-layer
/// RecognitionPipeline over dev lines, score L0 against the diplomatic labels with the
/// contract scorer. Skipped when the artifact store is absent.
/// </summary>
public sealed class PipelineIntegrationTests(ITestOutputHelper output)
{
    private static string FindDataDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".artifacts", "datasets", "p01-v0.9")))
            dir = dir.Parent;
        return dir is null ? "" : Path.Combine(dir.FullName, ".artifacts", "datasets", "p01-v0.9");
    }

    [SkippableFact]
    public void Train_ThenRecognizeThroughPipeline_ThenScore()
    {
        var dataDir = FindDataDir();
        Skip.IfNot(dataDir != "", "P01 dataset not downloaded.");

        var snapshot = P01LineDataset.Load(dataDir);
        var vocabulary = snapshot.Train.Concat(snapshot.Dev)
            .SelectMany(l => Graphemes.Enumerate(l.Text).Select(g => g.Text))
            .Distinct(StringComparer.Ordinal).ToList();
        var labels = new LabelSet(vocabulary);

        var checkpointDir = Path.Combine(Path.GetTempPath(), "lt-pipeline-test", Guid.NewGuid().ToString("N"));
        var config = new TrainingConfig
        {
            Epochs = 120, BatchSize = 8, LearningRate = 3e-3, Seed = 42,
            EarlyStoppingPatience = 120, HiddenSize = 64,
            ModelName = "pipeline-crnn", CheckpointDirectory = checkpointDir,
        };

        var model = new CrnnModel(labels.NumClasses, config.HiddenSize);
        var trainer = new CtcTrainer(model, labels, config);
        var run = trainer.Fit(snapshot.Train, snapshot.Dev);

        // Build a native project from the dev witness files (imported geometry).
        var project = new NativeProject
        {
            Id = new EntityId(EntityKind.Collection, "pilotdev"),
            Name = "p01-dev",
            Objects = [],
        };
        var surfaceIds = new List<EntityId>();
        foreach (var (baseName, split) in snapshot.WitnessSplitMap)
        {
            if (split != "dev") continue;
            var imported = PageXml.Import(
                File.ReadAllText(Path.Combine(dataDir, baseName + ".xml")),
                new EntityId(EntityKind.Capture, "cap"), new Checksum { Algorithm = "unavailable", Digest = "pending" });
            var captureId = new EntityId(EntityKind.Capture, Guid.NewGuid().ToString("N")[..12]);
            var surface = new NativeSurface
            {
                Id = new EntityId(EntityKind.Surface, Guid.NewGuid().ToString("N")[..12]),
                Kind = "papyrus-side",
                Captures = [new NativeCapture
                {
                    Id = captureId,
                    Frame = new CoordinateFrame { ImageRef = captureId, ImageChecksum = new Checksum { Algorithm = "unavailable", Digest = "pending" } },
                    ImageLocation = Path.Combine(dataDir, baseName + ".jpg"),
                }],
                Regions = imported.Regions,
                Lines = imported.Lines,
                Revisions = imported.Revisions,
            };
            surfaceIds.Add(surface.Id);
            project = project with { Objects = [.. project.Objects.Append(new NativeObject
            {
                Id = new EntityId(EntityKind.PhysicalObject, Guid.NewGuid().ToString("N")[..12]),
                Surfaces = [surface],
            })] };
        }

        // Run the pipeline per surface with the freshly trained model.
        var pipeline = new RecognitionPipeline(new InMemoryStoreAdapter());
        int distance = 0, referenceLength = 0;
        foreach (var surfaceId in surfaceIds)
        {
            var outcome = pipeline.Run(project, surfaceId, new RecognitionPipeline.Options
            {
                ModelDirectory = checkpointDir,
                ModelFactory = () => LoadBest(checkpointDir, labels),
                LabelSetFactory = () => labels,
            });

            foreach (var line in outcome.Lines)
            {
                // Score the L0 output against the surface's own diplomatic label (dev set).
                var truth = project.Objects.SelectMany(o => o.Surfaces).Single(s => s.Id == surfaceId)
                    .Revisions.FirstOrDefault(r => r.ScopeId == line.LineId)?.Text ?? "";
                var score = RecognitionScorer.Score(truth, line.L0Text);
                distance += score.ScalarDistance;
                referenceLength += score.ReferenceScalarLength;
            }
        }

        double cer = referenceLength == 0 ? 1.0 : (double)distance / referenceLength;
        Assert.True(cer < 1.0, $"pipeline CER {cer:0.###} must beat the empty baseline");
        output.WriteLine($"Pipeline dev CER: {cer:0.###} (surface-level, imported layout, trained {run.History.Count} epochs)");
    }

    private static CrnnModel LoadBest(string checkpointDir, LabelSet labels)
    {
        var fresh = new CrnnModel(labels.NumClasses);
        fresh.load(CheckpointStore.ModelPath(checkpointDir, "pipeline-crnn", ".best"));
        return fresh;
    }

    private sealed class InMemoryStoreAdapter : IProjectStore
    {
        public void SaveProject(NativeProject project) { }
        public NativeProject? LoadProject(EntityId id) => null;
        public IReadOnlyList<(EntityId Id, string Name)> ListProjects() => [];
    }
}
