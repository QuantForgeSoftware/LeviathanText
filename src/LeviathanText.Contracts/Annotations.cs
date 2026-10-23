namespace LeviathanText.Contracts;

/// <summary>
/// L3 scholarly annotation vocabulary — the closed set from greek-transcription.md §5.
/// Distinct statuses are deliberately not collapsed (e.g. NotYetTranscribed ≠ Illegible).
/// </summary>
public enum AnnotationKind
{
    /// <summary>Nobody has worked on this span yet.</summary>
    NotYetTranscribed,
    /// <summary>Letters visible, reading uncertain (imported editorial underdots map here, never into L1 text).</summary>
    Unclear,
    /// <summary>Traces exist, letters not recoverable from this capture.</summary>
    Illegible,
    /// <summary>No surface remains; see <see cref="Annotation.GapExtent"/>.</summary>
    Lost,
    /// <summary>Editorial supply — exists in L2 only, never enters L1.</summary>
    SuppliedEditorial,
    /// <summary>Competing readings retained; none silently chosen.</summary>
    AlternativeReading,
    /// <summary>Witness correction state: erased.</summary>
    Erased,
    /// <summary>Witness correction state: added.</summary>
    Added,
    /// <summary>Witness correction state: overwritten.</summary>
    Overwritten,
    /// <summary>Distinct writing agent; <see cref="Annotation.HandLabel"/> may be Unattributed.</summary>
    DifferentHand,
    /// <summary>Ordered witness interventions; recorded only when evidence supports the order.</summary>
    CorrectionSequence,
    /// <summary>Region exists, no annotation decision made — distinct from "no text present".</summary>
    UnannotatedRegion,
    /// <summary>A visible mark outside the profile's sign inventory; described, never guessed into a character.</summary>
    UnidentifiedMark,
    /// <summary>Visible glyph form that plain Unicode cannot express (lunate sigma, epichoric forms, ligatures).</summary>
    GlyphForm,
    /// <summary>Visible nonalphabetic sign from the profile's sign inventory (e.g. overbar, filling marker).</summary>
    Sign,
    /// <summary>Visible abbreviation with a linked L2 expansion; the L1 span is never rewritten.</summary>
    Abbreviation,
    /// <summary>Zero-width L2 word-division proposal at a scalar boundary.</summary>
    WordBoundary,
}

/// <summary>How a correction-sequence index was established.</summary>
public enum AnnotationOrderBasis
{
    Unknown,
    Explicit,
    Inferred,
}

/// <summary>Extent certainty for <see cref="AnnotationKind.Lost"/> spans.</summary>
public enum GapExtent
{
    /// <summary>Measured or structurally determined.</summary>
    Known,
    /// <summary>Annotator estimate; <see cref="Annotation.EstimatedScalarCount"/> carries the estimate.</summary>
    Estimated,
    Unknown,
}

/// <summary>
/// An L3 annotation anchored to a named revision by scalar offsets
/// (domain-and-pipeline.md §1.3; greek-transcription.md §4–§5).
/// </summary>
public sealed record Annotation
{
    public required EntityId Id { get; init; }

    public required AnnotationKind Kind { get; init; }

    /// <summary>The revision the offsets are valid against. Never silently re-anchored.</summary>
    public required EntityId AnchoredRevisionId { get; init; }

    public required ScalarRange Range { get; init; }

    /// <summary>For Lost: known / estimated / unknown extent.</summary>
    public GapExtent? GapExtent { get; init; }

    /// <summary>For Lost with Estimated extent: the estimated count of lost scalars.</summary>
    public int? EstimatedScalarCount { get; init; }

    /// <summary>For AlternativeReading: the alternative text; the span's own text is the other reading.</summary>
    public string? AlternativeText { get; init; }

    /// <summary>For DifferentHand: hand label or "unattributed"; never invented.</summary>
    public string? HandLabel { get; init; }

    /// <summary>For GlyphForm / Sign / UnidentifiedMark: profile sign ID or a free-text description.</summary>
    public string? Description { get; init; }

    /// <summary>For Abbreviation: the editorial expansion text.</summary>
    public string? ExpansionText { get; init; }

    /// <summary>For Abbreviation: the L2 revision carrying the expansion.</summary>
    public EntityId? ExpansionRevisionId { get; init; }

    /// <summary>For Abbreviation: the expansion's scalar range in the L2 revision.</summary>
    public ScalarRange? ExpansionRange { get; init; }

    /// <summary>For CorrectionSequence: optional grouping id shared by ordered interventions.</summary>
    public string? SequenceId { get; init; }

    /// <summary>For CorrectionSequence: optional explicit order within the sequence group.</summary>
    public int? SequenceIndex { get; init; }

    /// <summary>Whether SequenceIndex was explicit, inferred or not established.</summary>
    public AnnotationOrderBasis? OrderBasis { get; init; }

    public AgentRef? Author { get; init; }
}
