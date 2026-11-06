using LeviathanText.Contracts;

namespace LeviathanText.Api.Tests;

public sealed class JobStreamResynchronizationTests
{
    private static readonly EntityId Id = new(EntityKind.Job, "resync");
    private static JobRecord Pending => new() { Id = Id, Type = "noop", Inputs = [], Status = JobStatus.Pending,
        Owner = new AgentRef { Id = "generated", IsModel = false } };

    [Theory]
    [InlineData("trimmed")] [InlineData("restarted")] [InlineData("future")]
    public async Task Incomplete_replay_clears_cursor_then_observes_authoritative_snapshot(string mode)
    {
        var bus = new JobEventBus(new() { MaxEventsPerStream = 2 });
        if (mode != "restarted") for (int i = 0; i < 5; i++) bus.Publish(Id, "probe", "old" + i);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var stream = JobEventStream.Read(bus, Id, () => true, () => Pending,
            mode == "trimmed" ? 0 : 100, deadline.Token).GetAsyncEnumerator();
        Assert.True(await stream.MoveNextAsync()); Assert.Equal(JobEventBus.ResyncKind, stream.Current.EventType);
        Assert.Equal(string.Empty, stream.Current.EventId); Assert.Contains($"/api/v1/jobs/{Id}", stream.Current.Data);
        Assert.DoesNotContain("old", stream.Current.Data);
        Assert.True(await stream.MoveNextAsync()); Assert.Equal("job.progress", stream.Current.EventType);
        Assert.Null(stream.Current.EventId); Assert.Contains("pending", stream.Current.Data);
        await stream.DisposeAsync(); Assert.Equal(0, bus.InspectRetention().Subscribers);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Slow_consumer_overflow_resynchronizes_before_a_persisted_terminal_snapshot(bool characters)
    {
        var bus = new JobEventBus(); var current = Pending;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var stream = JobEventStream.Read(bus, Id, () => true, () => current, 0, deadline.Token).GetAsyncEnumerator();
        Assert.True(await stream.MoveNextAsync()); Assert.Equal("job.progress", stream.Current.EventType);
        for (int i = 0; i < (characters ? 5 : 1000); i++) bus.Publish(Id, "probe", characters ? new string('x', 15000) : i.ToString());
        current = current with { Status = JobStatus.Succeeded };
        Assert.True(await stream.MoveNextAsync()); Assert.Equal(JobEventBus.ResyncKind, stream.Current.EventType);
        Assert.Equal(string.Empty, stream.Current.EventId); Assert.Contains("slow_consumer", stream.Current.Data);
        Assert.True(await stream.MoveNextAsync()); Assert.Equal("job.completed", stream.Current.EventType);
        Assert.Null(stream.Current.EventId); Assert.False(await stream.MoveNextAsync());
        Assert.Equal(0, bus.InspectRetention().Subscribers);
    }

    [Fact]
    public async Task Full_subscription_capacity_yields_an_explicit_boundary_and_snapshot_then_closes()
    {
        var bus = new JobEventBus(new() { MaxSubscribers = 1 });
        using var occupied = bus.Subscribe(new EntityId(EntityKind.Job, "occupied"), _ => { });
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var stream = JobEventStream.Read(bus, Id, () => true, () => Pending, 0, deadline.Token).GetAsyncEnumerator();
        Assert.True(await stream.MoveNextAsync()); Assert.Equal(JobEventBus.ResyncKind, stream.Current.EventType);
        Assert.Contains("subscription_capacity", stream.Current.Data);
        Assert.True(await stream.MoveNextAsync()); Assert.Equal("job.progress", stream.Current.EventType);
        Assert.False(await stream.MoveNextAsync()); Assert.Equal(1, bus.InspectRetention().Subscribers);
    }

    [Fact]
    public async Task Revocation_suppresses_overflow_markers_and_snapshots_and_releases_subscription()
    {
        var bus = new JobEventBus(); bool authorized = true;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var stream = JobEventStream.Read(bus, Id, () => authorized, () => Pending, 0, deadline.Token).GetAsyncEnumerator();
        Assert.True(await stream.MoveNextAsync());
        for (int i = 0; i < 300; i++) bus.Publish(Id, "probe", "must-not-leak");
        authorized = false; Assert.False(await stream.MoveNextAsync()); Assert.Equal(0, bus.InspectRetention().Subscribers);
    }

    [Fact]
    public async Task Cancellation_while_waiting_releases_the_subscription_and_queue()
    {
        var bus = new JobEventBus(); using var cancelled = new CancellationTokenSource();
        await using var stream = JobEventStream.Read(bus, Id, () => true, () => Pending, 0, cancelled.Token).GetAsyncEnumerator();
        Assert.True(await stream.MoveNextAsync()); var waiting = stream.MoveNextAsync().AsTask(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        Assert.Equal(0, bus.InspectRetention().Subscribers);
    }
}
