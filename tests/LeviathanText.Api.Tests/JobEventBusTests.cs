using LeviathanText.Contracts;
using Xunit;

namespace LeviathanText.Api.Tests;

/// <summary>
/// Event-bus semantics behind the SSE endpoint (interchange-and-api.md §2.2):
/// replay from a cursor never loses events published between subscribe and replay,
/// and events are advisory duplicates of the authoritative job resource.
/// </summary>
public sealed class JobEventBusTests
{
    private static readonly EntityId Job = new(EntityKind.Job, "j1");

    [Fact]
    public void Publish_then_replay_returns_all_events_in_order()
    {
        var bus = new JobEventBus();
        bus.Publish(Job, "job.created", "{}");
        bus.Publish(Job, "job.progress", "{\"progress\":0.5}");

        var replayed = bus.Replay(Job, 0);
        Assert.Equal(["job.created", "job.progress"], replayed.Select(e => e.Kind).ToArray());
        Assert.True(replayed[1].Sequence > replayed[0].Sequence);
    }

    [Fact]
    public void Replay_from_cursor_skips_older_events()
    {
        var bus = new JobEventBus();
        bus.Publish(Job, "job.created", "{}");
        var last = bus.Publish(Job, "job.progress", "{}");

        var replayed = bus.Replay(Job, last.Sequence);
        Assert.Empty(replayed);
    }

    [Fact]
    public void Subscriber_receives_published_events()
    {
        var bus = new JobEventBus();
        using var subscription = bus.Subscribe(Job, evt => { _ = evt; });
        var received = new List<JobEventBus.JobEvent>();
        using var sub = bus.Subscribe(Job, received.Add);

        bus.Publish(Job, "job.completed", "{}");
        Assert.Single(received);
        Assert.Equal("job.completed", received[0].Kind);
    }

    [Fact]
    public void Other_jobs_events_do_not_cross()
    {
        var bus = new JobEventBus();
        var otherJob = new EntityId(EntityKind.Job, "j2");
        var received = new List<JobEventBus.JobEvent>();
        using var sub = bus.Subscribe(Job, received.Add);

        bus.Publish(otherJob, "job.created", "{}");
        Assert.Empty(received);
        Assert.Empty(bus.Replay(Job, 0));
    }
}
