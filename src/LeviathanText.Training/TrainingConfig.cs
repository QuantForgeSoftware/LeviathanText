using System.Security.Cryptography;
using System.Text;
using LeviathanText.Recognition;

namespace LeviathanText.Training;

/// <summary>
/// Training configuration. Deterministic by default (fixed seed) so runs are
/// reproducible where the backend permits (project-plan §5).
/// </summary>
public sealed record TrainingConfig
{
    public int Epochs { get; init; } = 60;
    public int BatchSize { get; init; } = 4;
    public double LearningRate { get; init; } = 1e-2;

    /// <summary>Adam weight decay (L2); 0 keeps the original pilot configuration.</summary>
    public double WeightDecay { get; init; }
    public int Seed { get; init; } = 1234;
    public int EarlyStoppingPatience { get; init; } = 25;
    /// <summary>Optional control stopping target; null keeps the ordinary epoch/patience schedule.</summary>
    public double? TargetValidationCer { get; init; }
    public long HiddenSize { get; init; } = 64;
    public string ModelName { get; init; } = "synthetic-crnn";
    public string CheckpointDirectory { get; init; } = "checkpoints";

    /// <summary>Null keeps externally prepared/legacy tensors unspecified; new image-based training declares its recipe.</summary>
    public LinePreprocessingProfile? PreprocessingProfile { get; init; }

    /// <summary>0 disables telemetry; otherwise collect every N completed epochs and the final epoch.</summary>
    public int DiagnosticsEveryNEpochs { get; init; }

    /// <summary>Bounded fixed input-order prediction samples per partition (0–100).</summary>
    public int DiagnosticsSampleCount { get; init; } = 3;

    /// <summary>Opt-in on-the-fly training augmentation; validation data is never augmented.</summary>
    public bool AugmentTraining { get; init; }

    /// <summary>Maximum horizontal stretch as a fraction of width (0.1 = ±10%).</summary>
    public double MaxHorizontalStretch { get; init; } = 0.1;

    /// <summary>Maximum vertical shift in pixels, filled with background.</summary>
    public int MaxVerticalShiftPx { get; init; } = 1;

    /// <summary>Fraction of pixels randomly set to ink/background (salt-and-pepper).</summary>
    public double SaltPepperFraction { get; init; } = 0.01;

    /// <summary>Training-only luminance exponent sampled uniformly in this range; [1,1] is the legacy identity.</summary>
    public double GammaMin { get; init; } = 1;
    public double GammaMax { get; init; } = 1;

    /// <summary>Stable hash of the parameters that change model meaning (excludes the output directory).</summary>
    public string ComputeHash()
    {
        LineAugmenter.ValidateGammaRange(GammaMin, GammaMax);
        if (TargetValidationCer is { } invalidTarget && (!double.IsFinite(invalidTarget) || invalidTarget < 0))
            throw new ArgumentOutOfRangeException(nameof(TargetValidationCer), "Target CER must be finite and nonnegative.");
        var canonical = $"{Epochs}|{BatchSize}|{LearningRate:R}|{Seed}|{HiddenSize}|{ModelName}|wd={WeightDecay:R}" +
                        $"|aug={AugmentTraining}:{MaxHorizontalStretch:R}:{MaxVerticalShiftPx}:{SaltPepperFraction:R}";
        if (PreprocessingProfile is { } profile)
        {
            profile.Validate();
            canonical += "|preparation=" + profile.Describe();
        }
        if (GammaMin != 1 || GammaMax != 1)
            canonical += FormattableString.Invariant($"|gamma={GammaMin:R}:{GammaMax:R}");
        if (TargetValidationCer is { } target)
            canonical += FormattableString.Invariant($"|target-validation-cer={target:R}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}
