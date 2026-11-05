using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Infrastructure;

namespace LeviathanText.Api;

/// <summary>Keep indirect service lookups within the project authorized for this request.</summary>
internal sealed class RequestProjectStore(LocalStore store) : IProjectStore
{
    private EntityId? _projectId;

    public void Bind(EntityId projectId) => _projectId = projectId;

    public NativeProject? LoadProject(EntityId id) => InScope(id) ? store.LoadProject(id) : null;

    public IReadOnlyList<(EntityId Id, string Name)> ListProjects()
    {
        var projects = store.ListProjects();
        return _projectId is { } projectId ? [.. projects.Where(project => project.Id == projectId)] : projects;
    }

    public long SaveProject(NativeProject project)
    {
        RequireScope(project.Id);
        return store.SaveProject(project);
    }

    public void DeleteProject(EntityId id)
    {
        RequireScope(id);
        store.DeleteProject(id);
    }

    private bool InScope(EntityId id) => _projectId is null || _projectId == id;

    private void RequireScope(EntityId id)
    {
        if (!InScope(id))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = id.ToString(),
                Message = "No project with this id exists in the request scope.",
            });
    }
}
