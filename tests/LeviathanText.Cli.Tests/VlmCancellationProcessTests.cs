using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;

namespace LeviathanText.Cli.Tests;

public sealed class VlmCancellationProcessTests
{
    [UnixSignalFact]
    public async Task Sigint_closes_the_real_CLI_with_every_planned_row_and_no_project_store()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lt-vlm-cancel", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        using var listener = new HttpListener();
        HttpListenerContext? context = null;
        using var child = new Process();
        Task<string>? stdout = null;
        Task<string>? stderr = null;
        bool started = false;
        try
        {
            using var image = new SKBitmap(12, 12);
            image.Erase(SKColors.White);
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            var bytes = encoded.ToArray();
            File.WriteAllBytes(Path.Combine(directory, "crop.png"), bytes);
            var manifest = Path.Combine(directory, "manifest.json");
            File.WriteAllText(manifest, JsonSerializer.Serialize(new
            {
                protocol = "VLM-BENCH-1", datasetId = "cancel-fixture", datasetVersion = 1,
                runPurpose = "exploratory", pathBase = "containing-manifest-directory",
                samples = Enumerable.Range(1, 2).Select(i => new
                {
                    sampleId = "sample-" + i, imagePath = "crop.png",
                    imageSha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
                    physicalUnit = "unit-" + i, material = "synthetic", reviewState = "synthetic",
                    exposure = "setup", rightsEvidenceRef = "generated-fixture", sourceGeometryRef = "whole-image",
                }),
            }));
            var portProbe = new TcpListener(IPAddress.Loopback, 0);
            portProbe.Start();
            int port = ((IPEndPoint)portProbe.LocalEndpoint).Port;
            portProbe.Stop();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.Start();
            var output = Path.Combine(directory, "run");
            var store = Path.Combine(directory, "must-not-exist.db");
            child.StartInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = directory, UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true,
            };
            foreach (var argument in new[]
            {
                typeof(Program).Assembly.Location, "--store", store,
                "benchmark", "vlm", manifest, "--model", "mock-cancellation-only",
                "--endpoint", $"http://127.0.0.1:{port}/v1", "--out", output,
                "--repetitions", "3", "--time-limit-minutes", "1", "--max-output-tokens", "512",
            }) child.StartInfo.ArgumentList.Add(argument);
            started = child.Start();
            Assert.True(started);
            stdout = child.StandardOutput.ReadToEndAsync();
            stderr = child.StandardError.ReadToEndAsync();

            // This owned listener has accepted the actual child's first request. Keep the
            // response open so SIGINT arrives during provider I/O, not before startup.
            context = await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(10));
            using var body = new StreamReader(context.Request.InputStream);
            var request = await body.ReadToEndAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Contains("data:image/png;base64,", request);
            Assert.Equal("/v1/chat/completions", context.Request.Url!.AbsolutePath);
            Assert.Equal(0, KillUnix(child.Id, 2)); // SIGINT to this child only, never a process group.
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            var logs = await stdout + "\n" + await stderr;
            Assert.True(child.ExitCode == 3, $"Expected incomplete-evidence exit3, got{child.ExitCode}. {logs}");

            var lines = File.ReadAllLines(Path.Combine(output, "results.jsonl"));
            Assert.Equal(6, lines.Length);
            var statuses = new List<string>();
            var identities = new HashSet<(string, int)>();
            foreach (var line in lines)
            {
                using var row = JsonDocument.Parse(line);
                statuses.Add(row.RootElement.GetProperty("status").GetString()!);
                Assert.True(identities.Add((row.RootElement.GetProperty("sampleId").GetString()!,
                    row.RootElement.GetProperty("repetition").GetInt32())));
                Assert.Equal("", row.RootElement.GetProperty("text").GetString());
            }
            Assert.Equal("Cancelled", statuses[0]);
            Assert.All(statuses.Skip(1), status => Assert.Equal("NotAttempted", status));
            using var summary = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(output, "summary.json")));
            Assert.Equal(6, summary.RootElement.GetProperty("planned").GetInt32());
            Assert.Equal(1, summary.RootElement.GetProperty("attempted").GetInt32());
            Assert.Equal(0, summary.RootElement.GetProperty("completed").GetInt32());
            Assert.False(File.Exists(store));
            Assert.False(File.Exists(store + "-wal"));
        }
        finally
        {
            context?.Response.Abort();
            listener.Close();
            if (started && !child.HasExited)
            {
                child.Kill(entireProcessTree: true);
                await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }
            if (stdout is not null) await stdout.WaitAsync(TimeSpan.FromSeconds(10));
            if (stderr is not null) await stderr.WaitAsync(TimeSpan.FromSeconds(10));
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [UnixSignalFact]
    public async Task Sigint_terminates_offline_validation_instead_of_swallowing_cancellation()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lt-vlm-offline-cancel", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        using var child = new Process();
        Task<string>? stderr = null;
        bool started = false;
        try
        {
            using var image = new SKBitmap(12, 12);
            image.Erase(SKColors.White);
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            var bytes = encoded.ToArray();
            File.WriteAllBytes(Path.Combine(directory, "crop.png"), bytes);
            var manifest = Path.Combine(directory, "manifest.json");
            File.WriteAllText(manifest, JsonSerializer.Serialize(new
            {
                protocol = "VLM-BENCH-1", datasetId = new string('d', 2 * 1024 * 1024), datasetVersion = 1,
                runPurpose = "exploratory", samples = new[] { new
                {
                    sampleId = "sample-01", imagePath = "crop.png",
                    imageSha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
                    physicalUnit = "unit-01", material = "synthetic", reviewState = "synthetic",
                    exposure = "setup", rightsEvidenceRef = "generated-fixture", sourceGeometryRef = "whole-image",
                } },
            }));
            var store = Path.Combine(directory, "must-not-exist.db");
            child.StartInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = directory, UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true,
            };
            foreach (var argument in new[]
            {
                typeof(Program).Assembly.Location, "--store", store,
                "benchmark", "vlm-validate", manifest,
            }) child.StartInfo.ArgumentList.Add(argument);
            started = child.Start();
            Assert.True(started);
            stderr = child.StandardError.ReadToEndAsync();

            // Reading a single character proves the real child has reached the
            // offline validation output. Leave its multi-megabyte JSON undrained,
            // so it cannot finish normally before the signal reaches that branch.
            var first = new char[1];
            Assert.Equal(1, await child.StandardOutput.ReadAsync(first.AsMemory()).AsTask().WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal('{', first[0]);
            Assert.Equal(0, KillUnix(child.Id, 2));
            var exit = child.WaitForExitAsync();
            Assert.Same(exit, await Task.WhenAny(exit, Task.Delay(TimeSpan.FromSeconds(3))));
            await exit;
            Assert.True(child.ExitCode == 130, $"Expected normal SIGINT exit 130, got {child.ExitCode}. {await stderr}");
            Assert.False(File.Exists(store));
            Assert.False(File.Exists(store + "-wal"));
        }
        finally
        {
            if (started && !child.HasExited)
            {
                child.Kill(entireProcessTree: true);
                await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }
            if (stderr is not null) await stderr.WaitAsync(TimeSpan.FromSeconds(10));
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int KillUnix(int processId, int signal);

    private sealed class UnixSignalFactAttribute : FactAttribute
    {
        public UnixSignalFactAttribute()
        {
            if (OperatingSystem.IsWindows()) Skip = "Unix SIGINT child-process test; actual Windows console signalling is not covered.";
        }
    }
}
