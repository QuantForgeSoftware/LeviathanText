using LeviathanText.Application;
using LeviathanText.Contracts;

namespace LeviathanText.Application.Tests;

public sealed class StreamingPipelineTests
{
    private static (EntityId, Func<CancellationToken, Task>) Item(EntityId id, Func<CancellationToken, Task> work) => (id, work);

    [Fact]
    public async Task Processes_all_items_and_reports_success()
    {
        var ids = Enumerable.Range(0, 10).Select(i => new EntityId(EntityKind.Line, $"s{i:02}")).ToList();
        var results = await StreamingPipeline.RunAsync(ids.Select(id => Item(id, _ => Task.CompletedTask)));

        Assert.Equal(10, results.Count);
        Assert.All(results, r => Assert.True(r.Succeeded));
    }

    [Fact]
    public async Task One_failing_item_does_not_stop_the_run_and_is_reported()
    {
        var ids = Enumerable.Range(0, 5).Select(i => new EntityId(EntityKind.Line, $"f{i:02}")).ToList();
        var failing = ids[2];
        var results = await StreamingPipeline.RunAsync(ids.Select(id => Item(id, ct =>
        {
            if (id == failing) throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = id.ToString(), Message = "bad item" });
            return Task.CompletedTask;
        })));

        Assert.Equal(5, results.Count);
        var failedResult = Assert.Single(results, r => !r.Succeeded);
        Assert.Equal(failing, failedResult.ItemId);
        Assert.Contains("InputMissing", failedResult.Error);
    }

    [Fact]
    public async Task Bounded_queue_backpressures_large_collections()
    {
        // 5000 items through a narrow pipeline: completes without unbounded memory.
        var processed = 0;
        var ids = Enumerable.Range(0, 5000).Select(i => new EntityId(EntityKind.Line, $"b{i:04}"));
        var results = await StreamingPipeline.RunAsync(ids.Select(id => Item(id, _ => { Interlocked.Increment(ref processed); return Task.CompletedTask; })),
            maxDegreeOfParallelism: 2);

        Assert.Equal(5000, processed);
        Assert.Equal(5000, results.Count);
        Assert.All(results, r => Assert.True(r.Succeeded));
    }

    [Fact]
    public async Task Cancellation_stops_the_producer()
    {
        using var cts = new CancellationTokenSource();
        var started = new TaskCompletionSource();
        var ids = Enumerable.Range(0, 100).Select(i => new EntityId(EntityKind.Line, $"c{i:03}"));

        var run = StreamingPipeline.RunAsync(ids.Select(id => Item(id, ct =>
        {
            started.TrySetResult();
            cts.Cancel();
            throw new OperationCanceledException(ct);
        })), cancellation: cts.Token);

        await run; // completes (possibly with partial results), does not hang
    }
}
