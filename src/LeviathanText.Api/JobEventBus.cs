using System.Collections.Concurrent;
using LeviathanText.Contracts;

namespace LeviathanText.Api;

/// <summary>
/// Job event bus for server-sent events (interchange-and-api.md §2.2): events are
/// advisory; the job resource remains authoritative. Replay from a cursor is supported
/// by the per-job event log.
/// </summary>
public sealed class JobEventBus
{
    public sealed record JobEvent(long Sequence, EntityId JobId, string Kind, string Payload);

    private readonly ConcurrentDictionary<EntityId, List<JobEvent>> _log = [];
    private readonly ConcurrentDictionary<EntityId, List<ChannelReaderBridge>> _subscribers = [];
    private long _sequence;

    public JobEvent Publish(EntityId jobId, string kind, string payload)
    {
        var evt = new JobEvent(Interlocked.Increment(ref _sequence), jobId, kind, payload);
        var log = _log.GetOrAdd(jobId, _ => []);
        lock (log) log.Add(evt);

        if (_subscribers.TryGetValue(jobId, out var subscribers))
            foreach (var subscriber in subscribers.ToArray())
                subscriber.Push(evt);
        return evt;
    }

    public IReadOnlyList<JobEvent> Replay(EntityId jobId, long fromSequence) =>
        _log.TryGetValue(jobId, out var log)
            ? [.. log.Where(e => e.Sequence > fromSequence)]
            : [];

    public IDisposable Subscribe(EntityId jobId, Action<JobEvent> onEvent)
    {
        var bridge = new ChannelReaderBridge(onEvent);
        _subscribers.AddOrUpdate(jobId,
            _ => [bridge],
            (_, list) => { lock (list) list.Add(bridge); return list; });
        return new Unsubscriber(_subscribers, jobId, bridge);
    }

    private sealed class ChannelReaderBridge(Action<JobEvent> onEvent)
    {
        public void Push(JobEvent evt) => onEvent(evt);
    }

    private sealed class Unsubscriber(
        ConcurrentDictionary<EntityId, List<ChannelReaderBridge>> subscribers,
        EntityId jobId,
        ChannelReaderBridge bridge) : IDisposable
    {
        public void Dispose()
        {
            if (subscribers.TryGetValue(jobId, out var list))
                lock (list) list.Remove(bridge);
        }
    }
}
