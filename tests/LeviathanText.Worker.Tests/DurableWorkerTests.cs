using System.Diagnostics;
using LeviathanText.Contracts;
using LeviathanText.Infrastructure;
using WorkerProgram = LeviathanText.Worker.Program;

namespace LeviathanText.Worker.Tests;

public sealed class DurableWorkerTests
{
    [Fact]
    public async Task Concurrent_workers_claim_each_pending_job_once()
    {
        var directory = Directory.CreateTempSubdirectory("lt-worker-claims-");
        try
        {
            string database = Path.Combine(directory.FullName, "store.db");
            using var firstStore = new LocalStore(database);
            using var secondStore = new LocalStore(database);
            var jobs = Enumerable.Range(0, 24).Select(index => NewJob($"race-{index}")).ToArray();
            foreach (var job in jobs) firstStore.SaveJob(job);
            using var gate = new Barrier(2);

            Task<JobRecord?[]> ClaimFrom(LocalStore store) => Task.Factory.StartNew(() =>
            {
                var worker = new WorkerHost(new WorkerProgram.LocalStoreJobStore(store), []);
                return jobs.Select(job =>
                {
                    Assert.True(gate.SignalAndWait(TimeSpan.FromSeconds(10)));
                    return worker.TryClaim(job);
                }).ToArray();
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            var results = await Task.WhenAll(ClaimFrom(firstStore), ClaimFrom(secondStore));
            for (var index = 0; index < jobs.Length; index++)
            {
                var winner = Assert.Single(results.Select(records => records[index]).OfType<JobRecord>());
                var persisted = Assert.IsType<JobRecord>(firstStore.LoadJob(jobs[index].Id));
                Assert.Equal(JobStatus.Running, persisted.Status);
                Assert.False(string.IsNullOrEmpty(winner.Lease!.ClaimId));
                Assert.Equal(winner.Lease, persisted.Lease);
            }
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Redirected_worker_process_claims_jobs_enqueued_after_an_idle_poll()
    {
        var directory = Directory.CreateTempSubdirectory("lt-worker-daemon-");
        try
        {
            string database = Path.Combine(directory.FullName, "store.db");
            using var store = new LocalStore(database);
            var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            start.ArgumentList.Add(typeof(WorkerProgram).Assembly.Location);
            start.ArgumentList.Add("--store");
            start.ArgumentList.Add(database);
            using var process = Assert.IsType<Process>(Process.Start(start));
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            try
            {
                process.StandardInput.Close();
                var ready = await process.StandardOutput.ReadLineAsync(deadline.Token);
                Assert.Contains("polling", ready);
                await Task.Delay(TimeSpan.FromMilliseconds(1250), deadline.Token);

                var job = NewJob("after-idle");
                store.SaveJob(job);
                var finished = await process.StandardOutput.ReadLineAsync(deadline.Token);
                Assert.Contains($"{job.Id} -> Succeeded", finished);
                Assert.Equal(JobStatus.Succeeded, store.LoadJob(job.Id)?.Status);
            }
            finally
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static JobRecord NewJob(string id) => new()
    {
        Id = new EntityId(EntityKind.Job, id),
        Type = "noop",
        Inputs = [],
        Owner = new AgentRef { Id = "test", IsModel = false },
        Status = JobStatus.Pending,
    };
}
