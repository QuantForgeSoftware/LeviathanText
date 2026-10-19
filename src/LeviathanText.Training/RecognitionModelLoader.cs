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

    public static LoadedRecognitionModel Load(string packageDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);
        if (!Directory.Exists(packageDirectory))
            throw new DirectoryNotFoundException($"Model package directory not found: {packageDirectory}");

        var manifestPath = Path.Combine(packageDirectory, ManifestFileName);
        var manifest = File.Exists(manifestPath) ? TrainingRunManifestWriter.Read(manifestPath) : null;
        var modelName = manifest?.ModelName ?? FindModelName(packageDirectory);

        var metadata = CheckpointStore.ReadMetadata(packageDirectory, modelName, ".best");
        if (metadata.Labels.Count == 0)
            throw Incompatible(modelName, "checkpoint metadata does not contain its label set");

        var labels = new LabelSet(metadata.Labels);
        var labelHash = CheckpointStore.LabelSetHash(labels);
        if (!string.Equals(labelHash, metadata.LabelSetHash, StringComparison.Ordinal))
            throw Incompatible(modelName, "label list does not hash to the recorded label-set hash");

        var modelPath = CheckpointStore.ModelPath(packageDirectory, modelName, ".best");
        if (!File.Exists(modelPath))
            throw Incompatible(modelName, "model weights are missing from the package");

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

        return new LoadedRecognitionModel(labels, model, metadata, manifest);
    }

    private static string FindModelName(string directory)
    {
        var metadataFiles = Directory.GetFiles(directory, "*.best.json");
        if (metadataFiles.Length != 1)
            throw new InvalidDataException(
                $"Expected exactly one *.best.json in the package, found {metadataFiles.Length}. Provide {ManifestFileName} or a single model.");
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
