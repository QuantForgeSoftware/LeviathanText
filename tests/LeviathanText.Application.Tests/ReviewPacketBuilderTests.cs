using LeviathanText.Contracts;
using LeviathanText.Formats;
using SkiaSharp;

namespace LeviathanText.Application.Tests;

public class ReviewPacketBuilderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"lt-packet-{Guid.NewGuid():N}");

    public ReviewPacketBuilderTests() => Directory.CreateDirectory(_dir);

    [Fact]
    public void Builds_crops_html_and_decision_csv()
    {
        var image = Path.Combine(_dir, "page.png");
        using (var surface = SKSurface.Create(new SKImageInfo(220, 120)))
        {
            surface.Canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.Black };
            surface.Canvas.DrawRect(new SKRect(20, 30, 200, 60), paint);
            using var snapshot = surface.Snapshot();
            using var data = snapshot.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.Create(image);
            data.SaveTo(stream);
        }

        var captureId = new EntityId(EntityKind.Capture, "cap-1");
        var lineId = new EntityId(EntityKind.Line, "ln-1");
        var line = new NativeLine
        {
            Id = lineId,
            RegionId = new EntityId(EntityKind.Region, "reg-1"),
            FrameRef = captureId,
            Polygon = [new(20, 30), new(200, 30), new(200, 60), new(20, 60)],
        };
        var l1 = new TextRevision
        {
            Id = new EntityId(EntityKind.TranscriptionRevision, "rev-l1"),
            ScopeId = lineId, Layer = TextLayer.L1Diplomatic,
            Author = new AgentRef { Id = "test", IsModel = false },
            CreatedAt = DateTimeOffset.UtcNow, Text = "αβγ",
        };
        var l2 = l1 with
        {
            Id = new EntityId(EntityKind.TranscriptionRevision, "rev-l2"),
            Layer = TextLayer.L2Editorial, Text = "ἀβγ",
        };
        var project = new NativeProject
        {
            Id = new EntityId(EntityKind.Collection, "col-1"), Name = "packet",
            Objects = [new NativeObject
            {
                Id = new EntityId(EntityKind.PhysicalObject, "obj-1"),
                Surfaces = [new NativeSurface
                {
                    Id = new EntityId(EntityKind.Surface, "surf-1"), Kind = "page",
                    Captures = [new NativeCapture
                    {
                        Id = captureId,
                        Frame = new CoordinateFrame { ImageRef = captureId, ImageChecksum = new Checksum { Algorithm = "sha256", Digest = new string('a', 64) } },
                        ImageLocation = image, WidthPx = 220, HeightPx = 120,
                    }],
                    Regions = [new NativeRegion { Id = new EntityId(EntityKind.Region, "reg-1"), FrameRef = captureId }],
                    Lines = [line], Revisions = [l1, l2],
                }],
            }],
        };

        var result = ReviewPacketBuilder.Build(project, Path.Combine(_dir, "packet"));
        Assert.Equal(1, result.LineCount);
        Assert.True(File.Exists(result.IndexPath));
        Assert.Equal("line_id,surface_id,decision,corrected_text,notes\n", File.ReadAllText(result.DecisionsPath));
        var html = File.ReadAllText(result.IndexPath);
        Assert.Contains("αβγ", html);
        Assert.Contains("ἀβγ", html);
        Assert.Single(Directory.GetFiles(Path.Combine(_dir, "packet", "crops"), "*.png"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
