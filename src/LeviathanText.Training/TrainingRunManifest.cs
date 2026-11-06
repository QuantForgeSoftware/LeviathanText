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
    public const int Legacy = 1;
    public const int TypedPreprocessing = 2;
    /// <summary>Older runtimes must reject packages whose preparation contract they cannot execute.</summary>
    public const int Current = TypedPreprocessing;
}

/// <summary>Explicit opt-in training history artifact, never fetched by ordinary model loading.</summary>
public sealed record TrainingRunDetailsArtifact
{
    public required string FileName { get; init; }
    public required long Bytes { get; init; }
    public required string Sha256 { get; init; }
}

public sealed record TrainingRunManifest
{
    public required string ModelName { get; init; }

    /// <summary>Task family this package serves. Older manifests default to recognition.</summary>
    public string Task { get; init; } = ModelTasks.Recognition;

    /// <summary>Package-format version, separate from the application contract version.</summary>
    public int PackageFormatVersion { get; init; } = ModelPackageFormat.Legacy;

    /// <summary>Architecture family used to interpret the weights.</summary>
    public string Architecture { get; init; } = "CrnnModel";

    /// <summary>Tokenizer/label-codec profile; the full label list remains in checkpoint metadata.</summary>
    public string TokenizerProfile { get; init; } = "grapheme-ctc";

    /// <summary>Documented preprocessing recipe applied before inference/training.</summary>
    public string Preprocessing { get; init; } = "line-crop>resize>luminance>ctc";

    /// <summary>Executable versioned pixel contract. Missing on legacy and externally prepared runs.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public LinePreprocessingProfile? PreprocessingProfile { get; init; }

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
    public double GammaMin { get; init; } = 1;
    public double GammaMax { get; init; } = 1;
    public required int TrainLines { get; init; }
    public required int DevLines { get; init; }
    /// <summary>Null when reserved label contents were not loaded. Legacy integer counts remain readable.</summary>
    public required int? TestLockedLines { get; init; }
    /// <summary>Null for legacy manifests whose load scope was not recorded.</summary>
    public DatasetPartitionAccess? LoadedPartitions { get; init; }

    /// <summary>Stable hash of the sorted witness→split assignment (corpus-manifest §5).</summary>
    public required string SplitHash { get; init; }

    public required string LabelSetHash { get; init; }

    /// <summary>SHA-256 of the best model file, or empty when the checkpoint was unavailable.</summary>
    public required string CheckpointDigest { get; init; }

    public required double BestValidationCer { get; init; }
    public required int EpochsCompleted { get; init; }
    public int DiagnosticsEveryNEpochs { get; init; }
    public int DiagnosticsSampleCount { get; init; } = 3;
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? TargetValidationCer { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Learning curve, retained for report generation.</summary>
    public IReadOnlyList<EpochRecord> History { get; init; } = [];

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public TrainingRunDetailsArtifact? RunDetailsArtifact { get; init; }
}

/// <summary>Writes and reads run manifests as JSON next to the checkpoint directory.</summary>
public static class TrainingRunManifestWriter
{
    public const string DefaultFileName = "run-manifest.json";
    public const string RunDetailsFileName = "training-run-details.json";

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

    // Retain the existing compiled writer entry point for callers with a known reserved count.
    public static TrainingRunManifest Create(CrnnModel model, LabelSet labels, TrainingConfig config,
        TrainingRunResult run, int trainLines, int devLines, int testLockedLines, string splitHash,
        DateTimeOffset? at = null) =>
        Create(model, labels, config, run, trainLines, devLines, (int?)testLockedLines, splitHash, at, null);

    public static TrainingRunManifest Create(
        CrnnModel model,
        LabelSet labels,
        TrainingConfig config,
        TrainingRunResult run,
        int trainLines,
        int devLines,
        int? testLockedLines,
        string splitHash,
        DateTimeOffset? at = null,
        DatasetPartitionAccess? loadedPartitions = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(run);
        config.PreprocessingProfile?.Validate(model.InputHeightPixels);

        return new TrainingRunManifest
        {
            ModelName = config.ModelName,
            Task = ModelTasks.Recognition,
            PackageFormatVersion = config.PreprocessingProfile is null ? ModelPackageFormat.Legacy : ModelPackageFormat.TypedPreprocessing,
            Architecture = "CrnnModel",
            TokenizerProfile = "grapheme-ctc",
            Preprocessing = config.PreprocessingProfile?.Describe() ?? "line-crop>resize>luminance>ctc",
            PreprocessingProfile = config.PreprocessingProfile,
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
            GammaMin = config.GammaMin,
            GammaMax = config.GammaMax,
            TrainLines = trainLines,
            DevLines = devLines,
            TestLockedLines = testLockedLines,
            LoadedPartitions = loadedPartitions,
            SplitHash = splitHash,
            LabelSetHash = CheckpointStore.LabelSetHash(labels),
            CheckpointDigest = FileDigest(run.BestCheckpointPath),
            BestValidationCer = run.BestValidationCer,
            EpochsCompleted = run.EpochsCompleted,
            DiagnosticsEveryNEpochs = config.DiagnosticsEveryNEpochs,
            DiagnosticsSampleCount = config.DiagnosticsSampleCount,
            TargetValidationCer = config.TargetValidationCer,
            CreatedAt = at ?? DateTimeOffset.UtcNow,
            History = run.History,
        };
    }

    public static string Write(string directory, TrainingRunManifest manifest, string fileName = DefaultFileName)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ValidateDestination(directory, fileName);
        var prepared = Prepare(manifest, directory);
        return WritePrepared(directory, prepared, fileName);
    }

    internal sealed record PreparedManifest(TrainingRunManifest Header, byte[] HeaderBytes, byte[]? DetailsBytes);

    /// <summary>Resolve and validate an existing pointer before callers modify or repackage its manifest.</summary>
    internal static TrainingRunManifest Expand(TrainingRunManifest manifest, string sourceDirectory)
    {
        ValidateHistory(manifest);
        if (manifest.RunDetailsArtifact is not { } artifact) return manifest;
        if (artifact.FileName != RunDetailsFileName)
            throw new InvalidDataException("Training details must use the fixed package-local filename.");
        if (artifact.Bytes <= 0 || artifact.Bytes > ModelPackageLimits.MaxRunDetailsBytes)
            throw new InvalidDataException("Training details byte count exceeds the allowed range.");
        if (artifact.Sha256 is not { Length: 64 } || !artifact.Sha256.All(Uri.IsHexDigit))
            throw new InvalidDataException("Training details must declare a SHA-256 digest.");

        string path = Path.Combine(sourceDirectory, RunDetailsFileName);
        if (!File.Exists(path)) throw new InvalidDataException("Referenced training details are missing.");
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Training details cannot be a symbolic link.");
        var bytes = ReadBoundedBytes(path, ModelPackageLimits.MaxRunDetailsBytes);
        if (bytes.LongLength != artifact.Bytes)
            throw new InvalidDataException("Training details byte count does not match the manifest.");
        var digest = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (!string.Equals(digest, artifact.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Training details digest does not match the manifest.");

        var full = Deserialize(bytes, path);
        if (full.RunDetailsArtifact is not null)
            throw new InvalidDataException("Training details cannot recursively reference another artifact.");
        static byte[] Identity(TrainingRunManifest value) => JsonSerializer.SerializeToUtf8Bytes(
            value with { History = [], RunDetailsArtifact = null }, JsonOptions);
        if (!Identity(manifest).AsSpan().SequenceEqual(Identity(full)))
            throw new InvalidDataException("Training details metadata does not match the compact manifest.");
        if (manifest.History.Count != 0)
        {
            var headerHistory = JsonSerializer.SerializeToUtf8Bytes(manifest.History, JsonOptions);
            var fullHistory = JsonSerializer.SerializeToUtf8Bytes(full.History, JsonOptions);
            var compactHistory = JsonSerializer.SerializeToUtf8Bytes(CompactHistory(full.History), JsonOptions);
            if (!headerHistory.AsSpan().SequenceEqual(fullHistory) && !headerHistory.AsSpan().SequenceEqual(compactHistory))
                throw new InvalidDataException("Training details history does not match the compact manifest.");
        }
        return full;
    }

    internal static PreparedManifest Prepare(TrainingRunManifest manifest, string sourceDirectory)
    {
        manifest = Expand(manifest, sourceDirectory);
        byte[] full = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions);
        if (full.LongLength <= ModelPackageLimits.MaxManifestBytes)
            return new PreparedManifest(manifest, full, null);
        if (full.LongLength > ModelPackageLimits.MaxRunDetailsBytes)
            throw new InvalidDataException($"Training details exceed the {ModelPackageLimits.MaxRunDetailsBytes}-byte limit.");

        var artifact = new TrainingRunDetailsArtifact
        {
            FileName = RunDetailsFileName, Bytes = full.LongLength,
            Sha256 = Convert.ToHexString(SHA256.HashData(full)).ToLowerInvariant(),
        };
        var header = manifest with { History = CompactHistory(manifest.History), RunDetailsArtifact = artifact };
        byte[] compact = JsonSerializer.SerializeToUtf8Bytes(header, JsonOptions);
        if (compact.LongLength > ModelPackageLimits.MaxManifestBytes)
        {
            header = header with { History = [] };
            compact = JsonSerializer.SerializeToUtf8Bytes(header, JsonOptions);
        }
        if (compact.LongLength > ModelPackageLimits.MaxManifestBytes)
            throw new InvalidDataException("Manifest metadata alone exceeds the model package limit.");
        return new PreparedManifest(header, compact, full);
    }

    private static IReadOnlyList<EpochRecord> CompactHistory(IReadOnlyList<EpochRecord> history) =>
        history.Select(epoch => epoch with { TrainingEvaluation = null, ValidationEvaluation = null }).ToArray();

    internal static string WritePrepared(string directory, PreparedManifest manifest, string fileName = DefaultFileName)
    {
        ValidateDestination(directory, fileName);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        if (manifest.DetailsBytes is not null)
            WriteFileAtomically(Path.Combine(directory, RunDetailsFileName), manifest.DetailsBytes);
        WriteFileAtomically(path, manifest.HeaderBytes);
        return path;
    }

    internal static void ValidateDestination(string directory, string fileName = DefaultFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (string.IsNullOrWhiteSpace(fileName) || fileName is "." or ".." || fileName.IndexOfAny(['/', '\\']) >= 0
            || Path.IsPathRooted(fileName)
            || string.Equals(fileName.TrimEnd(' ', '.'), RunDetailsFileName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The manifest must use a package-local filename distinct from the reserved training details filename.");
        ModelPackageLimits.EnsureNotSymbolicLink(directory, "Package output directory");
        ModelPackageLimits.EnsureFileOutput(Path.Combine(directory, fileName), "Manifest output");
        ModelPackageLimits.EnsureFileOutput(Path.Combine(directory, RunDetailsFileName), "Training details output");
    }

    internal static void WriteFileAtomically(string path, byte[] bytes)
    {
        ModelPackageLimits.EnsureFileOutput(path, "Package output");
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        string temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                stream.Write(bytes);
            ModelPackageLimits.EnsureFileOutput(path, "Package output");
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static TrainingRunManifest Read(string path) =>
        Deserialize(ReadBoundedBytes(path, ModelPackageLimits.MaxManifestBytes), path);

    /// <summary>Explicitly load full history after checking the bounded, package-local artifact's size and digest.</summary>
    public static TrainingRunManifest ReadRunDetails(string manifestPath)
    {
        var header = Read(manifestPath);
        return Expand(header, Path.GetDirectoryName(Path.GetFullPath(manifestPath))!);
    }

    private static byte[] ReadBoundedBytes(string path, long maximum)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > maximum)
            throw new InvalidDataException($"File {Path.GetFileName(path)} exceeds its {maximum}-byte limit.");
        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1) throw new InvalidDataException("File changed while reading.");
        return bytes;
    }

    private static TrainingRunManifest Deserialize(byte[] bytes, string path)
    {
        // Preserve File.ReadAllText's BOM-aware behavior while keeping reads bounded.
        using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var manifest = JsonSerializer.Deserialize<TrainingRunManifest>(reader.ReadToEnd(), JsonOptions)
            ?? throw new InvalidDataException($"Run manifest at {path} is empty.");
        ValidateHistory(manifest);
        return manifest;
    }

    private static void ValidateHistory(TrainingRunManifest manifest)
    {
        if (manifest.LoadedPartitions is { } scope
            && (!Enum.IsDefined(scope)
                || scope == DatasetPartitionAccess.TrainDev && manifest.TestLockedLines is not null
                || scope == DatasetPartitionAccess.TrainDevAndLocked && manifest.TestLockedLines is not >= 0))
            throw new InvalidDataException("Reserved line count must agree with the explicitly loaded dataset partitions.");
        if (manifest.History is null || manifest.History.Any(epoch => epoch is null))
            throw new InvalidDataException("Training history and its epoch entries cannot be null.");
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
