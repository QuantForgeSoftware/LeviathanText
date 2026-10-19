namespace LeviathanText.Training;

/// <summary>Per-epoch training record, retained for learning-curve reporting (todo Phase 3).</summary>
public sealed record EpochRecord(int Epoch, double TrainLoss, double ValidationLoss, double ValidationCer);

/// <summary>Outcome of a training run.</summary>
public sealed record TrainingRunResult(
    int StartEpoch,
    int EpochsCompleted,
    double BestValidationCer,
    string BestCheckpointPath,
    IReadOnlyList<EpochRecord> History);
