using System.Text.Json;
using LeviathanText.Contracts;
using LeviathanText.Recognition;

namespace LeviathanText.Training;

/// <summary>A packaged model ready for inference, with its labels and provenance.</summary>
public sealed record LoadedRecognitionModel(
    LabelSet Labels,
    CrnnModel Model,
    CheckpointMetadata Metadata,
    TrainingRunManifest? Manifest)
{
    public TorchLineRecognizer CreateRecognizer(CtcDecodeMode mode = CtcDecodeMode.Greedy, int beamWidth = 8) =>
        new(Model, Labels, mode, beamWidth);
}

/// <summary>
/// Loads a model package written by <see cref="ModelPackageWriter"/>: reads the manifest
/// and checkpoint metadata, reconstructs the architecture, verifies the label-set hash and
/// (when a manifest is present) the checkpoint digest, then restores the weights.
/// A package that cannot prove its own identity fails with ModelIncompatible.
/// </summary>
public static class RecognitionModelLoader
{
    public const string ManifestFileName = "run-manifest.json";

    /// <summary>Load the single recognition package, or fail with ModelIncompatible.</summary>
    public static LoadedRecognitionModel Load(string packageDirectory) => Load(packageDirectory, null);

    /// <summary>
    /// Load the package only when it serves one of <paramref name="tasks"/>.
    /// A task mismatch is reported as ModelIncompatible; use <see cref="LoadAll"/> for
    /// the catalog-style behavior of getting an empty result instead.
    /// </summary>
    public static LoadedRecognitionModel Load(string packageDirectory, IReadOnlyCollection<string>? tasks)
    {
        var models = LoadAll(packageDirectory, tasks);
        if (models.Count == 0)
            throw Incompatible(packageDirectory, "package does not declare a requested task");
        return models[0];
    }

    /// <summary>
    /// Load zero or one package after validating its package format and task filter.
    /// This mirrors catalog loading: an incompatible task is skipped, not guessed at.
    /// </summary>
    public static IReadOnlyList<LoadedRecognitionModel> LoadAll(
        string packageDirectory,
        IReadOnlyCollection<string>? tasks = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);
        if (!Directory.Exists(packageDirectory))
            throw Incompatible(packageDirectory, "package directory does not exist");

        TrainingRunManifest? manifest;
        try
        {
            var manifestPath = Path.Combine(packageDirectory, ManifestFileName);
            ModelPackageLimits.EnsureFileSize(manifestPath, ModelPackageLimits.MaxManifestBytes, "run manifest");
            manifest = File.Exists(manifestPath) ? TrainingRunManifestWriter.Read(manifestPath) : null;
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            throw Incompatible(packageDirectory, "run manifest is not readable");
        }

        if (manifest is not null)
        {
            if (manifest.PackageFormatVersion > ModelPackageFormat.Current)
                throw Incompatible(manifest.ModelName,
                    $"package format {manifest.PackageFormatVersion} is newer than supported format {ModelPackageFormat.Current}");
            var task = string.IsNullOrWhiteSpace(manifest.Task) ? ModelTasks.Recognition : manifest.Task;
            if (tasks is not null && !tasks.Contains(task, StringComparer.OrdinalIgnoreCase))
                return [];
        }

        var modelName = manifest?.ModelName ?? FindModelName(packageDirectory);

        CheckpointMetadata metadata;
        try
        {
            ModelPackageLimits.EnsureFileSize(
                CheckpointStore.MetadataPath(packageDirectory, modelName, ".best"),
                ModelPackageLimits.MaxMetadataBytes, "checkpoint metadata");
            metadata = CheckpointStore.ReadMetadata(packageDirectory, modelName, ".best");
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException
                                   or JsonException or InvalidDataException)
        {
            throw Incompatible(modelName, "checkpoint metadata is missing or its name does not match the manifest");
        }
        if (metadata.Labels.Count == 0)
            throw Incompatible(modelName, "checkpoint metadata does not contain its label set");

        var labels = new LabelSet(metadata.Labels);
        var labelHash = CheckpointStore.LabelSetHash(labels);
        if (!string.Equals(labelHash, metadata.LabelSetHash, StringComparison.Ordinal))
            throw Incompatible(modelName, "label list does not hash to the recorded label-set hash");

        var modelPath = CheckpointStore.ModelPath(packageDirectory, modelName, ".best");
        if (!File.Exists(modelPath))
            throw Incompatible(modelName, "model weights are missing from the package");
        try
        {
            ModelPackageLimits.EnsureFileSize(modelPath, ModelPackageLimits.MaxModelBytes, "model weights");
        }
        catch (InvalidDataException)
        {
            throw Incompatible(modelName, "model weights exceed the package size limit");
        }

        if (manifest is not null && !string.IsNullOrEmpty(manifest.CheckpointDigest)
            && !string.Equals(TrainingRunManifestWriter.FileDigest(modelPath), manifest.CheckpointDigest, StringComparison.Ordinal))
            throw Incompatible(modelName, "model weights do not match the manifest digest");

        var model = new CrnnModel(
            labels.NumClasses,
            metadata.HiddenSize,
            metadata.WidthDownsampleFactor,
            metadata.InputHeightPixels,
            metadata.Dropout);
        model.load(modelPath);
        model.eval();

        return [new LoadedRecognitionModel(labels, model, metadata, manifest)];
    }

    private static string FindModelName(string directory)
    {
        var metadataFiles = Directory.GetFiles(directory, "*.best.json");
        if (metadataFiles.Length != 1)
            throw Incompatible(directory,
                $"expected exactly one *.best.json in the package, found {metadataFiles.Length}; provide {ManifestFileName} or a single model");
        return Path.GetFileName(metadataFiles[0]).Replace(".best.json", string.Empty, StringComparison.Ordinal);
    }

    private static LeviathanException Incompatible(string scope, string message) =>
        new(new LeviathanError
        {
            Code = ErrorCode.ModelIncompatible,
            Scope = scope,
            RemedyHint = RemedyHint.ReRunStage,
            Message = $"Cannot load model package: {message}.",
        });
}
