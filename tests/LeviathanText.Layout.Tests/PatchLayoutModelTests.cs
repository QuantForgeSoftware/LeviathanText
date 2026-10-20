using SkiaSharp;

namespace LeviathanText.Layout.Tests;

public sealed class PatchLayoutModelTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"lt-patch-{Guid.NewGuid():N}");

    public PatchLayoutModelTests() => Directory.CreateDirectory(_dir);

    private static SKBitmap Page(int width, int height, params (int Top, int Bottom)[] bands)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        using var paint = new SKPaint { Color = SKColors.Black };
        foreach (var (top, bottom) in bands)
            canvas.DrawRect(new SKRect(width * 0.1f, top, width * 0.9f, bottom), paint);
        return bitmap;
    }

    private string SavePage(string name, SKBitmap bitmap)
    {
        var path = Path.Combine(_dir, name + ".png");
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);
        return path;
    }

    [Fact]
    public void Trains_on_synthetic_bands_and_detects_them()
    {
        var pages = new List<LayoutTrainingPage>();
        for (int variant = 0; variant < 6; variant++)
        {
            var bands = new (int, int)[]
            {
                (30 + variant * 5, 55 + variant * 5),
                (120, 150),
                (230 + variant * 3, 260 + variant * 3),
            };
            using var bitmap = Page(320, 360, bands);
            pages.Add(new LayoutTrainingPage($"synthetic-{variant}", SavePage($"synthetic-{variant}", bitmap), bands));
        }

        var model = PatchLayoutModel.Train(pages, new PatchLayoutOptions
        {
            Epochs = 80,
            BatchSize = 3,
            LearningRate = 1e-3,
            InputHeight = 192,
            MaxInputWidth = 192,
            Seed = 7,
        });

        using var target = Page(320, 360, (60, 90), (160, 190), (270, 300));
        var result = model.Detect(target);

        Assert.StartsWith("patch:", result.Method);
        Assert.InRange(result.Lines.Count, 2, 4);
        var tops = result.Lines.Select(l => l.Top).OrderBy(t => t).ToList();
        Assert.Contains(tops, t => Math.Abs(t - 60) <= 22);
        Assert.Contains(tops, t => Math.Abs(t - 160) <= 22);
        Assert.Contains(tops, t => Math.Abs(t - 270) <= 22);
    }

    [Fact]
    public void Augmented_training_still_learns_synthetic_bands()
    {
        var pages = new List<LayoutTrainingPage>();
        for (int variant = 0; variant < 4; variant++)
        {
            var bands = new (int, int)[] { (25 + variant * 2, 50 + variant * 2), (130, 160), (240, 270) };
            using var bitmap = Page(300, 340, bands);
            pages.Add(new LayoutTrainingPage($"aug-{variant}", SavePage($"aug-{variant}", bitmap), bands));
        }

        var model = PatchLayoutModel.Train(pages, new PatchLayoutOptions
        {
            Epochs = 60, BatchSize = 2, InputHeight = 160, MaxInputWidth = 200, Seed = 13,
            AugmentTraining = true, AugmentShiftRows = 5,
        });

        using var target = Page(300, 340, (55, 85), (165, 195));
        var result = model.Detect(target);
        Assert.NotEmpty(result.Lines);
        Assert.True(result.Lines.Count <= 4);
    }

    [Fact]
    public void Save_and_load_roundtrip_reproduces_detection()
    {
        using var page = Page(240, 300, (40, 70), (150, 180));
        var path = SavePage("roundtrip", page);
        var training = new List<LayoutTrainingPage>
        {
            new("a", path, [(40, 70), (150, 180)]),
            new("b", path, [(40, 70), (150, 180)]),
        };
        var model = PatchLayoutModel.Train(training, new PatchLayoutOptions
        {
            Epochs = 20, BatchSize = 2, InputHeight = 128, MaxInputWidth = 256, Seed = 3,
        });

        var dir = Path.Combine(_dir, "package");
        model.Save(dir);
        var loaded = PatchLayoutModel.Load(dir);

        using var target = Page(240, 300, (40, 70), (150, 180));
        var first = model.Detect(target).Lines.Select(l => (l.Top, l.Bottom)).ToList();
        var second = loaded.Detect(target).Lines.Select(l => (l.Top, l.Bottom)).ToList();
        Assert.Equal(first, second);
        Assert.True(File.Exists(Path.Combine(dir, "model.pt")));
        Assert.True(File.Exists(Path.Combine(dir, "layout-metadata.json")));
        Assert.True(File.Exists(Path.Combine(dir, "model-card.md")));
        var card = File.ReadAllText(Path.Combine(dir, "model-card.md"));
        Assert.Contains("Layout model card", card);
        Assert.Contains("SHA-256", card);
    }

    [Fact]
    public void Tampered_weights_are_rejected_by_the_recorded_digest()
    {
        using var page = Page(240, 300, (40, 70));
        var path = SavePage("tamper", page);
        var model = PatchLayoutModel.Train(
            [new LayoutTrainingPage("a", path, [(40, 70)])],
            new PatchLayoutOptions { Epochs = 10, InputHeight = 128, MaxInputWidth = 128, Seed = 6 });
        var dir = Path.Combine(_dir, "tamper-package");
        model.Save(dir);

        var weights = Path.Combine(dir, "model.pt");
        File.AppendAllText(weights, "tampered");
        var ex = Assert.Throws<InvalidDataException>(() => PatchLayoutModel.Load(dir));
        Assert.Contains("digest", ex.Message);
    }

    [Fact]
    public void Training_data_digest_is_order_independent_and_band_sensitive()
    {
        var first = new LayoutTrainingPage("a", "/tmp/a.png", [(10, 20), (30, 40)]);
        var second = new LayoutTrainingPage("b", "/tmp/b.png", [(50, 60)]);
        var reordered = new LayoutTrainingPage[]
        {
            new("b", "/tmp/b.png", [(50, 60)]),
            new("a", "/tmp/a.png", [(10, 20), (30, 40)]),
        };

        Assert.Equal(PatchLayoutModel.DataDigestOf([first, second]),
            PatchLayoutModel.DataDigestOf(reordered));
        Assert.NotEqual(PatchLayoutModel.DataDigestOf([first, second]),
            PatchLayoutModel.DataDigestOf([first with { Bands = [(10, 25)] }, second]));
    }

    [Fact]
    public void Incompatible_architecture_is_rejected()
    {
        var dir = Path.Combine(_dir, "bad");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "layout-metadata.json"),
            """{"Architecture":"different-model","InputHeight":128,"InputWidth":64,"Threshold":0.5}""");
        var ex = Assert.Throws<InvalidDataException>(() => PatchLayoutModel.Load(dir));
        Assert.Contains("not compatible", ex.Message);
    }

    [Fact]
    public void Threshold_can_be_changed_without_retraining()
    {
        using var page = Page(240, 300, (40, 70));
        var path = SavePage("threshold", page);
        var model = PatchLayoutModel.Train(
            [new LayoutTrainingPage("a", path, [(40, 70)])],
            new PatchLayoutOptions { Epochs = 10, InputHeight = 128, MaxInputWidth = 256, Seed = 5 });

        var stricter = model.WithThreshold(0.9f);
        Assert.Equal(0.9f, stricter.Threshold);
        Assert.True(model.Threshold == 0.5f);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
