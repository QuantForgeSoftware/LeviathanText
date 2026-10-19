using LeviathanText.Contracts;

namespace LeviathanText.Worker;

/// <summary>Durable job persistence used by the worker host (SQLite-backed locally).</summary>
public interface IJobStore
{
    IReadOnlyList<JobRecord> PendingJobs();
    void Save(JobRecord job);
    JobRecord? Find(EntityId id);
}
