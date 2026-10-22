using LeviathanText.Contracts;
using LeviathanText.Recognition;
using LeviathanText.Training;
using Xunit;

namespace LeviathanText.ML.Tests;

/// <summary>
/// A packaged model is self-describing and reloadable: weights, optimizer state,
/// metadata/label set, run manifest and model card travel together.
/// </summary>
public class ModelPackageTests
{
    private static readonly string[] Vocabulary = { "α", "β", "γ", "δ" };

    private static string NewDirectory(string name)
    {
        var dir = Path.Combine(Path.GetTempPath(), "lt-package-tests", name, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void Package_contains_artifacts_manifest_and_card_and_reloads()
    {
        var checkpointDir = NewDirectory("checkpoints");
        var labels = new LabelSet(Vocabulary);
        var train = SyntheticLineDataset.Generate(12, Vocabulary, seed: 7);
        var dev = SyntheticLineDataset.Generate(6, Vocabulary, seed: 8);
        var config = new TrainingConfig
        {
            Epochs = 2,
            BatchSize = 4,
            Seed = 5,
            HiddenSize = 16,
            ModelName = "packaged-crnn",
            CheckpointDirectory = checkpointDir,
        };

        var model = new CrnnModel(labels.NumClasses, config.HiddenSize);
        var trainer = new CtcTrainer(model, labels, config);
        var run = trainer.Fit(train, dev);
        trainer.RestoreBest();
        var manifest = TrainingRunManifestWriter.Create(
            model, labels, config, run,
            trainLines: train.Count, devLines: dev.Count, testLockedLines: 0,
            splitHash: TrainingRunManifestWriter.SplitHash(["a=train", "b=dev"]));

        var package = ModelPackageWriter.Write(NewDirectory("package"), checkpointDir, config.ModelName, manifest, "Reserved for pilot review.");

        Assert.True(File.Exists(package.ModelPath));
        Assert.True(File.Exists(package.OptimizerPath));
        Assert.True(File.Exists(package.MetadataPath));
        Assert.True(File.Exists(package.ManifestPath));
        Assert.True(File.Exists(package.ModelCardPath));

        var card = File.ReadAllText(package.ModelCardPath);
        Assert.Contains("packaged-crnn", card);
        Assert.Contains("width downsample 4", card);
        Assert.Contains("Architecture: `CrnnModel`", card);
        Assert.Contains("tokenizer `grapheme-ctc`", card);
        Assert.Contains("Directions: `ltr`", card);
        Assert.Contains("License: `unverified`", card);
        Assert.Contains("Reserved for pilot review.", card);
        Assert.Contains("not a threshold", card);

        var reloadedManifest = TrainingRunManifestWriter.Read(package.ManifestPath);
        Assert.Equal(manifest.ConfigHash, reloadedManifest.ConfigHash);
        Assert.Equal(manifest.SplitHash, reloadedManifest.SplitHash);

        var packageConfig = config with { CheckpointDirectory = package.Directory };
        var reloadedModel = new CrnnModel(labels.NumClasses, config.HiddenSize);
        var reloaded = new CtcTrainer(reloadedModel, labels, packageConfig);
        var metadata = reloaded.LoadCheckpoint(".best");
        Assert.Equal(manifest.LabelSetHash, metadata.LabelSetHash);

        var originalText = new TorchLineRecognizer(model, labels).Recognize([dev[0].Image])[0].Text;
        var packagedText = new TorchLineRecognizer(reloadedModel, labels).Recognize([dev[0].Image])[0].Text;
        Assert.Equal(originalText, packagedText);

        var loaded = RecognitionModelLoader.Load(package.Directory);
        Assert.Equal(config.HiddenSize, loaded.Metadata.HiddenSize);
        Assert.Equal(metadata.LabelSetHash, CheckpointStore.LabelSetHash(loaded.Labels));
        Assert.Equal(originalText, loaded.CreateRecognizer().Recognize([dev[0].Image])[0].Text);
    }

    [Fact]
    public void Package_without_manifest_uses_the_single_best_metadata()
    {
        var checkpointDir = NewDirectory("nomanifest-checkpoints");
        var labels = new LabelSet(Vocabulary);
        var train = SyntheticLineDataset.Generate(8, Vocabulary, seed: 21);
        var config = new TrainingConfig
        {
            Epochs = 2,
            BatchSize = 4,
            Seed = 2,
            HiddenSize = 16,
            ModelName = "nomanifest-crnn",
            CheckpointDirectory = checkpointDir,
        };

        var model = new CrnnModel(labels.NumClasses, config.HiddenSize);
        var trainer = new CtcTrainer(model, labels, config);
        var run = trainer.Fit(train, train);
        trainer.RestoreBest();
        var manifest = TrainingRunManifestWriter.Create(model, labels, config, run, train.Count, train.Count, 0, "split");
        var package = ModelPackageWriter.Write(NewDirectory("nomanifest-package"), checkpointDir, config.ModelName, manifest);

        File.Delete(package.ManifestPath);

        var loaded = RecognitionModelLoader.Load(package.Directory);
        Assert.Null(loaded.Manifest);
        Assert.Equal(labels.NumClasses, loaded.Labels.NumClasses);
    }

    [Fact]
    public void Missing_labels_in_metadata_are_rejected()
    {
        var checkpointDir = NewDirectory("nolabels-checkpoints");
        var labels = new LabelSet(Vocabulary);
        var train = SyntheticLineDataset.Generate(8, Vocabulary, seed: 22);
        var config = new TrainingConfig
        {
            Epochs = 2,
            BatchSize = 4,
            Seed = 2,
            HiddenSize = 16,
            ModelName = "nolabels-crnn",
            CheckpointDirectory = checkpointDir,
        };

        var model = new CrnnModel(labels.NumClasses, config.HiddenSize);
        var trainer = new CtcTrainer(model, labels, config);
        var run = trainer.Fit(train, train);
        var manifest = TrainingRunManifestWriter.Create(model, labels, config, run, train.Count, train.Count, 0, "split");
        var package = ModelPackageWriter.Write(NewDirectory("nolabels-package"), checkpointDir, config.ModelName, manifest);

        var metadata = CheckpointStore.ReadMetadata(package.Directory, config.ModelName, ".best");
        CheckpointStore.WriteMetadata(package.Directory, config.ModelName, metadata with { Labels = [] }, ".best");

        var ex = Assert.Throws<LeviathanException>(() => RecognitionModelLoader.Load(package.Directory));
        Assert.Equal(ErrorCode.ModelIncompatible, ex.Error.Code);
    }

    [Fact]
    public void Manifest_name_mismatch_is_reported_as_model_incompatible()
    {
        var checkpointDir = NewDirectory("mismatch-checkpoints");
        var labels = new LabelSet(Vocabulary);
        var train = SyntheticLineDataset.Generate(8, Vocabulary, seed: 31);
        var config = new TrainingConfig
        {
            Epochs = 2,
            BatchSize = 4,
            Seed = 2,
            HiddenSize = 16,
            ModelName = "mismatch-crnn",
            CheckpointDirectory = checkpointDir,
        };

        var model = new CrnnModel(labels.NumClasses, config.HiddenSize);
        var trainer = new CtcTrainer(model, labels, config);
        var run = trainer.Fit(train, train);
        var manifest = TrainingRunManifestWriter.Create(model, labels, config, run, train.Count, train.Count, 0, "split");
        var package = ModelPackageWriter.Write(NewDirectory("mismatch-package"), checkpointDir, config.ModelName, manifest);

        TrainingRunManifestWriter.Write(package.Directory, manifest with { ModelName = "not-in-package" });

        var ex = Assert.Throws<LeviathanException>(() => RecognitionModelLoader.Load(package.Directory));
        Assert.Equal(ErrorCode.ModelIncompatible, ex.Error.Code);
    }

    [Fact]
    public void Tampered_weights_are_rejected_by_the_manifest_digest()
    {
        var checkpointDir = NewDirectory("tamper-checkpoints");
        var labels = new LabelSet(Vocabulary);
        var train = SyntheticLineDataset.Generate(8, Vocabulary, seed: 12);
        var config = new TrainingConfig
        {
            Epochs = 2,
            BatchSize = 4,
            Seed = 9,
            HiddenSize = 16,
            ModelName = "tampered-crnn",
            CheckpointDirectory = checkpointDir,
        };

        var model = new CrnnModel(labels.NumClasses, config.HiddenSize);
        var run = new CtcTrainer(model, labels, config).Fit(train, train);
        var manifest = TrainingRunManifestWriter.Create(model, labels, config, run, train.Count, train.Count, 0, "split");
        var package = ModelPackageWriter.Write(NewDirectory("tampered-package"), checkpointDir, config.ModelName, manifest);

        File.WriteAllBytes(package.ModelPath, [1, 2, 3, 4]);

        var ex = Assert.Throws<LeviathanException>(() => RecognitionModelLoader.Load(package.Directory));
        Assert.Equal(ErrorCode.ModelIncompatible, ex.Error.Code);
    }

    [Fact]
    public void Missing_checkpoint_artifact_is_reported()
    {
        var labels = new LabelSet(Vocabulary);
        var emptyDir = NewDirectory("empty");
        var manifest = new TrainingRunManifest
        {
            ModelName = "missing",
            ConfigHash = "x",
            NumClasses = labels.NumClasses,
            HiddenSize = 16,
            WidthDownsampleFactor = 4,
            InputHeightPixels = 16,
            Dropout = 0,
            AugmentTraining = false,
            WeightDecay = 0,
            TrainLines = 1,
            DevLines = 1,
            TestLockedLines = 0,
            SplitHash = "split",
            LabelSetHash = CheckpointStore.LabelSetHash(labels),
            CheckpointDigest = "",
            BestValidationCer = 1,
            EpochsCompleted = 1,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        Assert.Throws<FileNotFoundException>(() =>
            ModelPackageWriter.Write(NewDirectory("package-missing"), emptyDir, "missing", manifest));
    }
}
