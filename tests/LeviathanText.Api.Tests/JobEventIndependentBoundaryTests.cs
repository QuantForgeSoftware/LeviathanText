using LeviathanText.Contracts;

namespace LeviathanText.Api.Tests;

// Pure generated event records: no API host, provider, model, corpus, fitting or worker dispatch.
public sealed class JobEventIndependentBoundaryTests
{
    private static readonly EntityId A = new(EntityKind.Job, "independent-a");
    private static readonly EntityId B = new(EntityKind.Job, "independent-b");
    private static JobRecord Pending => new()
    {
        Id = A, Type = "generated-noop", Inputs = [], Status = JobStatus.Pending,
        Owner = new AgentRef { Id = "generated", IsModel = false },
    };

    [Fact]
    public void Publication_rejected_by_pinned_cache_admission_reports_incomplete_zero_cursor_history()
    {
        var bus = new JobEventBus(new() { MaxStreams = 1 });
        using var pinned = bus.Subscribe(A, _ => { });
        var dropped = bus.Publish(B, "probe", "generated uncached event");
        Assert.Equal(B, dropped.JobId);
        var replay = bus.ReadReplay(B, 0);
        Assert.Empty(replay.Events);
        Assert.True(replay.RequiresResync, "No retained event is not complete history when cache admission rejected a publication.");
        Assert.Equal(1, bus.InspectRetention().Streams);
    }

    [Fact]
    public async Task Precancelled_replay_yields_nothing_and_releases_subscription_before_returning()
    {
        var bus = new JobEventBus();
        bus.Publish(A, "probe", "must not be delivered after cancellation");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await using var events = JobEventStream.Read(bus, A, () => true, () => Pending, 0, cancellation.Token).GetAsyncEnumerator();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => events.MoveNextAsync().AsTask());
        Assert.Equal(0, bus.InspectRetention().Subscribers);
    }

    [Fact]
    public async Task Cancellation_between_replay_yields_stops_before_the_next_event_and_releases_subscription()
    {
        var bus = new JobEventBus();
        bus.Publish(A, "probe", "first");
        bus.Publish(A, "probe", "second must not be delivered after cancellation");
        using var cancellation = new CancellationTokenSource();
        await using var events = JobEventStream.Read(bus, A, () => true, () => Pending, 0, cancellation.Token).GetAsyncEnumerator();
        Assert.True(await events.MoveNextAsync());
        Assert.Equal("first", events.Current.Data);
        Assert.Equal(1, bus.InspectRetention().Subscribers);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => events.MoveNextAsync().AsTask());
        Assert.Equal(0, bus.InspectRetention().Subscribers);
    }

    [Fact]
    public async Task Precancelled_empty_history_does_not_observe_or_yield_an_authoritative_snapshot()
    {
        var bus = new JobEventBus(); int observed = 0;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await using var events = JobEventStream.Read(bus, A, () => true, () => { observed++; return Pending; },
            0, cancellation.Token).GetAsyncEnumerator();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => events.MoveNextAsync().AsTask());
        Assert.Equal(0, observed);
        Assert.Equal(0, bus.InspectRetention().Subscribers);
    }
}
