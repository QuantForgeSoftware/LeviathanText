namespace LeviathanText.Training;

/// <summary>A fixed input-order sample, never an augmented image or editorial correction.</summary>
public sealed record TrainingPredictionSample(int LineIndex, string Reference, string Prediction);

/// <summary>
/// Micro totals on NFC Unicode scalars. Blank statistics count only valid CTC frames,
/// excluding batch padding. Samples retain the supplied text without normalization.
/// </summary>
public sealed record TrainingDiagnostics
{
    public int Lines { get; init; }
    public long ReferenceScalars { get; init; }
    public long PredictionScalars { get; init; }
    public long Insertions { get; init; }
    public long Deletions { get; init; }
    public long Substitutions { get; init; }
    public long ValidFrames { get; init; }
    public long BlankArgmaxFrames { get; init; }
    public double MeanBlankPosterior { get; init; }
    public IReadOnlyList<TrainingPredictionSample> Samples { get; init; } = [];

    public long ScalarDistance => Insertions + Deletions + Substitutions;
    public double Cer => ReferenceScalars == 0 ? (PredictionScalars == 0 ? 0 : 1) : (double)ScalarDistance / ReferenceScalars;
    public double? PredictionToReferenceRatio => ReferenceScalars == 0 ? null : (double)PredictionScalars / ReferenceScalars;
    public double BlankArgmaxFraction => ValidFrames == 0 ? 0 : (double)BlankArgmaxFrames / ValidFrames;
}

/// <summary>Evaluation loss and diagnostics from an unaugmented, evaluation-mode pass.</summary>
public sealed record TrainingEvaluation(double Loss, TrainingDiagnostics Diagnostics)
{
    public double Cer => Diagnostics.Cer;
}
