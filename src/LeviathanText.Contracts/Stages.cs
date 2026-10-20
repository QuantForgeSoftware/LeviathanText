namespace LeviathanText.Contracts;

/// <summary>Content-addressed artifact reference (domain-and-pipeline.md §2.1).</summary>
public sealed record Checksum
{
    /// <summary>Algorithm, e.g. "sha256".</summary>
    public required string Algorithm { get; init; }

    /// <summary>Lowercase hex digest.</summary>
    public required string Digest { get; init; }

    public override string ToString() => $"{Algorithm}:{Digest}";
}

/// <summary>Model identity recorded on every run that used one (domain-and-pipeline.md §3).</summary>
public sealed record ModelRef
{
    public required string PackageId { get; init; }
    public required string Version { get; init; }

    /// <summary>Hash of the model input/output contract this run was bound to.</summary>
    public required string IoContractHash { get; init; }
}

/// <summary>Pipeline stage names (domain-and-pipeline.md §2.2).</summary>
public enum StageName
{
    Ingest,
    Prepare,
    Layout,
    ReadingOrder,
    Extraction,
    Recognition,
    Alignment,
    Proposals,
    Export,
}

/// <summary>Whether layout geometry came from a model or a human/imported source; never indistinguishable (project-plan §7).</summary>
public enum LayoutSource
{
    Automatic,
    Manual,
    Imported,
}

public enum StageStatus
{
    Pending,
    Running,
    Succeeded,
    /// <summary>Failed: <see cref="StageRun.Failure"/> carries the closed-vocabulary error.</summary>
    Failed,
    /// <summary>Inputs changed; recorded invalidation, re-run is explicit.</summary>
    Invalidated,
}

/// <summary>An artifact bound to the entity it was produced for/consumed from.</summary>
public sealed record StageArtifact(EntityId Entity, Checksum Artifact);

/// <summary>Common stage envelope (domain-and-pipeline.md §2.1).</summary>
public sealed record StageRun
{
    public required EntityId Id { get; init; }
    public required StageName Stage { get; init; }

    /// <summary>Which declared I/O contract this run used; null means the pre-contract default.</summary>
    public string? ContractVersion { get; init; }

    public required IReadOnlyList<StageArtifact> Inputs { get; init; }
    public required IReadOnlyList<StageArtifact> Outputs { get; init; }

    /// <summary>Null for manual stages; manual layout still records <see cref="LayoutSource"/>.</summary>
    public ModelRef? Model { get; init; }

    public required string ConfigHash { get; init; }
    public required StageStatus Status { get; init; }
    public LeviathanError? Failure { get; init; }

    /// <summary>For Layout stages: provenance of the geometry. Confidence is None for manual layout.</summary>
    public LayoutSource? LayoutSourceUsed { get; init; }

    public TimeSpan? WallTime { get; init; }
}
