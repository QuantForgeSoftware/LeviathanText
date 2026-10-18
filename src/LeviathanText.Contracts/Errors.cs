namespace LeviathanText.Contracts;

/// <summary>
/// Closed machine-readable error vocabulary (domain-and-pipeline.md §6).
/// Adding a code is a contract change; workers must not invent local codes.
/// </summary>
public enum ErrorCode
{
    // --- jobs/stages (domain-and-pipeline.md §5) ---
    InputMissing,
    ChecksumMismatch,
    ModelIncompatible,
    OutOfMemory,
    Cancelled,
    StageContractViolation,
    RejectedArchive,
    RejectedPath,
    RejectedVersion,

    // --- revisions/annotations (domain-and-pipeline.md §6) ---
    RevisionConflict,
    StaleAnnotationAnchor,
    FrameUnregistered,
    ProfileVersionUnsupported,

    // --- interchange (interchange-and-api.md) ---
    SchemaVersionUnsupported,
    LossyExportRequiredReport,
    UnknownLabel,
}

/// <summary>Machine-readable suggestion accompanying an error, where one exists.</summary>
public enum RemedyHint
{
    None,
    ReRunStage,
    RebaseRevision,
    RegisterCaptures,
    AttachLossReport,
    MapUnknownLabel,
}

/// <summary>
/// Shared error shape for SDK and HTTP surfaces (domain-and-pipeline.md §6).
/// Details are structured and bounded; secrets are never included.
/// </summary>
public sealed record LeviathanError
{
    public required ErrorCode Code { get; init; }
    public required string Scope { get; init; }
    public IReadOnlyDictionary<string, string>? Details { get; init; }
    public RemedyHint RemedyHint { get; init; } = RemedyHint.None;
    public string? Message { get; init; }

    public override string ToString() =>
        $"{Code} (scope: {Scope}, remedy: {RemedyHint}){(Message is null ? "" : $": {Message}")}";

    // Details compares by content, so a round-tripped error is equal to its original.
    public bool Equals(LeviathanError? other)
    {
        if (other is null) return false;
        if (!Code.Equals(other.Code) || !Scope.Equals(other.Scope, StringComparison.Ordinal)
            || RemedyHint != other.RemedyHint || !string.Equals(Message, other.Message, StringComparison.Ordinal))
            return false;
        if (Details is null || other.Details is null) return Details is null && other.Details is null;
        if (Details.Count != other.Details.Count) return false;
        foreach (var (key, value) in Details)
            if (!other.Details.TryGetValue(key, out var otherValue) || !string.Equals(value, otherValue, StringComparison.Ordinal))
                return false;
        return true;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Code);
        hash.Add(Scope);
        hash.Add(RemedyHint);
        hash.Add(Message);
        if (Details is not null)
            foreach (var (key, value) in Details)
            {
                hash.Add(key.GetHashCode());
                hash.Add(value, StringComparer.Ordinal);
            }
        return hash.ToHashCode();
    }
}

/// <summary>Runtime exception carrying the contract error shape.</summary>
public sealed class LeviathanException(LeviathanError error) : Exception(error.Message ?? error.ToString())
{
    public LeviathanError Error { get; } = error;
}
