using LeviathanText.Contracts;

namespace LeviathanText.Worker.Tests;

public sealed class WorkerAttemptBudgetTests
{
    [Fact]
    public void Lost_finalization_still_consumes_an_attempt()
    {
        var store = new MemoryJobStore();
        var job = Job();
        store.Save(job);
        var handler = new RecordingHandler("noop", current =>
        {
            store.Save(current with { Lease = current.Lease! with { ClaimId = "replacement" } });
            return current;
        });
        var host = new WorkerHost(store, [handler]);

        Assert.Null(host.RunNext());
        Assert.Equal(1, host.CompletedAttempts);
        Assert.Single(handler.Executed);
        Assert.Equal("replacement", store.Find(job.Id)!.Lease!.ClaimId);
        Assert.Null(host.RunNext()); // Running jobs are not reclaimed by polling.
        Assert.Equal(1, host.CompletedAttempts);
    }

    [Fact]
    public void Empty_queue_and_failed_claim_do_not_consume_attempts()
    {
        var handler = new RecordingHandler("noop");
        var empty = new WorkerHost(new MemoryJobStore(), [handler]);
        Assert.Null(empty.RunNext());
        Assert.Equal(0, empty.CompletedAttempts);
        var lostRace = new WorkerHost(new RejectedClaimStore(Job()), [handler]);
        Assert.Null(lostRace.RunNext());
        Assert.Equal(0, lostRace.CompletedAttempts);
        Assert.Empty(handler.Executed);
    }

    [Fact]
    public void Authorization_rejection_and_unknown_type_each_consume_one_attempt()
    {
        var store = new MemoryJobStore();
        store.Save(Job());
        var handler = new RecordingHandler("noop");
        var host = new WorkerHost(store, [handler], authorize: _ => false);
        Assert.Equal(JobStatus.Cancelled, host.RunNext()!.Status);
        Assert.Equal(1, host.CompletedAttempts);
        Assert.Empty(handler.Executed);
        store.Save(Job() with { Id = new(EntityKind.Job, "unknown"), Type = "unknown" });
        // A separate host proves unsupported types count independently of authorization.
        var unsupported = new WorkerHost(store, []);
        Assert.Equal(JobStatus.Failed, unsupported.RunNext()!.Status);
        Assert.Equal(1, unsupported.CompletedAttempts);
    }

    [Fact]
    public void Retry_and_success_accumulate_in_one_host_lifetime()
    {
        var store = new MemoryJobStore();
        store.Save(Job());
        int attempts = 0;
        var host = new WorkerHost(store, [new RecordingHandler("noop", current =>
        {
            if (++attempts == 1) throw new InvalidOperationException("retry");
            return current;
        })]);
        Assert.Equal(JobStatus.Pending, host.RunNext()!.Status);
        Assert.Equal(1, host.CompletedAttempts);
        Assert.Equal(JobStatus.Succeeded, host.RunNext()!.Status);
        Assert.Equal(2, host.CompletedAttempts);
        Assert.Null(host.RunNext());
        Assert.Equal(2, host.CompletedAttempts);
    }

    private static JobRecord Job() => new()
    {
        Id = new(EntityKind.Job, "attempt"), Type = "noop", Inputs = [],
        Owner = new AgentRef { Id = "test", IsModel = false }, Status = JobStatus.Pending,
    };

    private sealed class RejectedClaimStore(JobRecord snapshot) : IJobStore
    {
        public IReadOnlyList<JobRecord> PendingJobs() => [snapshot];
        public JobRecord? Find(EntityId id) => snapshot;
        public void Save(JobRecord job) => throw new NotSupportedException();
        public JobRecord? Update(EntityId id, Func<JobRecord, JobRecord?> transition) => null;
    }
}
