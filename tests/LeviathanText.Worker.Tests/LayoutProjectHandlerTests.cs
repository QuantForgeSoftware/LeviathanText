using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Infrastructure;
using SkiaSharp;
using Xunit;

namespace LeviathanText.Worker.Tests;

/// <summary>
/// Batch layout across a project: image-only surfaces get detected; surfaces with existing
/// layout are skipped (never overwritten); counts are reported in the output artifact.
/// </summary>
public sealed class LayoutProjectHandlerTests : IDisposable
{
    private readonly string _db = Path.Combine(Path.GetTempPath(), $"lt-layproj-{Guid.NewGuid():N}.db");
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"lt-layproj-img-{Guid.NewGuid():N}");

    public LayoutProjectHandlerTests() => Directory.CreateDirectory(_dir);

    private string WriteImage(string name)
    {
        var path = Path.Combine(_dir, name + ".png");
        using var surface = SKSurface.Create(new SKImageInfo(240, 160));
        surface.Canvas.Clear(SKColors.White);
        using var paint = new SKPaint { Color = SKColors.Black };
        surface.Canvas.DrawRect(new SKRect(20, 30, 220, 50), paint);
        surface.Canvas.DrawRect(new SKRect(20, 100, 220, 120), paint);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);
        return path;
    }

    private static NativeSurface ImageOnly(string suffix, string imagePath) => new()
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
            ImageLocation = imagePath,
        }],
        Regions = [],
        Lines = [],
    };

    private NativeProject Seed(LocalStore store)
    {
        var withLayout = ImageOnly("done", WriteImage("done")) with
        {
            Regions = [new NativeRegion
            {
                Id = new EntityId(EntityKind.Region, "reg-done"),
                FrameRef = new EntityId(EntityKind.Capture, "cap-done"),
                Polygon = [new(0, 0), new(10, 0), new(10, 10), new(0, 10)],
                LayoutSource = LayoutSource.Manual,
                ReadingOrderIndex = 0,
            }],
            Lines = [new NativeLine
            {
                Id = new EntityId(EntityKind.Line, "ln-done"),
                RegionId = new EntityId(EntityKind.Region, "reg-done"),
                FrameRef = new EntityId(EntityKind.Capture, "cap-done"),
                Polygon = [new(1, 1), new(9, 1), new(9, 9), new(1, 9)],
                Baseline = [new(1, 8), new(9, 8)],
            }],
        };

        var project = new NativeProject
        {
            Id = new EntityId(EntityKind.Collection, "col-layout-batch"),
            Name = "layout-batch",
            Objects = [new NativeObject
            {
                Id = new EntityId(EntityKind.PhysicalObject, "obj"),
                Surfaces = [ImageOnly("a", WriteImage("a")), ImageOnly("b", WriteImage("b")), withLayout],
            }],
        };
        store.SaveProject(project);
        return project;
    }

    [Fact]
    public void Detects_missing_layout_and_skips_existing()
    {
        using var store = new LocalStore(_db);
        var project = Seed(store);

        var job = new JobRecord
        {
            Id = new EntityId(EntityKind.Job, "j-layout-project"),
            Type = LayoutProjectHandler.JobType,
            Inputs = [new StageArtifact(project.Id, new Checksum { Algorithm = "project", Digest = "" })],
            Owner = new AgentRef { Id = "test", IsModel = false },
            Status = JobStatus.Pending,
        };
        var jobs = new MemoryJobStore();
        jobs.Save(job);
        var host = new WorkerHost(jobs, [new LayoutProjectHandler(store)]);

        var finished = host.RunNext();
        Assert.Equal(JobStatus.Succeeded, finished!.Status);
        var digest = Assert.Single(finished.Outputs).Artifact.Digest;
        Assert.StartsWith("surfaces=2;lines=", digest);
        Assert.Contains(";failed=0;skipped=1", digest);

        var reloaded = store.LoadProject(project.Id)!;
        var surfaces = reloaded.Objects[0].Surfaces;
        Assert.All(surfaces.Take(2), s =>
        {
            Assert.NotEmpty(s.Regions);
            Assert.NotEmpty(s.Lines);
            Assert.Equal(LayoutSource.Automatic, s.Regions[0].LayoutSource);
        });
        // Existing manual layout untouched.
        Assert.Single(surfaces[2].Lines);
        Assert.Equal(LayoutSource.Manual, surfaces[2].Regions[0].LayoutSource);
    }

    public void Dispose()
    {
        if (File.Exists(_db)) File.Delete(_db);
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
