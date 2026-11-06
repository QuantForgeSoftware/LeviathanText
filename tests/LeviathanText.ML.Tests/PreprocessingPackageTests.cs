using LeviathanText.TestSupport;
using System.Text.Json.Nodes;
using LeviathanText.Contracts;
using LeviathanText.Imaging;
using LeviathanText.Recognition;
using LeviathanText.Training;
using SkiaSharp;
using Xunit;

namespace LeviathanText.ML.Tests;

public sealed class PreprocessingPackageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lt-preparation", Guid.NewGuid().ToString("N"));

    public PreprocessingPackageTests() => Directory.CreateDirectory(_root);

    private string Package(LinePreprocessingProfile? profile, int height = 16)
    {
        TorchSharp.torch.set_num_threads(1);
        var directory = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        var labels = new LabelSet(["α", "β"]);
        using var model = new CrnnModel(labels.NumClasses, hiddenSize: 8, inputHeight: profile?.Height ?? height);
        var config = new TrainingConfig { Epochs = 1, HiddenSize = 8, ModelName = "profile",
            CheckpointDirectory = Path.Combine(directory, "checkpoints"), PreprocessingProfile = profile };
        var effective = profile ?? LinePreprocessingProfile.P01Otsu(height);
        using var bitmap = new SKBitmap(80, 32);
        bitmap.Erase(new SKColor(150, 120, 90));
        for (int y = 8; y < 24; y++) for (int x = 15; x < 60; x++) bitmap.SetPixel(x, y, SKColors.Black);
        var lines = new[] { new LabeledLine("αβ", LinePreprocessor.Apply(bitmap, effective)) };
        var trainer = new CtcTrainer(model, labels, config);
        var run = trainer.Fit(lines, lines);
        var manifest = TrainingRunManifestWriter.Create(model, labels, config, run, 1, 1, 0, "split");
        return ModelPackageWriter.Write(Path.Combine(directory, "package"), config.CheckpointDirectory, config.ModelName, manifest).Directory;
    }

    private static void EditManifest(string directory, Action<JsonObject> edit)
    {
        var path = Path.Combine(directory, "run-manifest.json");
        var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        edit(json);
        File.WriteAllText(path, json.ToJsonString());
    }

    [CapabilityTheory(TestCapabilities.Fitting)]
    [InlineData("p01-otsu")]
    [InlineData("ink-positive-grayscale")]
    [InlineData("luminance-grayscale")]
    [InlineData("luminance-local-background-v1")]
    public void Declared_recipe_round_trips_and_training_inference_tensors_match(string recipe)
    {
        var profile = new LinePreprocessingProfile { Recipe = recipe, Height = 32 };
        var package = Package(profile);
        var loaded = RecognitionModelLoader.Load(package);
        using var model = loaded.Model;
        Assert.Equal(profile, loaded.PreprocessingProfile);
        Assert.Equal(profile, loaded.DatasetPreprocessingProfile);
        Assert.Equal(profile, loaded.Metadata.PreprocessingProfile);
        Assert.Equal(profile, loaded.Manifest!.PreprocessingProfile);
        Assert.Equal(ModelPackageFormat.TypedPreprocessing, loaded.Manifest.PackageFormatVersion);

        var dataDirectory = Path.Combine(_root, "data-" + recipe);
        Directory.CreateDirectory(dataDirectory);
        using var page = new SKBitmap(96, 48);
        page.Erase(new SKColor(165, 130, 95));
        for (int y = 12; y < 32; y++) for (int x = 20; x < 78; x++)
            page.SetPixel(x, y, new SKColor((byte)(x % 100), (byte)(y % 100), 20));
        using (var encoded = page.Encode(SKEncodedImageFormat.Png, 100))
        using (var stream = File.Create(Path.Combine(dataDirectory, "page.png"))) encoded.SaveTo(stream);
        File.WriteAllText(Path.Combine(dataDirectory, "page.xml"), """
            <PcGts xmlns="http://schema.primaresearch.org/PAGE/gts/pagecontent/2019-07-15">
              <Page imageFilename="page.png" imageWidth="96" imageHeight="48">
                <TextRegion id="r"><Coords points="0,0 95,0 95,47 0,47"/>
                  <TextLine id="l"><Coords points="12,10 82,10 82,34 12,34"/>
                    <TextEquiv><Unicode>αβ</Unicode></TextEquiv>
                  </TextLine>
                </TextRegion>
              </Page>
            </PcGts>
            """);
        var snapshot = P01LineDataset.LoadPartitions(dataDirectory, DatasetPartitionAccess.TrainDevAndLocked, ".png", preprocessingProfile: loaded.DatasetPreprocessingProfile);
        var training = Assert.Single(snapshot.TestLocked).Image;
        using var decoded = ImageDecoder.Decode(Path.Combine(dataDirectory, "page.png"));
        var crop = LineCropper.Extract(decoded.Bitmap, [new(12, 10), new(82, 10), new(82, 34), new(12, 34)]);
        using (crop.Bitmap)
        {
            var inference = loaded.PrepareLine(crop.Bitmap);
            Assert.Equal(32, training.Height);
            Assert.Equal(76, training.Width); // v1 padded crop: 86×36, resized to height 32
            Assert.Equal(training.Width, inference.Width);
            Assert.Equal(training.BackgroundValue, inference.BackgroundValue);
            Assert.Equal(training.Pixels, inference.Pixels);
        }
    }

    [CapabilityFact(TestCapabilities.Fitting)]
    public void Legacy_package_does_not_guess_recipe_from_description_and_raw_tensor_loading_is_preserved()
    {
        var package = Package(null, height: 32);
        EditManifest(package, j => j["Preprocessing"] = "externally rectified P46 grayscale crop; documentary text only");
        var loaded = RecognitionModelLoader.Load(package);
        using var model = loaded.Model;
        Assert.Null(loaded.Manifest!.PreprocessingProfile);
        Assert.Equal(ModelPackageFormat.Legacy, loaded.Manifest.PackageFormatVersion);
        Assert.Null(loaded.Metadata.PreprocessingProfile);
        Assert.Null(loaded.DatasetPreprocessingProfile);
        Assert.Equal(LinePreprocessingProfile.LegacyInkPositive(32), loaded.PreprocessingProfile);
        using var crop = new SKBitmap(16, 16);
        crop.Erase(new SKColor(50, 80, 100));
        var pixels = loaded.PrepareLine(crop);
        Assert.Equal(32, pixels.Height);
        Assert.Equal(LinePreprocessor.Apply(crop, LinePreprocessingProfile.LegacyInkPositive(32)).Pixels, pixels.Pixels);
        Assert.Single(loaded.CreateRecognizer().Recognize([pixels]));
    }

    [CapabilityFact(TestCapabilities.Fitting)]
    public void Checkpoint_only_package_retains_explicit_profile()
    {
        var profile = LinePreprocessingProfile.Luminance(32);
        var package = Package(profile);
        File.Delete(Path.Combine(package, "run-manifest.json"));
        var loaded = RecognitionModelLoader.Load(package);
        using var model = loaded.Model;
        Assert.Null(loaded.Manifest);
        Assert.Equal(profile, loaded.PreprocessingProfile);
        Assert.Equal(profile, loaded.DatasetPreprocessingProfile);
    }

    [CapabilityTheory(TestCapabilities.Fitting)]
    [InlineData("Version", "9")]
    [InlineData("Recipe", "unknown")]
    [InlineData("Geometry", "unknown")]
    [InlineData("Height", "16")]
    public void Unsupported_or_conflicting_explicit_contract_fails_before_loading_weights(string field, string value)
    {
        var package = Package(LinePreprocessingProfile.P01Otsu(32));
        EditManifest(package, j => j["PreprocessingProfile"]![field] = field is "Version" or "Height" ? JsonValue.Create(int.Parse(value)) : JsonValue.Create(value));
        File.WriteAllBytes(Path.Combine(package, "profile.best.model"), [1, 2, 3]); // must reject profile before native weight loading
        var error = Assert.Throws<LeviathanException>(() => RecognitionModelLoader.Load(package));
        Assert.Equal(ErrorCode.ModelIncompatible, error.Error.Code);
        Assert.Contains("preprocessing", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [CapabilityFact(TestCapabilities.Fitting)]
    public void Missing_required_profile_field_is_not_a_legacy_fallback()
    {
        var package = Package(LinePreprocessingProfile.P01Otsu());
        EditManifest(package, j => j["PreprocessingProfile"]!.AsObject().Remove("Version"));
        var error = Assert.Throws<LeviathanException>(() => RecognitionModelLoader.Load(package));
        Assert.Equal(ErrorCode.ModelIncompatible, error.Error.Code);
    }

    [CapabilityFact(TestCapabilities.Fitting)]
    public void Typed_package_cannot_claim_legacy_format_that_old_runtimes_would_misinterpret()
    {
        var package = Package(LinePreprocessingProfile.P01Otsu());
        EditManifest(package, j => j["PackageFormatVersion"] = ModelPackageFormat.Legacy);
        var error = Assert.Throws<LeviathanException>(() => RecognitionModelLoader.Load(package));
        Assert.Equal(ErrorCode.ModelIncompatible, error.Error.Code);
        Assert.Contains("format 2", error.Message);
    }

    [CapabilityFact(TestCapabilities.Fitting)]
    public void Resume_rejects_changed_or_unspecified_profile_and_preserves_matching_profile()
    {
        var profile = LinePreprocessingProfile.P01Otsu(32);
        var package = Package(profile);
        var loaded = RecognitionModelLoader.Load(package);
        using var model = loaded.Model;
        var config = new TrainingConfig { ModelName = "profile", CheckpointDirectory = package, PreprocessingProfile = profile };
        var trainer = new CtcTrainer(model, loaded.Labels, config);
        Assert.Equal(profile, trainer.LoadCheckpoint(".best").PreprocessingProfile);
        var mismatched = new CtcTrainer(model, loaded.Labels, config with { PreprocessingProfile = LinePreprocessingProfile.Luminance(32) });
        Assert.Equal(ErrorCode.ModelIncompatible, Assert.Throws<LeviathanException>(() => mismatched.LoadCheckpoint(".best")).Error.Code);
        var unspecified = new CtcTrainer(model, loaded.Labels, config with { PreprocessingProfile = null });
        Assert.Throws<LeviathanException>(() => unspecified.LoadCheckpoint(".best"));
        Assert.NotEqual(config.ComputeHash(), (config with { PreprocessingProfile = LinePreprocessingProfile.Luminance(32) }).ComputeHash());
    }

    [CapabilityFact(TestCapabilities.Fitting)]
    public void Writer_rejects_profile_conflict_before_creating_output_package()
    {
        var package = Package(LinePreprocessingProfile.P01Otsu());
        var manifest = TrainingRunManifestWriter.Read(Path.Combine(package, "run-manifest.json"));
        var destination = Path.Combine(_root, "invalid-package");
        Assert.Throws<InvalidDataException>(() => ModelPackageWriter.Write(destination, package, "profile",
            manifest with { PreprocessingProfile = LinePreprocessingProfile.Luminance() }));
        Assert.False(Directory.Exists(destination));
    }

    [CapabilityFact(TestCapabilities.Fitting)]
    public void Writer_checks_manifest_height_when_profile_is_inherited_from_checkpoint()
    {
        var package = Package(LinePreprocessingProfile.P01Otsu(32));
        var manifest = TrainingRunManifestWriter.Read(Path.Combine(package, "run-manifest.json"));
        var destination = Path.Combine(_root, "invalid-height");
        Assert.Throws<InvalidDataException>(() => ModelPackageWriter.Write(destination, package, "profile",
            manifest with { PreprocessingProfile = null, InputHeightPixels = 16 }));
        Assert.False(Directory.Exists(destination));
        var valid = ModelPackageWriter.Write(Path.Combine(_root, "inherited"), package, "profile",
            manifest with { PreprocessingProfile = null });
        var loaded = RecognitionModelLoader.Load(valid.Directory);
        using var model = loaded.Model;
        Assert.Equal(LinePreprocessingProfile.P01Otsu(32), loaded.PreprocessingProfile);
    }

    [CapabilityFact(TestCapabilities.Fitting)]
    public void Writer_rejects_an_unsupported_package_format_before_copying()
    {
        var package = Package(LinePreprocessingProfile.P01Otsu());
        var manifest = TrainingRunManifestWriter.Read(Path.Combine(package, "run-manifest.json"));
        var destination = Path.Combine(_root, "unsupported-format");
        Assert.Throws<InvalidDataException>(() => ModelPackageWriter.Write(destination, package, "profile",
            manifest with { PackageFormatVersion = ModelPackageFormat.Current + 1 }));
        Assert.False(Directory.Exists(destination));
    }

    [Fact]
    public void Legacy_PAGE_loader_retains_strict_threshold_golden_pixels()
    {
        var data = Path.Combine(_root, "legacy-page");
        Directory.CreateDirectory(data);
        using var page = new SKBitmap(8, 8);
        byte[] levels = [0, 32, 64, 96, 128, 160, 192, 255];
        for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++) page.SetPixel(x, y, new SKColor(levels[x], levels[x], levels[x]));
        using (var png = page.Encode(SKEncodedImageFormat.Png, 100))
        using (var stream = File.Create(Path.Combine(data, "p.png"))) png.SaveTo(stream);
        File.WriteAllText(Path.Combine(data, "p.xml"), """
            <PcGts xmlns="http://schema.primaresearch.org/PAGE/gts/pagecontent/2019-07-15">
              <Page imageFilename="p.png" imageWidth="8" imageHeight="8">
                <TextRegion id="r"><Coords points="0,0 7,0 7,7 0,7"/>
                  <TextLine id="l"><Coords points="0,0 7,0 7,7 0,7"/>
                    <TextEquiv><Unicode>α</Unicode></TextEquiv>
                  </TextLine>
                </TextRegion>
              </Page>
            </PcGts>
            """);
        var image = Assert.Single(P01LineDataset.LoadPartitions(data, DatasetPartitionAccess.TrainDevAndLocked, ".png", targetHeight: 8).TestLocked).Image;
        Assert.Equal(0, image.BackgroundValue);
        for (int y = 0; y < 8; y++) Assert.Equal(new float[] { 1, 1, 1, 0, 0, 0, 0, 0 }, image.Pixels.Skip(y * 8).Take(8));
    }

    [Fact]
    public void Dataset_rejects_an_explicit_height_that_conflicts_with_its_profile() =>
        Assert.Throws<InvalidDataException>(() => P01LineDataset.Load(_root, targetHeight: 16,
            preprocessingProfile: LinePreprocessingProfile.Luminance(32)));

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
