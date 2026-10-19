using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Layout;
using SkiaSharp;

namespace LeviathanText.Application.Tests;

public sealed class LearnedLayoutIntegrationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"lt-learned-{Guid.NewGuid():N}");

    public LearnedLayoutIntegrationTests() => Directory.CreateDirectory(_dir);

    private string WritePage(string name, params (int Top, int Bottom)[] bands)
    {
        var path = Path.Combine(_dir, name + ".png");
        using var bitmap = new SKBitmap(260, 320);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.Black };
            foreach (var (top, bottom) in bands)
                canvas.DrawRect(new SKRect(25, top, 235, bottom), paint);
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);
        return path;
    }

    private (NativeProject Project, EntityId SurfaceId) ImageOnlySurface(string imagePath)
    {
        var captureId = new EntityId(EntityKind.Capture, "cap1");
        var surface = new NativeSurface
        {
            Id = new EntityId(EntityKind.Surface, "surf1"),
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
        var project = new NativeProject
        {
            Id = new EntityId(EntityKind.Collection, "col1"),
            Name = "learned-layout",
            Objects = [new NativeObject { Id = new EntityId(EntityKind.PhysicalObject, "obj"), Surfaces = [surface] }],
        };
        return (project, surface.Id);
    }

    [Fact]
    public void Learned_detector_can_be_selected_for_layout_detection()
    {
        var training = new List<LayoutTrainingPage>();
        for (int variant = 0; variant < 4; variant++)
        {
            var bands = new (int, int)[] { (20 + variant * 3, 50 + variant * 3), (140, 175), (250 + variant, 285 + variant) };
            training.Add(new LayoutTrainingPage($"p{variant}", WritePage($"p{variant}", bands), bands));
        }
        var model = PatchLayoutModel.Train(training, new PatchLayoutOptions
        {
            Epochs = 40, BatchSize = 2, InputHeight = 160, MaxInputWidth = 200, Seed = 11,
        });

        var image = WritePage("target", (60, 95), (190, 225));
        var (project, surfaceId) = ImageOnlySurface(image);
        var store = new InMemoryStore();
        store.SaveProject(project);

        var outcome = new LayoutService(store).Detect(project, surfaceId, model: model);

        Assert.True(outcome.LineCount > 0);
        Assert.StartsWith("patch:", outcome.Method);
        Assert.Equal(LayoutSource.Automatic, outcome.Surface.Regions[0].LayoutSource);
    }

    [Fact]
    public void Without_a_model_the_projection_detector_is_used()
    {
        var image = WritePage("projection", (40, 80), (160, 200));
        var (project, surfaceId) = ImageOnlySurface(image);
        var store = new InMemoryStore();
        store.SaveProject(project);

        var outcome = new LayoutService(store).Detect(project, surfaceId);

        Assert.StartsWith("ProjectionProfileLayout", outcome.Method);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
