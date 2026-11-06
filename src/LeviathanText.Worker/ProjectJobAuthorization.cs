using LeviathanText.Contracts;
using LeviathanText.Infrastructure;

namespace LeviathanText.Worker;

/// <summary>Rechecks the admission-time membership requirement before dispatch.</summary>
public static class ProjectJobAuthorization
{
    /// <summary>Called between atomic work units; already committed units are retained.</summary>
    public static void Checkpoint(JobRecord job, Func<JobRecord, bool>? authorize)
    {
        if (authorize is not null && !authorize(job))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.Cancelled,
                Scope = job.Id.ToString(),
                Message = "Project authorization was revoked before the next work unit.",
            });
    }

    public static bool CanExecute(LocalStore store, JobRecord job)
    {
        // Absent bindings retain legacy/local/admin/opt-out admission semantics.
        if (job.BoundProjectId is not { } projectId) return true;
        var owner = store.FindUserById(job.Owner.Id);
        if (owner is null || owner.Disabled || owner.Role == UserRole.Reader) return false;
        return store.ProjectExists(projectId) &&
            (owner.Role == UserRole.Admin || store.IsProjectMember(projectId, owner.Id));
    }
}
