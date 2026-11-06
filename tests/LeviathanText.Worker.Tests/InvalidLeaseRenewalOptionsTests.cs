using LeviathanText.Contracts;
using LeviathanText.Worker;
using Xunit;

namespace LeviathanText.Worker.Tests;

public sealed class InvalidLeaseRenewalOptionsTests
{
    [Theory]
    [InlineData("zero")] [InlineData("negative")] [InlineData("equal-lease")]
    [InlineData("greater-than-lease")] [InlineData("null-factory")] [InlineData("null-ticks")]
    public void Invalid_configuration_refuses_before_any_store_read_claim_or_session(string invalid)
    {
        var store = new UntouchedStore(); int opened = 0;
        var options = new LeaseRenewalOptions
        {
            OpenSession = () => { opened++; throw new InvalidOperationException("Must not open"); },
            Interval = TimeSpan.FromSeconds(1),
        };
        options = invalid switch
        {
            "zero" => options with { Interval = TimeSpan.Zero },
            "negative" => options with { Interval = TimeSpan.FromMilliseconds(-1) },
            "equal-lease" => options with { Interval = TimeSpan.FromSeconds(3) },
            "greater-than-lease" => options with { Interval = TimeSpan.FromSeconds(4) },
            "null-factory" => options with { OpenSession = null! },
            "null-ticks" => options with { Ticks = null! },
            _ => throw new InvalidOperationException("Unknown generated option"),
        };
        var host = new WorkerHost(store, [], TimeSpan.FromSeconds(3), leaseRenewal: options);
        Assert.Throws<ArgumentException>(() => host.RunNext());
        Assert.Equal(0, store.PendingReads); Assert.Equal(0, store.Finds); Assert.Equal(0, store.Writes);
        Assert.Equal(0, opened); Assert.Equal(0, host.CompletedAttempts); Assert.Null(host.LastRenewal);
        Assert.Equal("unchanged-generated-claim-state", store.State);
    }
    private sealed class UntouchedStore : IJobStore
    {
        public int PendingReads, Finds, Writes;
        public string State { get; } = "unchanged-generated-claim-state";
        public IReadOnlyList<JobRecord> PendingJobs() { PendingReads++; throw new InvalidOperationException("Must not read"); }
        public JobRecord? Find(EntityId id) { Finds++; throw new InvalidOperationException("Must not read"); }
        public void Save(JobRecord job) { Writes++; throw new InvalidOperationException("Must not save"); }
        public JobRecord? Update(EntityId id, Func<JobRecord, JobRecord?> transition) { Writes++; throw new InvalidOperationException("Must not claim"); }
    }
}
