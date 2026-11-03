using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application;

/// <summary>
/// Server-issued editing sessions over the shared store (todo Phase 3 collaboration).
///
/// A session binds an authenticated principal to this server node and an optional surface.
/// It is advisory coordination state, not authentication: requests are still authenticated
/// individually. Sessions expire unless heartbeat and can be revoked by their owner or an
/// administrator. Two server processes over one store therefore see each other's sessions
/// without a broker, which is what makes presence meaningful in a multi-node deployment.
/// </summary>
public sealed class SessionService(ISessionStore store, string nodeId)
{
    public const int MinTtlMinutes = 1;
    public const int MaxTtlMinutes = 480;
    public const int MaxSessionsPerUser = 20;

    public string NodeId { get; } = string.IsNullOrWhiteSpace(nodeId) ? "unknown-node" : nodeId;

    public EditSession Create(UserAccount user, EntityId? surfaceId, int ttlMinutes)
    {
        var now = DateTimeOffset.UtcNow;
        var ttl = Ttl(ttlMinutes);
        var active = store.ListSessions(now).Count(s => s.UserId == user.Id);
        if (active >= MaxSessionsPerUser)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = user.Id,
                Details = new Dictionary<string, string> { ["activeSessions"] = active.ToString() },
                Message = "Too many active sessions for this user; revoke an old session first.",
            });
        return store.CreateSession(user.Id, user.Name, NodeId, surfaceId, now, ttl);
    }

    public EditSession Heartbeat(UserAccount user, string sessionId, int ttlMinutes)
    {
        var session = store.FindSession(sessionId)
            ?? throw Missing(sessionId);
        EnsureOwner(user, session);
        return store.TouchSession(sessionId, DateTimeOffset.UtcNow, Ttl(ttlMinutes))
            ?? throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.RevisionConflict,
                Scope = sessionId,
                Details = new Dictionary<string, string>
                {
                    ["expiresAt"] = session.ExpiresAt.ToString("O"),
                    ["revokedAt"] = session.RevokedAt?.ToString("O") ?? "none",
                },
                Message = "Session is expired or revoked; create a new session.",
            });
    }

    public bool Revoke(UserAccount user, string sessionId)
    {
        var session = store.FindSession(sessionId) ?? throw Missing(sessionId);
        if (session.UserId != user.Id && user.Role != UserRole.Admin)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = sessionId,
                Message = "Only the session owner or an administrator may revoke it.",
            });
        return store.RevokeSession(sessionId, DateTimeOffset.UtcNow);
    }

    public IReadOnlyList<EditSession> List(UserAccount user, EntityId? surfaceId = null,
        bool includeExpired = false) =>
        user.Role == UserRole.Admin
            ? store.ListSessions(DateTimeOffset.UtcNow, surfaceId, includeExpired)
            : [.. store.ListSessions(DateTimeOffset.UtcNow, surfaceId, includeExpired).Where(s => s.UserId == user.Id)];

    private static void EnsureOwner(UserAccount user, EditSession session)
    {
        if (session.UserId != user.Id)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = session.Id,
                Message = "Session belongs to another user.",
            });
    }

    private static TimeSpan Ttl(int ttlMinutes) =>
        TimeSpan.FromMinutes(Math.Clamp(ttlMinutes, MinTtlMinutes, MaxTtlMinutes));

    private static LeviathanException Missing(string sessionId) => new(new LeviathanError
    {
        Code = ErrorCode.InputMissing,
        Scope = sessionId,
        Message = "No such session.",
    });
}
