using LeviathanText.Contracts;

namespace LeviathanText.Core.Jobs;

/// <summary>
/// In-memory job table proving the C0 idempotency semantics (domain-and-pipeline.md §5;
/// interchange-and-api.md §2.2): duplicate creation returns the existing job; output
/// publication is idempotent — re-publishing identical outputs after success is a no-op,
/// and divergent publication of a finalized job is a hard error. Persistence replaces
/// this class in later packages without changing the semantics.
/// </summary>
public sealed class JobTable
{
    private readonly Dictionary<EntityId, JobRecord> _jobs = [];
    private readonly Dictionary<string, EntityId> _byIdempotencyKey = [];
    private int _nextId;

    /// <summary>Create a job. Same idempotency key returns the existing job, not a new one.</summary>
    public JobRecord Create(string type, AgentRef owner, IReadOnlyList<StageArtifact>? inputs = null,
        string? idempotencyKey = null, int priority = 0)
    {
        if (idempotencyKey is not null && _byIdempotencyKey.TryGetValue(idempotencyKey, out var existing))
            return _jobs[existing];

        var job = new JobRecord
        {
            Id = new EntityId(EntityKind.Job, $"j{Interlocked.Increment(ref _nextId):x8}"),
            Type = type,
            Inputs = inputs ?? [],
            IdempotencyKey = idempotencyKey,
            Owner = owner,
            Priority = priority,
            CreatedAt = DateTimeOffset.UtcNow,
            Status = JobStatus.Pending,
        };
        _jobs[job.Id] = job;
        if (idempotencyKey is not null) _byIdempotencyKey[idempotencyKey] = job.Id;
        return job;
    }

    public JobRecord Get(EntityId jobId) =>
        _jobs.TryGetValue(jobId, out var job) ? job
            : throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = jobId.ToString(),
                Message = "Unknown job.",
            });

    /// <summary>Publish one output artifact. Idempotent: identical artifacts merge silently.</summary>
    public JobRecord PublishOutput(EntityId jobId, StageArtifact artifact)
    {
        var job = Get(jobId);
        if (job.Status is JobStatus.Succeeded or JobStatus.Failed or JobStatus.Cancelled)
        {
            if (job.Outputs.Contains(artifact))
                return job; // re-run of finalized publication: no duplicate, no overwrite

            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = jobId.ToString(),
                Details = new Dictionary<string, string> { ["reason"] = "divergent output on finalized job" },
                Message = "Finalized job already published different outputs; a re-run may not overwrite them.",
            });
        }

        return _jobs[jobId] = job with
        {
            Outputs = [.. job.Outputs.Append(artifact)],
            Status = JobStatus.Succeeded,
        };
    }

    /// <summary>Record a retry attempt on a failed job (bounded, recorded).</summary>
    public JobRecord Retry(EntityId jobId, int maxRetries)
    {
        var job = Get(jobId);
        if (job.Status is not (JobStatus.Failed or JobStatus.Pending))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = jobId.ToString(),
                Message = $"Retry is only valid for failed/pending jobs (job is {job.Status}).",
            });
        if (job.RetriesUsed >= maxRetries)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = jobId.ToString(),
                Details = new Dictionary<string, string> { ["retriesUsed"] = job.RetriesUsed.ToString(), ["maxRetries"] = maxRetries.ToString() },
                Message = "Retry budget exhausted.",
            });

        return _jobs[jobId] = job with
        {
            Status = JobStatus.Pending,
            RetriesUsed = job.RetriesUsed + 1,
            Failure = null,
        };
    }

    public JobRecord Cancel(EntityId jobId)
    {
        var job = Get(jobId);
        if (job.Status is JobStatus.Succeeded or JobStatus.Failed or JobStatus.Cancelled) return job; // idempotent cancel
        return _jobs[jobId] = job with { Status = JobStatus.Cancelled };
    }
}
