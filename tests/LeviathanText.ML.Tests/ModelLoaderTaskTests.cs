using System.Text.Json.Nodes;
using LeviathanText.Contracts;
using LeviathanText.Recognition;
using LeviathanText.Training;
using Xunit;

namespace LeviathanText.ML.Tests;

/// <summary>
/// Package discovery is task-aware and version-aware: an unknown task is skipped when a
/// caller supplies a filter, and a future package format is rejected instead of being
/// interpreted with older assumptions.
/// </summary>
public class ModelLoaderTaskTests
{
    private static readonly string[] Vocabulary = ["α", "β"];

    private static string NewPackage(
        string task = ModelTasks.Recognition,
        int packageFormatVersion = ModelPackageFormat.Current)
    {
        var directory = Path.Combine(Path.GetTempPath(), "lt-model-loader-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var labels = new LabelSet(Vocabulary);
        var model = new CrnnModel(labels.NumClasses, hiddenSize: 8);
        model.save(CheckpointStore.ModelPath(directory, "tiny", ".best"));
        CheckpointStore.WriteMetadata(directory, "tiny", new CheckpointMetadata
        {
            Epoch = 1,
            GlobalStep = 1,
            BestValidationCer = 1.0,
            LabelSetHash = CheckpointStore.LabelSetHash(labels),
            Labels = labels.Labels,
            ConfigHash = "test",
            HiddenSize = 8,
            InputHeightPixels = CrnnModel.InputHeight,
            WidthDownsampleFactor = CrnnModel.DefaultWidthDownsampleFactor,
            TorchVersion = "test",
            CreatedAt = DateTimeOffset.UtcNow,
        }, ".best");
        TrainingRunManifestWriter.Write(directory, Manifest(task, packageFormatVersion));
        return directory;
    }

    private static TrainingRunManifest Manifest(
        string task = ModelTasks.Recognition,
        int packageFormatVersion = ModelPackageFormat.Current) => new()
    {
        ModelName = "tiny",
        Task = task,
        PackageFormatVersion = packageFormatVersion,
        ConfigHash = "test",
        NumClasses = 3,
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
        LabelSetHash = CheckpointStore.LabelSetHash(new LabelSet(Vocabulary)),
        CheckpointDigest = "",
        BestValidationCer = 1,
        EpochsCompleted = 1,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public void Task_filter_skips_mismatched_package()
    {
        var directory = NewPackage(ModelTasks.Segmentation);

        Assert.Empty(RecognitionModelLoader.LoadAll(directory, [ModelTasks.Recognition]));
        Assert.Single(RecognitionModelLoader.LoadAll(directory, [ModelTasks.Segmentation]));
        Assert.Single(RecognitionModelLoader.LoadAll(directory));

        var ex = Assert.Throws<LeviathanException>(() =>
            RecognitionModelLoader.Load(directory, [ModelTasks.Recognition]));
        Assert.Equal(ErrorCode.ModelIncompatible, ex.Error.Code);
    }

    [Fact]
    public void Newer_package_format_is_rejected()
    {
        var directory = NewPackage(packageFormatVersion: ModelPackageFormat.Current + 1);

        var ex = Assert.Throws<LeviathanException>(() => RecognitionModelLoader.Load(directory));
        Assert.Equal(ErrorCode.ModelIncompatible, ex.Error.Code);
        Assert.Contains("package format", ex.Error.Message);
    }

    [Fact]
    public void Legacy_manifest_without_task_or_format_defaults_to_recognition()
    {
        var directory = NewPackage();
        var manifestPath = Path.Combine(directory, RecognitionModelLoader.ManifestFileName);
        var node = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
        node.Remove(nameof(TrainingRunManifest.Task));
        node.Remove(nameof(TrainingRunManifest.PackageFormatVersion));
        File.WriteAllText(manifestPath, node.ToJsonString());

        Assert.Single(RecognitionModelLoader.LoadAll(directory, [ModelTasks.Recognition]));
        Assert.Empty(RecognitionModelLoader.LoadAll(directory, [ModelTasks.Segmentation]));
    }

    [Fact]
    public void Missing_package_directory_is_model_incompatible()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lt-model-loader-tests", Guid.NewGuid().ToString("N"));

        var ex = Assert.Throws<LeviathanException>(() => RecognitionModelLoader.Load(directory));
        Assert.Equal(ErrorCode.ModelIncompatible, ex.Error.Code);
    }

    [Fact]
    public void Invalid_package_without_manifest_or_single_model_is_model_incompatible()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lt-model-loader-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "not-a-model.bin"), "invalid");

        var ex = Assert.Throws<LeviathanException>(() => RecognitionModelLoader.Load(directory));
        Assert.Equal(ErrorCode.ModelIncompatible, ex.Error.Code);
    }
}
