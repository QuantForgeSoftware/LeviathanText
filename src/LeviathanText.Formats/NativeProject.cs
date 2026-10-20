using LeviathanText.Contracts;

namespace LeviathanText.Formats;

/// <summary>
/// Native project model — the object/surface/capture/region/line hierarchy of
/// domain-and-pipeline.md §1.1, as persisted by the native JSON format. Fragments are a
/// later addition; surfaces carry their geometry entities directly for now.
/// All ids are minted, stable EntityIds; external identifiers (TM, shelfmark, IIIF
/// canvas) are references, never primary keys.
/// </summary>
public sealed record NativeProject
{
    public required EntityId Id { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyList<NativeObject> Objects { get; init; }
}

public sealed record NativeObject
{
    public required EntityId Id { get; init; }

    /// <summary>External references: "TM:123", "inventory:4321", "shelfmark:…".</summary>
    public IReadOnlyDictionary<string, string> ExternalRefs { get; init; } =
        new Dictionary<string, string>();

    /// <summary>Material track of the witness, when known; unknown permitted.</summary>
    public MaterialTrack? Track { get; init; }

    /// <summary>Language period / witness dating / material as free metadata; unknown permitted.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    public required IReadOnlyList<NativeSurface> Surfaces { get; init; }
}

public sealed record NativeSurface
{
    public required EntityId Id { get; init; }

    /// <summary>codex-page | papyrus-side | inscription-face | other:…</summary>
    public required string Kind { get; init; }

    public required IReadOnlyList<NativeCapture> Captures { get; init; }
    public required IReadOnlyList<NativeRegion> Regions { get; init; }
    public required IReadOnlyList<NativeLine> Lines { get; init; }

    /// <summary>All revisions across layers, keyed by scope (surface- or line-level).</summary>
    public IReadOnlyList<TextRevision> Revisions { get; init; } = [];

    /// <summary>Scholarly annotations anchored to revisions (L3).</summary>
    public IReadOnlyList<Annotation> Annotations { get; init; } = [];

    /// <summary>
    /// Append-only audit of geometry/order edits (manual corrections). Text revisions do not
    /// cover geometry; this list does, without changing entity identity. Bounded snapshots
    /// (before/after as compact JSON) are for recovery and review, not for replay.
    /// </summary>
    public IReadOnlyList<GeometryEdit> GeometryEdits { get; init; } = [];

    /// <summary>
    /// Append-only review decisions (double review / adjudication ledger). Anchored to the
    /// exact text revision that was reviewed, never to "the line" in the abstract.
    /// </summary>
    public IReadOnlyList<ReviewDecision> ReviewDecisions { get; init; } = [];

    /// <summary>
    /// Append-only retractions of annotations. The original annotation stays in the ledger;
    /// active views exclude retracted ids. Retraction is not deletion of evidence.
    /// </summary>
    public IReadOnlyList<AnnotationRetraction> AnnotationRetractions { get; init; } = [];
}

public sealed record AnnotationRetraction
{
    /// <summary>Surface-scoped audit id (not an EntityId).</summary>
    public required string Id { get; init; }
    public required EntityId AnnotationId { get; init; }
    public required string Reviewer { get; init; }
    public required DateTimeOffset At { get; init; }
    public string? Reason { get; init; }
}

public enum ReviewOutcome
{
    Accept,
    Correct,
    Dispute,
}

public sealed record ReviewDecision
{
    /// <summary>Surface-scoped audit id (not an EntityId).</summary>
    public required string Id { get; init; }
    public required EntityId LineId { get; init; }
    public required EntityId RevisionId { get; init; }
    public required string Reviewer { get; init; }
    public required ReviewOutcome Outcome { get; init; }
    public required DateTimeOffset At { get; init; }
    public string? Note { get; init; }
    public EntityId? CorrectedRevisionId { get; init; }
}

public enum GeometryEditKind
{
    LineGeometry,
    LineAdded,
    LineDeleted,
    RegionOrder,
    LineOrder,
    LineSplit,
    LineMerged,
}

public sealed record GeometryEdit
{
    /// <summary>Surface-scoped audit id (not an EntityId).</summary>
    public required string Id { get; init; }
    public required GeometryEditKind Kind { get; init; }
    public required DateTimeOffset At { get; init; }
    public required string Author { get; init; }
    public EntityId? LineId { get; init; }
    public EntityId? RegionId { get; init; }
    public string? Before { get; init; }
    public string? After { get; init; }
    public string? Note { get; init; }
}

public sealed record NativeCapture
{
    public required EntityId Id { get; init; }

    /// <summary>The frame this capture defines: geometry referencing this capture is expressed against it.</summary>
    public required CoordinateFrame Frame { get; init; }

    /// <summary>Path or URI of the original image; original captures are immutable.</summary>
    public required string ImageLocation { get; init; }

    public int? WidthPx { get; init; }
    public int? HeightPx { get; init; }

    /// <summary>Rights evidence reference into the corpus manifest, when the image is from an external source.</summary>
    public string? RightsEvidenceRef { get; init; }
}

public sealed record NativeRegion
{
    public required EntityId Id { get; init; }
    public required EntityId FrameRef { get; init; }

    /// <summary>Polygon points in the frame's pixel coordinates; empty polygon = geometry absent (explicit).</summary>
    public IReadOnlyList<PointD> Polygon { get; init; } = [];

    public LayoutSource? LayoutSource { get; init; }
    public int? ReadingOrderIndex { get; init; }
}

public sealed record NativeLine
{
    public required EntityId Id { get; init; }
    public required EntityId RegionId { get; init; }
    public required EntityId FrameRef { get; init; }

    /// <summary>Baseline points (may be empty when geometry is absent/unavailable).</summary>
    public IReadOnlyList<PointD> Baseline { get; init; } = [];

    /// <summary>Current L1 diplomatic revision for this line, when transcribed.</summary>
    public EntityId? DiplomaticRevisionId { get; init; }

    /// <summary>The TextLine's own Coords polygon (pixel frame); empty when not supplied.</summary>
    public IReadOnlyList<PointD> Polygon { get; init; } = [];
}

public readonly record struct PointD(double X, double Y);
