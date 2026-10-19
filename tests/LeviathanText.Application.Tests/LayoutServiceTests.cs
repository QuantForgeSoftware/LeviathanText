using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using SkiaSharp;

namespace LeviathanText.Application.Tests;

public sealed class LayoutServiceTests : IDisposable
{
    private readonly string _image = Path.Combine(Path.GetTempPath(), $"lt-layout-{Guid.NewGuid():N}.png");

    public LayoutServiceTests()
    {
        // A page larger than the preview budget: detection runs scaled, coordinates map back.
        using var surface = SKSurface.Create(new SKImageInfo(1200, 800));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        using var paint = new SKPaint { Color = SKColors.Black };
        canvas.DrawRect(new SKRect(100, 100, 1100, 140), paint);
        canvas.DrawRect(new SKRect(100, 400, 1100, 440), paint);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        using var stream = File.OpenWrite(_image);
        data.SaveTo(stream);
    }

    private (NativeProject Project, EntityId SurfaceId) Seed(InMemoryStore store)
    {
        var captureId = new EntityId(EntityKind.Capture, "cap1");
        var surface = new NativeSurface
        {
            Id = new EntityId(EntityKind.Surface, "surf1"),
            Kind = "page",
            Captures = [new NativeCapture
            {
                Id = captureId,
                Frame = new CoordinateFrame { ImageRef = captureId, ImageChecksum = new Checksum { Algorithm = "unavailable", Digest = "pending" } },
                ImageLocation = _image,
                WidthPx = 1200,
                HeightPx = 800,
            }],
            Regions = [],
            Lines = [],
        };
        var project = new NativeProject
        {
            Id = new EntityId(EntityKind.Collection, "col1"),
            Name = "layout-scale",
            Objects = [new NativeObject { Id = new EntityId(EntityKind.PhysicalObject, "obj1"), Surfaces = [surface] }],
        };
        store.SaveProject(project);
        return (project, surface.Id);
    }

    [Fact]
    public void Scaled_detection_maps_geometry_back_to_the_original_frame()
    {
        var store = new InMemoryStore();
        var (project, surfaceId) = Seed(store);

        var outcome = new LayoutService(store).Detect(project, surfaceId, maxPixels: 300_000);

        Assert.True(outcome.LineCount >= 2);
        Assert.All(outcome.Surface.Lines, line =>
        {
            Assert.All(line.Polygon, p =>
            {
                Assert.InRange(p.X, 0, 1200);
                Assert.InRange(p.Y, 0, 800);
            });
        });

        var tops = outcome.Surface.Lines.Select(l => l.Polygon.Min(p => p.Y)).OrderBy(y => y).ToList();
        Assert.Contains(tops, y => Math.Abs(y - 100) <= 25);
        Assert.Contains(tops, y => Math.Abs(y - 400) <= 25);
    }

    public void Dispose()
    {
        if (File.Exists(_image)) File.Delete(_image);
    }
}
