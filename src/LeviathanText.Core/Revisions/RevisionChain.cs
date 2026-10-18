using LeviathanText.Contracts;
using LeviathanText.Core.Unicode;

namespace LeviathanText.Core.Revisions;

/// <summary>
/// In-memory revision chain proving the C0 revision semantics
/// (domain-and-pipeline.md §1.3, §6): stale parents fail with
/// <see cref="ErrorCode.RevisionConflict"/>; annotations anchored to superseded
/// revisions are never silently re-anchored; superseded revisions remain retrievable.
/// Persistence replaces this class in later packages without changing the semantics.
/// </summary>
public sealed class RevisionChain
{
    private readonly Dictionary<(EntityId Scope, TextLayer Layer), List<TextRevision>> _chains = new();
    private readonly Dictionary<EntityId, TextRevision> _byId = new();
    private int _nextId;

    /// <summary>The current (latest) revision of a scope/layer, or null.</summary>
    public TextRevision? Current(EntityId scopeId, TextLayer layer) =>
        _chains.TryGetValue((scopeId, layer), out var list) && list.Count > 0 ? list[^1] : null;

    /// <summary>All revisions of a scope/layer, oldest first. Superseded revisions stay retrievable.</summary>
    public IReadOnlyList<TextRevision> History(EntityId scopeId, TextLayer layer) =>
        _chains.TryGetValue((scopeId, layer), out var list) ? list : [];

    public TextRevision? Get(EntityId revisionId) => _byId.TryGetValue(revisionId, out var rev) ? rev : null;

    /// <summary>
    /// Append a revision. When <paramref name="parentRevisionId"/> is given it must be the
    /// current revision of the scope/layer; otherwise <see cref="ErrorCode.RevisionConflict"/>.
    /// </summary>
    public TextRevision Append(EntityId scopeId, TextLayer layer, string text, AgentRef author,
        EntityId? parentRevisionId = null, DateTimeOffset? createdAt = null)
    {
        var current = Current(scopeId, layer);
        if (parentRevisionId is { } requested)
        {
            if (current is null || requested != current.Id)
                throw new LeviathanException(new LeviathanError
                {
                    Code = ErrorCode.RevisionConflict,
                    Scope = $"{scopeId}/{layer}",
                    Details = new Dictionary<string, string>
                    {
                        ["requestedParent"] = requested.ToString(),
                        ["currentRevision"] = current?.Id.ToString() ?? "none",
                    },
                    RemedyHint = RemedyHint.RebaseRevision,
                    Message = "Write targeted a superseded revision; rebase on the current revision.",
                });
        }

        var revision = new TextRevision
        {
            Id = NextId(),
            ScopeId = scopeId,
            Layer = layer,
            ParentRevisionId = current?.Id,
            Author = author,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
            Text = text,
        };
        _chains.GetOrAddNew((scopeId, layer)).Add(revision);
        _byId[revision.Id] = revision;
        return revision;
    }

    /// <summary>
    /// Create a new revision whose text is the NFC form of <paramref name="revision"/>'s text,
    /// remapping <paramref name="annotations"/> onto it via the offset map. Annotations that
    /// would split a composed cluster fail with <see cref="ErrorCode.StaleAnnotationAnchor"/>
    /// and are left anchored to the original revision — nothing detaches silently.
    /// </summary>
    public (TextRevision Normalized, IReadOnlyList<Annotation> Remapped) NormalizeToNfc(
        TextRevision revision, AgentRef author, IEnumerable<Annotation> annotations)
    {
        var mapping = NfcMapper.Map(revision.Text);
        if (mapping.Normalized == revision.Text)
            return (revision, annotations.ToArray()); // already NFC: no-op, no new revision

        var remapped = new List<Annotation>();
        foreach (var annotation in annotations)
        {
            if (annotation.AnchoredRevisionId != revision.Id)
                throw new LeviathanException(new LeviathanError
                {
                    Code = ErrorCode.StaleAnnotationAnchor,
                    Scope = annotation.Id.ToString(),
                    RemedyHint = RemedyHint.RebaseRevision,
                    Message = $"Annotation {annotation.Id} is anchored to {annotation.AnchoredRevisionId}, not {revision.Id}.",
                });

            remapped.Add(annotation with
            {
                AnchoredRevisionId = default, // replaced below after the revision exists
                Range = mapping.MapRange(annotation.Range),
            });
        }

        var newRevision = Append(revision.ScopeId, revision.Layer, mapping.Normalized, author,
            parentRevisionId: revision.Id, createdAt: revision.CreatedAt);

        remapped = remapped.Select(a => a with { AnchoredRevisionId = newRevision.Id }).ToList();
        return (newRevision, remapped);
    }

    /// <summary>
    /// Validate that an annotation is anchored to the current revision of its scope/layer
    /// before a *write* against it (domain-and-pipeline.md §6: stale anchors fail loudly).
    /// Reading history is always permitted.
    /// </summary>
    public void RequireCurrentAnchor(Annotation annotation)
    {
        var revision = Get(annotation.AnchoredRevisionId)
            ?? throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StaleAnnotationAnchor,
                Scope = annotation.Id.ToString(),
                Message = $"Unknown anchored revision {annotation.AnchoredRevisionId}.",
            });

        var current = Current(revision.ScopeId, revision.Layer);
        if (current is null || current.Id != revision.Id)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StaleAnnotationAnchor,
                Scope = annotation.Id.ToString(),
                Details = new Dictionary<string, string> { ["currentRevision"] = current?.Id.ToString() ?? "none" },
                RemedyHint = RemedyHint.RebaseRevision,
                Message = "Annotation is anchored to a superseded revision.",
            });
    }

    private EntityId NextId()
    {
        var n = Interlocked.Increment(ref _nextId);
        return new EntityId(EntityKind.TranscriptionRevision, $"r{n:x8}");
    }
}

file static class DictionaryExtensions
{
    public static TValue GetOrAddNew<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key)
        where TKey : notnull
        where TValue : new()
    {
        if (!dictionary.TryGetValue(key, out var value))
        {
            value = new TValue();
            dictionary[key] = value;
        }
        return value;
    }
}
