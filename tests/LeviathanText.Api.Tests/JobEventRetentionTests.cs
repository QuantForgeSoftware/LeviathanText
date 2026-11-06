using LeviathanText.Contracts;

namespace LeviathanText.Api.Tests;

public sealed class JobEventRetentionTests
{
    [Fact]
    public void Default_replay_history_does_not_retain_more_than_the_registered_event_limit()
    {
        var bus = new JobEventBus(); var id = new EntityId(EntityKind.Job, "bounded-history");
        for (int i = 0; i < 10_000; i++) bus.Publish(id, "probe", i.ToString());
        var replay = bus.Replay(id, 0);
        Assert.True(replay.Count <= 8192, $"Unbounded replay retained {replay.Count} events.");
        Assert.Equal("9999", replay[^1].Payload);
    }

    [Fact]
    public void Default_idle_stream_registry_evicts_old_unsubscribed_job_history()
    {
        var bus = new JobEventBus();
        for (int i = 0; i < 256; i++) bus.Publish(new EntityId(EntityKind.Job, "bounded-" + i), "probe", "generated");
        Assert.Empty(bus.Replay(new EntityId(EntityKind.Job, "bounded-0"), 0));
        Assert.Single(bus.Replay(new EntityId(EntityKind.Job, "bounded-255"), 0));
    }

    private static readonly EntityId A = new(EntityKind.Job, "a"), B = new(EntityKind.Job, "b"), C = new(EntityKind.Job, "c");

    [Fact]
    public void Trimmed_window_reports_loss_but_an_acknowledged_boundary_can_resume()
    {
        var bus = new JobEventBus(new() { MaxEventsPerStream = 3 });
        var events = Enumerable.Range(0, 5).Select(i => bus.Publish(A, "probe", i.ToString())).ToArray();
        var old = bus.ReadReplay(A, 0);
        Assert.True(old.RequiresResync); Assert.Equal("history_trimmed", old.Reason);
        Assert.Equal(events.Skip(2), old.Events); Assert.Equal(events[^1].Sequence, old.Cursor);
        Assert.False(bus.ReadReplay(A, events[1].Sequence).RequiresResync);
        Assert.Empty(bus.ReadReplay(A, events[^1].Sequence).Events);
    }

    [Fact]
    public void Global_count_and_character_budgets_are_shared_by_pinned_streams()
    {
        var bus = new JobEventBus(new() { MaxRetainedEvents = 5, MaxRetainedCharacters = 100 });
        using var a = bus.Subscribe(A, _ => { }); using var b = bus.Subscribe(B, _ => { });
        for (int i = 0; i < 20; i++) { bus.Publish(A, "probe", "aaaaaaaaaa"); bus.Publish(B, "probe", "bbbbbbbbbb"); }
        var state = bus.InspectRetention();
        Assert.InRange(state.Events, 1, 5); Assert.InRange(state.Characters, 1, 100);
        Assert.Equal(state.Events, bus.Replay(A, 0).Count + bus.Replay(B, 0).Count);
        Assert.True(bus.ReadReplay(A, 0).RequiresResync); Assert.True(bus.ReadReplay(B, 0).RequiresResync);
    }

    [Fact]
    public void An_event_larger_than_total_budget_does_not_evict_unrelated_history()
    {
        var bus = new JobEventBus(new() { MaxRetainedCharacters = 128 });
        var first = bus.Publish(A, "probe", "small"); bus.Publish(B, "probe", new string('x', 200));
        Assert.Equal(first, Assert.Single(bus.Replay(A, 0)));
        Assert.Empty(bus.Replay(B, 0)); Assert.True(bus.ReadReplay(B, 0).RequiresResync);
        Assert.Equal(1, bus.InspectRetention().Events);
    }

    [Fact]
    public void Idle_eviction_preserves_a_subscribed_stream_and_new_cursors_recover()
    {
        var bus = new JobEventBus(new() { MaxStreams = 2 });
        using var pinned = bus.Subscribe(A, _ => { }); var a = bus.Publish(A, "probe", "a");
        bus.Publish(B, "probe", "b"); var c = bus.Publish(C, "probe", "c");
        Assert.Equal(a, Assert.Single(bus.Replay(A, 0))); Assert.Empty(bus.Replay(B, 0));
        Assert.True(bus.ReadReplay(B, 0).RequiresResync); Assert.True(bus.ReadReplay(C, 0).RequiresResync);
        Assert.False(bus.ReadReplay(C, c.Sequence).RequiresResync);
        Assert.Equal(2, bus.InspectRetention().Streams);
    }

    [Fact]
    public void Full_pinned_registry_does_not_fail_publication_and_disposal_releases_admission()
    {
        var bus = new JobEventBus(new() { MaxStreams = 1 });
        var pinned = bus.Subscribe(A, _ => { }); var dropped = bus.Publish(B, "probe", "authoritative job may proceed");
        Assert.False(bus.TrySubscribe(B, _ => { }, out var unavailable)); Assert.Null(unavailable);
        Assert.Equal(1, bus.InspectRetention().Streams); Assert.Empty(bus.Replay(B, 0));
        pinned.Dispose(); pinned.Dispose();
        using var next = bus.Subscribe(B, _ => { }); Assert.True(bus.ReadReplay(B, dropped.Sequence).RequiresResync);
        Assert.Equal(1, bus.InspectRetention().Subscribers);
    }

    [Fact]
    public void Global_and_per_stream_subscriber_limits_do_not_leak_pins_or_counts()
    {
        var bus = new JobEventBus(new() { MaxSubscribers = 2, MaxSubscribersPerStream = 1 });
        var first = bus.Subscribe(A, _ => { });
        Assert.False(bus.TrySubscribe(A, _ => { }, out _));
        var second = bus.Subscribe(B, _ => { });
        Assert.False(bus.TrySubscribe(C, _ => { }, out _));
        Assert.Throws<JobEventCapacityException>(() => bus.Subscribe(C, _ => { }));
        Assert.Equal(2, bus.InspectRetention().Subscribers); Assert.Equal(2, bus.InspectRetention().Streams);
        first.Dispose(); second.Dispose(); second.Dispose();
        Assert.Equal(0, bus.InspectRetention().Subscribers);
    }

    [Theory]
    [InlineData("payload")] [InlineData("kind")] [InlineData("newline")]
    public void Oversized_or_invalid_events_become_small_explicit_resynchronization_markers(string mode)
    {
        var bus = new JobEventBus(new() { MaxPayloadCharacters = 128, MaxKindCharacters = 32 });
        var observed = new List<JobEventBus.JobEvent>(); using var sub = bus.Subscribe(A, observed.Add);
        var evt = bus.Publish(A, mode == "kind" ? new string('k', 33) : mode == "newline" ? "bad\ninjection" : "probe",
            mode == "payload" ? new string('x', 129) : "small");
        Assert.Equal(JobEventBus.ResyncKind, evt.Kind); Assert.Equal(evt, Assert.Single(observed));
        Assert.True(bus.ReadReplay(A, 0).RequiresResync);
        Assert.DoesNotContain(new string('x', 129), evt.Payload);
        Assert.InRange(evt.Payload.Length, 1, 128); Assert.InRange(evt.Kind.Length, 1, 32);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Nested_burst_overflow_preserves_admitted_FIFO_then_one_gap_and_releases_pending_retention(bool characterBound)
    {
        var bus = new JobEventBus(new() { MaxPendingDeliveriesPerStream = 2, MaxPendingCharactersPerStream = 128 });
        var seen = new List<JobEventBus.JobEvent>();
        using var publisher = bus.Subscribe(A, evt =>
        {
            if (evt.Payload != "outer") return;
            for (int i = 0; i < 5; i++) bus.Publish(A, "probe", characterBound ? new string((char)('a' + i), 80) : "inner" + i);
            var pending = bus.InspectRetention(); Assert.InRange(pending.PendingDeliveries, 0, 2); Assert.InRange(pending.PendingCharacters, 0, 128);
        });
        using var observer = bus.Subscribe(A, seen.Add); bus.Publish(A, "probe", "outer");
        Assert.Equal(characterBound ? 3 : 4, seen.Count); Assert.Equal("outer", seen[0].Payload);
        Assert.Equal(JobEventBus.ResyncKind, seen[^1].Kind); Assert.Equal(6, seen[^1].Sequence);
        Assert.Equal(seen.OrderBy(e => e.Sequence), seen);
        Assert.True(bus.ReadReplay(A, 0).RequiresResync);
        Assert.Equal(0, bus.InspectRetention().PendingDeliveries); Assert.Equal(0, bus.InspectRetention().PendingCharacters);
    }

    [Fact]
    public void Repeated_callback_failures_are_capped_without_stranding_other_deliveries()
    {
        var bus = new JobEventBus(); int delivered = 0;
        using var nested = bus.Subscribe(A, evt => { if (evt.Payload == "outer") for (int i = 0; i < 40; i++) bus.Publish(A, "probe", "nested"); });
        using var failing = bus.Subscribe(A, _ => throw new InvalidOperationException("generated failure"));
        using var observer = bus.Subscribe(A, _ => delivered++);
        var error = Assert.Throws<AggregateException>(() => bus.Publish(A, "probe", "outer"));
        Assert.Equal(33, error.InnerExceptions.Count); Assert.Equal(41, delivered);
        Assert.Contains("9 additional", error.InnerExceptions[^1].Message);
        Assert.Equal(0, bus.InspectRetention().PendingDeliveries);
    }

    [Fact]
    public async Task Concurrent_job_churn_keeps_shared_bounds_and_releases_all_subscribers()
    {
        var bus = new JobEventBus(new() { MaxStreams = 8, MaxRetainedEvents = 64, MaxRetainedCharacters = 4096 });
        await Task.WhenAll(Enumerable.Range(0, 8).Select(worker => Task.Run(() =>
        {
            for (int i = 0; i < 200; i++)
            {
                var id = new EntityId(EntityKind.Job, $"w{worker}-{i % 4}");
                bus.Publish(id, "probe", i.ToString());
                if (bus.TrySubscribe(id, _ => { }, out var subscription)) subscription!.Dispose();
                _ = bus.ReadReplay(id, 0);
            }
        }))).WaitAsync(TimeSpan.FromSeconds(10));
        var state = bus.InspectRetention(); Assert.InRange(state.Streams, 1, 8);
        Assert.InRange(state.Events, 0, 64); Assert.InRange(state.Characters, 0, 4096);
        Assert.Equal(0, state.Subscribers); Assert.Equal(0, state.PendingDeliveries); Assert.Equal(0, state.PendingCharacters);
    }

    [Fact]
    public void Unknown_future_and_negative_cursors_are_never_silently_treated_as_complete()
    {
        var bus = new JobEventBus(); Assert.True(bus.ReadReplay(A, 42).RequiresResync);
        bus.Publish(A, "probe", "one"); Assert.True(bus.ReadReplay(A, 42).RequiresResync);
        Assert.Throws<ArgumentOutOfRangeException>(() => bus.ReadReplay(A, -1));
    }

    [Fact]
    public void Recycled_idle_slots_do_not_mutate_old_events_or_let_old_disposal_remove_new_subscribers()
    {
        var bus = new JobEventBus(new() { MaxStreams = 1 });
        var original = bus.Publish(A, "probe", "old-a"); var oldWindow = bus.Replay(A, 0);
        var oldSubscription = bus.Subscribe(A, _ => { }); oldSubscription.Dispose();
        bus.Publish(B, "probe", "first-b");
        var seen = new List<JobEventBus.JobEvent>(); using var current = bus.Subscribe(B, seen.Add);
        oldSubscription.Dispose(); bus.Publish(B, "probe", "next-b");
        Assert.Equal(original, Assert.Single(oldWindow)); Assert.Equal(A, oldWindow[0].JobId);
        Assert.Equal(B, Assert.Single(seen).JobId); Assert.Equal("next-b", seen[0].Payload);
        Assert.Equal(1, bus.InspectRetention().Subscribers); Assert.Equal(1, bus.InspectRetention().Streams);
        Assert.Empty(bus.Replay(A, 0));
    }

    [Fact]
    public void Lru_eviction_uses_recent_access_and_keeps_registry_retention_consistent()
    {
        var bus = new JobEventBus(new() { MaxStreams = 2 });
        bus.Publish(A, "probe", "a"); bus.Publish(B, "probe", "b");
        Assert.Single(bus.Replay(A, 0)); // A is most recently used now.
        bus.Publish(C, "probe", "c");
        Assert.Single(bus.Replay(A, 0)); Assert.Empty(bus.Replay(B, 0)); Assert.Single(bus.Replay(C, 0));
        var state = bus.InspectRetention(); Assert.Equal(2, state.Streams); Assert.Equal(2, state.Events);
        Assert.Equal(12, state.Characters); Assert.Equal(0, state.PendingDeliveries);
    }
}
