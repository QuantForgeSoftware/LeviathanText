using System.Globalization;
using System.Text.Json.Nodes;
using LeviathanText.Recognition;
using LeviathanText.Training;
using Xunit;
using static TorchSharp.torch;

namespace LeviathanText.ML.Tests;

public sealed class PhotometricAugmentationTests
{
    private static LineImage Image(float background = 0) => new(8, 8,
        Enumerable.Range(0, 64).Select(i => (i % 4) / 3f).ToArray(), background);

    [Theory]
    [InlineData(0f)]
    [InlineData(1f)]
    [InlineData(.5f)]
    public void Fixed_gamma_preserves_endpoints_and_geometry_and_transforms_gray_background(float background)
    {
        var image = Image(background);
        var original = image.Pixels.ToArray();
        var actual = LineAugmenter.Apply(image, new Random(1), 0, 0, 0, gammaMin: 2, gammaMax: 2);
        Assert.Equal(image.Width, actual.Width);
        Assert.Equal(image.Height, actual.Height);
        Assert.Equal((float)Math.Pow(background, 2), actual.BackgroundValue);
        Assert.Equal(original.Select(p => (float)Math.Pow(p, 2)), actual.Pixels);
        Assert.Contains(0f, actual.Pixels);
        Assert.Contains(1f, actual.Pixels);
        Assert.Equal(original, image.Pixels);
        Assert.Equal(background, image.BackgroundValue);
    }

    [Fact]
    public void Seeded_uniform_gamma_is_reproducible_and_stays_within_the_declared_range()
    {
        var image = new LineImage(8, 8, Enumerable.Repeat(.5f, 64).ToArray(), backgroundValue: 1);
        var observed = new HashSet<float>();
        for (int seed = 0; seed < 20; seed++)
        {
            var first = LineAugmenter.Apply(image, new Random(seed), 0, 0, 0, .9, 1.1);
            var second = LineAugmenter.Apply(image, new Random(seed), 0, 0, 0, .9, 1.1);
            Assert.Equal(first.Pixels, second.Pixels);
            Assert.All(first.Pixels, p => Assert.InRange(p, (float)Math.Pow(.5, 1.1), (float)Math.Pow(.5, .9)));
            Assert.Equal(1, first.BackgroundValue);
            observed.Add(first.Pixels[0]);
        }
        Assert.True(observed.Count > 1);
    }

    [Fact]
    public void Identity_gamma_preserves_the_legacy_rng_stream_and_shifted_background()
    {
        var image = Image(.75f);
        var legacyRandom = new CountingRandom();
        var gammaRandom = new CountingRandom();
        var legacy = LineAugmenter.Apply(image, legacyRandom, .1, 1, .01);
        var identity = LineAugmenter.Apply(image, gammaRandom, .1, 1, .01, 1, 1);
        Assert.Equal(legacy.Pixels, identity.Pixels);
        Assert.Equal(legacy.BackgroundValue, identity.BackgroundValue);
        // Original path: one stretch draw and one noise draw per pixel, one shift draw.
        Assert.Equal(65, legacyRandom.DoubleCalls);
        Assert.Equal(65, gammaRandom.DoubleCalls);
        Assert.Equal(1, gammaRandom.IntCalls);
        Assert.Equal(Enumerable.Repeat(.75f, 8), identity.Pixels.Take(8));
        Assert.Equal(image.Pixels.Take(56), identity.Pixels.Skip(8));
    }

    [Fact]
    public void Gamma_is_applied_after_shift_and_noise_with_matching_exposed_background()
    {
        var image = new LineImage(8, 8, Enumerable.Repeat(.25f, 64).ToArray(), .5f);
        var random = new CountingRandom();
        var actual = LineAugmenter.Apply(image, random, 0, 1, 0, 2, 2);
        Assert.Equal(.25f, actual.BackgroundValue);
        Assert.Equal(Enumerable.Repeat(.25f, 8), actual.Pixels.Take(8));
        Assert.Equal(Enumerable.Repeat(.0625f, 56), actual.Pixels.Skip(8));
        Assert.Equal(1, random.DoubleCalls); // fixed exponent does not need a random draw
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-.1, 1)]
    [InlineData(double.NaN, 1)]
    [InlineData(.9, double.PositiveInfinity)]
    [InlineData(1.1, .9)]
    public void Invalid_gamma_bounds_are_rejected_by_augmentation_and_training_config(double min, double max)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LineAugmenter.Apply(Image(), new Random(1), 0, 0, 0, min, max));
        var labels = new LabelSet(["α", "β"]);
        using var model = new CrnnModel(labels.NumClasses, hiddenSize: 8);
        Assert.Throws<ArgumentOutOfRangeException>(() => new CtcTrainer(model, labels, new() { GammaMin = min, GammaMax = max }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TrainingConfig { GammaMin = min, GammaMax = max }.ComputeHash());
    }

    [Fact]
    public void Identity_gamma_retains_the_legacy_config_hash()
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Assert.Equal("f07af4db69e67adf60f91e60fda613f965b8d3474e52478d0590111e5c25c13e", new TrainingConfig().ComputeHash());
            Assert.Equal(new TrainingConfig().ComputeHash(), new TrainingConfig { GammaMin = 1, GammaMax = 1 }.ComputeHash());
            Assert.NotEqual(new TrainingConfig().ComputeHash(), new TrainingConfig { GammaMin = .9, GammaMax = 1.1 }.ComputeHash());
        }
        finally { CultureInfo.CurrentCulture = culture; }
    }

    [Fact]
    public void Gamma_settings_do_not_augment_evaluation_or_consume_evaluation_rng()
    {
        set_num_threads(1);
        var labels = new LabelSet(["α", "β"]);
        using var model = new CrnnModel(labels.NumClasses, hiddenSize: 8, dropout: .7);
        var trainer = new CtcTrainer(model, labels, new TrainingConfig { AugmentTraining = true, GammaMin = .9, GammaMax = 1.1 });
        var lines = SyntheticLineDataset.Generate(3, ["α", "β"], seed: 44);
        manual_seed(18);
        using var expectedTensor = rand(5);
        var expectedRng = expectedTensor.data<float>().ToArray();
        manual_seed(18);
        var first = trainer.EvaluateDetailed(lines);
        using var actualTensor = rand(5);
        Assert.Equal(expectedRng, actualTensor.data<float>().ToArray());
        var second = trainer.EvaluateDetailed(lines);
        Assert.Equal(first.Loss, second.Loss);
        Assert.Equal(first.Cer, second.Cer);
        Assert.Equal(first.Diagnostics.Samples, second.Diagnostics.Samples);
        Assert.True(model.training);
    }

    [Fact]
    public void Gamma_affects_only_opted_in_training_and_round_trips_in_the_manifest()
    {
        set_num_threads(1);
        var lines = new[] { new LabeledLine("αβ", new LineImage(16, 48,
            Enumerable.Range(0, 16 * 48).Select(i => .1f + .8f * (i % 48) / 47).ToArray())) };
        var labels = new LabelSet(["α", "β"]);
        (double Loss, float[] Weights) Run(bool augment, double min, double max)
        {
            manual_seed(17);
            using var model = new CrnnModel(labels.NumClasses, hiddenSize: 8, dropout: .7);
            var config = new TrainingConfig { Seed = 31, AugmentTraining = augment,
                MaxHorizontalStretch = 0, MaxVerticalShiftPx = 0, SaltPepperFraction = 0, GammaMin = min, GammaMax = max };
            var loss = new CtcTrainer(model, labels, config).TrainEpoch(lines, 3);
            return (loss, model.parameters().SelectMany(p => p.data<float>().ToArray()).ToArray());
        }
        var baseline = Run(false, 1, 1);
        var disabled = Run(false, .9, 1.1);
        Assert.Equal(baseline.Loss, disabled.Loss);
        Assert.Equal(baseline.Weights, disabled.Weights);
        var identity = Run(true, 1, 1);
        Assert.Equal(baseline.Loss, identity.Loss);
        Assert.Equal(baseline.Weights, identity.Weights);
        Assert.NotEqual(identity.Loss, Run(true, 2, 2).Loss);

        string directory = Path.Combine(Path.GetTempPath(), "lt-gamma-manifest", Guid.NewGuid().ToString("N"));
        try
        {
            using var model = new CrnnModel(labels.NumClasses, hiddenSize: 8);
            var config = new TrainingConfig { ModelName = "gamma", CheckpointDirectory = directory,
                GammaMin = .9, GammaMax = 1.1, AugmentTraining = true };
            var manifest = TrainingRunManifestWriter.Create(model, labels, config,
                new TrainingRunResult(0, 0, 1, "", []), 1, 1, 0, "split");
            var path = TrainingRunManifestWriter.Write(directory, manifest);
            var restored = TrainingRunManifestWriter.Read(path);
            Assert.Equal(.9, restored.GammaMin);
            Assert.Equal(1.1, restored.GammaMax);
            var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            json.Remove("GammaMin"); json.Remove("GammaMax");
            File.WriteAllText(path, json.ToJsonString());
            var legacy = TrainingRunManifestWriter.Read(path);
            Assert.Equal(1, legacy.GammaMin);
            Assert.Equal(1, legacy.GammaMax);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private sealed class CountingRandom : Random
    {
        public int DoubleCalls { get; private set; }
        public int IntCalls { get; private set; }
        public override double NextDouble() { DoubleCalls++; return .5; }
        public override int Next(int minValue, int maxValue) { IntCalls++; return 1; }
    }
}
