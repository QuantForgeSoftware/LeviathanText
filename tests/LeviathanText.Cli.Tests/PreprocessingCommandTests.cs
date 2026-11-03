using LeviathanText.Application;
using LeviathanText.Imaging;
using LeviathanText.Recognition;
using LeviathanText.Training;
using SkiaSharp;

namespace LeviathanText.Cli.Tests;

public sealed class PreprocessingCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lt-preprocessing-cli", Guid.NewGuid().ToString("N"));

    public PreprocessingCommandTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "data"));
        for (int i = 0; i < 3; i++)
        {
            using var bitmap = new SKBitmap(80, 48);
            bitmap.Erase(new SKColor(173, 142, 107));
            for (int y = 14; y < 34; y++)
            for (int x = 12; x < 68; x++)
                if (x % 9 < 4) bitmap.SetPixel(x, y, new SKColor(37, 31, 26));
            using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            using (var stream = File.Create(Path.Combine(_root, "data", $"{i}.png"))) encoded.SaveTo(stream);
            File.WriteAllText(Path.Combine(_root, "data", $"{i}.xml"), $"""
                <PcGts xmlns="http://schema.primaresearch.org/PAGE/gts/pagecontent/2019-07-15">
                  <Page imageFilename="{i}.png" imageWidth="80" imageHeight="48">
                    <TextRegion id="r"><Coords points="0,0 79,0 79,47 0,47"/>
                      <TextLine id="l"><Coords points="8,8 72,8 72,40 8,40"/>
                        <TextEquiv><Unicode>αβ</Unicode></TextEquiv>
                      </TextLine>
                    </TextRegion>
                  </Page>
                </PcGts>
                """);
        }
    }

    private int Run(params string[] args) => LeviathanText.Cli.Program.Main(["--store", Path.Combine(_root, "store.db"), .. args]);

    private string Package(LinePreprocessingProfile? profile)
    {
        var directory = Path.Combine(_root, "package-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var labels = new LabelSet(["α", "β"]);
        using var model = new CrnnModel(labels.NumClasses, hiddenSize: 8, inputHeight: 32);
        string path = CheckpointStore.ModelPath(directory, "test", ".best");
        model.save(path);
        CheckpointStore.WriteMetadata(directory, "test", new CheckpointMetadata
        {
            Epoch = 1, GlobalStep = 1, BestValidationCer = 1, LabelSetHash = CheckpointStore.LabelSetHash(labels),
            Labels = labels.Labels, ConfigHash = "fixture", HiddenSize = 8, InputHeightPixels = 32,
            WidthDownsampleFactor = 4, TorchVersion = "fixture", CreatedAt = DateTimeOffset.UtcNow,
            PreprocessingProfile = profile,
        }, ".best");
        if (profile is not null)
            TrainingRunManifestWriter.Write(directory, new TrainingRunManifest
            {
                ModelName = "test", ConfigHash = "fixture", NumClasses = labels.NumClasses, HiddenSize = 8,
                PackageFormatVersion = ModelPackageFormat.TypedPreprocessing,
                WidthDownsampleFactor = 4, InputHeightPixels = 32, Dropout = 0, AugmentTraining = false,
                WeightDecay = 0, TrainLines = 1, DevLines = 1, TestLockedLines = 1, SplitHash = "fixture",
                LabelSetHash = CheckpointStore.LabelSetHash(labels), CheckpointDigest = TrainingRunManifestWriter.FileDigest(path),
                BestValidationCer = 1, EpochsCompleted = 1, CreatedAt = DateTimeOffset.UtcNow,
                PreprocessingProfile = profile, Preprocessing = profile.Describe(),
            });
        return directory;
    }

    [Theory]
    [InlineData("p01")]
    [InlineData("legacy")]
    [InlineData("luminance")]
    public void Reloaded_package_preparation_matches_training_and_application_tensors(string recipe)
    {
        var profile = recipe switch
        {
            "p01" => LinePreprocessingProfile.P01Otsu(32),
            "legacy" => LinePreprocessingProfile.LegacyInkPositive(32),
            _ => LinePreprocessingProfile.Luminance(32),
        };
        var loaded = RecognitionModelLoader.Load(Package(profile));
        using var model = loaded.Model;
        var snapshot = P01LineDataset.Load(Path.Combine(_root, "data"), ".png", preprocessingProfile: loaded.DatasetPreprocessingProfile);
        var training = Assert.Single(snapshot.Train).Image;
        using var page = ImageDecoder.Decode(Path.Combine(_root, "data", "2.png"));
        var extracted = LineCropper.Extract(page.Bitmap, new SKPoint[] { new(8, 8), new(72, 8), new(72, 40), new(8, 40) });
        using var crop = extracted.Bitmap;
        var inference = loaded.PrepareLine(crop);
        var application = RecognitionPipeline.ToLineImage(crop, loaded.PreprocessingProfile);
        Assert.Equal(32, training.Height);
        Assert.Equal(training.Width, inference.Width);
        Assert.Equal(training.Pixels, inference.Pixels);
        Assert.Equal(training.Pixels, application.Pixels);
        Assert.Equal(training.BackgroundValue, inference.BackgroundValue);
        Assert.Equal(training.BackgroundValue, application.BackgroundValue);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Image_line_OCR_supports_custom_height_for_old_and_declared_packages(bool declared)
    {
        string package = Package(declared ? LinePreprocessingProfile.Luminance(32) : null);
        Assert.Equal(0, Run("image", "ocr", Path.Combine(_root, "data", "2.png"), Path.Combine(_root, "line.txt"),
            "--package", package, "--mode", "line"));
        Assert.True(File.Exists(Path.Combine(_root, "line.txt")));
    }

    [Fact]
    public void XML_OCR_and_line_evaluation_use_the_declared_custom_height_profile()
    {
        string package = Package(LinePreprocessingProfile.P01Otsu(32));
        Assert.Equal(0, Run("image", "ocr", Path.Combine(_root, "data", "2.xml"), Path.Combine(_root, "xml.txt"), "--package", package));
        Assert.Equal(0, Run("evaluate", "lines", Path.Combine(_root, "data"), "--package", package, "--image-ext", ".png",
            "--out", Path.Combine(_root, "evaluation.csv")));
        Assert.True(File.Exists(Path.Combine(_root, "evaluation.csv")));
    }

    [Fact]
    public void External_geometry_requires_prepared_line_mode()
    {
        string package = Package(LinePreprocessingProfile.Luminance(32) with { Geometry = "externally-prepared-line" });
        Assert.Equal(1, Run("image", "ocr", Path.Combine(_root, "data", "2.png"), Path.Combine(_root, "segment.txt"), "--package", package));
        Assert.Equal(1, Run("image", "ocr", Path.Combine(_root, "data", "2.xml"), Path.Combine(_root, "xml.txt"), "--package", package));
        Assert.Equal(0, Run("image", "ocr", Path.Combine(_root, "data", "2.png"), Path.Combine(_root, "line.txt"), "--package", package, "--mode", "line"));
        Assert.False(File.Exists(Path.Combine(_root, "segment.txt")));
        Assert.False(File.Exists(Path.Combine(_root, "xml.txt")));
    }

    public void Dispose() => Directory.Delete(_root, true);
}
