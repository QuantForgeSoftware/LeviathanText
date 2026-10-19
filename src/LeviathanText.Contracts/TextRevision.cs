namespace LeviathanText.Contracts;

/// <summary>
/// Scalar offsets are the contract's annotation currency (greek-transcription.md §4):
/// a half-open range of Unicode code points against a named revision's text.
/// UTF-16 and grapheme positions are conversions, never stored values.
/// </summary>
public readonly record struct ScalarRange
{
    public ScalarRange(int start, int end)
    {
        if (start < 0 || end < start) throw new ArgumentOutOfRangeException(nameof(start), "Range must satisfy 0 <= start <= end.");
        Start = start;
        End = end;
    }

    /// <summary>Inclusive start scalar offset.</summary>
    public int Start { get; }

    /// <summary>Exclusive end scalar offset (may equal text scalar length for end-anchored annotations).</summary>
    public int End { get; }

    public bool IsEmpty => Start == End;

    public override string ToString() => $"[{Start},{End})";
}

/// <summary>Text layers are distinct and never collapse (greek-transcription.md §1).</summary>
public enum TextLayer
{
    /// <summary>Untouched model output, as produced.</summary>
    L0RawRecognition,
    /// <summary>Documented representation of the visible witness; the default training-label source.</summary>
    L1Diplomatic,
    /// <summary>Editorial reading: divisions, accents, expansions, restorations — never a visual-training label.</summary>
    L2Editorial,
}

/// <summary>Who or what authored a revision or accepted a proposal.</summary>
public sealed record AgentRef
{
    /// <summary>Stable agent identifier, e.g. a user ID or a provider/model identity.</summary>
    public required string Id { get; init; }

    /// <summary>True for model/AI authorship; provenance must then carry model identity too.</summary>
    public required bool IsModel { get; init; }

    /// <summary>Model identity + version when <see cref="IsModel"/> (e.g. "provider/model@2026-01").</summary>
    public string? ModelIdentity { get; init; }
}

/// <summary>
/// A text revision defines the full text of one layer in one scope and is the anchor
/// space for annotations (domain-and-pipeline.md §1.3). Revisions are immutable; edits
/// create new revisions with <see cref="ParentRevisionId"/> set.
/// </summary>
public sealed record TextRevision
{
    public required EntityId Id { get; init; }
    public required EntityId ScopeId { get; init; }
    public required TextLayer Layer { get; init; }

    /// <summary>Null for the first revision of the scope/layer.</summary>
    public EntityId? ParentRevisionId { get; init; }

    public required AgentRef Author { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Logical-order Unicode text as stored (NFC per greek-transcription.md §4).</summary>
    public required string Text { get; init; }

    /// <summary>Normalized (NFC) form, if normalization produced a distinct revision; otherwise null.</summary>
    public string? NormalizedText { get; init; }

    /// <summary>
    /// Mean confidence for this revision where the producer reports one (L0 recognition);
    /// <see cref="Confidence.None"/> for human revisions. The scale travels with the value.
    /// </summary>
    public Confidence Confidence { get; init; } = Confidence.None;
}
