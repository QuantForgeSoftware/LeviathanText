using LeviathanText.Contracts;

namespace LeviathanText.Api.Tests;

public sealed class BoundedJobQueueTests
{
    private static readonly EntityId Id = new(EntityKind.Job, "queue");
    private static JobEventBus.JobEvent Event(int sequence, string payload = "data") => new(sequence, Id, "probe", payload);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Overflow_is_bounded_coalesced_and_cleared_only_through_an_observed_cursor(bool characters)
    {
        using var queue = new BoundedJobEventQueue(2, characters ? 12 : 100);
        queue.Push(Event(1)); queue.Push(Event(2)); queue.Push(Event(3)); queue.Push(Event(4));
        Assert.Equal(characters ? 1 : 2, queue.Retained.Events); Assert.InRange(queue.Retained.Characters, 0, characters ? 12 : 100);
        Assert.Equal(4, queue.GapThrough);
        queue.DiscardThrough(3); Assert.Equal(4, queue.GapThrough); Assert.Equal(0, queue.Retained.Events);
        queue.DiscardThrough(4); Assert.Equal(0, queue.GapThrough);
        queue.Push(Event(5)); Assert.True(queue.TryRead(out var next)); Assert.Equal(5, next!.Sequence);
        Assert.Equal(0, queue.Retained.Characters);
    }

    [Fact]
    public async Task Wake_is_coalesced_and_disposal_releases_waiters_and_payloads()
    {
        using var queue = new BoundedJobEventQueue(); using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var waiting = queue.WaitAsync(deadline.Token); Assert.False(waiting.IsCompleted);
        queue.Push(Event(1)); Assert.True(await waiting);
        queue.AcknowledgeWake(); Assert.True(queue.TryRead(out _));
        var next = queue.WaitAsync(deadline.Token); Assert.False(next.IsCompleted);
        queue.Dispose(); Assert.False(await next); queue.Push(Event(2));
        Assert.Equal((0, 0L), queue.Retained); Assert.Equal(0, queue.GapThrough);
    }

    [Fact]
    public async Task Waiting_is_cancellable_without_an_unbounded_signal_queue()
    {
        using var queue = new BoundedJobEventQueue(); using var cancelled = new CancellationTokenSource();
        var waiting = queue.WaitAsync(cancelled.Token); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
    }

    [Theory]
    [InlineData(0, 10)] [InlineData(10, 0)] [InlineData(-1, 10)]
    public void Invalid_queue_budgets_are_refused(int events, int characters) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new BoundedJobEventQueue(events, characters));
}
