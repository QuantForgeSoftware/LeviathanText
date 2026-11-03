using LeviathanText.Recognition;

namespace LeviathanText.Training;

internal static class PreprocessingContract
{
    internal static LinePreprocessingProfile? Resolve(TrainingRunManifest? manifest, CheckpointMetadata metadata)
    {
        metadata.PreprocessingProfile?.Validate(metadata.InputHeightPixels);
        if (manifest?.PreprocessingProfile is { } declared)
        {
            declared.Validate(metadata.InputHeightPixels);
            if (metadata.PreprocessingProfile is { } checkpoint && declared != checkpoint)
                throw new InvalidDataException("Manifest and checkpoint preprocessing profiles disagree.");
        }
        var resolved = manifest?.PreprocessingProfile ?? metadata.PreprocessingProfile;
        if (manifest is not null && resolved is not null)
        {
            if (manifest.PackageFormatVersion < ModelPackageFormat.TypedPreprocessing)
                throw new InvalidDataException("Typed preprocessing requires model package format 2 or later.");
            resolved.Validate(manifest.InputHeightPixels);
        }
        return resolved;
    }
}
