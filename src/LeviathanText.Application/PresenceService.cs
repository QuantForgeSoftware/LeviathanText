using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application;

/// <summary>
/// Advisory presence over the shared store: editors heartbeat on a surface and entries are
/// TTL-pruned. Processes sharing the store see the same state, but this is not a distributed
/// session registry, lock, or authorization source.
/// </summary>
public sealed class PresenceService(IPresenceStore store)
{
    public static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);
    public const int MaxEditorsPerSurface = 100;

    public IReadOnlyList<string> Touch(EntityId surfaceId, string editorId, DateTimeOffset? now = null)
    {
        var at = now ?? DateTimeOffset.UtcNow;
        if (string.IsNullOrWhiteSpace(editorId) || editorId.Length > 120)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = "presence",
                Message = "Editor id must be a non-empty string (max 120 characters).",
            });

        var active = store.ListPresence(surfaceId, at - Ttl);
        if (!active.Contains(editorId, StringComparer.Ordinal) && active.Count >= MaxEditorsPerSurface)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = "presence",
                Details = new Dictionary<string, string> { ["max"] = MaxEditorsPerSurface.ToString() },
                Message = "Too many active editors on this surface.",
            });
        store.TouchPresence(surfaceId, editorId, at);
        return store.ListPresence(surfaceId, at - Ttl);
    }

    public IReadOnlyList<string> List(EntityId surfaceId, DateTimeOffset? now = null)
    {
        return store.ListPresence(surfaceId, (now ?? DateTimeOffset.UtcNow) - Ttl);
    }

    public IReadOnlyList<string> Leave(EntityId surfaceId, string editorId)
    {
        store.LeavePresence(surfaceId, editorId);
        return store.ListPresence(surfaceId, DateTimeOffset.UtcNow - Ttl);
    }
}
