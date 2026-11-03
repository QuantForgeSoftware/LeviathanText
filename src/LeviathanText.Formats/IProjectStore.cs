using LeviathanText.Contracts;

namespace LeviathanText.Formats;

/// <summary>
/// Persistence contract for native projects (project-plan §4 storage). Implementations must be
/// transactional per save; unknown project ids load as null. Backed by SQLite locally;
/// PostgreSQL/object storage for shared deployments later.
/// </summary>
public interface IProjectStore
{
    /// <summary>
    /// Persist the document and return its new optimistic-concurrency version. Callers that
    /// keep mutating the returned object must carry this version forward.
    /// </summary>
    long SaveProject(NativeProject project);
    NativeProject? LoadProject(EntityId id);
    IReadOnlyList<(EntityId Id, string Name)> ListProjects();

    /// <summary>
    /// Remove a project and its stored document. Stores that do not support deletion keep
    /// the default (explicit failure) so callers cannot assume it silently works.
    /// </summary>
    void DeleteProject(EntityId id) =>
        throw new NotSupportedException("This project store does not support deletion.");
}

/// <summary>
/// Optional server-issued editing sessions used by shared deployments. Sessions bind a user
/// to a node and surface, expire unless heartbeat, and are advisory coordination records;
/// they never participate in authorization.
/// </summary>
public interface ISessionStore
{
    EditSession CreateSession(string userId, string userName, string nodeId,
        EntityId? surfaceId, DateTimeOffset now, TimeSpan ttl);
    EditSession? FindSession(string id);
    EditSession? TouchSession(string id, DateTimeOffset now, TimeSpan ttl);
    bool RevokeSession(string id, DateTimeOffset now);
    IReadOnlyList<EditSession> ListSessions(DateTimeOffset now, EntityId? surfaceId = null, bool includeExpired = false);
}

/// <summary>
/// Optional presence persistence used by shared deployments. Entries are advisory and
/// TTL-pruned; they never participate in authorization.
/// </summary>
public interface IPresenceStore
{
    void TouchPresence(EntityId surfaceId, string editorId, DateTimeOffset at);
    IReadOnlyList<string> ListPresence(EntityId surfaceId, DateTimeOffset since);
    void LeavePresence(EntityId surfaceId, string editorId);
}
