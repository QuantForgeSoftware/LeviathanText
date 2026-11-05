using LeviathanText.Api;
using LeviathanText.Contracts;
using LeviathanText.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LeviathanText.Api.Tests;

public class JobQuotaTests
{
    [Fact]
    public void Quota_counts_active_jobs_beyond_the_recent_jobs_page()
    {
        using var store = new LocalStore(":memory:");
        var owner = new AgentRef { Id = "busy-owner", IsModel = false };
        for (var i = 0; i < 101; i++)
            store.SaveJob(new JobRecord
            {
                Id = new EntityId(EntityKind.Job, $"existing-{i}"),
                Type = "noop",
                Inputs = [],
                Owner = owner,
                Status = JobStatus.Pending,
            });
        var enqueuer = new JobEnqueuer(store, new JobEventBus(), new ConfigurationBuilder().Build());

        var error = Assert.Throws<LeviathanException>(() => enqueuer.Enqueue("noop", owner, maxActivePerOwner: 101));

        Assert.Equal(ErrorCode.StageContractViolation, error.Error.Code);
        Assert.Equal(101, store.LoadJobsByStatus(JobStatus.Pending).Count);
        Assert.NotNull(enqueuer.Enqueue("noop", new AgentRef { Id = "other-owner", IsModel = false }, maxActivePerOwner: 101));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Concurrent_distinct_jobs_cannot_exceed_the_owner_quota(bool independentConnections)
    {
        var directory = Directory.CreateTempSubdirectory("lt-quota-race-");
        var database = Path.Combine(directory.FullName, "store.db");
        try
        {
            using var anchor = new LocalStore(database);
            var events = new JobEventBus();
            var configuration = new ConfigurationBuilder().Build();
            var owner = new AgentRef { Id = "one-owner", IsModel = false };
            using var barrier = new Barrier(16);
            var requests = Enumerable.Range(0, 16).Select(i => Task.Factory.StartNew(() =>
            {
                using var store = independentConnections ? new LocalStore(database) : anchor.OpenPeer();
                var enqueuer = new JobEnqueuer(store, events, configuration);
                Assert.True(barrier.SignalAndWait(TimeSpan.FromSeconds(10)));
                try { return enqueuer.Enqueue("noop", owner, idempotencyKey: $"request-{i}", maxActivePerOwner: 1); }
                catch (LeviathanException error) when (error.Error.Code == ErrorCode.StageContractViolation)
                {
                    Assert.Contains("quota", error.Error.Message);
                    return null;
                }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();

            var replies = await Task.WhenAll(requests);

            var accepted = Assert.Single(replies, reply => reply is not null)!;
            Assert.Equal(accepted.Id, Assert.Single(anchor.LoadJobs()).Id);
            Assert.Single(events.Replay(accepted.Id, 0));
        }
        finally
        {
            foreach (var mode in new[] { SqliteOpenMode.ReadWriteCreate, SqliteOpenMode.ReadWrite })
            {
                using var poolKey = new SqliteConnection(new SqliteConnectionStringBuilder
                {
                    DataSource = database,
                    Mode = mode,
                }.ToString());
                SqliteConnection.ClearPool(poolKey);
            }
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Per_owner_active_quota_rejects_excess_jobs_and_idempotency_still_wins()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lt-quota-{Guid.NewGuid():N}.db");
        try
        {
            using var store = new LocalStore(path);
            var config = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["LeviathanText:Jobs:MaxActivePerOwner"] = "1" }).Build();
            var enqueuer = new JobEnqueuer(store, new JobEventBus(), config);
            var owner = new AgentRef { Id = "quota-owner", IsModel = false };

            var first = enqueuer.Enqueue("noop", owner, idempotencyKey: "same");
            var duplicate = enqueuer.Enqueue("noop", owner, idempotencyKey: "same");
            Assert.Equal(first.Id, duplicate.Id);

            var ex = Assert.Throws<LeviathanException>(() => enqueuer.Enqueue("noop", owner));
            Assert.Equal(ErrorCode.StageContractViolation, ex.Error.Code);
            Assert.Contains("quota", ex.Error.Message);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
