using LeviathanText.Contracts;

namespace LeviathanText.Application;

/// <summary>
/// Best-effort presence for a single API process: editors heartbeat on a surface and are
/// removed after a short TTL. This is not a durable collaboration ledger and does not provide
/// cross-process presence; the API documents that limit. No identity/authorization decision
/// depends on presence.
/// </summary>
public sealed class PresenceService
{
    public static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);
    public const int MaxEditorsPerSurface = 100;

    private readonly object _gate = new();
    private readonly Dictionary<EntityId, Dictionary<string, DateTimeOffset>> _editors = [];

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

        lock (_gate)
        {
            if (!_editors.TryGetValue(surfaceId, out var active))
            {
                active = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
                _editors[surfaceId] = active;
            }
            Prune(active, at);
            if (!active.ContainsKey(editorId) && active.Count >= MaxEditorsPerSurface)
                throw new LeviathanException(new LeviathanError
                {
                    Code = ErrorCode.StageContractViolation,
                    Scope = "presence",
                    Details = new Dictionary<string, string> { ["max"] = MaxEditorsPerSurface.ToString() },
                    Message = "Too many active editors on this surface.",
                });
            active[editorId] = at;
            return active.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
        }
    }

    public IReadOnlyList<string> List(EntityId surfaceId, DateTimeOffset? now = null)
    {
        var at = now ?? DateTimeOffset.UtcNow;
        lock (_gate)
        {
            if (!_editors.TryGetValue(surfaceId, out var active)) return [];
            Prune(active, at);
            if (active.Count == 0) _editors.Remove(surfaceId);
            return active.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
        }
    }

    public IReadOnlyList<string> Leave(EntityId surfaceId, string editorId)
    {
        lock (_gate)
        {
            if (!_editors.TryGetValue(surfaceId, out var active)) return [];
            active.Remove(editorId);
            if (active.Count == 0) _editors.Remove(surfaceId);
            return active.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
        }
    }

    private static void Prune(Dictionary<string, DateTimeOffset> active, DateTimeOffset now)
    {
        foreach (var stale in active.Where(pair => now - pair.Value > Ttl).Select(pair => pair.Key).ToArray())
            active.Remove(stale);
    }
}
