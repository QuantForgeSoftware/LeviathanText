using LeviathanText.Contracts;

namespace LeviathanText.Worker;

/// <summary>
/// A job handler registered with the worker host. Handlers receive the claimed job and
/// must be idempotent: at-least-once execution means a handler may be re-run after a
/// crash, and republishing identical outputs must merge silently (JobTable contract).
/// </summary>
public interface IJobHandler
{
    /// <summary>The closed job type this handler executes, e.g. "pipeline.run".</summary>
    string Type { get; }

    /// <summary>Execute the job; return the updated record (status/outputs set by the host).</summary>
    JobRecord Execute(JobRecord job, CancellationToken cancellationToken);
}
