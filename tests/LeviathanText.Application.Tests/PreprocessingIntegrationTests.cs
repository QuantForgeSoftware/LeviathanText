using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Imaging;
using LeviathanText.Recognition;
using SkiaSharp;

namespace LeviathanText.Application.Tests;

public sealed class PreprocessingIntegrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lt-preparation-app", Guid.NewGuid().ToString("N"));

    public PreprocessingIntegrationTests() => Directory.CreateDirectory(_root);

    private static SKBitmap Page()
    {
        var bitmap = new SKBitmap(80, 48);
        bitmap.Erase(new SKColor(173, 142, 107));
        for (int y = 14; y < 34; y++)
        for (int x = 12; x < 68; x++)
            if (x % 9 < 4) bitmap.SetPixel(x, y, new SKColor(37, 31, 26));
        return bitmap;
    }

    private static NativeLine Line() => new()
    {
        Id = new(EntityKind.Line, "line"), RegionId = new(EntityKind.Region, "region"),
        FrameRef = new(EntityKind.Capture, "capture"),
        Polygon = [new(8, 8), new(72, 8), new(72, 40), new(8, 40)],
    };

    private NativeProject Project(string image) => new()
    {
        Id = new(EntityKind.Collection, "project"), Name = "preparation",
        Objects = [new NativeObject
        {
            Id = new(EntityKind.PhysicalObject, "object"), Surfaces = [new NativeSurface
            {
                Id = new(EntityKind.Surface, "surface"), Kind = "page",
                Captures = [new NativeCapture
                {
                    Id = new(EntityKind.Capture, "capture"), ImageLocation = image,
                    Frame = new CoordinateFrame { ImageRef = new(EntityKind.Capture, "capture"), ImageChecksum = new() { Algorithm = "unavailable", Digest = "pending" } },
                }],
                Lines = [Line()], Regions = [],
            }],
        }],
    };

    [Fact]
    public void Legacy_static_preparation_preserves_exact_original_pixels()
    {
        using var page = Page();
        var actual = RecognitionPipeline.ToLineImage(page);
        int width = Math.Max(8, (int)Math.Round((float)page.Width * 16 / page.Height));
        using var resized = page.Resize(new SKImageInfo(width, 16), new SKSamplingOptions(SKFilterMode.Linear));
        var expected = new float[width * 16];
        var bytes = resized.Bytes;
        for (int p = 0, b = 0; p < expected.Length; p++, b += resized.Info.BytesPerPixel)
            expected[p] = 1f - (.299f * bytes[b + 2] + .587f * bytes[b + 1] + .114f * bytes[b]) / 255f;
        Assert.Equal(16, actual.Height);
        Assert.Equal(width, actual.Width);
        Assert.Equal(0f, actual.BackgroundValue);
        Assert.Equal(expected, actual.Pixels);
    }

    [Theory]
    [InlineData("p01")]
    [InlineData("legacy")]
    [InlineData("luminance")]
    [InlineData("local-background")]
    public void Pipeline_uses_declared_profile_and_model_height(string recipe)
    {
        var profile = recipe switch
        {
            "p01" => LinePreprocessingProfile.P01Otsu(32),
            "legacy" => LinePreprocessingProfile.LegacyInkPositive(32),
            "local-background" => LinePreprocessingProfile.LocalBackground(32),
            _ => LinePreprocessingProfile.Luminance(32),
        };
        string image = Path.Combine(_root, "page.png");
        using (var bitmap = Page())
        using (var data = bitmap.Encode(SKEncodedImageFormat.Png, 100))
        using (var stream = File.Create(image)) data.SaveTo(stream);
        using var decoded = ImageDecoder.Decode(image);
        var crop = LineCropper.Extract(decoded.Bitmap, Line().Polygon.Select(p => new SKPoint((float)p.X, (float)p.Y)).ToArray());
        using var extracted = crop.Bitmap;
        var expectedInput = LinePreprocessor.Apply(extracted, profile);
        var appInput = RecognitionPipeline.ToLineImage(extracted, profile);
        Assert.Equal(expectedInput.Pixels, appInput.Pixels);
        Assert.Equal(expectedInput.BackgroundValue, appInput.BackgroundValue);
        var labels = new LabelSet(["α", "β"]);
        using var model = new CrnnModel(labels.NumClasses, hiddenSize: 8, inputHeight: 32);
        var expected = new TorchLineRecognizer(model, labels).Recognize([expectedInput])[0];
        var outcome = new RecognitionPipeline(new InMemoryStore()).Run(Project(image), new(EntityKind.Surface, "surface"), new()
        {
            ModelFactory = () => model, LabelSetFactory = () => labels, PreprocessingProfile = profile,
        }, persistL0: false);
        var actual = Assert.Single(outcome.Lines);
        Assert.Equal(expected.Text, actual.L0Text);
        Assert.Equal(expected.Graphemes, actual.Confidence);
        Assert.Equal("crop>" + profile.Describe(), actual.TransformChain);
    }

    [Fact]
    public void Legacy_task_constructors_respect_custom_model_height()
    {
        var labels = new LabelSet(["α", "β"]);
        using var model = new CrnnModel(labels.NumClasses, hiddenSize: 8, inputHeight: 32);
        using var page = Page();
        var missingGeometry = Line() with { Id = new(EntityKind.Line, "missing"), Polygon = [], Baseline = [] };
        var records = new RecognitionTaskModel(model, labels).Predict(page, [Line(), missingGeometry]);
        Assert.Equal(2, records.Count);
        Assert.Null(records[1].Bbox);
        var alignment = new ForcedAlignmentTaskModel(model, labels).Predict(page, [(Line(), "αβ")]);
        Assert.Equal("αβ", Assert.Single(alignment).Text);
    }

    [Fact]
    public void Page_task_models_reject_external_geometry_and_wrong_profile_height()
    {
        var labels = new LabelSet(["α", "β"]);
        using var model = new CrnnModel(labels.NumClasses, hiddenSize: 8, inputHeight: 32);
        Assert.Throws<InvalidDataException>(() => new RecognitionTaskModel(model, labels, LinePreprocessingProfile.Luminance(16)));
        var external = LinePreprocessingProfile.Luminance(32) with { Geometry = "externally-prepared-line" };
        using var page = Page();
        Assert.Throws<InvalidOperationException>(() => new RecognitionTaskModel(model, labels, external).Predict(page, [Line()]));
        Assert.Throws<InvalidOperationException>(() => new ForcedAlignmentTaskModel(model, labels, external).Predict(page, [(Line(), "α")]));
    }

    public void Dispose() => Directory.Delete(_root, true);
}
