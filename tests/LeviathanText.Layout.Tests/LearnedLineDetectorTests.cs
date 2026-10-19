using SkiaSharp;

namespace LeviathanText.Layout.Tests;

public class LearnedLineDetectorTests
{
    /// <summary>Page with dark bands at fixed positions (same convention as the projection tests).</summary>
    private static SKBitmap Page(params (int Top, int Height)[] bands)
    {
        using var surface = SKSurface.Create(new SKImageInfo(200, bands.Max(b => b.Top + b.Height) + 40));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        using var paint = new SKPaint { Color = SKColors.Black };
        foreach (var (top, h) in bands)
            canvas.DrawRect(new SKRect(30, top + 20, 170, top + 20 + h), paint);
        using var image = surface.Snapshot();
        return SKBitmap.FromImage(image);
    }

    private static LearnedLineDetector.TrainingExample Example(SKBitmap page, IReadOnlyList<(int Top, int Height)> bands)
    {
        // Page() draws each band at top+20; labels use (top, bottom) in page pixels.
        var shifted = bands.Select(b => (b.Top + 20, b.Top + 20 + b.Height)).ToList();
        return new LearnedLineDetector.TrainingExample(
            RowFeatureExtractor.Profile(page),
            RowFeatureExtractor.Labels(page.Height, shifted));
    }

    [Fact]
    public void Trains_and_detects_the_same_synthetic_bands()
    {
        var trainingPages = new List<LearnedLineDetector.TrainingExample>();
        for (int variant = 0; variant < 6; variant++)
        {
            using var page = Page((0, 12), (30 + variant * 4, 12), (70 + variant * 5, 12));
            trainingPages.Add(Example(page, [(0, 12), (30 + variant * 4, 12), (70 + variant * 5, 12)]));
        }
        var detector = LearnedLineDetector.Train(trainingPages);

        using var target = Page((0, 12), (36, 12), (76, 12));
        var result = detector.Detect(target);

        using var trainPage = Page((0, 12), (30, 12), (70, 12));
        var trainFit = detector.Detect(trainPage);

        Assert.True(result.Lines.Count == 3,
            $"detected {result.Lines.Count}: {string.Join("; ", result.Lines)}; train-fit {trainFit.Lines.Count}; " +
            $"weights={string.Join(",", detector.Weights.Select(w => w.ToString("F2")))}; bias={detector.Bias:F2}");
        Assert.StartsWith("learned:", result.Method);
        // Bands roughly where the ink is (tolerance: the model smooths via features).
        Assert.InRange(result.Lines[0].Top, 15, 26);
        Assert.InRange(result.Lines[2].Top, 90, 102);
    }

    [Fact]
    public void Blank_page_yields_no_lines()
    {
        var trainingPages = new List<LearnedLineDetector.TrainingExample>();
        using (var page = Page((0, 12)))
            trainingPages.Add(Example(page, [(0, 12)]));
        var detector = LearnedLineDetector.Train(trainingPages);

        using var blank = new SKBitmap(120, 80);
        blank.Erase(SKColors.White);
        Assert.Empty(detector.Detect(blank).Lines);
    }

    [Fact]
    public void Persisted_model_round_trips_and_reproduces_results()
    {
        var trainingPages = new List<LearnedLineDetector.TrainingExample>();
        using (var page = Page((0, 12), (40, 12)))
            trainingPages.Add(Example(page, [(0, 12), (40, 12)]));
        var detector = LearnedLineDetector.Train(trainingPages);

        var json = detector.ToJson();
        var loaded = LearnedLineDetector.FromJson(json);

        using var target = Page((0, 12), (40, 12));
        var first = detector.Detect(target);
        var second = loaded.Detect(target);
        Assert.Equal(first.Lines.Select(l => (l.Top, l.Bottom)), second.Lines.Select(l => (l.Top, l.Bottom)));
    }

    [Fact]
    public void Incompatible_feature_version_is_rejected()
    {
        var json = """{"ModelVersion":"row-logit-v1","Weights":[0],"Bias":0,"FeatureVersion":99}""";
        var ex = Assert.Throws<InvalidDataException>(() => LearnedLineDetector.FromJson(json));
        Assert.Contains("incompatible", ex.Message);
    }

    [Fact]
    public void Training_is_deterministic_for_the_same_seed_and_data()
    {
        var training = new List<LearnedLineDetector.TrainingExample>();
        using (var page = Page((0, 12), (40, 12)))
            training.Add(Example(page, [(0, 12), (40, 12)]));

        var a = LearnedLineDetector.Train(training);
        var b = LearnedLineDetector.Train(training);
        Assert.Equal(a.Weights, b.Weights);
        Assert.Equal(a.Bias, b.Bias);
    }

    [Fact]
    public void Feature_vector_has_the_contract_size_and_is_finite()
    {
        using var page = Page((0, 10));
        var profile = RowFeatureExtractor.Profile(page);
        var features = RowFeatureExtractor.Features(profile, 5);
        Assert.Equal(RowFeatureExtractor.FeatureCount, features.Length);
        Assert.All(features, f => Assert.True(float.IsFinite(f)));
    }
}
