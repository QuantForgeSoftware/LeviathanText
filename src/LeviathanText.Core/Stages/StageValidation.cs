using LeviathanText.Contracts;

namespace LeviathanText.Core.Stages;

/// <summary>
/// Stage validity per domain-and-pipeline.md §2.3: a stage run is valid iff its declared
/// inputs' checksums still match, and the model/config bindings that produced it are
/// unchanged. Invalid runs are recorded as invalidated; re-run is explicit.
/// </summary>
public static class StageValidation
{
    /// <summary>
    /// The bindings a stage run must still satisfy to remain valid: current input
    /// checksums, model identity, config hash and contract version.
    /// </summary>
    public sealed record Expectation(
        IReadOnlyList<StageArtifact> Inputs,
        ModelRef? Model,
        string ConfigHash,
        string? ContractVersion);

    public static Expectation ExpectationsOf(StageRun run) => new(run.Inputs, run.Model, run.ConfigHash, run.ContractVersion);

    /// <summary>True when the run is still valid under the given current expectations.</summary>
    public static bool IsValid(StageRun run, Expectation current)
    {
        if (run.Status is StageStatus.Failed or StageStatus.Pending)
            return false; // only completed work can be a valid checkpoint

        if (!SameArtifacts(run.Inputs, current.Inputs)) return false;
        if (run.ConfigHash != current.ConfigHash) return false;
        if (run.ContractVersion != current.ContractVersion) return false;
        if (run.Model is null || current.Model is null)
            return run.Model is null && current.Model is null;
        return run.Model == current.Model;
    }

    /// <summary>
    /// Classify a run against current expectations (§2.1 status vocabulary):
    /// <see cref="StageStatus.Succeeded"/> when valid, otherwise
    /// <see cref="StageStatus.Invalidated"/> with the mismatch recorded in the details.
    /// </summary>
    public static StageRun Classify(StageRun run, Expectation current)
    {
        if (IsValid(run, current)) return run;

        var reasons = new List<string>();
        if (!SameArtifacts(run.Inputs, current.Inputs)) reasons.Add("input-checksum-changed");
        if (run.ConfigHash != current.ConfigHash) reasons.Add("config-changed");
        if (run.ContractVersion != current.ContractVersion) reasons.Add("contract-version-changed");
        if (run.Model != current.Model) reasons.Add("model-changed");

        return run with
        {
            Status = StageStatus.Invalidated,
            Failure = new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = run.Id.ToString(),
                Details = new Dictionary<string, string> { ["reasons"] = string.Join(",", reasons) },
                RemedyHint = RemedyHint.ReRunStage,
                Message = "Stage inputs or bindings changed; dependent stages invalidated.",
            },
        };
    }

    private static bool SameArtifacts(IReadOnlyList<StageArtifact> declared, IReadOnlyList<StageArtifact> current)
    {
        if (declared.Count != current.Count) return false;
        // Order-independent: a stage declares its inputs; the set must still match.
        return declared.All(current.Contains);
    }
}
