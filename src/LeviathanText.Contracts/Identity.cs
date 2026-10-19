namespace LeviathanText.Contracts;

/// <summary>Kinds of entity in the C0 identity hierarchy (domain-and-pipeline.md §1.1).</summary>
public enum EntityKind
{
    Collection,
    PhysicalObject,
    Surface,
    Fragment,
    Capture,
    Derivative,
    Region,
    Line,
    GraphemeSpan,
    TranscriptionRevision,
    Annotation,
    Proposal,
    Job,
    Model,
    Export,
}

/// <summary>
/// Opaque, stable, project-minted identifier (domain-and-pipeline.md §1.2).
/// External IDs (TM numbers, shelfmarks, IIIF canvas IDs) are references on entities,
/// never primary keys, so they never appear inside this value.
/// Format: <c>kind-prefix-base62</c>, e.g. <c>ln-0A1b2C</c>. The prefix is validated against the kind.
/// </summary>
public readonly record struct EntityId
{
    private static readonly Dictionary<EntityKind, string> Prefixes = new()
    {
        [EntityKind.Collection] = "col",
        [EntityKind.PhysicalObject] = "obj",
        [EntityKind.Surface] = "surf",
        [EntityKind.Fragment] = "frag",
        [EntityKind.Capture] = "cap",
        [EntityKind.Derivative] = "der",
        [EntityKind.Region] = "reg",
        [EntityKind.Line] = "ln",
        [EntityKind.GraphemeSpan] = "gs",
        [EntityKind.TranscriptionRevision] = "rev",
        [EntityKind.Annotation] = "ann",
        [EntityKind.Proposal] = "prop",
        [EntityKind.Job] = "job",
        [EntityKind.Model] = "mdl",
        [EntityKind.Export] = "exp",
    };

    public EntityId(EntityKind kind, string suffix)
    {
        Kind = kind;
        Suffix = ValidateSuffix(suffix) ?? throw new ArgumentException("Suffix must be 1–63 chars of [A-Za-z0-9_.:-].", nameof(suffix));
    }

    public EntityKind Kind { get; }

    /// <summary>Opaque suffix, stable for the life of the entity.</summary>
    public string Suffix { get; }

    public string Prefix => Prefixes[Kind];

    public override string ToString() => $"{Prefix}-{Suffix}";

    public static EntityId Parse(string value, EntityKind? expectedKind = null)
    {
        var dash = value.IndexOf('-');
        if (dash <= 0 || dash == value.Length - 1)
            throw new FormatException($"EntityId '{value}' must be '<prefix>-<suffix>'.");

        var prefix = value[..dash];
        var kind = Prefixes.FirstOrDefault(kvp => kvp.Value == prefix).Key;
        if (prefix.Length == 0)
            throw new FormatException($"EntityId '{value}' has an empty prefix.");
        if (!Enum.IsDefined(kind) || Prefixes[kind] != prefix)
            throw new FormatException($"EntityId '{value}' has an unknown prefix '{prefix}'.");

        if (expectedKind is { } wanted && kind != wanted)
            throw new FormatException($"EntityId '{value}' is kind {kind}, expected {wanted}.");

        return new EntityId(kind, value[(dash + 1)..]);
    }

    private static string? ValidateSuffix(string suffix)
    {
        if (suffix.Length is 0 or > 63) return null;
        foreach (var c in suffix)
            if (!(char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or ':' or '-'))
                return null;
        return suffix;
    }
}
