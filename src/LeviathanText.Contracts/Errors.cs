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
}

/// <summary>Runtime exception carrying the contract error shape.</summary>
public sealed class LeviathanException(LeviathanError error) : Exception(error.Message ?? error.ToString())
{
    public LeviathanError Error { get; } = error;
}
