namespace LeviathanText.Contracts;

/// <summary>
/// Manifest vocabulary types shared across modules. Full serialization lives with the
/// corpus-manifest contract (corpus-manifest.md); these types carry the controlled
/// vocabularies so all modules speak the same terms (todo §5, DATA-01).
/// </summary>
public enum MaterialTrack
{
    Manuscript,
    Papyrus,
    Inscription,
    Auxiliary,
    Mixed,
}

/// <summary>What a labeled asset's labels actually are — the guard against mislabeling (corpus-manifest.md §4).</summary>
public enum LabelGranularity
{
    None,
    Line,
    Word,
    CharacterBox,
    Glyph,
    Region,
}

/// <summary>Whether an image and an audited label actually pair (corpus-manifest.md §4).</summary>
public enum PairingStatus
{
    Paired,
    ImageOnly,
    LabelOnly,
    /// <summary>Files exist under the same name but alignment is unverified (e.g. P01's five extra images).</summary>
    UnpairedUnsynced,
}

/// <summary>"Missing character boxes" means unknown, never background (corpus-manifest.md §4).</summary>
public enum AnnotationCompleteness
{
    Complete,
    Partial,
    Unknown,
}

/// <summary>Reference to the versioned transcription profile a label set follows (greek-transcription.md §6).</summary>
public sealed record TranscriptionProfileRef
{
    public required string ProfileId { get; init; }

    public required string Version { get; init; }

    /// <summary>True for imported labels predating the profile; conversion is a recorded, auditable act.</summary>
    public bool PreProject { get; init; }
}
