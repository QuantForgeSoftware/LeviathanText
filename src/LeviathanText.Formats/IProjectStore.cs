using LeviathanText.Contracts;

namespace LeviathanText.Formats;

/// <summary>
/// Persistence contract for native projects (todo §4 storage). Implementations must be
/// transactional per save; unknown project ids load as null. Backed by SQLite locally;
/// PostgreSQL/object storage for shared deployments later.
/// </summary>
public interface IProjectStore
{
    void SaveProject(NativeProject project);
    NativeProject? LoadProject(EntityId id);
    IReadOnlyList<(EntityId Id, string Name)> ListProjects();

    /// <summary>
    /// Remove a project and its stored document. Stores that do not support deletion keep
    /// the default (explicit failure) so callers cannot assume it silently works.
    /// </summary>
    void DeleteProject(EntityId id) =>
        throw new NotSupportedException("This project store does not support deletion.");
}
