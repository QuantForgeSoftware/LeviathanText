using LeviathanText.Recognition;
using LeviathanText.Training;
using Xunit;

namespace LeviathanText.ML.Tests;

public class ModelPackageCatalogTests
{
    private static readonly string[] Vocabulary = ["α", "β"];

    private static string NewRoot() => Path.Combine(Path.GetTempPath(), "lt-catalog-tests", Guid.NewGuid().ToString("N"));

    private static void WritePackage(string root, string name, string task = ModelTasks.Recognition,
        int packageFormatVersion = ModelPackageFormat.Current)
    {
        var directory = Path.Combine(root, name);
        Directory.CreateDirectory(directory);
        var labels = new LabelSet(Vocabulary);
        var model = new CrnnModel(labels.NumClasses, hiddenSize: 8);
        model.save(CheckpointStore.ModelPath(directory, name, ".best"));
        CheckpointStore.WriteMetadata(directory, name, new CheckpointMetadata
        {
            Epoch = 1,
            GlobalStep = 1,
            BestValidationCer = 0.5,
            LabelSetHash = CheckpointStore.LabelSetHash(labels),
            Labels = labels.Labels,
            ConfigHash = "test",
            HiddenSize = 8,
            InputHeightPixels = CrnnModel.InputHeight,
            WidthDownsampleFactor = CrnnModel.DefaultWidthDownsampleFactor,
            TorchVersion = "test",
            CreatedAt = DateTimeOffset.UtcNow,
        }, ".best");
        TrainingRunManifestWriter.Write(directory, new TrainingRunManifest
        {
            ModelName = name,
            Task = task,
            PackageFormatVersion = packageFormatVersion,
            ConfigHash = "test",
            NumClasses = labels.NumClasses,
            HiddenSize = 8,
            WidthDownsampleFactor = CrnnModel.DefaultWidthDownsampleFactor,
            InputHeightPixels = CrnnModel.InputHeight,
            Dropout = 0,
            AugmentTraining = false,
            WeightDecay = 0,
            TrainLines = 1,
            DevLines = 1,
            TestLockedLines = 0,
            SplitHash = "split",
            LabelSetHash = CheckpointStore.LabelSetHash(labels),
            CheckpointDigest = "",
            BestValidationCer = 0.5,
            EpochsCompleted = 1,
            CreatedAt = DateTimeOffset.UtcNow,
        });
    }

    [Fact]
    public void Catalog_lists_packages_and_filters_by_task()
    {
        var root = NewRoot();
        WritePackage(root, "rec", ModelTasks.Recognition);
        WritePackage(root, "seg", ModelTasks.Segmentation);

        var all = ModelPackageCatalog.List(root);
        Assert.Equal(["rec", "seg"], all.Select(p => p.Name).ToArray());
        Assert.Equal(3, all.Single(p => p.Name == "rec").NumClasses);

        var recognition = ModelPackageCatalog.List(root, [ModelTasks.Recognition]);
        Assert.Equal("rec", Assert.Single(recognition).Name);
        var segmentation = ModelPackageCatalog.List(root, [ModelTasks.Segmentation]);
        Assert.Equal("seg", Assert.Single(segmentation).Name);
    }

    [Fact]
    public void Catalog_skips_broken_and_future_format_packages()
    {
        var root = NewRoot();
        WritePackage(root, "ok");
        WritePackage(root, "future", packageFormatVersion: ModelPackageFormat.Current + 1);
        Directory.CreateDirectory(Path.Combine(root, "broken"));

        Assert.Equal("ok", Assert.Single(ModelPackageCatalog.List(root)).Name);
        Assert.Null(ModelPackageCatalog.Find(root, "missing"));
        Assert.Null(ModelPackageCatalog.Find(root, "../ok"));
    }
}
