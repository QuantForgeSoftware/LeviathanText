using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Infrastructure;
using LeviathanText.Recognition;
using LeviathanText.Training;
using SkiaSharp;
using Xunit;

namespace LeviathanText.Worker.Tests;

/// <summary>
/// Durable batch recognition across a project: each surface with lines gets L0 revisions
/// with provenance; the job output records surface and line counts.
/// </summary>
public sealed class RecognizeProjectHandlerTests : IDisposable
{
    private readonly string _db = Path.Combine(Path.GetTempPath(), $"lt-recproj-{Guid.NewGuid():N}.db");
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"lt-recproj-img-{Guid.NewGuid():N}");
    private readonly string _package = Path.Combine(Path.GetTempPath(), $"lt-recproj-pkg-{Guid.NewGuid():N}");

    public RecognizeProjectHandlerTests()
    {
        Directory.CreateDirectory(_dir);
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

    private string WriteImage(string name)
    {
        var path = Path.Combine(_dir, name + ".png");
        using var surface = SKSurface.Create(new SKImageInfo(200, 100));
        surface.Canvas.Clear(SKColors.White);
        using var paint = new SKPaint { Color = SKColors.Black };
        for (int x = 10; x < 190; x += 6) surface.Canvas.DrawLine(x, 30, x + 2, 70, paint);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);
        return path;
    }

    private NativeProject Seed(LocalStore store)
    {
        NativeSurface Surface(string suffix) => new()
        {
            Id = new EntityId(EntityKind.Surface, "surf-" + suffix),
            Kind = "page",
            Captures = [new NativeCapture
            {
                Id = new EntityId(EntityKind.Capture, "cap-" + suffix),
                Frame = new CoordinateFrame
                {
                    ImageRef = new EntityId(EntityKind.Capture, "cap-" + suffix),
                    ImageChecksum = new Checksum { Algorithm = "unavailable", Digest = "pending" },
                },
                ImageLocation = WriteImage(suffix),
            }],
            Regions = [new NativeRegion
            {
                Id = new EntityId(EntityKind.Region, "reg-" + suffix),
                FrameRef = new EntityId(EntityKind.Capture, "cap-" + suffix),
                Polygon = [new(0, 0), new(200, 0), new(200, 100), new(0, 100)],
                LayoutSource = LayoutSource.Manual,
                ReadingOrderIndex = 0,
            }],
            Lines = [new NativeLine
            {
                Id = new EntityId(EntityKind.Line, "ln-" + suffix),
                RegionId = new EntityId(EntityKind.Region, "reg-" + suffix),
                FrameRef = new EntityId(EntityKind.Capture, "cap-" + suffix),
                Polygon = [new(10, 20), new(190, 20), new(190, 80), new(10, 80)],
                Baseline = [new(10, 78), new(190, 78)],
            }],
        };

        var project = new NativeProject
        {
            Id = new EntityId(EntityKind.Collection, "col-batch"),
            Name = "batch-recognition",
            Objects = [new NativeObject { Id = new EntityId(EntityKind.PhysicalObject, "obj"), Surfaces = [Surface("a"), Surface("b")] }],
        };
        store.SaveProject(project);
        return project;
    }

    [Fact]
    public void Recognizes_every_surface_and_persists_l0()
    {
        using var store = new LocalStore(_db);
        var project = Seed(store);

        var job = new JobRecord
        {
            Id = new EntityId(EntityKind.Job, "j-project-rec"),
            Type = RecognizeProjectHandler.JobType,
            Inputs = [new StageArtifact(project.Id, new Checksum
            {
                Algorithm = "model-package",
                Digest = RecognizeSurfaceHandler.PackagePrefix + _package,
            })],
            Owner = new AgentRef { Id = "test", IsModel = false },
            Status = JobStatus.Pending,
        };
        var jobs = new MemoryJobStore();
        jobs.Save(job);
        var host = new WorkerHost(jobs, [new RecognizeProjectHandler(store)]);

        var finished = host.RunNext();
        Assert.Equal(JobStatus.Succeeded, finished!.Status);
        Assert.Equal("surfaces=2;lines=2;failed=0", Assert.Single(finished.Outputs).Artifact.Digest);

        var reloaded = store.LoadProject(project.Id)!;
        var surfaces = reloaded.Objects[0].Surfaces;
        Assert.All(surfaces, s => Assert.Single(s.Revisions, r => r.Layer == TextLayer.L0RawRecognition));
        Assert.All(surfaces, s => Assert.All(
            s.Revisions.Where(r => r.Layer == TextLayer.L0RawRecognition),
            r => Assert.True(r.Author.IsModel)));
    }

    public void Dispose()
    {
        if (File.Exists(_db)) File.Delete(_db);
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        if (Directory.Exists(_package)) Directory.Delete(_package, recursive: true);
    }
}
