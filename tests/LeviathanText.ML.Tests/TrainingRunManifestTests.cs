using LeviathanText.Recognition;
using LeviathanText.Training;
using Xunit;

namespace LeviathanText.ML.Tests;

/// <summary>
/// Reproducibility manifest: architecture, split, label set and checkpoint identity for
/// one run, plus JSON round-trip (todo Phase 3 experiment reproducibility).
/// </summary>
public class TrainingRunManifestTests
{
    private static readonly string[] Vocabulary = { "α", "β", "γ", "δ" };

    private static string NewDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lt-manifest-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void Manifest_captures_architecture_split_and_checkpoint_digest()
    {
        var dir = NewDirectory();
        var labels = new LabelSet(Vocabulary);
        var train = SyntheticLineDataset.Generate(16, Vocabulary, seed: 3);
        var dev = SyntheticLineDataset.Generate(8, Vocabulary, seed: 4);

        var config = new TrainingConfig
        {
            Epochs = 3,
            BatchSize = 4,
            LearningRate = 1e-2,
            Seed = 11,
            EarlyStoppingPatience = 10,
            HiddenSize = 32,
            ModelName = "manifest-crnn",
            CheckpointDirectory = dir,
        };

        var model = new CrnnModel(labels.NumClasses, config.HiddenSize, widthDownsampleFactor: 2);
        var run = new CtcTrainer(model, labels, config).Fit(train, dev);
        var splitHash = TrainingRunManifestWriter.SplitHash(["b=dev", "a=train"]);

        var manifest = TrainingRunManifestWriter.Create(
            model, labels, config, run,
            trainLines: train.Count, devLines: dev.Count, testLockedLines: 0, splitHash: splitHash);

        Assert.Equal("manifest-crnn", manifest.ModelName);
        Assert.Equal(config.ComputeHash(), manifest.ConfigHash);
        Assert.Equal(labels.NumClasses, manifest.NumClasses);
        Assert.Equal(32, manifest.HiddenSize);
        Assert.Equal(2, manifest.WidthDownsampleFactor);
        Assert.Equal(16, manifest.InputHeightPixels);
        Assert.Equal(0, manifest.Dropout);
        Assert.False(manifest.AugmentTraining);
        Assert.Equal(0, manifest.WeightDecay);
        Assert.Equal(16, manifest.TrainLines);
        Assert.Equal(8, manifest.DevLines);
        Assert.Equal(0, manifest.TestLockedLines);
        Assert.Equal(splitHash, manifest.SplitHash);
        Assert.Equal(CheckpointStore.LabelSetHash(labels), manifest.LabelSetHash);
        Assert.Equal(64, manifest.CheckpointDigest.Length);
        Assert.EndsWith(".best.model", run.BestCheckpointPath);
        Assert.Equal(manifest.CheckpointDigest,
            TrainingRunManifestWriter.FileDigest(CheckpointStore.ModelPath(dir, config.ModelName, ".best")));
        Assert.Equal(run.History.Count, manifest.History.Count);

        var path = TrainingRunManifestWriter.Write(dir, manifest);
        var read = TrainingRunManifestWriter.Read(path);

        Assert.Equal(manifest with { History = [] }, read with { History = [] });
        Assert.Equal(manifest.History, read.History);
    }

    [Fact]
    public void Split_hash_is_order_independent_and_content_sensitive()
    {
        var first = TrainingRunManifestWriter.SplitHash(["w1=train", "w2=dev", "w3=test"]);
        var reordered = TrainingRunManifestWriter.SplitHash(["w3=test", "w1=train", "w2=dev"]);
        var different = TrainingRunManifestWriter.SplitHash(["w1=train", "w2=test", "w3=dev"]);

        Assert.Equal(first, reordered);
        Assert.NotEqual(first, different);
        Assert.Equal(64, first.Length);
    }

    [Fact]
    public void File_digest_tracks_content_and_handles_missing_files()
    {
        var dir = NewDirectory();
        var path = Path.Combine(dir, "artifact.bin");
        File.WriteAllText(path, "one");
        var first = TrainingRunManifestWriter.FileDigest(path);
        File.WriteAllText(path, "two");
        var second = TrainingRunManifestWriter.FileDigest(path);

        Assert.Equal(64, first.Length);
        Assert.NotEqual(first, second);
        Assert.Empty(TrainingRunManifestWriter.FileDigest(Path.Combine(dir, "missing.bin")));
    }
}
