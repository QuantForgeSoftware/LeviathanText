using LeviathanText.Contracts;

namespace LeviathanText.Api.Tests;

public sealed class JobEventStreamBoundaryTests
{
    private static readonly EntityId Id = new(EntityKind.Job, "stream-boundary");
    private static JobRecord Pending => new()
    {
        Id = Id, Type = "noop", Inputs = [], Status = JobStatus.Pending,
        Owner = new AgentRef { Id = "fixture", IsModel = false },
    };

    [Fact]
    public async Task Revocation_during_store_observation_suppresses_the_snapshot()
    {
        var bus = new JobEventBus();
        bool authorized = true;
        int observed = 0;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var events = JobEventStream.Read(bus, Id, () => authorized, () =>
        {
            observed++;
            authorized = false;
            return Pending;
        }, 0, deadline.Token).GetAsyncEnumerator();

        Assert.False(await events.MoveNextAsync());
        Assert.Equal(1, observed);
    }

    [Fact]
    public async Task Event_published_during_replay_is_delivered_once_after_the_replayed_event()
    {
        var bus = new JobEventBus();
        var first = bus.Publish(Id, "probe", "first");
        JobEventBus.JobEvent? second = null;
        bool published = false;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var events = JobEventStream.Read(bus, Id, () =>
        {
            if (!published)
            {
                published = true;
                // Replay has taken its snapshot, but this subscription is already live.
                second = bus.Publish(Id, "probe", "second");
            }
            return true;
        }, () => Pending, 0, deadline.Token).GetAsyncEnumerator();
        var delivered = new List<(string? Id, string Data)>();
        while (await events.MoveNextAsync())
        {
            var item = events.Current;
            delivered.Add((item.EventId, item.Data));
            if (item.Data == "second") break;
        }

        Assert.Equal(["first", "second"], delivered.Where(item => item.Id is not null).Select(item => item.Data));
        Assert.Equal(first.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture), delivered[0].Id);
        Assert.Equal(second!.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture), delivered[^1].Id);
        Assert.Single(delivered, item => item.Data == "first");
        Assert.Single(delivered, item => item.Data == "second");
    }

    [Fact]
    public async Task Missing_observed_job_closes_the_stream_without_inventing_a_payload()
    {
        var bus = new JobEventBus();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var events = JobEventStream.Read(bus, Id, () => true, () => null, 0, deadline.Token).GetAsyncEnumerator();

        Assert.False(await events.MoveNextAsync());
    }
}
