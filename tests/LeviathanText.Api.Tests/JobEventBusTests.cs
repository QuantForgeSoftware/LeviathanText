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
    [Fact]
    public void Reentrant_publication_waits_until_every_subscriber_receives_the_outer_event()
    {
        var bus = new JobEventBus();
        var first = new List<JobEventBus.JobEvent>();
        var second = new List<JobEventBus.JobEvent>();
        using var publisher = bus.Subscribe(Job, evt =>
        {
            first.Add(evt);
            if (evt.Payload == "outer") bus.Publish(Job, "probe", "nested");
        });
        using var observer = bus.Subscribe(Job, second.Add);

        bus.Publish(Job, "probe", "outer");

        Assert.Equal(["outer", "nested"], first.Select(evt => evt.Payload));
        Assert.Equal(["outer", "nested"], second.Select(evt => evt.Payload));
        Assert.Equal(bus.Replay(Job, 0).Select(evt => evt.Sequence), second.Select(evt => evt.Sequence));
    }

    [Fact]
    public void Nested_publication_returns_recorded_event_before_delivery_and_outer_call_drains_it()
    {
        var bus = new JobEventBus();
        var observed = new List<string>();
        using var subscription = bus.Subscribe(Job, evt =>
        {
            observed.Add(evt.Payload);
            if (evt.Payload != "outer") return;
            var nested = bus.Publish(Job, "probe", "nested");
            Assert.Equal(["outer"], observed);
            Assert.Equal(nested, bus.Replay(Job, evt.Sequence).Single());
        });
        bus.Publish(Job, "probe", "outer");
        Assert.Equal(["outer", "nested"], observed);
    }

    [Fact]
    public void Reentrant_subscription_changes_apply_to_publication_time_snapshots()
    {
        var bus = new JobEventBus();
        var removed = new List<string>();
        var added = new List<string>();
        IDisposable? old = null, replacement = null;
        using var changes = bus.Subscribe(Job, evt =>
        {
            if (evt.Payload != "outer") return;
            old!.Dispose();
            replacement = bus.Subscribe(Job, next => added.Add(next.Payload));
            bus.Publish(Job, "probe", "nested");
            replacement.Dispose(); // Already admitted nested delivery keeps its snapshot.
        });
        using (old = bus.Subscribe(Job, evt => removed.Add(evt.Payload)))
        {
            bus.Publish(Job, "probe", "outer");
            bus.Publish(Job, "probe", "after");
        }
        Assert.Equal(["outer"], removed);
        Assert.Equal(["nested"], added);
    }

    [Fact]
    public void Deep_nested_publication_uses_a_bounded_callback_stack_and_matches_replay()
    {
        var bus = new JobEventBus();
        const int count = 5000;
        int active = 0, maxActive = 0, published = 0;
        var observed = new List<long>();
        using var publisher = bus.Subscribe(Job, _ =>
        {
            active++;
            maxActive = Math.Max(maxActive, active);
            if (++published < count) bus.Publish(Job, "probe", "nested");
            active--;
        });
        using var observer = bus.Subscribe(Job, evt => observed.Add(evt.Sequence));
        bus.Publish(Job, "probe", "outer");
        Assert.Equal(count, observed.Count);
        Assert.Equal(1, maxActive);
        Assert.Equal(bus.Replay(Job, 0).Select(evt => evt.Sequence), observed);
    }

    [Fact]
    public void Callback_failure_does_not_strand_nested_events_or_other_subscribers_and_is_rethrown()
    {
        var bus = new JobEventBus();
        var expected = new InvalidOperationException("generated callback failure");
        var observed = new List<string>();
        using var failing = bus.Subscribe(Job, evt =>
        {
            if (evt.Payload != "outer") return;
            bus.Publish(Job, "probe", "nested");
            throw expected;
        });
        using var observer = bus.Subscribe(Job, evt => observed.Add(evt.Payload));
        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => bus.Publish(Job, "probe", "outer")));
        Assert.Contains(nameof(Callback_failure_does_not_strand_nested_events_or_other_subscribers_and_is_rethrown), expected.StackTrace);
        bus.Publish(Job, "probe", "after");
        Assert.Equal(["outer", "nested", "after"], observed);
        Assert.Equal(bus.Replay(Job, 0).Select(evt => evt.Payload), observed);
    }

    [Fact]
    public void Multiple_callback_failures_are_reported_after_all_admitted_deliveries()
    {
        var bus = new JobEventBus();
        var first = new InvalidOperationException("first");
        var second = new ArgumentException("second");
        var observed = new List<string>();
        using var publisher = bus.Subscribe(Job, evt =>
        {
            if (evt.Payload != "outer") return;
            bus.Publish(Job, "probe", "nested");
            throw first;
        });
        using var failing = bus.Subscribe(Job, evt => { if (evt.Payload == "nested") throw second; });
        using var observer = bus.Subscribe(Job, evt => observed.Add(evt.Payload));
        var error = Assert.Throws<AggregateException>(() => bus.Publish(Job, "probe", "outer"));
        Assert.Equal([first, second], error.InnerExceptions);
        bus.Publish(Job, "probe", "after");
        Assert.Equal(["outer", "nested", "after"], observed);
    }

    [Fact]
    public async Task Concurrent_publications_cannot_overtake_a_delivery_in_progress()
    {
        var bus = new JobEventBus();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var secondStarted = new ManualResetEventSlim();
        var deliveredSecond = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var blocking = bus.Subscribe(Job, evt =>
        {
            if (evt.Payload != "first") return;
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
        });
        var received = new System.Collections.Concurrent.ConcurrentQueue<JobEventBus.JobEvent>();
        using var receiving = bus.Subscribe(Job, evt =>
        {
            received.Enqueue(evt);
            if (evt.Payload == "second") deliveredSecond.TrySetResult();
        });
        var first = Task.Factory.StartNew(() => bus.Publish(Job, "probe", "first"),
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        Task<JobEventBus.JobEvent>? second = null;
        bool overtook = false;
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            second = Task.Factory.StartNew(() =>
            {
                secondStarted.Set();
                return bus.Publish(Job, "probe", "second");
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            Assert.True(secondStarted.Wait(TimeSpan.FromSeconds(10)));
            overtook = await Task.WhenAny(deliveredSecond.Task, Task.Delay(200)) == deliveredSecond.Task;
        }
        finally
        {
            release.Set();
            await first.WaitAsync(TimeSpan.FromSeconds(10));
            if (second is not null) await second.WaitAsync(TimeSpan.FromSeconds(10));
        }

        Assert.False(overtook);
        Assert.Equal(["first", "second"], received.Select(evt => evt.Payload));
        Assert.Equal(received.Select(evt => evt.Sequence), bus.Replay(Job, 0).Select(evt => evt.Sequence));
    }

    [Fact]
    public async Task Concurrent_publish_replay_and_subscription_churn_keep_ordered_unique_snapshots()
    {
        var bus = new JobEventBus();
        const int publishers = 4, count = 250;
        var received = new System.Collections.Concurrent.ConcurrentQueue<JobEventBus.JobEvent>();
        using var stable = bus.Subscribe(Job, received.Enqueue);
        using var start = new ManualResetEventSlim();
        var work = Enumerable.Range(0, publishers).Select(publisher => Task.Run(() =>
        {
            start.Wait();
            for (int i = 0; i < count; i++) bus.Publish(Job, "probe", $"{publisher}:{i}");
        })).ToList();
        work.Add(Task.Run(() =>
        {
            start.Wait();
            for (int i = 0; i < count; i++)
            {
                using var transient = bus.Subscribe(Job, _ => { });
                var snapshot = bus.Replay(Job, 0);
                Assert.Equal(snapshot.OrderBy(evt => evt.Sequence).Select(evt => evt.Sequence),
                    snapshot.Select(evt => evt.Sequence));
            }
        }));
        start.Set();
        await Task.WhenAll(work).WaitAsync(TimeSpan.FromSeconds(15));

        var all = bus.Replay(Job, 0);
        Assert.Equal(publishers * count, all.Count);
        Assert.Equal(all.Count, all.Select(evt => evt.Payload).Distinct().Count());
        Assert.Equal(all.OrderBy(evt => evt.Sequence).Select(evt => evt.Sequence), all.Select(evt => evt.Sequence));
        Assert.Equal(all.Select(evt => evt.Sequence), received.Select(evt => evt.Sequence));
        var cursor = all[all.Count / 2].Sequence;
        Assert.Equal(all.Where(evt => evt.Sequence > cursor), bus.Replay(Job, cursor));
    }

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
