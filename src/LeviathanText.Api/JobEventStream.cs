using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using LeviathanText.Contracts;
using LeviathanText.Contracts.Serialization;

namespace LeviathanText.Api;

/// <summary>Bounded advisory replay/live delivery plus authoritative, freshly authorized SQLite observations.</summary>
public static class JobEventStream
{
    public static async IAsyncEnumerable<SseItem<string>> Read(JobEventBus bus, EntityId jobId,
        Func<bool> authorized, Func<JobRecord?> observe, long fromSequence,
        [EnumeratorCancellation] CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        using var queue = new BoundedJobEventQueue();
        bool subscribed = bus.TrySubscribe(jobId, queue.Push, out var admission);
        using var subscription = admission;
        long cursor = fromSequence;
        string? lastPayload = null;
        var replay = bus.ReadReplay(jobId, cursor);
        if (!subscribed || replay.RequiresResync)
        {
            if (!authorized()) yield break;
            cancellation.ThrowIfCancellationRequested();
            cursor = replay.Cursor;
            queue.DiscardThrough(cursor);
            yield return Resync(jobId, subscribed ? replay.Reason ?? "history_unavailable" : "subscription_capacity");
        }
        else foreach (var evt in replay.Events)
        {
            if (!authorized()) yield break;
            cancellation.ThrowIfCancellationRequested();
            cursor = evt.Sequence;
            if (evt.Kind.StartsWith("job.", StringComparison.Ordinal)) lastPayload = evt.Payload;
            yield return new(evt.Payload, evt.Kind) { EventId = evt.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture) };
        }

        cancellation.ThrowIfCancellationRequested();
        var available = queue.WaitAsync(cancellation);
        while (authorized())
        {
            cancellation.ThrowIfCancellationRequested();
            if (queue.GapThrough is > 0)
            {
                cursor = Math.Max(cursor, bus.ReadReplay(jobId, 0).Cursor);
                queue.DiscardThrough(cursor);
                lastPayload = null;
                if (!authorized()) yield break;
                cancellation.ThrowIfCancellationRequested();
                yield return Resync(jobId, "slow_consumer");
            }
            var job = observe();
            if (job is null || !authorized()) yield break;
            cancellation.ThrowIfCancellationRequested();
            var payload = ContractJson.ToJson(job);
            if (payload != lastPayload)
            {
                lastPayload = payload;
                var kind = job.Status switch
                {
                    JobStatus.Succeeded => "job.completed", JobStatus.Failed => "job.failed",
                    JobStatus.Cancelled => "job.cancelled", _ => "job.progress",
                };
                yield return new(payload, kind);
            }
            if (!subscribed || job.Status is JobStatus.Succeeded or JobStatus.Failed or JobStatus.Cancelled) yield break;

            bool ready;
            try { ready = await available.WaitAsync(TimeSpan.FromSeconds(1), cancellation).ConfigureAwait(false); }
            catch (TimeoutException) { continue; }
            if (!ready) yield break;
            queue.AcknowledgeWake();
            int examined = 0;
            while (examined++ < 64 && queue.GapThrough == 0 && queue.TryRead(out var evt))
            {
                if (!authorized()) yield break;
                cancellation.ThrowIfCancellationRequested();
                if (evt.Sequence <= cursor) continue;
                if (evt.Kind == JobEventBus.ResyncKind)
                {
                    cursor = Math.Max(evt.Sequence, bus.ReadReplay(jobId, 0).Cursor);
                    queue.DiscardThrough(cursor);
                    lastPayload = null;
                    yield return Resync(jobId, "live_delivery_gap");
                    break;
                }
                cursor = evt.Sequence;
                if (evt.Kind.StartsWith("job.", StringComparison.Ordinal)) lastPayload = evt.Payload;
                yield return new(evt.Payload, evt.Kind) { EventId = evt.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture) };
            }
            available = queue.WaitAsync(cancellation);
        }
    }

    private static SseItem<string> Resync(EntityId jobId, string reason) =>
        new(JsonSerializer.Serialize(new { reason, jobId = jobId.ToString(), snapshotUrl = $"/api/v1/jobs/{jobId}" }), JobEventBus.ResyncKind)
        { EventId = string.Empty }; // Clear EventSource's process-local Last-Event-ID before an authoritative snapshot.
}
