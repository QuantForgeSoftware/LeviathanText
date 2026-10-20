using LeviathanText.Contracts;

namespace LeviathanText.Worker.Tests;

/// <summary>In-memory IJobStore for worker-host tests.</summary>
public sealed class MemoryJobStore : IJobStore
{
    public readonly Dictionary<EntityId, JobRecord> Jobs = [];

    public IReadOnlyList<JobRecord> PendingJobs() => [.. Jobs.Values.Where(j => j.Status == JobStatus.Pending)];

    public void Save(JobRecord job) => Jobs[job.Id] = job;

    public JobRecord? Find(EntityId id) => Jobs.GetValueOrDefault(id);
}

public sealed class RecordingHandler(string type, Func<JobRecord, JobRecord>? behavior = null) : IJobHandler
{
    public List<EntityId> Executed { get; } = [];

    public string Type => type;

    public JobRecord Execute(JobRecord job, CancellationToken cancellationToken)
    {
        Executed.Add(job.Id);
        return behavior?.Invoke(job) ?? job with
        {
            Outputs = [new StageArtifact(job.Id, new Checksum { Algorithm = "test", Digest = "done" })],
        };
    }
}

public class WorkerHostTests
{
    private static JobRecord NewJob(string type = "noop", string? key = null) => new()
    {
        Id = new EntityId(EntityKind.Job, $"j{Guid.NewGuid().ToString("N")[..10]}"),
        Type = type,
        Inputs = [],
        Owner = new AgentRef { Id = "test", IsModel = false },
        IdempotencyKey = key,
        Status = JobStatus.Pending,
    };

    private static (MemoryJobStore, WorkerHost) NewHost()
    {
        var store = new MemoryJobStore();
        return (store, new WorkerHost(store, [new RecordingHandler("noop")]));
    }

    [Fact]
    public void Claim_sets_lease_and_running_status()
    {
        var (store, host) = NewHost();
        var job = NewJob();
        store.Save(job);

        var claimed = host.TryClaim(job, new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero));
        Assert.NotNull(claimed);
        Assert.Equal(JobStatus.Running, claimed!.Status);
        Assert.Equal(host.WorkerId, claimed.Lease!.Worker);
        Assert.True(claimed.Lease.ExpiresAt > new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void A_held_lease_blocks_a_second_worker()
    {
        var (store, first) = NewHost();
        var second = new WorkerHost(store, [new RecordingHandler("noop")]);
        var job = NewJob();
        store.Save(job);

        var now = new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);
        Assert.NotNull(first.TryClaim(job, now));
        Assert.Null(second.TryClaim(job, now)); // lease held
    }

    [Fact]
    public void Expired_lease_can_be_reclaimed()
    {
        var store = new MemoryJobStore();
        var first = new WorkerHost(store, [new RecordingHandler("noop")], leaseDuration: TimeSpan.FromSeconds(1));
        var second = new WorkerHost(store, [new RecordingHandler("noop")], leaseDuration: TimeSpan.FromSeconds(1));
        var job = NewJob();
        store.Save(job);

        var now = new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);
        var claimed = first.TryClaim(job, now);
        Assert.NotNull(claimed);

        var later = now.AddSeconds(5); // lease (1s) expired; reclaimable
        var reclaimed = second.TryClaim(job, later);
        Assert.NotNull(reclaimed);
        Assert.Equal(second.WorkerId, reclaimed!.Lease!.Worker);
    }

    [Fact]
    public void Claimable_jobs_order_by_priority_then_creation_time()
    {
        var (store, host) = NewHost();
        var t0 = DateTimeOffset.UtcNow;
        var lowOld = NewJob() with { Id = new EntityId(EntityKind.Job, "j-low-old"), Priority = 1, CreatedAt = t0 };
        var highNew = NewJob() with { Id = new EntityId(EntityKind.Job, "j-high-new"), Priority = 9, CreatedAt = t0.AddMinutes(1) };
        var highOld = NewJob() with { Id = new EntityId(EntityKind.Job, "j-high-old"), Priority = 9, CreatedAt = t0 };
        store.Save(lowOld); store.Save(highNew); store.Save(highOld);

        Assert.Equal([highOld.Id, highNew.Id, lowOld.Id], host.ClaimableJobs().Select(j => j.Id));
    }

    [Fact]
    public void RunNext_executes_and_finalizes()
    {
        var (store, host) = NewHost();
        var job = NewJob();
        store.Save(job);

        var finished = host.RunNext();
        Assert.NotNull(finished);
        Assert.Equal(JobStatus.Succeeded, finished!.Status);
        Assert.Single(finished.Outputs);
        Assert.Null(finished.Lease);
    }

    [Fact]
    public void Finished_jobs_are_never_re_executed()
    {
        var (store, host) = NewHost();
        var job = NewJob();
        store.Save(job);
        host.RunNext();
        var before = (store.Jobs[job.Id].Outputs, store.Jobs[job.Id].Status);

        host.RunNext(); // nothing claimable
        var after = (store.Jobs[job.Id].Outputs, store.Jobs[job.Id].Status);
        Assert.Equal(before, after); // restart safety: no duplication, no overwrite
    }

    [Fact]
    public void Handler_failure_releases_the_lease_and_requeues_within_budget()
    {
        var store = new MemoryJobStore();
        var failing = new RecordingHandler("fail", _ => throw new InvalidOperationException("boom"));
        var host = new WorkerHost(store, [failing]) { MaxRetries = 1 };

        var job = NewJob("fail");
        store.Save(job);

        var firstRun = host.RunNext();
        Assert.Equal(JobStatus.Pending, firstRun!.Status); // retry queued
        Assert.Equal(1, firstRun.RetriesUsed);
        Assert.NotNull(firstRun.Failure);
        Assert.Null(firstRun.Lease);

        var secondRun = host.RunNext();
        Assert.Equal(JobStatus.Failed, secondRun!.Status); // budget exhausted
        Assert.Equal(2, secondRun.RetriesUsed);
    }

    [Fact]
    public void Heartbeat_renews_only_for_the_lease_holder()
    {
        var (store, host) = NewHost();
        var job = NewJob();
        store.Save(job);
        var claimed = host.TryClaim(job);

        var renewed = host.Heartbeat(job.Id);
        Assert.NotNull(renewed);
        Assert.True(renewed!.Lease!.ExpiresAt > claimed!.Lease!.ExpiresAt);

        // Another worker's heartbeat is refused.
        var other = new WorkerHost(store, []);
        Assert.Null(other.Heartbeat(job.Id));
    }

    [Fact]
    public void Unknown_job_type_fails_loudly_with_closed_vocabulary()
    {
        var store = new MemoryJobStore();
        var host = new WorkerHost(store, [new RecordingHandler("noop")]);
        var job = NewJob("unregistered-type");
        store.Save(job);

        var ex = Assert.Throws<LeviathanException>(() => host.RunNext());
        Assert.Equal(ErrorCode.StageContractViolation, ex.Error.Code);
        Assert.Contains("No handler", ex.Error.Message);
    }

    private sealed class CancelItselfHandler(IJobStore store) : IJobHandler
    {
        public string Type => "cancel-self";

        public JobRecord Execute(JobRecord job, CancellationToken cancellationToken)
        {
            var current = store.Find(job.Id)!;
            store.Save(current with { Status = JobStatus.Cancelled });
            throw new OperationCanceledException();
        }
    }

    [Fact]
    public void Api_cancellation_survives_the_host_catch_and_is_not_resurrected()
    {
        var jobs = new MemoryJobStore();
        var job = new JobRecord
        {
            Id = new EntityId(EntityKind.Job, "job-cancel"),
            Type = "cancel-self",
            Inputs = [],
            Owner = new AgentRef { Id = "test", IsModel = false },
            Status = JobStatus.Pending,
        };
        jobs.Save(job);
        var host = new WorkerHost(jobs, [new CancelItselfHandler(jobs)]);

        var finished = host.RunNext();

        Assert.NotNull(finished);
        Assert.Equal(JobStatus.Cancelled, finished!.Status);
        Assert.Null(finished.Lease);
        Assert.Empty(host.ClaimableJobs()); // never re-executed
    }
}
