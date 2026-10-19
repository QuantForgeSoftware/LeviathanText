using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LeviathanText.Recognition;

namespace LeviathanText.Training;

/// <summary>Sidecar metadata written next to every checkpoint.</summary>
public sealed record CheckpointMetadata
{
    public required int Epoch { get; init; }
    public required long GlobalStep { get; init; }
    public required double BestValidationCer { get; init; }
    public required string LabelSetHash { get; init; }
    public required string ConfigHash { get; init; }
    public required string TorchVersion { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// Names and (de)serializes the three checkpoint artifacts: model state, optimizer
/// state and a JSON metadata sidecar. Paths are derived from a directory, a model name
/// and an optional suffix (e.g. ".best").
/// </summary>
public static class CheckpointStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string ModelPath(string directory, string name, string suffix = "") =>
        Path.Combine(directory, $"{name}{suffix}.model");

    public static string OptimizerPath(string directory, string name, string suffix = "") =>
        Path.Combine(directory, $"{name}{suffix}.optim");

    public static string MetadataPath(string directory, string name, string suffix = "") =>
        Path.Combine(directory, $"{name}{suffix}.json");

    public static void WriteMetadata(string directory, string name, CheckpointMetadata metadata, string suffix = "") =>
        File.WriteAllText(MetadataPath(directory, name, suffix), JsonSerializer.Serialize(metadata, JsonOptions));

    public static CheckpointMetadata ReadMetadata(string directory, string name, string suffix = "") =>
        JsonSerializer.Deserialize<CheckpointMetadata>(File.ReadAllText(MetadataPath(directory, name, suffix)), JsonOptions)
        ?? throw new InvalidDataException($"Checkpoint metadata at {MetadataPath(directory, name, suffix)} is empty.");

    public static string LabelSetHash(LabelSet labels) => HashOf(string.Join('\u001f', labels.Labels));

    public static string HashOf(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
