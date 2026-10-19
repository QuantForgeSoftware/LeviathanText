using System.Security.Cryptography;
using System.Text;

namespace LeviathanText.Training;

/// <summary>
/// Training configuration. Deterministic by default (fixed seed) so runs are
/// reproducible where the backend permits (todo §5).
/// </summary>
public sealed record TrainingConfig
{
    public int Epochs { get; init; } = 60;
    public int BatchSize { get; init; } = 4;
    public double LearningRate { get; init; } = 1e-2;
    public int Seed { get; init; } = 1234;
    public int EarlyStoppingPatience { get; init; } = 25;
    public long HiddenSize { get; init; } = 64;
    public string ModelName { get; init; } = "synthetic-crnn";
    public string CheckpointDirectory { get; init; } = "checkpoints";

    /// <summary>Opt-in on-the-fly training augmentation; validation data is never augmented.</summary>
    public bool AugmentTraining { get; init; }

    /// <summary>Maximum horizontal stretch as a fraction of width (0.1 = ±10%).</summary>
    public double MaxHorizontalStretch { get; init; } = 0.1;

    /// <summary>Maximum vertical shift in pixels, filled with background.</summary>
    public int MaxVerticalShiftPx { get; init; } = 1;

    /// <summary>Fraction of pixels randomly set to ink/background (salt-and-pepper).</summary>
    public double SaltPepperFraction { get; init; } = 0.01;

    /// <summary>Stable hash of the parameters that change model meaning (excludes the output directory).</summary>
    public string ComputeHash()
    {
        var canonical = $"{Epochs}|{BatchSize}|{LearningRate:R}|{Seed}|{HiddenSize}|{ModelName}" +
                        $"|aug={AugmentTraining}:{MaxHorizontalStretch:R}:{MaxVerticalShiftPx}:{SaltPepperFraction:R}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}
