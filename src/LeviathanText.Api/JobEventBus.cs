using System.Runtime.ExceptionServices;
using LeviathanText.Contracts;

namespace LeviathanText.Api;

/// <summary>Bounded advisory replay. SQLite job state remains authoritative.</summary>
public sealed class JobEventBus
{
    public sealed record JobEvent(long Sequence, EntityId JobId, string Kind, string Payload);
    public sealed record ReplayWindow(IReadOnlyList<JobEvent> Events, bool RequiresResync, long Cursor, string? Reason);
    public sealed record RetentionState(int Streams, int Events, long Characters, int Subscribers,
        long PendingDeliveries, long PendingCharacters);
    public sealed record Limits
    {
        public int MaxStreams { get; init; } = 128;
        public int MaxRetainedEvents { get; init; } = 8192;
        public int MaxEventsPerStream { get; init; } = 8192;
        public long MaxRetainedCharacters { get; init; } = 2 * 1024 * 1024;
        public int MaxPayloadCharacters { get; init; } = 16384;
        public int MaxKindCharacters { get; init; } = 128;
        public int MaxSubscribers { get; init; } = 128;
        public int MaxSubscribersPerStream { get; init; } = 32;
        public int MaxPendingDeliveriesPerStream { get; init; } = 256;
        public int MaxPendingCharactersPerStream { get; init; } = 65536;
    }

    public const string ResyncKind = "job.resync-required";
    private readonly Limits _limits;
    private readonly object _registryGate = new(), _retentionGate = new();
    private readonly Dictionary<EntityId, JobStream> _streams = [];
    private readonly LinkedList<JobStream> _lru = new();
    private long _sequence, _retainedCharacters, _pendingDeliveries, _pendingCharacters;
    private int _retainedEvents, _subscribers;
    private bool _hasEvictedStreams;
    private bool _hasUncachedPublications;

    public JobEventBus() : this(new Limits()) { }
    public JobEventBus(Limits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        if (limits.MaxStreams < 1 || limits.MaxRetainedEvents < 1 || limits.MaxEventsPerStream < 1
            || limits.MaxRetainedCharacters < 1 || limits.MaxPayloadCharacters < 128 || limits.MaxKindCharacters < 32
            || limits.MaxSubscribers < 1 || limits.MaxSubscribersPerStream < 1
            || limits.MaxPendingDeliveriesPerStream < 1 || limits.MaxPendingCharactersPerStream < 128)
            throw new ArgumentOutOfRangeException(nameof(limits), "Event limits must be positive and admit a small resynchronization marker.");
        _limits = limits;
    }

    public JobEvent Publish(EntityId jobId, string kind, string payload)
    {
        var stream = Pin(jobId, create: true);
        if (stream is null) // All cache slots are pinned. Never fail the authoritative job operation.
        {
            lock (_registryGate)
            {
                // A subscriber may have admitted this job after the failed lookup.
                // Either pin it now or record the loss and sequence atomically with
                // registry creation, so a later stream inherits the missing history.
                if (_streams.TryGetValue(jobId, out var admitted))
                {
                    admitted.Pins++; TouchLocked(admitted); stream = admitted;
                }
                else
                {
                    _hasUncachedPublications = true;
                    return MakeEvent(Interlocked.Increment(ref _sequence), jobId, kind, payload);
                }
            }
        }
        try
        {
            lock (stream.Gate)
            {
                var evt = MakeEvent(Interlocked.Increment(ref _sequence), jobId, kind, payload);
                stream.LastSequence = evt.Sequence;
                if (evt.Kind == ResyncKind) stream.LostThrough = Math.Max(stream.LostThrough, evt.Sequence);
                Retain(stream, evt);
                if (!stream.IsDraining && stream.Subscribers.Count == 0) return evt;
                var characters = Characters(evt);
                Delivery? immediate = null;
                // Once a nested batch loses an event, coalesce the remainder behind
                // one explicit gap so its sequence cannot overtake admitted deliveries.
                if (!stream.IsDraining && characters <= _limits.MaxPendingCharactersPerStream)
                    immediate = new(evt, stream.Subscribers.ToArray());
                else if (stream.PendingGap != 0 || stream.Pending.Count >= _limits.MaxPendingDeliveriesPerStream
                    || stream.PendingCharacters + characters > _limits.MaxPendingCharactersPerStream)
                {
                    stream.PendingGap = evt.Sequence;
                    stream.LostThrough = Math.Max(stream.LostThrough, evt.Sequence);
                }
                else
                {
                    stream.Pending.Enqueue(new Delivery(evt, stream.Subscribers.ToArray()));
                    stream.PendingCharacters += characters;
                    Interlocked.Increment(ref _pendingDeliveries);
                    Interlocked.Add(ref _pendingCharacters, characters);
                }
                if (stream.IsDraining) return evt;
                stream.IsDraining = true;
                List<Exception>? failures = null;
                int failureCount = 0;
                try
                {
                    while (immediate is not null || stream.Pending.Count != 0 || stream.PendingGap != 0)
                    {
                        Delivery delivery;
                        if (immediate is not null)
                        {
                            // The active delivery is not pending retention. Nested
                            // publications still enter the bounded FIFO below it.
                            delivery = immediate; immediate = null;
                        }
                        else if (stream.Pending.TryDequeue(out var queued))
                        {
                            delivery = queued;
                            stream.PendingCharacters -= Characters(delivery.Event);
                            Interlocked.Decrement(ref _pendingDeliveries);
                            Interlocked.Add(ref _pendingCharacters, -Characters(delivery.Event));
                        }
                        else
                        {
                            var sequence = stream.PendingGap;
                            stream.PendingGap = 0;
                            delivery = new(new(sequence, jobId, ResyncKind, "{\"reason\":\"reentrant_delivery_overflow\"}"), stream.Subscribers.ToArray());
                        }
                        foreach (var subscriber in delivery.Subscribers)
                            try { subscriber.Push(delivery.Event); }
                            catch (Exception error)
                            {
                                failureCount++;
                                if (failureCount <= 32) (failures ??= []).Add(error);
                            }
                    }
                }
                finally { stream.IsDraining = false; }
                if (failureCount > 32) failures!.Add(new InvalidOperationException($"{failureCount - 32} additional event callback failures were omitted."));
                if (failures is { Count: 1 }) ExceptionDispatchInfo.Capture(failures[0]).Throw();
                if (failures is { Count: > 1 }) throw new AggregateException(failures);
                return evt;
            }
        }
        finally { Unpin(stream); }
    }

    /// <summary>Only the retained window; use ReadReplay to detect incomplete history.</summary>
    public IReadOnlyList<JobEvent> Replay(EntityId jobId, long fromSequence) => ReadReplay(jobId, fromSequence).Events;

    public ReplayWindow ReadReplay(EntityId jobId, long fromSequence)
    {
        if (fromSequence < 0) throw new ArgumentOutOfRangeException(nameof(fromSequence));
        var stream = Pin(jobId, create: false);
        if (stream is null)
        {
            bool evicted, uncached;
            lock (_registryGate) { evicted = _hasEvictedStreams; uncached = _hasUncachedPublications; }
            return new([], fromSequence > 0 || evicted || uncached, Interlocked.Read(ref _sequence),
                fromSequence > 0 ? "unknown_cursor" : uncached ? "capacity_history_missing" : evicted ? "history_evicted" : null);
        }
        try
        {
            lock (stream.Gate)
            {
                bool future = fromSequence > stream.LastSequence;
                bool unknown = stream.UnknownHistoryThrough is { } cutoff && fromSequence < cutoff;
                bool gap = future || fromSequence < stream.LostThrough || unknown;
                return new([.. stream.Log.Where(e => e.Sequence > fromSequence)], gap,
                    Math.Max(stream.LastSequence, gap ? Interlocked.Read(ref _sequence) : 0),
                    future ? "unknown_cursor" : unknown ? "history_evicted" : gap ? "history_trimmed" : null);
            }
        }
        finally { Unpin(stream); }
    }

    public IDisposable Subscribe(EntityId jobId, Action<JobEvent> onEvent) =>
        TrySubscribe(jobId, onEvent, out var subscription) ? subscription! : throw new JobEventCapacityException();

    public bool TrySubscribe(EntityId jobId, Action<JobEvent> onEvent, out IDisposable? subscription)
    {
        ArgumentNullException.ThrowIfNull(onEvent);
        subscription = null;
        lock (_registryGate) if (_subscribers >= _limits.MaxSubscribers) return false;
        var stream = Pin(jobId, create: true);
        if (stream is null) return false;
        bool reserved;
        lock (_registryGate) { reserved = _subscribers < _limits.MaxSubscribers; if (reserved) _subscribers++; }
        if (!reserved) { Unpin(stream); return false; }
        lock (stream.Gate)
        {
            if (stream.Subscribers.Count >= _limits.MaxSubscribersPerStream)
            {
                ReleaseSubscriber(stream);
                return false;
            }
            var bridge = new Subscriber(onEvent);
            stream.Subscribers.Add(bridge);
            subscription = new Unsubscriber(this, stream, bridge);
            return true; // Keep this pin until disposal.
        }
    }

    public RetentionState InspectRetention()
    {
        int streams, subscribers, events; long characters;
        lock (_registryGate) { streams = _streams.Count; subscribers = _subscribers; }
        lock (_retentionGate) { events = _retainedEvents; characters = _retainedCharacters; }
        return new(streams, events, characters, subscribers,
            Interlocked.Read(ref _pendingDeliveries), Interlocked.Read(ref _pendingCharacters));
    }

    private JobEvent MakeEvent(long sequence, EntityId id, string kind, string payload)
    {
        if (kind is null || payload is null || kind.Length > _limits.MaxKindCharacters
            || kind.AsSpan().IndexOfAny('\r', '\n', '\0') >= 0 || payload.Length > _limits.MaxPayloadCharacters)
            return new(sequence, id, ResyncKind, "{\"reason\":\"event_payload_rejected\"}");
        return new(sequence, id, kind, payload);
    }

    private JobStream? Pin(EntityId id, bool create)
    {
        lock (_registryGate)
        {
            if (_streams.TryGetValue(id, out var existing)) { existing.Pins++; TouchLocked(existing); return existing; }
            if (!create) return null;
            JobStream? stream = null;
            if (_streams.Count >= _limits.MaxStreams)
            {
                stream = EvictIdleLocked();
                if (stream is null) return null;
            }
            stream ??= new JobStream();
            // Only a zero-pin slot is recycled; no active replay, subscriber or
            // delivery can still use its identity/log. Previously returned events
            // are immutable records with their own copied job ID.
            stream.Id = id;
            stream.UnknownHistoryThrough = _hasEvictedStreams || _hasUncachedPublications ? Interlocked.Read(ref _sequence) : null;
            stream.Pins = 1;
            stream.LastSequence = stream.LostThrough = stream.PendingGap = 0;
            _streams.Add(id, stream);
            _lru.AddLast(stream.Node);
            return stream;
        }
    }

    private void TouchLocked(JobStream stream)
    {
        if (ReferenceEquals(_lru.Last, stream.Node)) return;
        _lru.Remove(stream.Node); _lru.AddLast(stream.Node);
    }
    private void Unpin(JobStream stream) { lock (_registryGate) { stream.Pins--; TouchLocked(stream); } }
    private bool EvictIdle()
    {
        lock (_registryGate) return EvictIdleLocked() is not null;
    }
    private JobStream? EvictIdleLocked()
    {
        var node = _lru.First;
        while (node is not null && node.Value.Pins != 0) node = node.Next;
        if (node is null) return null;
        var oldest = node.Value;
        _streams.Remove(oldest.Id);
        _lru.Remove(node);
        _hasEvictedStreams = true;
        while (oldest.Log.TryDequeue(out var evt)) ReleaseRetention(evt);
        return oldest;
    }

    private void Retain(JobStream stream, JobEvent evt)
    {
        if (Characters(evt) > _limits.MaxRetainedCharacters)
        {
            stream.LostThrough = Math.Max(stream.LostThrough, evt.Sequence);
            return;
        }
        while (true)
        {
            lock (_retentionGate)
            {
                // Eviction/reservation of this pinned stream's log is one atomic
                // accounting operation, rather than three locks for a full cache.
                while (stream.Log.Count != 0 && (stream.Log.Count >= _limits.MaxEventsPerStream
                    || _retainedEvents >= _limits.MaxRetainedEvents
                    || _retainedCharacters + Characters(evt) > _limits.MaxRetainedCharacters))
                {
                    var old = stream.Log.Dequeue(); _retainedEvents--; _retainedCharacters -= Characters(old);
                    stream.LostThrough = Math.Max(stream.LostThrough, old.Sequence);
                }
                if (_retainedEvents < _limits.MaxRetainedEvents
                    && _retainedCharacters + Characters(evt) <= _limits.MaxRetainedCharacters)
                {
                    stream.Log.Enqueue(evt);
                    _retainedEvents++; _retainedCharacters += Characters(evt);
                    return;
                }
            }
            // Never acquire the registry while holding the retention lock.
            if (!EvictIdle()) { stream.LostThrough = Math.Max(stream.LostThrough, evt.Sequence); return; }
        }
    }
    private void ReleaseRetention(JobEvent evt)
    {
        lock (_retentionGate) { _retainedEvents--; _retainedCharacters -= Characters(evt); }
    }
    private static long Characters(JobEvent evt) => (long)evt.Kind.Length + evt.Payload.Length;
    private void ReleaseSubscriber(JobStream stream)
    {
        lock (_registryGate) { _subscribers--; stream.Pins--; TouchLocked(stream); }
    }
    private sealed class JobStream
    {
        public JobStream() => Node = new(this);
        public LinkedListNode<JobStream> Node { get; }
        public EntityId Id { get; set; }
        public long? UnknownHistoryThrough { get; set; }
        public object Gate { get; } = new();
        public Queue<JobEvent> Log { get; } = [];
        public List<Subscriber> Subscribers { get; } = [];
        public Queue<Delivery> Pending { get; } = [];
        public int Pins { get; set; }
        public long LastSequence { get; set; }
        public long LostThrough { get; set; }
        public long PendingGap { get; set; }
        public long PendingCharacters { get; set; }
        public bool IsDraining { get; set; }
    }
    private sealed record Delivery(JobEvent Event, Subscriber[] Subscribers);
    private sealed class Subscriber(Action<JobEvent> onEvent) { public void Push(JobEvent evt) => onEvent(evt); }
    private sealed class Unsubscriber(JobEventBus bus, JobStream stream, Subscriber subscriber) : IDisposable
    {
        private JobEventBus? _owner = bus;
        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            if (owner is null) return;
            lock (stream.Gate) { stream.Subscribers.Remove(subscriber); owner.ReleaseSubscriber(stream); }
        }
    }
}

public sealed class JobEventCapacityException : InvalidOperationException
{
    public JobEventCapacityException() : base("The bounded job-event subscription capacity is full; refresh authoritative job state.") { }
}
