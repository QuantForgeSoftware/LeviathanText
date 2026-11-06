using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Infrastructure;

namespace LeviathanText.Api;

/// <summary>Keep indirect service lookups within the project authorized for this request.</summary>
internal sealed class RequestProjectStore(LocalStore store) : IProjectStore
{
    private EntityId? _projectId;
    private WriteAuthorization? _writeAuthorization;
    private Action? _beforeWrite;

    private sealed record WriteAuthorization(string? ActorId, bool RequireMembership, string? ExpectedKeyHash);

    public void Bind(EntityId projectId, Action? beforeWrite = null)
    {
        _projectId = projectId;
        _writeAuthorization = null;
        _beforeWrite = beforeWrite;
    }

    public void BindAuthorized(EntityId projectId, string? actorId, bool requireMembership,
        string? expectedKeyHash = null, Action? beforeWrite = null)
    {
        _projectId = projectId;
        _writeAuthorization = new(actorId, requireMembership, expectedKeyHash);
        _beforeWrite = beforeWrite;
    }

    internal void RefreshMembershipRequirement(bool requireMembership)
    {
        if (_writeAuthorization is { } authorization)
            _writeAuthorization = authorization with { RequireMembership = requireMembership };
    }

    public NativeProject? LoadProject(EntityId id) => InScope(id) ? store.LoadProject(id) : null;

    public IReadOnlyList<(EntityId Id, string Name)> ListProjects()
    {
        var projects = store.ListProjects();
        return _projectId is { } projectId ? [.. projects.Where(project => project.Id == projectId)] : projects;
    }

    public long SaveProject(NativeProject project)
    {
        RequireScope(project.Id);
        // Live configuration refresh stays outside the store lock/SQLite transaction.
        _beforeWrite?.Invoke();
        if (_writeAuthorization is { } authorization)
        {
            try { return store.SaveProjectAuthorized(project, authorization.ActorId, authorization.RequireMembership, authorization.ExpectedKeyHash); }
            catch (ProjectWriteAccessDeniedException denied) { throw new NativeAiAccessDeniedException(denied); }
        }
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
