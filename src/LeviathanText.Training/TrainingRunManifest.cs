using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LeviathanText.Recognition;

namespace LeviathanText.Training;

/// <summary>
/// Reproducibility manifest for one training run (todo Phase 3: dataset versions,
/// comparable runs, learning curves and reproducible reports). It records what the
/// checkpoint alone cannot prove: which architecture produced it, which split and label
/// set were used, the split hash for leakage currency, and the checkpoint digest.
/// </summary>
public static class ModelTasks
{
    public const string Recognition = "recognition";
    public const string Segmentation = "segmentation";
    public const string ReadingOrder = "reading_order";
}

public static class ModelPackageFormat
{
    /// <summary>Bumped only when an older loader can no longer interpret the package safely.</summary>
    public const int Current = 1;
}

public sealed record TrainingRunManifest
{
    public required string ModelName { get; init; }

    /// <summary>Task family this package serves. Older manifests default to recognition.</summary>
    public string Task { get; init; } = ModelTasks.Recognition;

    /// <summary>Package-format version, separate from the application contract version.</summary>
    public int PackageFormatVersion { get; init; } = ModelPackageFormat.Current;

    /// <summary>Architecture family used to interpret the weights.</summary>
    public string Architecture { get; init; } = "CrnnModel";

    /// <summary>Tokenizer/label-codec profile; the full label list remains in checkpoint metadata.</summary>
    public string TokenizerProfile { get; init; } = "grapheme-ctc";

    /// <summary>Documented preprocessing recipe applied before inference/training.</summary>
    public string Preprocessing { get; init; } = "line-crop>resize>luminance>ctc";

    /// <summary>Directions the packaged model actually supports, comma-separated.</summary>
    public string SupportedDirections { get; init; } = "ltr";

    /// <summary>Known script coverage; <c>unknown</c> is an explicit non-claim.</summary>
    public string Scripts { get; init; } = "unknown";

    /// <summary>Runtime/backend requirement recorded for reproduction.</summary>
    public string Runtime { get; init; } = "TorchSharp-CPU";

    /// <summary>Rights identifier for weights; <c>unverified</c> blocks redistribution claims.</summary>
    public string License { get; init; } = "unverified";

    /// <summary>Source/provenance note; empty means not supplied, not public domain.</summary>
    public string Provenance { get; init; } = "";
    public required string ConfigHash { get; init; }
    public required long NumClasses { get; init; }
    public required long HiddenSize { get; init; }
    public required int WidthDownsampleFactor { get; init; }
    public required int InputHeightPixels { get; init; }
    public required double Dropout { get; init; }
    public required bool AugmentTraining { get; init; }
    public required double WeightDecay { get; init; }
    public required int TrainLines { get; init; }
    public required int DevLines { get; init; }
    public required int TestLockedLines { get; init; }

    /// <summary>Stable hash of the sorted witness→split assignment (corpus-manifest §5).</summary>
    public required string SplitHash { get; init; }

    public required string LabelSetHash { get; init; }

    /// <summary>SHA-256 of the best model file, or empty when the checkpoint was unavailable.</summary>
    public required string CheckpointDigest { get; init; }

    public required double BestValidationCer { get; init; }
    public required int EpochsCompleted { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Learning curve, retained for report generation.</summary>
    public IReadOnlyList<EpochRecord> History { get; init; } = [];
}

/// <summary>Writes and reads run manifests as JSON next to the checkpoint directory.</summary>
public static class TrainingRunManifestWriter
{
    public const string DefaultFileName = "run-manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Order-independent hash of split assignments such as <c>"witness=dev"</c>.</summary>
    public static string SplitHash(IEnumerable<string> splitAssignments)
    {
        ArgumentNullException.ThrowIfNull(splitAssignments);
        var canonical = string.Join('\n', splitAssignments.OrderBy(s => s, StringComparer.Ordinal));
        return Hash(canonical);
    }

    public static string FileDigest(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return string.Empty;
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public static TrainingRunManifest Create(
        CrnnModel model,
        LabelSet labels,
        TrainingConfig config,
        TrainingRunResult run,
        int trainLines,
        int devLines,
        int testLockedLines,
        string splitHash,
        DateTimeOffset? at = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(run);

        return new TrainingRunManifest
        {
            ModelName = config.ModelName,
            Task = ModelTasks.Recognition,
            PackageFormatVersion = ModelPackageFormat.Current,
            Architecture = "CrnnModel",
            TokenizerProfile = "grapheme-ctc",
            Preprocessing = "line-crop>resize>luminance>ctc",
            SupportedDirections = "ltr",
            Scripts = "unknown",
            Runtime = "TorchSharp-CPU",
            License = "unverified",
            ConfigHash = config.ComputeHash(),
            NumClasses = model.NumClasses,
            HiddenSize = model.HiddenSize,
            WidthDownsampleFactor = model.WidthDownsampleFactor,
            InputHeightPixels = model.InputHeightPixels,
            Dropout = model.Dropout,
            AugmentTraining = config.AugmentTraining,
            WeightDecay = config.WeightDecay,
            TrainLines = trainLines,
            DevLines = devLines,
            TestLockedLines = testLockedLines,
            SplitHash = splitHash,
            LabelSetHash = CheckpointStore.LabelSetHash(labels),
            CheckpointDigest = FileDigest(run.BestCheckpointPath),
            BestValidationCer = run.BestValidationCer,
            EpochsCompleted = run.EpochsCompleted,
            CreatedAt = at ?? DateTimeOffset.UtcNow,
            History = run.History,
        };
    }

    public static string Write(string directory, TrainingRunManifest manifest, string fileName = DefaultFileName)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, JsonSerializer.Serialize(manifest, JsonOptions));
        return path;
    }

    public static TrainingRunManifest Read(string path) =>
        JsonSerializer.Deserialize<TrainingRunManifest>(File.ReadAllText(path), JsonOptions)
        ?? throw new InvalidDataException($"Run manifest at {path} is empty.");

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
