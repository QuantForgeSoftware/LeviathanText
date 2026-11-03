using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Infrastructure;
using LeviathanText.Recognition;
using LeviathanText.Training;
using SkiaSharp;

namespace LeviathanText.Worker.Tests;

public sealed class PreprocessingHandlerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lt-preprocessing-worker", Guid.NewGuid().ToString("N"));

    public PreprocessingHandlerTests() => Directory.CreateDirectory(_root);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Worker_propagates_declared_preparation_and_rejects_external_page_geometry(bool external)
    {
        string image = Path.Combine(_root, "page.png");
        using (var bitmap = new SKBitmap(80, 48))
        {
            bitmap.Erase(SKColors.White);
            using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.Create(image);
            data.SaveTo(stream);
        }
        string package = Path.Combine(_root, "package");
        Directory.CreateDirectory(package);
        var profile = LinePreprocessingProfile.Luminance(32) with { Geometry = external ? "externally-prepared-line" : "line-crop" };
        var labels = new LabelSet(["α", "β"]);
        using (var model = new CrnnModel(labels.NumClasses, hiddenSize: 8, inputHeight: 32))
            model.save(CheckpointStore.ModelPath(package, "fixture", ".best"));
        CheckpointStore.WriteMetadata(package, "fixture", new CheckpointMetadata
        {
            Epoch = 1, GlobalStep = 1, BestValidationCer = 1, LabelSetHash = CheckpointStore.LabelSetHash(labels),
            Labels = labels.Labels, ConfigHash = "fixture", HiddenSize = 8, InputHeightPixels = 32,
            WidthDownsampleFactor = 4, TorchVersion = "fixture", CreatedAt = DateTimeOffset.UtcNow,
            PreprocessingProfile = profile,
        }, ".best");
        var surface = new NativeSurface
        {
            Id = new(EntityKind.Surface, "surface"), Kind = "page", Regions = [],
            Captures = [new NativeCapture
            {
                Id = new(EntityKind.Capture, "capture"), ImageLocation = image,
                Frame = new CoordinateFrame { ImageRef = new(EntityKind.Capture, "capture"), ImageChecksum = new() { Algorithm = "unavailable", Digest = "pending" } },
            }],
            Lines = [new NativeLine
            {
                Id = new(EntityKind.Line, "line"), RegionId = new(EntityKind.Region, "region"), FrameRef = new(EntityKind.Capture, "capture"),
                Polygon = [new(8, 8), new(72, 8), new(72, 40), new(8, 40)],
            }],
        };
        var project = new NativeProject { Id = new(EntityKind.Collection, "project"), Name = "profile", Objects = [new NativeObject { Id = new(EntityKind.PhysicalObject, "object"), Surfaces = [surface] }] };
        using var store = new LocalStore(Path.Combine(_root, "store.db"));
        store.SaveProject(project);
        var job = new JobRecord
        {
            Id = new(EntityKind.Job, "job"), Type = RecognizeSurfaceHandler.JobType,
            Inputs = [new StageArtifact(surface.Id, new Checksum { Algorithm = "model-package", Digest = RecognizeSurfaceHandler.PackagePrefix + package })],
            Owner = new AgentRef { Id = "fixture", IsModel = false }, Status = JobStatus.Pending,
        };
        if (external)
        {
            Assert.Throws<InvalidOperationException>(() => new RecognizeSurfaceHandler(store).Execute(job, CancellationToken.None));
            Assert.Empty(store.LoadProject(project.Id)!.Objects[0].Surfaces[0].Revisions);
        }
        else
        {
            var completed = new RecognizeSurfaceHandler(store).Execute(job, CancellationToken.None);
            Assert.Equal("1", Assert.Single(completed.Outputs).Artifact.Digest);
            Assert.Single(store.LoadProject(project.Id)!.Objects[0].Surfaces[0].Revisions);
        }
    }

    public void Dispose() => Directory.Delete(_root, true);
}
