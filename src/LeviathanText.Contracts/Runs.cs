namespace LeviathanText.Contracts;

/// <summary>
/// One recorded training/evaluation run (experiment registry). Config/metrics/history are
/// open-ended but bounded maps/lists so different model families can be compared without schema
/// churn. Artifacts are paths/digests, not embedded binaries.
/// </summary>
public sealed record RunRecord
{
    public required string Id { get; init; }
    /// <summary>recognition | layout | other:…</summary>
    public required string Kind { get; init; }
    public required string Name { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public string? CreatedBy { get; init; }

    /// <summary>Split hash or training-data digest identifying the dataset snapshot.</summary>
    public string? DatasetDigest { get; init; }

    public IReadOnlyDictionary<string, string> Config { get; init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, double> Metrics { get; init; } = new Dictionary<string, double>();

    /// <summary>Optional learning-curve series (validation CER/loss per epoch, or training loss).</summary>
    public IReadOnlyList<double> History { get; init; } = [];

    public IReadOnlyList<string> Artifacts { get; init; } = [];
    public string? Notes { get; init; }
}
