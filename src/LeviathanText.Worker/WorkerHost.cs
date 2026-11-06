using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Worker;

/// <summary>
/// Durable job runner with atomic claims and attempt-checked status transitions.
///
/// Polling claims pending jobs only. Running jobs are excluded even after expiry,
/// since their handlers may still be publishing side effects.
/// </summary>
public sealed class WorkerHost(IJobStore store, IReadOnlyList<IJobHandler> handlers, TimeSpan? leaseDuration = null,
    Func<JobRecord, bool>? authorize = null, LeaseRenewalOptions? leaseRenewal = null)
{
    private readonly TimeSpan _leaseDuration = leaseDuration ?? TimeSpan.FromSeconds(30);

    public LeaseRenewalSummary? LastRenewal { get; private set; }

    public EntityId WorkerId { get; } = new(EntityKind.Job, $"w{Guid.NewGuid().ToString("N")[..10]}");

    /// <summary>
    /// Completed dispatch attempts during this host's single-threaded lifetime. Counts
    /// retries, rejected authorization and unsupported types, including an attempt whose
    /// finalization loses its claim. Idle polls and unsuccessful claims do not count.
    /// </summary>
    public long CompletedAttempts { get; private set; }

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

    /// <summary>Legacy nonce-less calls cannot prove attempt ownership and always refuse.
    /// Migrate callers to HeartbeatAttempt using the claim captured at dispatch.</summary>
    public JobRecord? Heartbeat(EntityId jobId, DateTimeOffset? now = null) => null;

    /// <summary>Manual renewal for this exact captured attempt, never inferred from current state.</summary>
    public JobRecord? HeartbeatAttempt(EntityId jobId, string capturedClaimId, DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(capturedClaimId)) return null;
        var at = now ?? DateTimeOffset.UtcNow;
        return store.Update(jobId, current =>
        {
            if (current.Status != JobStatus.Running || current.Lease is not { } lease
                || lease.Worker != WorkerId || lease.ClaimId != capturedClaimId)
                return null;
            var expiry = at + _leaseDuration;
            return current with { Lease = lease with
            {
                LastHeartbeat = lease.LastHeartbeat is { } last && last > at ? last : at,
                ExpiresAt = expiry > lease.ExpiresAt ? expiry : lease.ExpiresAt,
            } };
        });
    }

    /// <summary>
    /// Dispatch one claimable job. Returns its saved record, preserved API cancellation,
    /// or null when no claim succeeds or finalization loses ownership. Use
    /// <see cref="CompletedAttempts"/> to count work independently of finalization.
    /// </summary>
    public JobRecord? RunNext(CancellationToken cancellation = default, DateTimeOffset? now = null)
    {
        leaseRenewal?.Validate(_leaseDuration);
        foreach (var job in ClaimableJobs(now ?? DateTimeOffset.UtcNow))
        {
            cancellation.ThrowIfCancellationRequested();
            var claimed = TryClaim(job.Id, now ?? DateTimeOffset.UtcNow, allowRunning: false);
            if (claimed is null) continue;

            // Disabled by default. The independent session is never shared with handlers.
            LastRenewal = null;
            JobRecord finalized;
            using (var renewal = leaseRenewal is null ? null : new AttemptLeaseRenewal(
                new(claimed.Id, WorkerId, claimed.Lease!.ClaimId!), _leaseDuration, leaseRenewal))
            {
                try { finalized = ExecuteAttempt(claimed, cancellation); }
                finally { if (renewal is not null) LastRenewal = renewal.StopAndJoin(); }
            }
            CompletedAttempts++;
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
        try
        {
            if (authorize is not null && !authorize(claimed))
                return claimed with
                {
                    Status = JobStatus.Cancelled,
                    Lease = null,
                    Failure = new LeviathanError
                    {
                        Code = ErrorCode.Cancelled,
                        Scope = claimed.Id.ToString(),
                        Message = "Project authorization was revoked before execution.",
                    },
                };

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
        catch (LeviathanException ex) when (ex.Error.Code == ErrorCode.Cancelled)
        {
            return claimed with { Status = JobStatus.Cancelled, Lease = null, Failure = ex.Error };
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
