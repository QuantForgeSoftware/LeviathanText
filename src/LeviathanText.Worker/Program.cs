using System.Runtime.InteropServices;
using System.Globalization;
using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Infrastructure;

namespace LeviathanText.Worker;

/// <summary>
/// Local worker entry point (todo: independently launchable C# worker). Processes
/// durable jobs from the local store until cancelled:
///   leviathan-worker [--store &lt;path&gt;] [--once | --max-jobs &lt;positive integer&gt;]
/// A job budget counts completed dispatch attempts (including failed/requeued attempts), not idle
/// polls. Exiting after a budget allows an external supervisor to recycle native resources.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var storePath = "leviathan.db";
        bool runOnce = false;
        bool storeSpecified = false;
        int? maxJobs = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--store" when !storeSpecified && i + 1 < args.Length &&
                                         !string.IsNullOrWhiteSpace(args[i + 1]) &&
                                         !args[i + 1].StartsWith("--", StringComparison.Ordinal):
                    storePath = args[++i];
                    storeSpecified = true;
                    break;
                case "--once" when !runOnce && maxJobs is null:
                    runOnce = true;
                    break;
                case "--max-jobs" when !runOnce && maxJobs is null && i + 1 < args.Length &&
                                           int.TryParse(args[i + 1], NumberStyles.None,
                                               CultureInfo.InvariantCulture, out var count) && count > 0:
                    maxJobs = count;
                    i++;
                    break;
                default:
                    // Parse before opening the store, resolving images, or loading models.
                    Console.Error.WriteLine($"Invalid or conflicting option: {args[i]}");
                    Console.Error.WriteLine("Usage: leviathan-worker [--store <path>] [--once | --max-jobs <positive integer>]");
                    return 2;
            }
        }

        using var store = new LocalStore(storePath);
        var imageCache = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(storePath))!, "images");
        var imageResolver = new ImageResolver(store, imageCache);
        var jobStore = new LocalStoreJobStore(store);
        var layoutModels = LoadLayoutModels(storePath);
        try
        {
            Func<string, LeviathanText.Layout.PatchLayoutModel?> resolveLayoutModel =
                kind => layoutModels.GetValueOrDefault(kind) ?? layoutModels.GetValueOrDefault("*");
            var host = new WorkerHost(jobStore,
                [new NoOpHandler(),
                 new LayoutDetectHandler(store, imageResolver, resolveLayoutModel),
                 new LayoutProjectHandler(store, imageResolver, jobStore, resolveLayoutModel,
                     authorize: job => ProjectJobAuthorization.CanExecute(store, job)),
                 new RecognizeSurfaceHandler(store, imageResolver),
                 new RecognizeProjectHandler(store, imageResolver, jobStore,
                     authorize: job => ProjectJobAuthorization.CanExecute(store, job)),
                 new ImportDirectoryHandler(store, jobStore)],
                authorize: job => ProjectJobAuthorization.CanExecute(store, job));
            var workerId = host.WorkerId.ToString();

            using var stopping = new CancellationTokenSource();
            using var terminate = OperatingSystem.IsWindows() ? null :
                PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
                {
                    context.Cancel = true;
                    stopping.Cancel();
                });
            ConsoleCancelEventHandler interrupt = (_, args) =>
            {
                args.Cancel = true;
                stopping.Cancel();
            };
            Console.CancelKeyPress += interrupt;
            try
            {
                Console.WriteLine($"worker {workerId} polling {storePath}");
                while (!stopping.IsCancellationRequested)
                {
                    // Drain the current handler on shutdown. Interrupting it mid-write
                    // could requeue a job whose partial side effects have committed.
                    var finished = host.RunNext();
                    if (finished is { } job)
                        Console.WriteLine($"{DateTimeOffset.UtcNow:O} {job.Id} -> {job.Status}{(job.Failure is null ? "" : $" ({job.Failure})")}");
                    if (runOnce || maxJobs is { } budget && host.CompletedAttempts >= budget) break;
                    if (finished is null || finished.Status == JobStatus.Pending) await Task.Delay(1000, stopping.Token);
                }
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
            finally
            {
                Console.CancelKeyPress -= interrupt;
            }

            return 0;
        }
        finally
        {
            foreach (var model in layoutModels.Values) model.Dispose();
        }
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

        public JobRecord? Update(EntityId id, Func<JobRecord, JobRecord?> transition) => store.UpdateJob(id, transition);
    }

    /// <summary>Explicit factory for an opted-in host; Main remains disabled by default.</summary>
    public sealed class LocalStoreLeaseRenewalSession : ILeaseRenewalSession
    {
        private readonly LocalStore peer;
        public LocalStoreLeaseRenewalSession(LocalStore anchor) => peer = anchor.OpenLeaseRenewalPeer();
        public LeaseRenewalResult Renew(LeaseAttemptIdentity attempt, DateTimeOffset now, TimeSpan duration) =>
            peer.RenewJobLeaseOnce(attempt.JobId, attempt.WorkerId, attempt.ClaimId, now, duration) switch
            {
                JobLeaseRenewalResult.Renewed => LeaseRenewalResult.Renewed,
                JobLeaseRenewalResult.Contended => LeaseRenewalResult.Contended,
                JobLeaseRenewalResult.OwnershipLost => LeaseRenewalResult.OwnershipLost,
                _ => throw new InvalidOperationException("Unknown durable renewal outcome."),
            };
        public void Dispose() => peer.Dispose();
    }

    /// <summary>Placeholder handler proving the claim/execute/finalize protocol end to end.</summary>
    /// <summary>
    /// Optional learned layout packages for worker jobs. Environment variable
    /// LEVIATHANTEXT_LAYOUT_MODEL_DIR points at a directory whose subdirectories are named by
    /// surface kind (or "*"), each containing a layout package. Broken packages are skipped.
    /// </summary>
    static Dictionary<string, LeviathanText.Layout.PatchLayoutModel> LoadLayoutModels(string storePath)
    {
        var models = new Dictionary<string, LeviathanText.Layout.PatchLayoutModel>(StringComparer.OrdinalIgnoreCase);
        var root = Environment.GetEnvironmentVariable("LEVIATHANTEXT_LAYOUT_MODEL_DIR")
                   ?? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(storePath))!, "layout-models");
        if (!Directory.Exists(root)) return models;

        try
        {
            foreach (var directory in Directory.GetDirectories(root))
            {
                try
                {
                    var kind = Path.GetFileName(directory);
                    var loaded = LeviathanText.Layout.PatchLayoutModel.Load(directory);
                    if (models.TryGetValue(kind, out var previous)) previous.Dispose();
                    models[kind] = loaded;
                    Console.Error.WriteLine($"layout model loaded for kind {kind}: {directory}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"layout model skipped ({Path.GetFileName(directory)}): {ex.Message}");
                }
            }
        }
        catch
        {
            foreach (var model in models.Values) model.Dispose();
            throw;
        }
        return models;
    }

    public sealed class NoOpHandler : IJobHandler
    {
        public string Type => "noop";
        public JobRecord Execute(JobRecord job, CancellationToken cancellationToken) => job with
        {
            Outputs = [new StageArtifact(job.Id, new Checksum { Algorithm = "noop", Digest = "completed" })],
        };
    }
}
