using System.Text;

namespace LeviathanText.Training;

/// <summary>Files written for one distributable model package.</summary>
public sealed record ModelPackage(
    string Directory,
    string ModelPath,
    string OptimizerPath,
    string MetadataPath,
    string ManifestPath,
    string ModelCardPath);

/// <summary>
/// Packages a selected checkpoint as a self-describing model directory: model weights,
/// optimizer state (for resume), checkpoint metadata (including the label set), the run
/// manifest and a model card with architecture, data hashes, metrics and caveats.
/// The label set travels with the checkpoint, so inference never depends on external
/// state (domain-and-pipeline.md §3).
/// </summary>
public static class ModelPackageWriter
{
    public static ModelPackage Write(
        string packageDirectory,
        string sourceCheckpointDirectory,
        string modelName,
        TrainingRunManifest manifest,
        string? cardNotes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceCheckpointDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.PackageFormatVersion < ModelPackageFormat.Legacy || manifest.PackageFormatVersion > ModelPackageFormat.Current)
            throw new InvalidDataException($"Unsupported model package format {manifest.PackageFormatVersion}.");
        manifest = TrainingRunManifestWriter.Expand(manifest, sourceCheckpointDirectory);

        ModelPackageLimits.EnsureFileSize(CheckpointStore.MetadataPath(sourceCheckpointDirectory, modelName, ".best"),
            ModelPackageLimits.MaxMetadataBytes, "checkpoint metadata");
        var checkpointMetadata = CheckpointStore.ReadMetadata(sourceCheckpointDirectory, modelName, ".best");
        if (manifest.PreprocessingProfile is not null || checkpointMetadata.PreprocessingProfile is not null)
            manifest = manifest with { PackageFormatVersion = Math.Max(manifest.PackageFormatVersion, ModelPackageFormat.TypedPreprocessing) };
        var profile = PreprocessingContract.Resolve(manifest, checkpointMetadata);
        if (profile is not null)
            manifest = manifest with { PreprocessingProfile = profile, Preprocessing = profile.Describe() };
        var preparedManifest = TrainingRunManifestWriter.Prepare(manifest, sourceCheckpointDirectory);

        var sourceModel = CheckpointStore.ModelPath(sourceCheckpointDirectory, modelName, ".best");
        ModelPackageLimits.EnsureFileSize(sourceModel, ModelPackageLimits.MaxModelBytes, "model weights");
        ModelPackageLimits.EnsureFileSize(CheckpointStore.OptimizerPath(sourceCheckpointDirectory, modelName, ".best"),
            ModelPackageLimits.MaxOptimizerBytes, "optimizer state");
        ModelPackageLimits.EnsureFileSize(CheckpointStore.MetadataPath(sourceCheckpointDirectory, modelName, ".best"),
            ModelPackageLimits.MaxMetadataBytes, "checkpoint metadata");
        foreach (var source in new[] { sourceModel,
                     CheckpointStore.OptimizerPath(sourceCheckpointDirectory, modelName, ".best"),
                     CheckpointStore.MetadataPath(sourceCheckpointDirectory, modelName, ".best") })
            if (!File.Exists(source)) throw new FileNotFoundException("Checkpoint artifact is missing.", source);
        TrainingRunManifestWriter.ValidateDestination(packageDirectory);
        foreach (var output in new[] { Path.GetFileName(sourceModel),
                     Path.GetFileName(CheckpointStore.OptimizerPath(sourceCheckpointDirectory, modelName, ".best")),
                     Path.GetFileName(CheckpointStore.MetadataPath(sourceCheckpointDirectory, modelName, ".best")), "model-card.md" })
            ModelPackageLimits.EnsureFileOutput(Path.Combine(packageDirectory, output), "Package output");
        Directory.CreateDirectory(packageDirectory);
        var modelPath = Copy(sourceModel, packageDirectory);
        var optimizerPath = Copy(CheckpointStore.OptimizerPath(sourceCheckpointDirectory, modelName, ".best"), packageDirectory);
        var metadataPath = Copy(CheckpointStore.MetadataPath(sourceCheckpointDirectory, modelName, ".best"), packageDirectory);

        var manifestPath = TrainingRunManifestWriter.WritePrepared(packageDirectory, preparedManifest, "run-manifest.json");
        var cardPath = Path.Combine(packageDirectory, "model-card.md");
        TrainingRunManifestWriter.WriteFileAtomically(cardPath, Encoding.UTF8.GetBytes(BuildCard(modelName, preparedManifest.Header, cardNotes)));

        return new ModelPackage(packageDirectory, modelPath, optimizerPath, metadataPath, manifestPath, cardPath);
    }

    private static string Copy(string source, string directory)
    {
        if (!File.Exists(source)) throw new FileNotFoundException("Checkpoint artifact is missing.", source);
        var target = Path.Combine(directory, Path.GetFileName(source));
        ModelPackageLimits.EnsureFileOutput(target, "Package output");
        string temporary = Path.Combine(directory, $".{Path.GetFileName(source)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.Copy(source, temporary, overwrite: false);
            ModelPackageLimits.EnsureFileOutput(target, "Package output");
            File.Move(temporary, target, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return target;
    }

    private static string BuildCard(string modelName, TrainingRunManifest manifest, string? notes)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Model card — {modelName}");
        sb.AppendLine();
        sb.AppendLine($"- Contract: `{LeviathanText.Contracts.ContractVersion.Current}`");
        sb.AppendLine($"- Task: `{manifest.Task}`");
        sb.AppendLine($"- Package format: `{manifest.PackageFormatVersion}`");
        sb.AppendLine($"- Architecture: `{manifest.Architecture}`; tokenizer `{manifest.TokenizerProfile}`");
        sb.AppendLine($"- Preprocessing: `{manifest.Preprocessing}`");
        sb.AppendLine($"- Preparation contract: `{manifest.PreprocessingProfile?.Describe() ?? "legacy-unspecified (route defaults retained)"}`");
        sb.AppendLine($"- Directions: `{manifest.SupportedDirections}`; scripts: `{manifest.Scripts}`");
        sb.AppendLine($"- Runtime: `{manifest.Runtime}`");
        sb.AppendLine($"- License: `{manifest.License}`");
        if (!string.IsNullOrWhiteSpace(manifest.Provenance))
            sb.AppendLine($"- Provenance: {manifest.Provenance.Trim()}");
        sb.AppendLine($"- Created: {manifest.CreatedAt:O}");
        sb.AppendLine($"- Config hash: `{manifest.ConfigHash}`");
        sb.AppendLine($"- Checkpoint SHA-256: `{manifest.CheckpointDigest}`");
        sb.AppendLine($"- Label-set hash: `{manifest.LabelSetHash}` ({manifest.NumClasses} classes including blank)");
        sb.AppendLine();
        sb.AppendLine("## Architecture");
        sb.AppendLine();
        sb.AppendLine($"- Hidden size {manifest.HiddenSize}; input height {manifest.InputHeightPixels} px; width downsample {manifest.WidthDownsampleFactor}; dropout {manifest.Dropout:0.##}");
        sb.AppendLine();
        sb.AppendLine("## Data");
        sb.AppendLine();
        sb.AppendLine($"- train {manifest.TrainLines} / dev {manifest.DevLines} / locked test {manifest.TestLockedLines} lines");
        sb.AppendLine($"- split hash: `{manifest.SplitHash}`");
        sb.AppendLine();
        sb.AppendLine("## Training");
        sb.AppendLine();
        sb.AppendLine($"- {manifest.EpochsCompleted} epochs; augmentation {manifest.AugmentTraining}; weight decay {manifest.WeightDecay:R}");
        sb.AppendLine($"- best dev CER: {manifest.BestValidationCer:0.####} (pilot measurement, not a threshold)");
        if (manifest.RunDetailsArtifact is { } details)
            sb.AppendLine($"- Full training details: [{details.FileName}]({details.FileName}); {details.Bytes} bytes; SHA-256 `{details.Sha256}`");
        sb.AppendLine();
        sb.AppendLine("## Honest caveats");
        sb.AppendLine();
        sb.AppendLine("- Pilot-scale labels may not be expert-adjudicated (DATA-03 status applies).");
        sb.AppendLine("- A CER in the 0.7–0.9 range is not suitable for unattended scholarly use.");
        sb.AppendLine("- Locked-test reuse rules apply; this card does not authorize a new evaluation.");
        if (!string.IsNullOrWhiteSpace(notes))
        {
            sb.AppendLine();
            sb.AppendLine(notes.Trim());
        }

        return sb.ToString();
    }
}
