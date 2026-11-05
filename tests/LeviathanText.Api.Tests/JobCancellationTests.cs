using LeviathanText.Contracts;
using LeviathanText.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace LeviathanText.Api.Tests;

public sealed class JobCancellationTests
{
    [Fact]
    public async Task Concurrent_cancellation_persists_and_announces_once()
    {
        var directory = Directory.CreateTempSubdirectory("lt-job-cancel-");
        try
        {
            string database = Path.Combine(directory.FullName, "store.db");
            using var firstStore = new LocalStore(database);
            using var secondStore = new LocalStore(database);
            var events = new JobEventBus();
            var config = new ConfigurationBuilder().Build();
            var first = new JobEnqueuer(firstStore, events, config);
            var second = new JobEnqueuer(secondStore, events, config);
            var pending = first.Enqueue("noop", new AgentRef { Id = "test", IsModel = false });
            var running = pending with
            {
                Status = JobStatus.Running,
                Lease = new LeaseInfo
                {
                    Worker = new EntityId(EntityKind.Job, "worker"),
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(1),
                },
            };
            firstStore.SaveJob(running);
            using var gate = new Barrier(2);

            Task<JobRecord> CancelFrom(JobEnqueuer enqueuer) => Task.Factory.StartNew(() =>
            {
                Assert.True(gate.SignalAndWait(TimeSpan.FromSeconds(10)));
                return enqueuer.Cancel(pending.Id);
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            var results = await Task.WhenAll(CancelFrom(first), CancelFrom(second));
            Assert.All(results, result =>
            {
                Assert.Equal(JobStatus.Cancelled, result.Status);
                Assert.Null(result.Lease);
            });
            Assert.Equal(JobStatus.Cancelled, firstStore.LoadJob(pending.Id)?.Status);
            Assert.Single(events.Replay(pending.Id, 0), entry => entry.Kind == "job.cancelled");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
