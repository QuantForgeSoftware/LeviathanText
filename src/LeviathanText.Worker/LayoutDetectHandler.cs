using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Worker;

/// <summary>
/// Durable layout job: the job's first input artifact names the surface to process
/// (checksum fields are informational for this job type). Runs the projection detector,
/// refuses to overwrite existing layout, and finalizes idempotently through the host.
/// </summary>
public sealed class LayoutDetectHandler(IProjectStore store) : IJobHandler
{
    public const string JobType = "layout.detect";

    public string Type => JobType;

    public JobRecord Execute(JobRecord job, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var input = job.Inputs.FirstOrDefault()
            ?? throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = job.Id.ToString(),
                Message = "layout.detect needs the target surface as its first input artifact.",
            });

        var projectId = input.Artifact.Digest is { Length: > 0 } digest && digest.StartsWith("proj:", StringComparison.Ordinal)
            ? (EntityId?)EntityId.Parse(digest[5..], EntityKind.Collection)
            : FindProjectForSurface(input.Entity);

        var project = projectId is { } id ? store.LoadProject(id) : null;
        if (project is null)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = input.Entity.ToString(),
                Message = "Surface not found in any project.",
            });

        var service = new LayoutService(store);
        var outcome = service.Detect(project, input.Entity);

        return job with
        {
            Outputs = [new StageArtifact(outcome.Surface.Id, new Checksum
            {
                Algorithm = "layout-line-count",
                Digest = outcome.LineCount.ToString(),
            })],
        };
    }

    private EntityId? FindProjectForSurface(EntityId surfaceId)
    {
        foreach (var (id, _) in store.ListProjects())
        {
            var project = store.LoadProject(id);
            if (project is not null && project.Objects.SelectMany(o => o.Surfaces).Any(s => s.Id == surfaceId))
                return id;
        }
        return null;
    }
}
