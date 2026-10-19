using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Worker;

/// <summary>
/// Durable job runner (todo §4: durable jobs with leases, heartbeats, cancellation,
/// retries; at-least-once execution with idempotent output publication).
///
/// Claim protocol: a pending job whose lease is absent/expired is claimed by writing a
/// timed lease; heartbeats renew it; a finalizing write must still carry the claimed
/// lease. Completed/failed/cancelled jobs are never re-executed, so a restarted worker
/// cannot duplicate accepted revisions or overwrite finalized exports.
/// </summary>
public sealed class WorkerHost(IJobStore store, IReadOnlyList<IJobHandler> handlers, TimeSpan? leaseDuration = null)
{
    private readonly TimeSpan _leaseDuration = leaseDuration ?? TimeSpan.FromSeconds(30);

    public EntityId WorkerId { get; } = new(EntityKind.Job, $"w{Guid.NewGuid().ToString("N")[..10]}");

    public IReadOnlyList<JobRecord> ClaimableJobs()
    {
        var now = DateTimeOffset.UtcNow;
        return [.. store.PendingJobs()
            .Where(j => j.Lease is null || j.Lease.ExpiresAt <= now)];
    }

    /// <summary>Claim a job for this worker (timed lease). Returns null when someone else holds it.
    /// A job with an expired lease (Pending or Running) is reclaimable — the holder is presumed dead.</summary>
    public JobRecord? TryClaim(JobRecord job, DateTimeOffset? now = null)
    {
        var at = now ?? DateTimeOffset.UtcNow;
        var current = store.Find(job.Id);
        if (current is null) return null;
        if (current.Status is JobStatus.Succeeded or JobStatus.Failed or JobStatus.Cancelled) return null;
        if (current.Lease is { } held && held.ExpiresAt > at) return null; // actively held by someone

        var claimed = current with
        {
            Status = JobStatus.Running,
            Lease = new LeaseInfo
            {
                Worker = WorkerId,
                ExpiresAt = (now ?? DateTimeOffset.UtcNow) + _leaseDuration,
                LastHeartbeat = now ?? DateTimeOffset.UtcNow,
            },
        };
        store.Save(claimed);
        return claimed;
    }

    /// <summary>Renew the lease (heartbeat) if this worker still holds the job.</summary>
    public JobRecord? Heartbeat(EntityId jobId, DateTimeOffset? now = null)
    {
        var job = store.Find(jobId);
        if (job?.Lease is not { } lease || lease.Worker != WorkerId) return null;
        var renewed = job with { Lease = lease with { LastHeartbeat = now ?? DateTimeOffset.UtcNow, ExpiresAt = (now ?? DateTimeOffset.UtcNow) + _leaseDuration } };
        store.Save(renewed);
        return renewed;
    }

    /// <summary>Run one claimable job of a registered type; returns the finalized record or null if nothing claimable.</summary>
    public JobRecord? RunNext(CancellationToken cancellation = default, DateTimeOffset? now = null)
    {
        foreach (var job in ClaimableJobs())
        {
            cancellation.ThrowIfCancellationRequested();
            var handler = handlers.FirstOrDefault(h => h.Type == job.Type)
                ?? throw new LeviathanException(new LeviathanError
                {
                    Code = ErrorCode.StageContractViolation,
                    Scope = job.Id.ToString(),
                    Details = new Dictionary<string, string> { ["type"] = job.Type },
                    Message = "No handler registered for this job type.",
                });

            var claimed = TryClaim(job, now);
            if (claimed is null) continue;

            try
            {
                var executed = handler.Execute(claimed, cancellation);
                var finalized = executed with
                {
                    Status = executed.Status is JobStatus.Failed ? JobStatus.Failed : JobStatus.Succeeded,
                    Lease = null,
                };
                store.Save(finalized);
                return finalized;
            }
            catch (OperationCanceledException)
            {
                // Cooperative cancellation: checkpoints remain valid; job becomes pending again.
                store.Save(claimed with { Status = JobStatus.Pending, Lease = null });
                return null;
            }
            catch (LeviathanException ex)
            {
                return RecordFailure(claimed, ex.Error);
            }
            catch (Exception ex)
            {
                return RecordFailure(claimed, new LeviathanError
                {
                    Code = ErrorCode.StageContractViolation,
                    Scope = job.Id.ToString(),
                    Details = new Dictionary<string, string> { ["exception"] = ex.GetType().Name },
                    Message = ex.Message,
                });
            }
        }
        return null;
    }

    private JobRecord RecordFailure(JobRecord claimed, LeviathanError error)
    {
        var failed = claimed with
        {
            Status = claimed.RetriesUsed < MaxRetries ? JobStatus.Pending : JobStatus.Failed,
            RetriesUsed = claimed.RetriesUsed + 1,
            Failure = error,
            Lease = null, // release; a pending retry may be claimed again later
        };
        store.Save(failed);
        return failed;
    }

    /// <summary>Retry budget per job (bounded, recorded; JobRecord.RetriesUsed counts attempts).</summary>
    public int MaxRetries { get; set; } = 3;
}
