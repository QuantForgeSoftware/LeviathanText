using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Infrastructure;
using System.Diagnostics;

namespace LeviathanText.Worker.Tests;

public sealed class ProjectJobAuthorizationTests
{
    [Fact]
    public async Task Standalone_worker_cancels_a_revoked_project_job_before_dispatch()
    {
        var directory = Directory.CreateTempSubdirectory("lt-revoked-worker-");
        try
        {
            var database = Path.Combine(directory.FullName, "store.db");
            using var store = new LocalStore(database);
            var projectId = new EntityId(EntityKind.Collection, "process-project");
            store.SaveProject(new NativeProject { Id = projectId, Name = "process", Objects = [] });
            var owner = store.CreateUser("process-owner", UserRole.Annotator, "fixture-process-hash");
            store.AddProjectMember(projectId, owner.Id);
            var job = new JobRecord
            {
                Id = new(EntityKind.Job, "process-revoked"), Type = LayoutProjectHandler.JobType,
                Inputs = [new StageArtifact(projectId, new Checksum { Algorithm = "project", Digest = "" })],
                Owner = new AgentRef { Id = owner.Id, IsModel = false }, Status = JobStatus.Pending,
                BoundProjectId = projectId,
            };
            store.SaveJob(job);
            store.RemoveProjectMember(projectId, owner.Id);
            var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            };
            foreach (var argument in new[] { typeof(Program).Assembly.Location, "--store", database, "--once" })
                start.ArgumentList.Add(argument);
            using var process = Assert.IsType<Process>(Process.Start(start));
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            try
            {
                var output = process.StandardOutput.ReadToEndAsync(deadline.Token);
                var errors = process.StandardError.ReadToEndAsync(deadline.Token);
                await process.WaitForExitAsync(deadline.Token);
                Assert.Equal(0, process.ExitCode);
                Assert.Contains("-> Cancelled", await output);
                Assert.DoesNotContain("Unhandled exception", await errors);
                Assert.Equal(JobStatus.Cancelled, store.LoadJob(job.Id)!.Status);
            }
            finally
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
        finally { directory.Delete(recursive: true); }
    }

    [Theory]
    [InlineData("member", true, true)]
    [InlineData("revoked", true, false)]
    [InlineData("disabled", true, false)]
    [InlineData("reader", true, false)]
    [InlineData("missing", true, false)]
    [InlineData("admin", true, true)]
    [InlineData("revoked", false, true)]
    public void Dispatch_rechecks_bound_membership_before_any_handler_side_effect(string state, bool bound, bool expected)
    {
        using var store = new LocalStore(":memory:");
        var projectId = new EntityId(EntityKind.Collection, "dispatch-project");
        store.SaveProject(new NativeProject { Id = projectId, Name = "dispatch", Objects = [] });
        var user = store.CreateUser("owner", state == "admin" ? UserRole.Admin :
            state == "reader" ? UserRole.Reader : UserRole.Annotator, "fixture-hash");
        store.AddProjectMember(projectId, user.Id);
        var job = new JobRecord
        {
            Id = new(EntityKind.Job, "dispatch"), Type = "probe", Inputs = [],
            Owner = new AgentRef { Id = state == "missing" ? "missing-owner" : user.Id, IsModel = false }, Status = JobStatus.Pending,
            BoundProjectId = bound ? projectId : null,
        };
        store.SaveJob(job);
        // Admission has finished; now revoke access before the worker claims it.
        if (state is "revoked" or "admin") store.RemoveProjectMember(projectId, user.Id);
        if (state == "disabled") store.DisableUser(user.Id);
        var handler = new ProbeHandler();
        var worker = new WorkerHost(new Program.LocalStoreJobStore(store), [handler],
            authorize: record => ProjectJobAuthorization.CanExecute(store, record));

        var result = Assert.IsType<JobRecord>(worker.RunNext());

        Assert.Equal(expected, handler.Executed);
        Assert.Equal(expected ? JobStatus.Succeeded : JobStatus.Cancelled, result.Status);
        Assert.Null(result.Lease);
        Assert.Equal(0, result.RetriesUsed);
        var persisted = Assert.IsType<JobRecord>(store.LoadJob(job.Id));
        Assert.Equal(result.Status, persisted.Status);
        Assert.Equal(result.Failure, persisted.Failure);
        Assert.Null(persisted.Lease);
        Assert.Null(worker.RunNext());
    }

    private sealed class ProbeHandler : IJobHandler
    {
        public string Type => "probe";
        public bool Executed { get; private set; }
        public JobRecord Execute(JobRecord job, CancellationToken cancellationToken)
        {
            Executed = true;
            return job;
        }
    }
}
