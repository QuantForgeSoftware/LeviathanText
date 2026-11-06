using LeviathanText.Api;
using LeviathanText.Contracts;

namespace LeviathanText.Api.Tests;

public sealed class JobEventStreamFairnessTests
{
    [Fact]
    public async Task Busy_live_queue_cannot_starve_a_persisted_terminal_observation()
    {
        var bus = new JobEventBus();
        var id = new EntityId(EntityKind.Job, "fairness");
        var current = new JobRecord
        {
            Id = id, Type = "noop", Inputs = [], Status = JobStatus.Pending,
            Owner = new AgentRef { Id = "fixture", IsModel = false },
        };
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var events = JobEventStream.Read(bus, id, () => true, () => current, 0, deadline.Token)
            .GetAsyncEnumerator(deadline.Token);
        Assert.True(await events.MoveNextAsync());
        Assert.Equal("job.progress", events.Current.EventType);
        for (int i = 0; i < 200; i++) bus.Publish(id, "probe", i.ToString());
        Assert.True(await events.MoveNextAsync()); // Enter a live delivery batch.
        Assert.Equal("probe", events.Current.EventType);
        current = current with { Status = JobStatus.Succeeded };
        int probes = 1;
        while (await events.MoveNextAsync())
        {
            if (events.Current.EventType == "job.completed") break;
            Assert.Equal("probe", events.Current.EventType);
            Assert.True(++probes <= 64, "The live queue starved persisted status observation.");
        }
        Assert.Equal("job.completed", events.Current.EventType);
        Assert.Equal(64, probes);
        Assert.Null(events.Current.EventId);
        Assert.False(await events.MoveNextAsync());
    }
}
