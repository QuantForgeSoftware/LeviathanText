namespace LeviathanText.Contracts;

/// <summary>
/// A coordinate frame: the exact image (capture or derivative, pinned by checksum)
/// geometry is expressed against (domain-and-pipeline.md §1.2; project-plan §4). Geometry
/// never transfers between frames without an explicit registration.
/// </summary>
public sealed record CoordinateFrame
{
    public required EntityId ImageRef { get; init; }

    /// <summary>Pins the exact pixels the geometry was drawn against; a re-captured or
    /// re-derived image with a different checksum is a different frame.</summary>
    public required Checksum ImageChecksum { get; init; }

    public override string ToString() => $"{ImageRef}@{ImageChecksum.Digest[..12]}";
}

/// <summary>
/// An explicit, recorded registration between two frames (project-plan §3: multiple captures
/// need explicit registration before geometry transfers between them).
/// Transform parameters live with the imaging implementation; this contract record
/// carries the identity of the frames, a bounded description of the transform and the
/// responsible agent.
/// </summary>
public sealed record Registration
{
    public required CoordinateFrame From { get; init; }
    public required CoordinateFrame To { get; init; }

    /// <summary>Bounded, human-readable transform identity, e.g. "homography:v1#3f2a".</summary>
    public required string TransformDescription { get; init; }

    public required AgentRef EvidenceBy { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Job lifecycle status (domain-and-pipeline.md §5).</summary>
public enum JobStatus
{
    Pending,
    Running,
    Succeeded,
    Failed,
    Cancelled,
}

/// <summary>Timed worker lease; expiry releases the job for retry.</summary>
public sealed record LeaseInfo
{
    public required EntityId Worker { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? LastHeartbeat { get; init; }
}

/// <summary>Durable job record (domain-and-pipeline.md §5; interchange-and-api.md §2.2).</summary>
public sealed record JobRecord
{
    public required EntityId Id { get; init; }

    /// <summary>Job type from a closed set per deployment; e.g. "pipeline.run", "export.build".</summary>
    public required string Type { get; init; }

    public required IReadOnlyList<StageArtifact> Inputs { get; init; }

    /// <summary>Client-supplied key for idempotent creation; duplicate creation returns the existing job.</summary>
    public string? IdempotencyKey { get; init; }

    public required AgentRef Owner { get; init; }
    public LeaseInfo? Lease { get; init; }

    /// <summary>Bounded progress; semantics are job-type-defined (percent or stage counters).</summary>
    public double? Progress { get; init; }

    public int RetriesUsed { get; init; }
    public required JobStatus Status { get; init; }
    public LeviathanError? Failure { get; init; }

    /// <summary>Finalized outputs, published idempotently (complete-then-visible).</summary>
    public IReadOnlyList<StageArtifact> Outputs { get; init; } = [];
}
