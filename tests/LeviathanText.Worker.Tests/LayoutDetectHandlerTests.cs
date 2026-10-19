using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Infrastructure;
using SkiaSharp;
using Xunit;

namespace LeviathanText.Worker.Tests;

/// <summary>
/// Durable layout job end to end: enqueue with a surface input, claim through the host,
/// detect layout on real image bytes, finalize with an output artifact. Existing layout
/// is never overwritten (the handler surfaces StageContractViolation).
/// </summary>
public sealed class LayoutDetectHandlerTests : IDisposable
{
    private readonly string _db = Path.Combine(Path.GetTempPath(), $"lt-worker-{Guid.NewGuid():N}.db");
    private readonly string _image = Path.Combine(Path.GetTempPath(), $"lt-worker-{Guid.NewGuid():N}.png");

    public LayoutDetectHandlerTests()
    {
        using var surface = SKSurface.Create(new SKImageInfo(120, 80));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        using var paint = new SKPaint { Color = SKColors.Black };
        canvas.DrawRect(new SKRect(10, 10, 110, 20), paint);
        canvas.DrawRect(new SKRect(10, 40, 110, 50), paint);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(_image);
        data.SaveTo(stream);
    }

    private (NativeProject Project, EntityId SurfaceId) Seed(LocalStore store)
    {
        var captureId = new EntityId(EntityKind.Capture, "cap1");
        var surfaceId = new EntityId(EntityKind.Surface, "surf1");
        var project = new NativeProject
        {
            Id = new EntityId(EntityKind.Collection, "col1"),
            Name = "worker-layout",
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
                            Regions = [],
                            Lines = [],
                        },
                    ],
                },
            ],
        };
        store.SaveProject(project);
        return (project, surfaceId);
    }

    private static JobRecord LayoutJob(EntityId surfaceId) => new()
    {
        Id = new EntityId(EntityKind.Job, "j-layout"),
        Type = LayoutDetectHandler.JobType,
        Inputs = [new StageArtifact(surfaceId, new Checksum { Algorithm = "surface", Digest = "" })],
        Owner = new AgentRef { Id = "test", IsModel = false },
        Status = JobStatus.Pending,
    };

    [Fact]
    public void Runs_detection_and_finalizes_with_an_output_artifact()
    {
        using var store = new LocalStore(_db);
        var (project, surfaceId) = Seed(store);

        var jobs = new MemoryJobStore();
        jobs.Save(LayoutJob(surfaceId));
        var host = new WorkerHost(jobs, [new LayoutDetectHandler(store)]);

        var finished = host.RunNext();
        Assert.NotNull(finished);
        Assert.Equal(JobStatus.Succeeded, finished!.Status);
        var output = Assert.Single(finished.Outputs);
        Assert.Equal(surfaceId, output.Entity);
        Assert.True(int.Parse(output.Artifact.Digest) >= 1); // at least one detected line

        var reloaded = store.LoadProject(project.Id)!;
        Assert.True(reloaded.Objects[0].Surfaces[0].Lines.Count >= 1);
        Assert.Equal(LayoutSource.Automatic, reloaded.Objects[0].Surfaces[0].Regions[0].LayoutSource);
    }

    [Fact]
    public void Existing_layout_is_not_overwritten_and_the_job_retries_then_fails()
    {
        using var store = new LocalStore(_db);
        var (project, surfaceId) = Seed(store);

        var jobs = new MemoryJobStore();
        jobs.Save(LayoutJob(surfaceId));
        var host = new WorkerHost(jobs, [new LayoutDetectHandler(store)]) { MaxRetries = 1 };
        host.RunNext(); // succeeds once

        // A second job for the same surface must not overwrite; retry budget exhausts.
        var second = LayoutJob(surfaceId) with { Id = new EntityId(EntityKind.Job, "j-layout-2") };
        jobs.Save(second);

        var retried = host.RunNext();
        Assert.Equal(JobStatus.Pending, retried!.Status);  // retry queued (StageContractViolation)
        Assert.Contains("StageContractViolation", retried.Failure!.ToString());

        var failed = host.RunNext();
        Assert.Equal(JobStatus.Failed, failed!.Status);

        var reloaded = store.LoadProject(project.Id)!;
        Assert.Single(reloaded.Objects[0].Surfaces[0].Regions); // unchanged
    }

    public void Dispose()
    {
        if (File.Exists(_db)) File.Delete(_db);
        if (File.Exists(_image)) File.Delete(_image);
    }
}
