using System.Diagnostics;
using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Infrastructure;
using WorkerProgram = LeviathanText.Worker.Program;

namespace LeviathanText.Worker.Tests;

public sealed class WorkerBudgetTests
{
    [Fact]
    public async Task Budget_stops_after_exactly_two_completed_attempts()
    {
        using var fixture = new Fixture();
        using var store = new LocalStore(fixture.Database);
        for (var index = 0; index < 3; index++) store.SaveJob(Job($"job-{index}"));

        var result = await fixture.Run("--max-jobs", "2");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(2, store.LoadJobsByStatus(JobStatus.Succeeded).Count);
        Assert.Single(store.LoadJobsByStatus(JobStatus.Pending));
        Assert.Empty(store.LoadJobsByStatus(JobStatus.Running));
    }

    [Fact]
    public async Task Budget_counts_failed_and_requeued_attempts()
    {
        using var fixture = new Fixture();
        using var store = new LocalStore(fixture.Database);
        // Missing recognition input throws a retryable error; one completed attempt
        // returns Pending. An unknown handler finalizes Failed immediately.
        store.SaveJob(Job("a-retry") with { Type = RecognizeSurfaceHandler.JobType });
        store.SaveJob(Job("b-unknown") with { Type = "unknown", Priority = 1 });

        var result = await fixture.Run("--max-jobs", "2");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(JobStatus.Failed, store.LoadJob(new(EntityKind.Job, "b-unknown"))!.Status);
        var retry = store.LoadJob(new(EntityKind.Job, "a-retry"))!;
        Assert.Equal(JobStatus.Pending, retry.Status);
        Assert.Equal(1, retry.RetriesUsed);
        Assert.Null(retry.Lease);
    }

    [Fact]
    public async Task Idle_polls_do_not_consume_budget()
    {
        using var fixture = new Fixture();
        using var store = new LocalStore(fixture.Database);
        using var process = fixture.Start("--max-jobs", "1");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            Assert.Contains("polling", await process.StandardOutput.ReadLineAsync(deadline.Token));
            var errors = process.StandardError.ReadToEndAsync(deadline.Token);
            await Task.Delay(1250, deadline.Token);
            Assert.False(process.HasExited);
            store.SaveJob(Job("after-idle"));
            var output = await process.StandardOutput.ReadToEndAsync(deadline.Token);
            await process.WaitForExitAsync(deadline.Token);
            Assert.Equal(0, process.ExitCode);
            Assert.Contains("after-idle -> Succeeded", output);
            Assert.Equal("", await errors);
            Assert.Equal(JobStatus.Succeeded, store.LoadJob(new(EntityKind.Job, "after-idle"))!.Status);
        }
        finally
        {
            await Fixture.Stop(process);
        }
    }

    [Theory]
    [InlineData("--max-jobs", "0")]
    [InlineData("--max-jobs", "-1")]
    [InlineData("--max-jobs", "abc")]
    [InlineData("--max-jobs", "2147483648")]
    [InlineData("--max-jobs")]
    [InlineData("--max-jobs", "1", "--max-jobs", "2")]
    [InlineData("--once", "--max-jobs", "1")]
    [InlineData("--max-jobs", "1", "--once")]
    [InlineData("--store")]
    [InlineData("--unknown")]
    public async Task Invalid_options_exit_without_creating_store_or_cache(params string[] options)
    {
        using var fixture = new Fixture();
        var result = await fixture.Run(options);
        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Usage:", result.Error);
        Assert.DoesNotContain("polling", result.Output);
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Directory));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Once_still_means_one_poll(bool enqueue)
    {
        using var fixture = new Fixture();
        using var store = new LocalStore(fixture.Database);
        if (enqueue)
        {
            store.SaveJob(Job("first"));
            store.SaveJob(Job("second"));
        }
        var result = await fixture.Run("--once");
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(enqueue ? 1 : 0, store.LoadJobsByStatus(JobStatus.Succeeded).Count);
        Assert.Equal(enqueue ? 1 : 0, store.LoadJobsByStatus(JobStatus.Pending).Count);
    }

    [Theory]
    [InlineData("-TERM")]
    [InlineData("-INT")]
    public async Task Shutdown_signal_drains_current_import_before_recycling(string signal)
    {
        if (OperatingSystem.IsWindows()) return; // POSIX FIFO and signals are Unix-only.
        using var fixture = new Fixture();
        using var store = new LocalStore(fixture.Database);
        var project = new ProjectService(store).Create("drain-on-shutdown");
        var source = System.IO.Directory.CreateDirectory(Path.Combine(fixture.Directory, "source")).FullName;
        var pipe = Path.Combine(source, "blocked.xml");
        using (var mkfifo = StartCommand("mkfifo", pipe))
        {
            await mkfifo.WaitForExitAsync();
            Assert.Equal(0, mkfifo.ExitCode);
        }
        var import = Job("a-import") with
        {
            Type = ImportDirectoryHandler.JobType,
            Inputs = [new StageArtifact(project.Id, new Checksum
            {
                Algorithm = "page-xml-directory", Digest = "dir:" + source,
            })],
        };
        store.SaveJob(import);
        store.SaveJob(Job("b-left-pending"));
        using var process = fixture.Start("--max-jobs", "2");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        Process? writer = null;
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(deadline.Token);
            var errors = process.StandardError.ReadToEndAsync(deadline.Token);
            while (store.LoadJob(import.Id)?.Status != JobStatus.Running)
                await Task.Delay(10, deadline.Token);
            // The FIFO withholds PAGE contents, so this handler cannot finish before
            // the signal, independently of CPU speed or database scheduling.
            using (var kill = StartCommand("/bin/kill", signal, process.Id.ToString()))
            {
                await kill.WaitForExitAsync(deadline.Token);
                Assert.Equal(0, kill.ExitCode);
            }
            await Task.Delay(100, deadline.Token);
            Assert.False(process.HasExited);
            writer = StartCommand("/bin/sh", "-c", "cat > \"$1\"", "--", pipe);
            await writer.StandardInput.WriteAsync("""
                <PcGts xmlns="http://schema.primaresearch.org/PAGE/gts/pagecontent/2019-07-15">
                  <Page imageFilename="generated.png" imageWidth="200" imageHeight="100"/>
                </PcGts>
                """);
            writer.StandardInput.Close();
            await writer.WaitForExitAsync(deadline.Token);
            Assert.Equal(0, writer.ExitCode);
            await process.WaitForExitAsync(deadline.Token);
            Assert.Equal(0, process.ExitCode);
            Assert.Contains("a-import -> Succeeded", await output);
            Assert.Equal("", await errors);
            var finalized = store.LoadJob(import.Id)!;
            Assert.Equal(JobStatus.Succeeded, finalized.Status);
            Assert.Null(finalized.Lease);
            Assert.Equal(0, finalized.RetriesUsed);
            Assert.Single(store.LoadProject(project.Id)!.Objects);
            Assert.Equal(JobStatus.Pending, store.LoadJob(new(EntityKind.Job, "b-left-pending"))!.Status);
        }
        finally
        {
            await Fixture.Stop(process);
            if (writer is not null)
            {
                await Fixture.Stop(writer);
                writer.Dispose();
            }
        }
    }

    private static Process StartCommand(string command, params string[] arguments)
    {
        var start = new ProcessStartInfo(command) { UseShellExecute = false, RedirectStandardInput = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        return Process.Start(start)!;
    }

    private static JobRecord Job(string id) => new()
    {
        Id = new(EntityKind.Job, id), Type = "noop", Inputs = [],
        Owner = new AgentRef { Id = "test", IsModel = false }, Status = JobStatus.Pending,
    };

    private sealed class Fixture : IDisposable
    {
        public string Directory { get; } = System.IO.Directory.CreateTempSubdirectory("lt-worker-budget-").FullName;
        public string Database => Path.Combine(Directory, "store.db");

        public Process Start(params string[] options)
        {
            var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
            {
                UseShellExecute = false, RedirectStandardInput = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
            };
            // Isolate optional auto-loaded layout models from the invoking environment.
            start.Environment["LEVIATHANTEXT_LAYOUT_MODEL_DIR"] = Path.Combine(Directory, "no-layout-models");
            start.ArgumentList.Add(typeof(WorkerProgram).Assembly.Location);
            start.ArgumentList.Add("--store");
            start.ArgumentList.Add(Database);
            foreach (var option in options) start.ArgumentList.Add(option);
            var process = Process.Start(start)!;
            process.StandardInput.Close();
            return process;
        }

        public async Task<(int ExitCode, string Output, string Error)> Run(params string[] options)
        {
            using var process = Start(options);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                var output = process.StandardOutput.ReadToEndAsync(deadline.Token);
                var error = process.StandardError.ReadToEndAsync(deadline.Token);
                await process.WaitForExitAsync(deadline.Token);
                return (process.ExitCode, await output, await error);
            }
            finally { await Stop(process); }
        }

        public static async Task Stop(Process process)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }

        public void Dispose() => System.IO.Directory.Delete(Directory, recursive: true);
    }
}
