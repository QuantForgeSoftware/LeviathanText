using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Infrastructure;

namespace LeviathanText.Worker;

/// <summary>
/// Local worker entry point (todo: independently launchable C# worker). Processes
/// durable jobs from the local store until cancelled:
///   leviathan-worker [--store &lt;path&gt;] [--once]
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var storePath = "leviathan.db";
        bool runOnce = false;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--store" && i + 1 < args.Length) storePath = args[++i];
            else if (args[i] == "--once") runOnce = true;
        }

        using var store = new LocalStore(storePath);
        var imageCache = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(storePath))!, "images");
        var imageResolver = new ImageResolver(store, imageCache);
        var jobStore = new LocalStoreJobStore(store);
        var host = new WorkerHost(jobStore,
            [new NoOpHandler(),
             new LayoutDetectHandler(store, imageResolver),
             new LayoutProjectHandler(store, imageResolver, jobStore),
             new RecognizeSurfaceHandler(store, imageResolver),
             new RecognizeProjectHandler(store, imageResolver, jobStore),
             new ImportDirectoryHandler(store, jobStore)]);
        var workerId = host.WorkerId.ToString();

        Console.WriteLine($"worker {workerId} polling {storePath}");
        do
        {
            var finished = host.RunNext();
            if (finished is { } job)
                Console.WriteLine($"{DateTimeOffset.UtcNow:O} {job.Id} -> {job.Status}{(job.Failure is null ? "" : $" ({job.Failure})")}");
            if (runOnce) return 0;
            await Task.Delay(1000);
        }
        while (!Console.IsInputRedirected); // Ctrl+C terminates

        return 0;
    }

    /// <summary>
    /// Adapter exposing LocalStore's durable job table to the host protocol. Reads are
    /// DB-backed so jobs enqueued by other processes (API) are picked up.
    /// </summary>
    public sealed class LocalStoreJobStore(LocalStore store) : IJobStore
    {
        public IReadOnlyList<JobRecord> PendingJobs() => store.LoadJobsByStatus(JobStatus.Pending);

        public void Save(JobRecord job) => store.SaveJob(job);

        public JobRecord? Find(EntityId id) => store.LoadJob(id);
    }

    /// <summary>Placeholder handler proving the claim/execute/finalize protocol end to end.</summary>
    public sealed class NoOpHandler : IJobHandler
    {
        public string Type => "noop";
        public JobRecord Execute(JobRecord job, CancellationToken cancellationToken) => job with
        {
            Outputs = [new StageArtifact(job.Id, new Checksum { Algorithm = "noop", Digest = "completed" })],
        };
    }
}
