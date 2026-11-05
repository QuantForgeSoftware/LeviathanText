using LeviathanText.Contracts;

namespace LeviathanText.Worker;

/// <summary>Durable job persistence used by the worker host (SQLite-backed locally).</summary>
public interface IJobStore
{
    IReadOnlyList<JobRecord> PendingJobs();
    void Save(JobRecord job);
    JobRecord? Find(EntityId id);

    /// <summary>Atomically apply a conditional transition to the current record. A null transition rejects it.</summary>
    JobRecord? Update(EntityId id, Func<JobRecord, JobRecord?> transition);
}
