using LeviathanText.Contracts;
using LeviathanText.Contracts.Serialization;
using LeviathanText.Infrastructure;

namespace LeviathanText.Api;

/// <summary>
/// Durable job creation for the HTTP surface: jobs persist in SQLite before any
/// acknowledgment, creation is idempotent by client key, and every transition is
/// announced on the SSE event bus (interchange-and-api.md §2.2: events are advisory;
/// the job resource remains authoritative).
/// </summary>
public sealed class JobEnqueuer(LocalStore store, JobEventBus bus, IConfiguration configuration)
{
    public JobRecord Enqueue(string type, AgentRef owner, IReadOnlyList<StageArtifact>? inputs = null,
        string? idempotencyKey = null, int priority = 0, int maxActivePerOwner = 0, EntityId? boundProjectId = null)
    {
        JobRecord? ExistingJob() => idempotencyKey is null ? null : store.FindJobByIdempotencyKey(idempotencyKey);
        if (ExistingJob() is { } existing) return existing;

        if (priority is < 0 or > 100)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = "jobs",
                Message = "Job priority must be between 0 and 100.",
            });

        maxActivePerOwner = maxActivePerOwner > 0
            ? maxActivePerOwner
            : configuration.GetValue<int>("LeviathanText:Jobs:MaxActivePerOwner");
        var job = new JobRecord
        {
            Id = new EntityId(EntityKind.Job, Guid.NewGuid().ToString("N")[..12]),
            Type = type,
            Inputs = inputs ?? [],
            BoundProjectId = boundProjectId,
            IdempotencyKey = idempotencyKey,
            Owner = owner,
            Priority = priority,
            CreatedAt = DateTimeOffset.UtcNow,
            Status = JobStatus.Pending,
        };
        if (!store.TryCreateJob(job, maxActivePerOwner))
        {
            if (ExistingJob() is { } winner) return winner;
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = "jobs",
                Details = new Dictionary<string, string>
                {
                    ["owner"] = owner.Id,
                    ["active"] = store.CountActiveJobs(owner.Id).ToString(),
                    ["max"] = maxActivePerOwner.ToString(),
                },
                Message = "Per-owner active-job quota exceeded.",
            });
        }
        bus.Publish(job.Id, "job.created", ContractJson.ToJson(job));
        return job;
    }

    public JobRecord RecordProgress(JobRecord job, double progress)
    {
        var updated = job with { Progress = Math.Clamp(progress, 0, 1) };
        store.SaveJob(updated);
        bus.Publish(job.Id, "job.progress", ContractJson.ToJson(updated));
        return updated;
    }

    public JobRecord Complete(JobRecord job, IReadOnlyList<StageArtifact> outputs)
    {
        var updated = job with { Status = JobStatus.Succeeded, Outputs = outputs, Lease = null };
        store.SaveJob(updated);
        bus.Publish(job.Id, "job.completed", ContractJson.ToJson(updated));
        return updated;
    }

    public JobRecord Fail(JobRecord job, LeviathanError error)
    {
        var updated = job with { Status = JobStatus.Failed, Failure = error, Lease = null };
        store.SaveJob(updated);
        bus.Publish(job.Id, "job.failed", ContractJson.ToJson(updated));
        return updated;
    }

    public JobRecord Cancel(EntityId jobId)
    {
        var cancelled = store.UpdateJob(jobId, current =>
            current.Status is JobStatus.Pending or JobStatus.Running
                ? current with { Status = JobStatus.Cancelled, Lease = null }
                : null);
        if (cancelled is not null)
        {
            bus.Publish(cancelled.Id, "job.cancelled", ContractJson.ToJson(cancelled));
            return cancelled;
        }

        return store.LoadJob(jobId)
            ?? throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = jobId.ToString(), Message = "Unknown job." });
    }
}
