using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application.Tests;

/// <summary>In-memory IProjectStore for use-case tests.</summary>
public sealed class InMemoryStore : IProjectStore
{
    private readonly Dictionary<string, (string Json, long Version)> _projects = [];

    public long SaveProject(NativeProject project)
    {
        var key = project.Id.ToString();
        if (project.Version > 0)
        {
            if (!_projects.TryGetValue(key, out var existing))
                throw new InvalidOperationException("Project is not persisted.");
            if (existing.Version != project.Version)
                throw new LeviathanException(new LeviathanError
                {
                    Code = ErrorCode.RevisionConflict,
                    Scope = key,
                    Details = new Dictionary<string, string>
                    {
                        ["expectedVersion"] = project.Version.ToString(),
                        ["currentVersion"] = existing.Version.ToString(),
                    },
                    RemedyHint = RemedyHint.RebaseRevision,
                    Message = "Project was changed by another writer; reload before saving.",
                });
            _projects[key] = (NativeJson.Save(project with { Version = 0 }), existing.Version + 1);
            return existing.Version + 1;
        }

        // Version 0 is an unversioned/legacy write (new project); keep it compatible.
        var next = _projects.TryGetValue(key, out var existingEntry) ? existingEntry.Version + 1 : 1;
        _projects[key] = (NativeJson.Save(project with { Version = 0 }), next);
        return next;
    }

    public NativeProject? LoadProject(EntityId id) =>
        _projects.TryGetValue(id.ToString(), out var entry)
            ? NativeJson.Load(entry.Json) with { Version = entry.Version }
            : null;

    public IReadOnlyList<(EntityId Id, string Name)> ListProjects() =>
        [.. _projects.Select(kv => (EntityId.Parse(kv.Key), NativeJson.Load(kv.Value.Json).Name))];
}
