using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Worker;

/// <summary>
/// Durable job runner with atomic claims and attempt-checked status transitions.
///
/// Polling claims pending jobs only. Running jobs are excluded even after expiry,
/// since their handlers may still be publishing side effects.
/// </summary>
public sealed class WorkerHost(IJobStore store, IReadOnlyList<IJobHandler> handlers, TimeSpan? leaseDuration = null)
{
    private readonly TimeSpan _leaseDuration = leaseDuration ?? TimeSpan.FromSeconds(30);

    public EntityId WorkerId { get; } = new(EntityKind.Job, $"w{Guid.NewGuid().ToString("N")[..10]}");

    public IReadOnlyList<JobRecord> ClaimableJobs() => ClaimableJobs(DateTimeOffset.UtcNow);

    private IReadOnlyList<JobRecord> ClaimableJobs(DateTimeOffset now) =>
        [.. store.PendingJobs()
            .Where(j => j.Lease is null || j.Lease.ExpiresAt <= now)
            .OrderByDescending(j => j.Priority)
            .ThenBy(j => j.CreatedAt)
            .ThenBy(j => j.Id.ToString(), StringComparer.Ordinal)];

    /// <summary>
    /// Explicitly claim a pending job or recover an expired running claim after ensuring
    /// its previous handler has stopped. The poller only claims pending jobs.
    /// </summary>
    public JobRecord? TryClaim(JobRecord job, DateTimeOffset? now = null) =>
        TryClaim(job.Id, now ?? DateTimeOffset.UtcNow, allowRunning: true);

    private JobRecord? TryClaim(EntityId id, DateTimeOffset now, bool allowRunning)
    {
        var lease = new LeaseInfo
        {
            Worker = WorkerId,
            ClaimId = Guid.NewGuid().ToString("N"),
            ExpiresAt = now + _leaseDuration,
            LastHeartbeat = now,
        };
        return store.Update(id, current =>
        {
            if (current.Status != JobStatus.Pending && !(allowRunning && current.Status == JobStatus.Running))
                return null;
            if (current.Lease is { } held && held.ExpiresAt > now) return null;
            return current with { Status = JobStatus.Running, Lease = lease };
        });
    }

    /// <summary>Renew the lease (heartbeat) if this worker still holds the job.</summary>
    public JobRecord? Heartbeat(EntityId jobId, DateTimeOffset? now = null)
    {
        var at = now ?? DateTimeOffset.UtcNow;
        return store.Update(jobId, current =>
        {
            if (current.Status != JobStatus.Running || current.Lease is not { } lease || lease.Worker != WorkerId)
                return null;
            return current with { Lease = lease with { LastHeartbeat = at, ExpiresAt = at + _leaseDuration } };
        });
    }

    /// <summary>Run one claimable job of a registered type; returns the finalized record or null if nothing claimable.</summary>
    public JobRecord? RunNext(CancellationToken cancellation = default, DateTimeOffset? now = null)
    {
        foreach (var job in ClaimableJobs(now ?? DateTimeOffset.UtcNow))
        {
            cancellation.ThrowIfCancellationRequested();
            var claimed = TryClaim(job.Id, now ?? DateTimeOffset.UtcNow, allowRunning: false);
            if (claimed is null) continue;

            var finalized = ExecuteAttempt(claimed, cancellation);
            var saved = store.Update(claimed.Id, current =>
                current.Status == JobStatus.Running &&
                current.Lease is { } lease && lease.Worker == WorkerId && lease.ClaimId == claimed.Lease!.ClaimId
                    ? finalized : null);
            return saved ?? (store.Find(claimed.Id) is { Status: JobStatus.Cancelled } cancelled ? cancelled : null);
        }
        return null;
    }

    private JobRecord ExecuteAttempt(JobRecord claimed, CancellationToken cancellation)
    {
        var handler = handlers.FirstOrDefault(h => h.Type == claimed.Type);
        if (handler is null)
        {
            return claimed with
            {
                Status = JobStatus.Failed,
                Lease = null,
                Failure = new LeviathanError
                {
                    Code = ErrorCode.StageContractViolation,
                    Scope = claimed.Id.ToString(),
                    Details = new Dictionary<string, string> { ["type"] = claimed.Type },
                    Message = "No handler registered for this job type.",
                },
            };
        }

        try
        {
            var executed = handler.Execute(claimed, cancellation);
            return executed with
            {
                Status = executed.Status is JobStatus.Failed ? JobStatus.Failed : JobStatus.Succeeded,
                Lease = null,
            };
        }
        catch (OperationCanceledException)
        {
            // The conditional write preserves an API cancellation. A still owned
            // claim can return to Pending after cooperative cancellation.
            return claimed with { Status = JobStatus.Pending, Lease = null };
        }
        catch (LeviathanException ex)
        {
            return FailedAttempt(claimed, ex.Error);
        }
        catch (Exception ex)
        {
            return FailedAttempt(claimed, new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = claimed.Id.ToString(),
                Details = new Dictionary<string, string> { ["exception"] = ex.GetType().Name },
                Message = ex.Message,
            });
        }
    }

    private JobRecord FailedAttempt(JobRecord claimed, LeviathanError error) =>
        claimed with
        {
            Status = claimed.RetriesUsed < MaxRetries ? JobStatus.Pending : JobStatus.Failed,
            RetriesUsed = claimed.RetriesUsed + 1,
            Failure = error,
            Lease = null, // release; a pending retry may be claimed again later
        };

    /// <summary>Retry budget per job (bounded, recorded; JobRecord.RetriesUsed counts attempts).</summary>
    public int MaxRetries { get; set; } = 3;
}
