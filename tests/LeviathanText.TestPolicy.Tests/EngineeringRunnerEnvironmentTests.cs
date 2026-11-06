using System.Diagnostics;
using Xunit;

namespace LeviathanText.TestPolicy.Tests;

/// <summary>Exercises the real shell runner with a recording dotnet double, never a provider/model/test suite.</summary>
public sealed class EngineeringRunnerEnvironmentTests
{
    [SkippableTheory]
    [InlineData("1")]
    [InlineData("0")]
    public async Task Engineering_runner_overrides_inherited_experiment_and_live_provider_flags(string inherited)
    {
        Skip.If(OperatingSystem.IsWindows(), "This shell entry point requires a Unix host.");
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "scripts", "verify-engineering.sh")))
            repository = repository.Parent;
        Assert.NotNull(repository);
        string root = Directory.CreateTempSubdirectory("lt-engineering-guard-").FullName;
        try
        {
            string bin = Directory.CreateDirectory(Path.Combine(root, "bin")).FullName;
            string recording = Path.Combine(root, "calls.txt");
            string executable = Path.Combine(bin, "dotnet");
            // Synthetic TRX only satisfies the runner's receipt parser. It is not evidence
            // that any real engineering case executed: no .NET suite is launched here.
            await File.WriteAllTextAsync(executable, """
                #!/bin/bash
                set -eu
                printf '%s|%s|%s|%s\n' "$LEVIATHANTEXT_RUN_FITTING" "$LEVIATHANTEXT_RUN_CORPUS" "$LEVIATHANTEXT_RUN_LOCKED" "$LEVIATHAN_OLLAMA_LIVE" >> "$RECORDED_ENGINEERING_CALLS"
                results=''; receipt=''
                while (($#)); do
                  case "$1" in
                    --results-directory) results=$2; shift 2 ;;
                    --logger) receipt=${2#trx;LogFileName=}; shift 2 ;;
                    *) shift ;;
                  esac
                done
                [[ -n "$results" && -n "$receipt" ]]
                printf '%s\n' '<TestRun><ResultSummary><Counters executed="1" failed="0" /></ResultSummary></TestRun>' > "$results/$receipt"
                """);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var start = new ProcessStartInfo("/bin/bash")
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
                WorkingDirectory = root,
            };
            start.ArgumentList.Add(Path.Combine(repository!.FullName, "scripts", "verify-engineering.sh"));
            start.ArgumentList.Add("--no-build"); start.ArgumentList.Add("--results");
            start.ArgumentList.Add(Path.Combine(root, "receipts"));
            start.Environment.TryGetValue("PATH", out var inheritedPath);
            start.Environment["PATH"] = bin + Path.PathSeparator + (inheritedPath ?? "/usr/bin:/bin");
            start.Environment["RECORDED_ENGINEERING_CALLS"] = recording;
            foreach (string flag in new[] { "LEVIATHANTEXT_RUN_FITTING", "LEVIATHANTEXT_RUN_CORPUS", "LEVIATHANTEXT_RUN_LOCKED", "LEVIATHAN_OLLAMA_LIVE" })
                start.Environment[flag] = inherited;
            using var process = Process.Start(start)!;
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> error = process.StandardError.ReadToEndAsync();
            try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20)); }
            finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } }
            Assert.True(process.ExitCode == 0, await output + Environment.NewLine + await error);
            string[] calls = await File.ReadAllLinesAsync(recording);
            Assert.Equal(15, calls.Length); // 13 selected suites and exactly two codec selections.
            Assert.All(calls, call => Assert.Equal("0|0|0|0", call));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
