using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application.Tests;

/// <summary>In-memory IProjectStore for use-case tests.</summary>
public sealed class InMemoryStore : IProjectStore
{
    private readonly Dictionary<string, string> _projects = [];

    public void SaveProject(NativeProject project) => _projects[project.Id.ToString()] = NativeJson.Save(project);

    public NativeProject? LoadProject(EntityId id) =>
        _projects.TryGetValue(id.ToString(), out var json) ? NativeJson.Load(json) : null;

    public IReadOnlyList<(EntityId Id, string Name)> ListProjects() =>
        [.. _projects.Select(kv => (EntityId.Parse(kv.Key), NativeJson.Load(kv.Value).Name))];
}
