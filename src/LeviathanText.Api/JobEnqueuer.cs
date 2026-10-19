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
public sealed class JobEnqueuer(LocalStore store, JobEventBus bus)
{
    public JobRecord Enqueue(string type, AgentRef owner, IReadOnlyList<StageArtifact>? inputs = null, string? idempotencyKey = null)
    {
        if (idempotencyKey is not null)
        {
            var existing = store.FindJobByIdempotencyKey(idempotencyKey);
            if (existing is not null) return existing; // duplicate creation returns the existing job
        }

        var job = new JobRecord
        {
            Id = new EntityId(EntityKind.Job, Guid.NewGuid().ToString("N")[..12]),
            Type = type,
            Inputs = inputs ?? [],
            IdempotencyKey = idempotencyKey,
            Owner = owner,
            Status = JobStatus.Pending,
        };
        store.SaveJob(job);
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
        var job = store.LoadJob(jobId)
            ?? throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = jobId.ToString(), Message = "Unknown job." });
        if (job.Status is JobStatus.Succeeded or JobStatus.Failed or JobStatus.Cancelled) return job; // idempotent
        var updated = job with { Status = JobStatus.Cancelled, Lease = null };
        store.SaveJob(updated);
        bus.Publish(job.Id, "job.cancelled", ContractJson.ToJson(updated));
        return updated;
    }
}
