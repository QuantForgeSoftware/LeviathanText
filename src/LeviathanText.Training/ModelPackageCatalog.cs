using System.Text.Json;

namespace LeviathanText.Training;

/// <summary>Metadata for one discovered model package. Inspecting does not load weights.</summary>
public sealed record ModelPackageInfo(
    string Name,
    string Directory,
    string ModelName,
    string Task,
    int PackageFormatVersion,
    int NumClasses,
    long HiddenSize,
    double BestValidationCer,
    DateTimeOffset ModifiedAt);

/// <summary>
/// Bounded, read-only model catalog. A broken or future-format package is skipped rather
/// than making the whole catalog unusable; callers that want a hard failure should use
/// <see cref="RecognitionModelLoader.Load"/> for one named package.
/// </summary>
public static class ModelPackageCatalog
{
    public static IReadOnlyList<ModelPackageInfo> List(
        string root,
        IReadOnlyCollection<string>? tasks = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        if (!Directory.Exists(root)) return [];

        var result = new List<ModelPackageInfo>();
        foreach (var directory in Directory.GetDirectories(root).OrderBy(path => path, StringComparer.Ordinal))
        {
            var info = Inspect(directory, tasks);
            if (info is not null) result.Add(info);
        }
        return result;
    }

    public static ModelPackageInfo? Find(
        string root,
        string name,
        IReadOnlyCollection<string>? tasks = null)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Contains('/') || name.Contains('\\')
            || name.Contains("..", StringComparison.Ordinal))
            return null;
        return Inspect(Path.Combine(root, name), tasks);
    }

    private static ModelPackageInfo? Inspect(string directory, IReadOnlyCollection<string>? tasks)
    {
        try
        {
            if (!Directory.Exists(directory)) return null;
            var manifestPath = Path.Combine(directory, RecognitionModelLoader.ManifestFileName);
            TrainingRunManifest? manifest = null;
            if (File.Exists(manifestPath))
            {
                manifest = TrainingRunManifestWriter.Read(manifestPath);
                if (manifest.PackageFormatVersion > ModelPackageFormat.Current) return null;
            }
            var task = string.IsNullOrWhiteSpace(manifest?.Task) ? ModelTasks.Recognition : manifest.Task;
            if (tasks is not null && !tasks.Contains(task, StringComparer.OrdinalIgnoreCase)) return null;

            var modelName = manifest?.ModelName;
            if (string.IsNullOrWhiteSpace(modelName))
            {
                var metadataFiles = Directory.GetFiles(directory, "*.best.json");
                if (metadataFiles.Length != 1) return null;
                modelName = Path.GetFileName(metadataFiles[0]).Replace(".best.json", string.Empty, StringComparison.Ordinal);
            }

            var metadata = CheckpointStore.ReadMetadata(directory, modelName, ".best");
            if (metadata.Labels.Count == 0) return null;
            if (!File.Exists(CheckpointStore.ModelPath(directory, modelName, ".best"))) return null;
            var labels = new Recognition.LabelSet(metadata.Labels);
            if (!string.Equals(CheckpointStore.LabelSetHash(labels), metadata.LabelSetHash, StringComparison.Ordinal))
                return null;

            return new ModelPackageInfo(
                Path.GetFileName(directory),
                directory,
                modelName,
                task,
                manifest?.PackageFormatVersion ?? 0,
                labels.NumClasses,
                metadata.HiddenSize,
                metadata.BestValidationCer,
                Directory.GetLastWriteTimeUtc(directory));
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
