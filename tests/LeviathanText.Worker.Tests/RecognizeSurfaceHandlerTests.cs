using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Infrastructure;
using LeviathanText.Recognition;
using LeviathanText.Training;
using SkiaSharp;
using Xunit;

namespace LeviathanText.Worker.Tests;

/// <summary>
/// Durable recognition job end to end: a verified model package (labels + architecture +
/// weights travel together) recognizes a surface and the raw output is persisted as L0
/// revisions with model provenance. Missing/invalid packages fail before inference.
/// </summary>
public sealed class RecognizeSurfaceHandlerTests : IDisposable
{
    private readonly string _db = Path.Combine(Path.GetTempPath(), $"lt-rec-{Guid.NewGuid():N}.db");
    private readonly string _image = Path.Combine(Path.GetTempPath(), $"lt-rec-{Guid.NewGuid():N}.png");
    private readonly string _package = Path.Combine(Path.GetTempPath(), $"lt-pkg-{Guid.NewGuid():N}");

    public RecognizeSurfaceHandlerTests()
    {
        using var surface = SKSurface.Create(new SKImageInfo(200, 100));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        using var paint = new SKPaint { Color = SKColors.Black };
        for (int x = 10; x < 190; x += 6)
            canvas.DrawLine(x, 30, x + 2, 70, paint);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(_image);
        data.SaveTo(stream);

        Directory.CreateDirectory(_package);
        var labels = new LabelSet(["α", "β"]);
        var model = new CrnnModel(labels.NumClasses, hiddenSize: 8);
        model.save(CheckpointStore.ModelPath(_package, "tiny", ".best"));
        CheckpointStore.WriteMetadata(_package, "tiny", new CheckpointMetadata
        {
            Epoch = 1,
            GlobalStep = 1,
            BestValidationCer = 1.0,
            LabelSetHash = CheckpointStore.LabelSetHash(labels),
            Labels = labels.Labels,
            ConfigHash = "test",
            HiddenSize = 8,
            InputHeightPixels = CrnnModel.InputHeight,
            WidthDownsampleFactor = CrnnModel.DefaultWidthDownsampleFactor,
            TorchVersion = "test",
            CreatedAt = DateTimeOffset.UtcNow,
        }, ".best");
    }

    private (NativeProject Project, EntityId LineId, EntityId SurfaceId) Seed(LocalStore store)
    {
        var captureId = new EntityId(EntityKind.Capture, "cap1");
        var lineId = new EntityId(EntityKind.Line, "ln1");
        var surfaceId = new EntityId(EntityKind.Surface, "surf1");
        var project = new NativeProject
        {
            Id = new EntityId(EntityKind.Collection, "col1"),
            Name = "worker-recognition",
            Objects =
            [
                new NativeObject
                {
                    Id = new EntityId(EntityKind.PhysicalObject, "obj1"),
                    Surfaces =
                    [
                        new NativeSurface
                        {
                            Id = surfaceId,
                            Kind = "page",
                            Captures = [new NativeCapture
                            {
                                Id = captureId,
                                Frame = new CoordinateFrame { ImageRef = captureId, ImageChecksum = new Checksum { Algorithm = "unavailable", Digest = "pending" } },
                                ImageLocation = _image,
                            }],
                            Regions =
                            [
                                new NativeRegion
                                {
                                    Id = new EntityId(EntityKind.Region, "reg1"),
                                    FrameRef = captureId,
                                    Polygon = [new(0, 0), new(200, 0), new(200, 100), new(0, 100)],
                                    LayoutSource = LayoutSource.Manual,
                                    ReadingOrderIndex = 0,
                                },
                            ],
                            Lines =
                            [
                                new NativeLine
                                {
                                    Id = lineId,
                                    RegionId = new EntityId(EntityKind.Region, "reg1"),
                                    FrameRef = captureId,
                                    Polygon = [new(10, 20), new(190, 20), new(190, 80), new(10, 80)],
                                    Baseline = [new(10, 78), new(190, 78)],
                                },
                            ],
                        },
                    ],
                },
            ],
        };
        store.SaveProject(project);
        return (project, lineId, surfaceId);
    }

    private JobRecord Job(EntityId surfaceId, string packageDirectory) => new()
    {
        Id = new EntityId(EntityKind.Job, "j-rec"),
        Type = RecognizeSurfaceHandler.JobType,
        Inputs = [new StageArtifact(surfaceId, new Checksum
        {
            Algorithm = "model-package",
            Digest = RecognizeSurfaceHandler.PackagePrefix + packageDirectory,
        })],
        Owner = new AgentRef { Id = "test", IsModel = false },
        Status = JobStatus.Pending,
    };

    [Fact]
    public void Verified_package_recognizes_and_persists_l0_revisions()
    {
        using var store = new LocalStore(_db);
        var (project, lineId, surfaceId) = Seed(store);

        var jobs = new MemoryJobStore();
        jobs.Save(Job(surfaceId, _package));
        var host = new WorkerHost(jobs, [new RecognizeSurfaceHandler(store)]);

        var finished = host.RunNext();
        Assert.NotNull(finished);
        Assert.Equal(JobStatus.Succeeded, finished!.Status);
        var output = Assert.Single(finished.Outputs);
        Assert.Equal("1", output.Artifact.Digest);

        var reloaded = store.LoadProject(project.Id)!;
        var revisions = reloaded.Objects[0].Surfaces[0].Revisions
            .Where(r => r.Layer == TextLayer.L0RawRecognition)
            .ToList();
        var revision = Assert.Single(revisions);
        Assert.Equal(lineId, revision.ScopeId);
        Assert.True(revision.Author.IsModel);
        Assert.Contains("tiny", revision.Author.ModelIdentity);
    }

    [Fact]
    public void Missing_package_fails_without_touching_the_project()
    {
        using var store = new LocalStore(_db);
        var (project, _, surfaceId) = Seed(store);

        var jobs = new MemoryJobStore();
        jobs.Save(Job(surfaceId, Path.Combine(_package, "missing")));
        var host = new WorkerHost(jobs, [new RecognizeSurfaceHandler(store)]) { MaxRetries = 0 };

        var finished = host.RunNext();
        Assert.Equal(JobStatus.Failed, finished!.Status);
        Assert.Empty(store.LoadProject(project.Id)!.Objects[0].Surfaces[0].Revisions);
    }

    public void Dispose()
    {
        if (File.Exists(_db)) File.Delete(_db);
        if (File.Exists(_image)) File.Delete(_image);
        if (Directory.Exists(_package)) Directory.Delete(_package, recursive: true);
    }
}
