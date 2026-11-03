using System.Text.Json;
using System.Text.Json.Nodes;
using LeviathanText.Recognition;
using LeviathanText.Training;
using Xunit;

namespace LeviathanText.ML.Tests;

public sealed class TrainingDetailsArtifactTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lt-training-details", Guid.NewGuid().ToString("N"));

    public TrainingDetailsArtifactTests() => Directory.CreateDirectory(_root);

    private static TrainingRunManifest Manifest(int epochs = 4000, bool heavy = true)
    {
        var diagnostics = new TrainingEvaluation(.5, new TrainingDiagnostics
        {
            Lines = 2, ReferenceScalars = 160, PredictionScalars = 150, Deletions = 10,
            ValidFrames = 100, BlankArgmaxFrames = 80, MeanBlankPosterior = .75,
            Samples = Enumerable.Range(0, 3).Select(i => new TrainingPredictionSample(i,
                new string('α', 80), new string('β', 75))).ToArray(),
        });
        return new TrainingRunManifest
        {
            ModelName = "details", ConfigHash = "config", NumClasses = 3, HiddenSize = 8,
            WidthDownsampleFactor = 4, InputHeightPixels = 16, Dropout = 0, AugmentTraining = false,
            WeightDecay = 0, TrainLines = 2, DevLines = 1, TestLockedLines = 0, SplitHash = "split",
            LabelSetHash = "labels", CheckpointDigest = "", BestValidationCer = .5,
            EpochsCompleted = epochs, CreatedAt = DateTimeOffset.UnixEpoch,
            History = Enumerable.Range(1, epochs).Select(e => new EpochRecord(e, 1, 2, .5)
            {
                GlobalStep = e * 2,
                TrainingEvaluation = heavy && e % 20 == 0 ? diagnostics : null,
                ValidationEvaluation = heavy && e % 20 == 0 ? diagnostics : null,
            }).ToArray(),
        };
    }

    private string Write(TrainingRunManifest manifest, string name = "run") =>
        TrainingRunManifestWriter.Write(Path.Combine(_root, name), manifest);

    private static void Mutate(string manifestPath, Action<JsonObject> mutation)
    {
        var json = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
        mutation(json);
        File.WriteAllText(manifestPath, json.ToJsonString());
    }

    [Fact]
    public void Large_history_spills_full_details_and_retains_scalar_curves_and_exact_full_recovery()
    {
        var full = Manifest();
        string path = Write(full);
        var header = TrainingRunManifestWriter.Read(path);
        Assert.True(new FileInfo(path).Length <= ModelPackageLimits.MaxManifestBytes);
        var artifact = Assert.IsType<TrainingRunDetailsArtifact>(header.RunDetailsArtifact);
        Assert.Equal(TrainingRunManifestWriter.RunDetailsFileName, artifact.FileName);
        Assert.True(artifact.Bytes > ModelPackageLimits.MaxManifestBytes);
        Assert.Equal(4000, header.History.Count);
        Assert.All(header.History, e => Assert.Null(e.TrainingEvaluation));
        Assert.Equal(full.History.Select(e => (e.Epoch, e.TrainLoss, e.ValidationLoss, e.ValidationCer, e.GlobalStep)),
            header.History.Select(e => (e.Epoch, e.TrainLoss, e.ValidationLoss, e.ValidationCer, e.GlobalStep)));
        var restored = TrainingRunManifestWriter.ReadRunDetails(path);
        Assert.Null(restored.RunDetailsArtifact);
        Assert.Equal(JsonSerializer.Serialize(full), JsonSerializer.Serialize(restored));
        Assert.Equal(3, restored.History[19].TrainingEvaluation!.Diagnostics.Samples.Count);
    }

    [Fact]
    public void Very_long_scalar_history_moves_entirely_to_details_instead_of_exceeding_header_limit()
    {
        var full = Manifest(epochs: 20_000, heavy: false);
        string path = Write(full);
        var header = TrainingRunManifestWriter.Read(path);
        Assert.Empty(header.History);
        Assert.NotNull(header.RunDetailsArtifact);
        Assert.Equal(20_000, TrainingRunManifestWriter.ReadRunDetails(path).History.Count);
    }

    [Fact]
    public void Small_manifest_bytes_and_read_behavior_are_unchanged()
    {
        var manifest = Manifest(epochs: 2);
        string expected = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        string path = Write(manifest);
        Assert.Equal(expected, File.ReadAllText(path));
        Assert.Null(TrainingRunManifestWriter.Read(path).RunDetailsArtifact);
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(path)!, TrainingRunManifestWriter.RunDetailsFileName)));
        Assert.Equal(expected, JsonSerializer.Serialize(TrainingRunManifestWriter.ReadRunDetails(path), new JsonSerializerOptions { WriteIndented = true }));
    }

    [Theory]
    [InlineData("training-run-details.json")]
    [InlineData("TRAINING-RUN-DETAILS.JSON")]
    [InlineData("training-run-details.json.")]
    [InlineData("../run-manifest.json")]
    public void Manifest_filename_cannot_collide_with_reserved_details_or_escape_the_package(string fileName)
    {
        string directory = Path.Combine(_root, "not-published");
        Assert.Throws<InvalidDataException>(() => TrainingRunManifestWriter.Write(directory, Manifest(), fileName));
        Assert.False(Directory.Exists(directory));
    }

    [Theory]
    [InlineData("run-manifest.json", false)]
    [InlineData("run-manifest.json", true)]
    [InlineData("training-run-details.json", false)]
    [InlineData("training-run-details.json", true)]
    public void Manifest_publication_rejects_existing_and_dangling_output_links(string fileName, bool dangling)
    {
        string directory = Path.Combine(_root, "output");
        Directory.CreateDirectory(directory);
        string outside = Path.Combine(_root, "outside.json");
        if (!dangling) File.WriteAllText(outside, "untouched");
        File.CreateSymbolicLink(Path.Combine(directory, fileName), outside);
        Assert.Throws<InvalidDataException>(() => TrainingRunManifestWriter.Write(directory, Manifest()));
        if (!dangling) Assert.Equal("untouched", File.ReadAllText(outside));
        else Assert.False(File.Exists(outside));
        Assert.Single(Directory.EnumerateFileSystemEntries(directory));
    }

    [Fact]
    public void Output_directory_link_is_rejected_without_writing_through_it()
    {
        string actual = Path.Combine(_root, "actual-directory");
        Directory.CreateDirectory(actual);
        string linked = Path.Combine(_root, "linked-directory");
        Directory.CreateSymbolicLink(linked, actual);
        Assert.Throws<InvalidDataException>(() => TrainingRunManifestWriter.Write(linked, Manifest()));
        Assert.Empty(Directory.EnumerateFileSystemEntries(actual));
    }

    [Theory]
    [InlineData("run-manifest.json")]
    [InlineData("training-run-details.json")]
    public void Reserved_manifest_output_directories_are_rejected_before_any_write(string fileName)
    {
        string directory = Path.Combine(_root, "directory-output");
        Directory.CreateDirectory(Path.Combine(directory, fileName));
        string sentinel = Path.Combine(directory, fileName, "keep.txt");
        File.WriteAllText(sentinel, "untouched");

        Assert.Throws<InvalidDataException>(() => TrainingRunManifestWriter.Write(directory, Manifest()));

        Assert.Equal("untouched", File.ReadAllText(sentinel));
        Assert.Single(Directory.EnumerateFileSystemEntries(directory));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Null_history_or_epoch_entries_are_rejected_on_write_and_read(bool nullEpoch)
    {
        var manifest = Manifest(epochs: 1) with { History = nullEpoch ? new EpochRecord[] { null! } : null! };
        string directory = Path.Combine(_root, "invalid-history");
        Assert.Throws<InvalidDataException>(() => TrainingRunManifestWriter.Write(directory, manifest));
        Assert.False(Directory.Exists(directory));
        string path = Path.Combine(_root, "invalid.json");
        File.WriteAllText(path, JsonSerializer.Serialize(manifest));
        Assert.Throws<InvalidDataException>(() => TrainingRunManifestWriter.Read(path));
        Assert.Throws<InvalidDataException>(() => TrainingRunManifestWriter.ReadRunDetails(path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Null_history_inside_hash_valid_details_is_a_clear_data_error(bool nullEpoch)
    {
        var full = Manifest(epochs: 1) with { History = nullEpoch ? new EpochRecord[] { null! } : null! };
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(full);
        var header = Manifest(epochs: 1) with { History = [], RunDetailsArtifact = new TrainingRunDetailsArtifact
        {
            FileName = TrainingRunManifestWriter.RunDetailsFileName, Bytes = bytes.Length,
            Sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant(),
        } };
        string path = Path.Combine(_root, "run-manifest.json");
        File.WriteAllBytes(Path.Combine(_root, TrainingRunManifestWriter.RunDetailsFileName), bytes);
        File.WriteAllText(path, JsonSerializer.Serialize(header));
        Assert.Throws<InvalidDataException>(() => TrainingRunManifestWriter.ReadRunDetails(path));
    }

    [Fact]
    public void Explicit_null_details_hash_is_rejected_as_invalid_data()
    {
        string path = Write(Manifest());
        Mutate(path, j => j["RunDetailsArtifact"]!["Sha256"] = null);
        var error = Assert.Throws<InvalidDataException>(() => TrainingRunManifestWriter.ReadRunDetails(path));
        Assert.Contains("SHA-256", error.Message);
    }

    [Theory]
    [InlineData("FileName", "../outside.json")]
    [InlineData("FileName", "/absolute/training-run-details.json")]
    [InlineData("Sha256", "0000000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("Sha256", "bad")]
    public void Unsafe_paths_and_bad_hashes_are_rejected(string field, string value)
    {
        string path = Write(Manifest());
        Mutate(path, j => j["RunDetailsArtifact"]![field] = value);
        Assert.Throws<InvalidDataException>(() => TrainingRunManifestWriter.ReadRunDetails(path));
    }

    [Fact]
    public void Changed_details_bytes_and_recorded_size_are_rejected()
    {
        string path = Write(Manifest());
        string details = Path.Combine(Path.GetDirectoryName(path)!, TrainingRunManifestWriter.RunDetailsFileName);
        Mutate(path, j => j["RunDetailsArtifact"]!["Bytes"] = new FileInfo(details).Length + 1);
        Assert.Throws<InvalidDataException>(() => TrainingRunManifestWriter.ReadRunDetails(path));
        path = Write(Manifest(), "tamper");
        details = Path.Combine(Path.GetDirectoryName(path)!, TrainingRunManifestWriter.RunDetailsFileName);
        using (var stream = new FileStream(details, FileMode.Open, FileAccess.Write)) stream.WriteByte((byte)'[');
        Assert.Throws<InvalidDataException>(() => TrainingRunManifestWriter.ReadRunDetails(path));
    }

    [Fact]
    public void Oversized_details_or_pointer_are_rejected_before_large_reads()
    {
        string path = Write(Manifest());
        Mutate(path, j => j["RunDetailsArtifact"]!["Bytes"] = ModelPackageLimits.MaxRunDetailsBytes + 1);
        Assert.Throws<InvalidDataException>(() => TrainingRunManifestWriter.ReadRunDetails(path));
        path = Write(Manifest(), "oversized-file");
        string details = Path.Combine(Path.GetDirectoryName(path)!, TrainingRunManifestWriter.RunDetailsFileName);
        using (var stream = new FileStream(details, FileMode.Open, FileAccess.Write)) stream.SetLength(ModelPackageLimits.MaxRunDetailsBytes + 1);
        Assert.Throws<InvalidDataException>(() => TrainingRunManifestWriter.ReadRunDetails(path));
    }

    [Fact]
    public void Uncompacted_oversized_header_is_not_read_as_an_ordinary_manifest()
    {
        string path = Path.Combine(_root, "oversized-header.json");
        File.WriteAllText(path, JsonSerializer.Serialize(Manifest()));
        Assert.True(new FileInfo(path).Length > ModelPackageLimits.MaxManifestBytes);
        Assert.Throws<InvalidDataException>(() => TrainingRunManifestWriter.Read(path));
    }

    [Fact]
    public void Oversized_metadata_is_rejected_before_output_creation()
    {
        var manifest = Manifest(epochs: 1) with { Provenance = new string('x', checked((int)ModelPackageLimits.MaxManifestBytes)) };
        string directory = Path.Combine(_root, "not-published");
        Assert.Throws<InvalidDataException>(() => TrainingRunManifestWriter.Write(directory, manifest));
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public void Header_metadata_or_scalar_history_cannot_disagree_with_details()
    {
        string path = Write(Manifest());
        Mutate(path, j => j["ModelName"] = "other");
        Assert.Throws<InvalidDataException>(() => TrainingRunManifestWriter.ReadRunDetails(path));
        path = Write(Manifest(), "wrong-history");
        Mutate(path, j => j["History"]![0]!["ValidationCer"] = .1);
        Assert.Throws<InvalidDataException>(() => TrainingRunManifestWriter.ReadRunDetails(path));
    }

    [Fact]
    public void Package_load_does_not_autoload_details_and_repack_retains_verified_full_history()
    {
        string checkpoint = Path.Combine(_root, "checkpoint");
        var labels = new LabelSet(["α", "β"]);
        using var model = new CrnnModel(labels.NumClasses, hiddenSize: 8);
        var config = new TrainingConfig { HiddenSize = 8, ModelName = "details", CheckpointDirectory = checkpoint };
        var trainer = new CtcTrainer(model, labels, config);
        trainer.SaveCheckpoint(isBest: true);
        var full = Manifest() with { ConfigHash = config.ComputeHash(), LabelSetHash = CheckpointStore.LabelSetHash(labels),
            CheckpointDigest = TrainingRunManifestWriter.FileDigest(CheckpointStore.ModelPath(checkpoint, "details", ".best")) };
        var first = ModelPackageWriter.Write(Path.Combine(_root, "first"), checkpoint, "details", full);
        var header = TrainingRunManifestWriter.Read(first.ManifestPath);
        Assert.NotNull(header.RunDetailsArtifact);
        var second = ModelPackageWriter.Write(Path.Combine(_root, "second"), first.Directory, "details", header);
        Assert.Equal(4000, TrainingRunManifestWriter.ReadRunDetails(second.ManifestPath).History.Count);
        Assert.Equal(header.RunDetailsArtifact!.Sha256, TrainingRunManifestWriter.Read(second.ManifestPath).RunDetailsArtifact!.Sha256);
        File.Delete(Path.Combine(first.Directory, TrainingRunManifestWriter.RunDetailsFileName));
        var loaded = RecognitionModelLoader.Load(first.Directory);
        loaded.Model.Dispose(); // ordinary inference reads only the bounded header
        Assert.Throws<InvalidDataException>(() => TrainingRunManifestWriter.ReadRunDetails(first.ManifestPath));
        string destination = Path.Combine(_root, "missing-details-repack");
        Assert.Throws<InvalidDataException>(() => ModelPackageWriter.Write(destination, first.Directory, "details", header));
        Assert.False(Directory.Exists(destination));
    }

    [Theory]
    [InlineData("run-manifest.json")]
    [InlineData("training-run-details.json")]
    [InlineData("model-card.md")]
    [InlineData("details.best.model")]
    [InlineData("details.best.optim")]
    [InlineData("details.best.json")]
    public void Package_rejects_output_links_before_any_checkpoint_copy(string linkedName)
    {
        string checkpoint = Path.Combine(_root, "checkpoint");
        var labels = new LabelSet(["α", "β"]);
        using var model = new CrnnModel(labels.NumClasses, hiddenSize: 8);
        var config = new TrainingConfig { HiddenSize = 8, ModelName = "details", CheckpointDirectory = checkpoint };
        new CtcTrainer(model, labels, config).SaveCheckpoint(isBest: true);
        string destination = Path.Combine(_root, "linked-package");
        Directory.CreateDirectory(destination);
        string outside = Path.Combine(_root, "untouched.txt");
        File.WriteAllText(outside, "untouched");
        File.CreateSymbolicLink(Path.Combine(destination, linkedName), outside);
        Assert.Throws<InvalidDataException>(() => ModelPackageWriter.Write(destination, checkpoint, "details", Manifest()));
        Assert.Equal("untouched", File.ReadAllText(outside));
        Assert.Single(Directory.EnumerateFileSystemEntries(destination));
    }

    [Theory]
    [InlineData("run-manifest.json")]
    [InlineData("training-run-details.json")]
    [InlineData("model-card.md")]
    [InlineData("details.best.model")]
    [InlineData("details.best.optim")]
    [InlineData("details.best.json")]
    public void Package_rejects_reserved_output_directories_before_overwriting_existing_files(string directoryName)
    {
        string checkpoint = Path.Combine(_root, "checkpoint");
        var labels = new LabelSet(["α", "β"]);
        using var model = new CrnnModel(labels.NumClasses, hiddenSize: 8);
        var config = new TrainingConfig { HiddenSize = 8, ModelName = "details", CheckpointDirectory = checkpoint };
        new CtcTrainer(model, labels, config).SaveCheckpoint(isBest: true);
        var manifest = Manifest() with
        {
            ConfigHash = config.ComputeHash(), LabelSetHash = CheckpointStore.LabelSetHash(labels),
            CheckpointDigest = TrainingRunManifestWriter.FileDigest(CheckpointStore.ModelPath(checkpoint, "details", ".best")),
        };
        string destination = Path.Combine(_root, "directory-package");
        Directory.CreateDirectory(destination);
        string[] outputs = ["run-manifest.json", "training-run-details.json", "model-card.md",
            "details.best.model", "details.best.optim", "details.best.json"];
        foreach (string output in outputs)
        {
            string path = Path.Combine(destination, output);
            if (output == directoryName)
            {
                Directory.CreateDirectory(path);
                File.WriteAllText(Path.Combine(path, "keep.txt"), "directory sentinel");
            }
            else File.WriteAllText(path, "file sentinel: " + output);
        }

        Assert.Throws<InvalidDataException>(() => ModelPackageWriter.Write(destination, checkpoint, "details", manifest));

        foreach (string output in outputs)
        {
            string path = Path.Combine(destination, output);
            if (output == directoryName) Assert.Equal("directory sentinel", File.ReadAllText(Path.Combine(path, "keep.txt")));
            else Assert.Equal("file sentinel: " + output, File.ReadAllText(path));
        }
        Assert.Equal(outputs.Length, Directory.EnumerateFileSystemEntries(destination).Count());
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
