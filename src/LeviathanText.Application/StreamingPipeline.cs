using System.Collections.Concurrent;
using System.Threading.Channels;
using LeviathanText.Contracts;

namespace LeviathanText.Application;

/// <summary>
/// Collection-scale streaming (todo §4: "page/tile streaming and bounded queues;
/// no stage may assume whole-collection memory residency"). Feeds items through a
/// per-item processor with a bounded queue: producers stall when the consumer falls
/// behind, so peak memory stays bounded regardless of collection size. Supports
/// cooperative cancellation and per-item failure isolation (one bad item does not
/// stop the run; failures are reported, never swallowed).
/// </summary>
public static class StreamingPipeline
{
    public sealed record ItemResult(EntityId ItemId, bool Succeeded, string? Error);

    /// <summary>
    /// Process items with bounded concurrency. Returns one result per input item, in
    /// completion order.
    /// </summary>
    public static async Task<IReadOnlyList<ItemResult>> RunAsync(
        IEnumerable<(EntityId Id, Func<CancellationToken, Task> Work)> items,
        int maxDegreeOfParallelism = 2,
        CancellationToken cancellation = default)
    {
        if (maxDegreeOfParallelism < 1) throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism));

        var results = new ConcurrentBag<ItemResult>();
        var channel = Channel.CreateBounded<(EntityId Id, Func<CancellationToken, Task> Work)>(
            new BoundedChannelOptions(capacity: maxDegreeOfParallelism * 2)
            {
                FullMode = BoundedChannelFullMode.Wait, // producers stall — bounded memory
                SingleReader = maxDegreeOfParallelism == 1,
            });

        // Producer: feed all items, then complete.
        var producer = Task.Run(async () =>
        {
            try
            {
                foreach (var item in items)
                {
                    cancellation.ThrowIfCancellationRequested();
                    await channel.Writer.WriteAsync(item, cancellation).ConfigureAwait(false);
                }
                channel.Writer.Complete();
            }
            catch (OperationCanceledException)
            {
                channel.Writer.Complete();
            }
            catch (Exception ex)
            {
                channel.Writer.Complete(ex);
            }
        }, CancellationToken.None);

        // Consumers.
        var consumers = Enumerable.Range(0, maxDegreeOfParallelism).Select(_ => Task.Run(async () =>
        {
            await foreach (var (id, work) in channel.Reader.ReadAllAsync(cancellation).ConfigureAwait(false))
            {
                try
                {
                    await work(cancellation).ConfigureAwait(false);
                    results.Add(new ItemResult(id, true, null));
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (LeviathanException ex)
                {
                    results.Add(new ItemResult(id, false, ex.Error.ToString()));
                }
                catch (Exception ex)
                {
                    results.Add(new ItemResult(id, false, $"{ex.GetType().Name}: {ex.Message}"));
                }
            }
        }, CancellationToken.None)).ToList();

        await producer.ConfigureAwait(false);
        try
        {
            await Task.WhenAll(consumers).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cooperative shutdown: results so far are returned, cancelled items unprocessed.
        }

        return [.. results];
    }
}
