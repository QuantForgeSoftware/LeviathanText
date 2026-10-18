namespace LeviathanText.Contracts;

/// <summary>What a confidence number is; three different things, never conflated (domain-and-pipeline.md §4).</summary>
public enum ConfidenceScale
{
    /// <summary>Raw model probability (e.g. softmax output).</summary>
    RawModelProbability,
    /// <summary>Post-calibration score; calibration method named in the model card.</summary>
    Calibrated,
    /// <summary>Annotator/agent uncertainty judgment.</summary>
    AnnotatorUncertainty,
}

/// <summary>
/// A confidence value always carries its scale and source. Absent confidence (e.g. manual
/// layout) is <see cref="None"/>, never a fabricated value.
/// </summary>
public readonly record struct Confidence
{
    private Confidence(double value, ConfidenceScale scale)
    {
        Value = value;
        Scale = scale;
    }

    public double Value { get; }

    public ConfidenceScale Scale { get; }

    /// <summary>Sentinel for "no confidence semantics apply here".</summary>
    public static readonly Confidence None = new(double.NaN, default);

    public bool IsNone => double.IsNaN(Value);

    public static Confidence Of(double value, ConfidenceScale scale)
    {
        if (value is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(value), "Confidence must be within [0,1].");
        return new(value, scale);
    }

    public override string ToString() => IsNone ? "none" : $"{Value:0.###} ({Scale})";
}
