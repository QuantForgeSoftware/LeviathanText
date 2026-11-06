using System.Net;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkiaSharp;

namespace LeviathanText.Cli.Tests;

public sealed class VlmBenchmarkCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lt-vlm-cli", Guid.NewGuid().ToString("N"));
    public VlmBenchmarkCommandTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void Validation_checks_exact_image_bytes_without_opening_a_project_store()
    {
        var manifest = Manifest();
        var (code, output) = Run("benchmark", "vlm-validate", manifest, "--allow-provisional");
        Assert.Equal(0, code);
        using var json = JsonDocument.Parse(output);
        Assert.Equal(1, json.RootElement.GetProperty("samples").GetInt32());
        Assert.Equal("exploratory", json.RootElement.GetProperty("runPurpose").GetString());
        Assert.False(File.Exists(Path.Combine(_root, "unused.db")));
    }

    [Fact]
    public void Validation_resolves_image_paths_from_manifest_directory()
    {
        var manifest = Manifest();
        Assert.Equal(0, Run("benchmark", "vlm-validate", manifest, "--allow-provisional").Code);
    }

    [Fact]
    public void Incorrect_image_hash_is_rejected_without_creating_a_store()
    {
        var manifest = Manifest(hash: new string('0', 64));
        Assert.NotEqual(0, Run("benchmark", "vlm-validate", manifest, "--allow-provisional").Code);
        Assert.False(File.Exists(Path.Combine(_root, "unused.db")));
    }

    [Fact]
    public void Provisional_inputs_require_explicit_exploratory_opt_in()
    {
        var manifest = Manifest();
        Assert.NotEqual(0, Run("benchmark", "vlm-validate", manifest).Code);
        Assert.False(File.Exists(Path.Combine(_root, "unused.db")));
    }

    [Fact]
    public void Confirmation_cannot_run_through_the_development_command()
    {
        var manifest = Manifest(purpose: "confirmation");
        Assert.NotEqual(0, Run("benchmark", "vlm-validate", manifest, "--allow-provisional").Code);
        Assert.False(File.Exists(Path.Combine(_root, "unused.db")));
    }

    [Fact]
    public async Task Run_receipt_identifies_each_loaded_producer_assembly()
    {
        var output = await CompletedRun();
        var run = ReadNode(output, "run-manifest.json");
        var producers = run["producerAssemblies"]?.AsArray();
        Assert.NotNull(producers);
        Assembly[] expected = [typeof(VlmBenchmarkCommand).Assembly, typeof(AI.VisionHttpProvider).Assembly,
            typeof(Evaluation.VlmBenchmarkScorer).Assembly, typeof(SkiaSharp.SKCodec).Assembly];
        Assert.Equal(expected.Length, producers.Count);
        foreach (var assembly in expected)
        {
            var producer = Assert.Single(producers, p => p!["name"]!.GetValue<string>() == assembly.GetName().Name)!;
            Assert.Equal(assembly.GetName().Version!.ToString(), producer["version"]!.GetValue<string>());
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(assembly.Location))),
                producer["sha256"]!.GetValue<string>());
        }
        var cli = Assert.Single(producers, p => p!["name"]!.GetValue<string>() == "LeviathanText.Cli")!;
        Assert.Equal(run["codeAssemblySha256"]!.GetValue<string>(), cli["sha256"]!.GetValue<string>());
    }

    [Fact]
    public async Task AI_binary_identity_changes_without_changing_the_CLI_binary_receipt()
    {
        var manifest = Manifest();
        var originalAiBytes = File.ReadAllBytes(typeof(AI.VisionHttpProvider).Assembly.Location);
        var originalCliBytes = File.ReadAllBytes(typeof(VlmBenchmarkCommand).Assembly.Location);
        var receipts = new List<JsonNode>();
        for (int variant = 0; variant < 2; variant++)
        {
            var isolated = Path.Combine(_root, "assemblies-" + variant);
            Directory.CreateDirectory(isolated);
            var cliPath = Path.Combine(isolated, "LeviathanText.Cli.dll");
            var aiPath = Path.Combine(isolated, "LeviathanText.AI.dll");
            File.WriteAllBytes(cliPath, originalCliBytes);
            // A PE overlay changes the copied file identity without rewriting IL or shared binaries.
            var aiBytes = variant == 0 ? originalAiBytes : originalAiBytes.Concat(Encoding.UTF8.GetBytes("receipt-overlay-v2")).ToArray();
            File.WriteAllBytes(aiPath, aiBytes);
            var context = new ProducerAssemblyContext(aiPath);
            try
            {
                var command = context.LoadFromAssemblyPath(cliPath).GetType(typeof(VlmBenchmarkCommand).FullName!)!;
                using var handler = new MockHandler((_, _) => Task.FromResult(Completion("αβ")));
                var output = Path.Combine(_root, "isolated-run-" + variant);
                var task = (Task<int>)command.GetMethod(nameof(VlmBenchmarkCommand.RunAsync))!
                    .Invoke(null, [RunArguments(manifest, output), handler, CancellationToken.None])!;
                Assert.Equal(0, await task);
                var run = ReadNode(output, "run-manifest.json");
                var producers = run["producerAssemblies"]?.AsArray();
                Assert.NotNull(producers);
                var ai = Assert.Single(producers, p => p!["name"]!.GetValue<string>() == "LeviathanText.AI")!;
                Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(aiBytes)), ai["sha256"]!.GetValue<string>());
                receipts.Add(run);
            }
            finally { context.Unload(); }
        }
        Assert.Equal(receipts[0]["codeAssemblySha256"]!.GetValue<string>(), receipts[1]["codeAssemblySha256"]!.GetValue<string>());
        static string AiHash(JsonNode receipt) => receipt["producerAssemblies"]!.AsArray()
            .Single(p => p!["name"]!.GetValue<string>() == "LeviathanText.AI")!["sha256"]!.GetValue<string>();
        Assert.NotEqual(AiHash(receipts[0]), AiHash(receipts[1]));
        Assert.Equal(originalAiBytes, File.ReadAllBytes(typeof(AI.VisionHttpProvider).Assembly.Location));
    }

    private sealed class ProducerAssemblyContext(string aiPath) : AssemblyLoadContext(isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName assemblyName) => assemblyName.Name == "LeviathanText.AI"
            ? LoadFromAssemblyPath(aiPath) : null;
    }

    [Theory]
    [InlineData("image-only")]
    [InlineData("ocr-assisted")]
    public async Task Requests_use_frozen_inputs_without_labels_ids_or_source_provenance(string mode)
    {
        var manifest = Manifest();
        var input = JsonNode.Parse(File.ReadAllText(manifest))!;
        input["samples"]![0]!["baselineText"] = "FROZEN_BASELINE";
        input["samples"]![0]!["physicalUnit"] = "PRIVATE_PHYSICAL_UNIT";
        input["samples"]![0]!["rightsEvidenceRef"] = "PRIVATE_RIGHTS_LOCATION";
        input["samples"]![0]!["sourceGeometryRef"] = "provenance.json#/geometry/0";
        File.WriteAllText(manifest, input.ToJsonString());
        File.WriteAllText(Path.Combine(_root, "references.json"), "PRIVATE_REFERENCE_LABEL");
        var snapshot = File.ReadAllBytes(manifest);
        var payloads = new List<string>();
        using var handler = new MockHandler(async (request, cancellation) =>
        {
            payloads.Add(await request.Content!.ReadAsStringAsync(cancellation));
            input["samples"]![0]!["baselineText"] = "CHANGED_BASELINE";
            File.WriteAllText(manifest, input.ToJsonString());
            return Completion("αβ");
        });
        var output = Path.Combine(_root, "run");

        Assert.Equal(0, await VlmBenchmarkCommand.RunAsync(RunArguments(manifest, output, 2, mode), handler));

        Assert.Equal(2, payloads.Count);
        foreach (var payload in payloads)
        {
            Assert.Contains("data:image/png;base64,", payload);
            Assert.Equal(mode == "ocr-assisted", payload.Contains("FROZEN_BASELINE", StringComparison.Ordinal));
            foreach (var forbidden in new[] { "CHANGED_BASELINE", "PRIVATE_REFERENCE_LABEL", "sample-01", "PRIVATE_PHYSICAL_UNIT", "PRIVATE_RIGHTS_LOCATION", "crop.png" })
                Assert.DoesNotContain(forbidden, payload);
        }
        Assert.Equal(snapshot, File.ReadAllBytes(Path.Combine(output, "input-manifest.json")));
        var run = ReadNode(output, "run-manifest.json");
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(snapshot)), run["inputManifestSha256"]!.GetValue<string>());
        Assert.Equal(Path.GetDirectoryName(manifest), run["imagePathBase"]!.GetValue<string>());
        Assert.Equal(manifest, run["inputManifestPath"]!.GetValue<string>());
        var replayPath = Path.Combine(output, "resolved-input-manifest.json");
        var replayBytes = File.ReadAllBytes(replayPath);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(replayBytes)), run["resolvedInputManifestSha256"]!.GetValue<string>());
        var replay = JsonNode.Parse(replayBytes)!;
        Assert.Equal(Path.Combine(_root, "crop.png"), replay["samples"]![0]!["imagePath"]!.GetValue<string>());
        Assert.Equal("FROZEN_BASELINE", replay["samples"]![0]!["baselineText"]!.GetValue<string>());
        Assert.Equal(_root, replay["sourceEvidenceBase"]!.GetValue<string>());
        Assert.Equal("provenance.json#/geometry/0", replay["samples"]![0]!["sourceGeometryRef"]!.GetValue<string>());
        Assert.Equal("PRIVATE_RIGHTS_LOCATION", replay["samples"]![0]!["rightsEvidenceRef"]!.GetValue<string>());
        Assert.Equal(0, await VlmBenchmarkCommand.RunAsync(["vlm-validate", replayPath, "--allow-provisional"]));
        var replayOutput = Path.Combine(_root, "replay-run");
        using var replayHandler = new MockHandler((_, _) => Task.FromResult(Completion("αβ")));
        Assert.Equal(0, await VlmBenchmarkCommand.RunAsync(RunArguments(replayPath, replayOutput), replayHandler));
        Assert.Equal(_root, ReadNode(replayOutput, "run-manifest.json")["sourceEvidenceBase"]!.GetValue<string>());
        Assert.False(File.Exists(Path.Combine(_root, "unused.db")));
    }

    [Fact]
    public async Task Label_fields_are_rejected_before_a_request_or_output_directory_is_created()
    {
        var manifest = Manifest();
        var input = JsonNode.Parse(File.ReadAllText(manifest))!;
        input["samples"]![0]!["referenceText"] = "DO_NOT_SEND";
        File.WriteAllText(manifest, input.ToJsonString());
        var calls = 0;
        using var handler = new MockHandler((_, _) => { calls++; return Task.FromResult(Completion("αβ")); });
        var output = Path.Combine(_root, "run");

        await Assert.ThrowsAsync<JsonException>(() => VlmBenchmarkCommand.RunAsync(RunArguments(manifest, output), handler));

        Assert.Equal(0, calls);
        Assert.False(Directory.Exists(output));
    }

    [Fact]
    public async Task Expanded_run_receipt_is_rejected_before_output_or_first_request()
    {
        var manifestPath = Manifest();
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var manifest = JsonSerializer.Deserialize<VlmInputManifest>(File.ReadAllBytes(manifestPath), options)!;
        var sample = manifest.Samples[0];
        manifest = manifest with
        {
            DatasetId = "",
            Samples = Enumerable.Range(0, 1000).Select(i => sample with
            {
                SampleId = i.ToString("D4") + new string('s', 124),
            }).ToList(),
        };
        var resolved = manifest with
        {
            PathBase = "containing-manifest-directory", SourceEvidenceBase = _root,
            Samples = manifest.Samples.Select(s => s with { ImagePath = Path.Combine(_root, "crop.png") }).ToList(),
        };
        const int byteLimit = 4 * 1024 * 1024;
        // The input and resolved replay both fit, but the three-repetition request
        // plan is larger than the sample manifest and pushes the run receipt over.
        manifest = manifest with { DatasetId = new string('d', byteLimit - JsonSerializer.SerializeToUtf8Bytes(resolved, options).Length - 1024) };
        File.WriteAllBytes(manifestPath, JsonSerializer.SerializeToUtf8Bytes(manifest, options));
        Assert.True(new FileInfo(manifestPath).Length < byteLimit);
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(resolved with { DatasetId = manifest.DatasetId }, options).Length < byteLimit);
        var output = Path.Combine(_root, "run");
        using var cancellation = new CancellationTokenSource();
        int calls = 0;
        using var handler = new MockHandler((_, token) =>
        {
            calls++;
            cancellation.Cancel();
            return Task.FromCanceled<HttpResponseMessage>(token);
        });

        var error = await Record.ExceptionAsync(() => VlmBenchmarkCommand.RunAsync(
            RunArguments(manifestPath, output, 3), handler, cancellation.Token));

        // If this guard regresses, prove the accepted run cannot be read by its
        // own scorer, without issuing any real model request or retaining fixtures.
        long? writtenReceiptBytes = null;
        if (error is null)
        {
            writtenReceiptBytes = new FileInfo(Path.Combine(output, "run-manifest.json")).Length;
            await Assert.ThrowsAsync<InvalidDataException>(() => Score(output, "unused-references.json", Path.Combine(_root, "score.json")));
        }
        Assert.True(error is InvalidDataException, $"Expected preflight rejection; actual error={error?.GetType().Name ?? "none"}, written run receipt={writtenReceiptBytes} bytes, mock calls={calls}.");
        Assert.Contains("run receipt", Assert.IsType<InvalidDataException>(error).Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, calls);
        Assert.False(Directory.Exists(output));
        Assert.False(File.Exists(Path.Combine(_root, "unused.db")));
    }

    [Fact]
    public async Task Large_dataset_identity_round_trips_when_serialized_receipts_fit()
    {
        var manifest = Manifest();
        var input = JsonNode.Parse(File.ReadAllText(manifest))!;
        input["datasetId"] = new string('d', 2 * 1024 * 1024);
        File.WriteAllText(manifest, input.ToJsonString());
        var output = Path.Combine(_root, "run");
        using var handler = new MockHandler((_, _) => Task.FromResult(Completion("αβ")));

        Assert.Equal(0, await VlmBenchmarkCommand.RunAsync(RunArguments(manifest, output), handler));
        Assert.Equal(0, await Score(output, References(output), Path.Combine(_root, "score.json")));

        Assert.True(new FileInfo(Path.Combine(output, "run-manifest.json")).Length <= 4 * 1024 * 1024);
        Assert.Equal(input["datasetId"]!.GetValue<string>(), ReadNode(output, "run-manifest.json")["datasetId"]!.GetValue<string>());
    }

    [Fact]
    public async Task Changed_crop_is_rejected_on_the_next_request_and_existing_outputs_are_preserved()
    {
        var manifest = Manifest();
        int calls = 0;
        using var handler = new MockHandler((_, _) =>
        {
            calls++;
            File.WriteAllBytes(Path.Combine(_root, "crop.png"), [1, 2, 3]);
            return Task.FromResult(Completion("αβ"));
        });
        var output = Path.Combine(_root, "run");
        Assert.Equal(3, await VlmBenchmarkCommand.RunAsync(RunArguments(manifest, output, 2), handler));
        var rows = ReadRows(output);
        Assert.Equal(["Completed", "InputRejected"], rows.Select(r => r["status"]!.GetValue<string>()));
        Assert.Equal(1, calls);
        var retained = Directory.GetFiles(output).ToDictionary(path => Path.GetFileName(path)!, File.ReadAllBytes);
        manifest = Manifest();
        using var neverCalled = new MockHandler((_, _) => throw new InvalidOperationException("No request should occur."));
        await Assert.ThrowsAsync<IOException>(() => VlmBenchmarkCommand.RunAsync(RunArguments(manifest, output), neverCalled));
        foreach (var file in retained) Assert.Equal(file.Value, File.ReadAllBytes(Path.Combine(output, file.Key!)));
    }

    [Fact]
    public async Task Cancellation_records_every_request_and_scoring_keeps_the_full_denominator()
    {
        var manifest = Manifest();
        using var cancellation = new CancellationTokenSource();
        using var handler = new MockHandler(async (_, token) =>
        {
            cancellation.Cancel();
            await Task.Delay(Timeout.Infinite, token);
            return Completion("unreachable");
        });
        var output = Path.Combine(_root, "run");
        Assert.Equal(3, await VlmBenchmarkCommand.RunAsync(RunArguments(manifest, output, 3), handler, cancellation.Token));
        Assert.Equal(["Cancelled", "NotAttempted", "NotAttempted"],
            ReadRows(output).Select(row => row["status"]!.GetValue<string>()));
        var references = References(output);
        var scorePath = Path.Combine(_root, "score.json");

        Assert.Equal(0, await Score(output, references, scorePath));

        var reports = JsonNode.Parse(File.ReadAllText(scorePath))!["reports"]!.AsArray();
        Assert.Equal(3, reports.Count);
        foreach (var report in reports)
        {
            Assert.Equal(1, report!["score"]!["overall"]!["naturalLineCount"]!.GetValue<int>());
            Assert.Equal(2, report["score"]!["overall"]!["deletions"]!.GetValue<int>());
            Assert.Equal(0, report["score"]!["overall"]!["validCompletedCount"]!.GetValue<int>());
        }
    }

    [Theory]
    [InlineData("missing-reference")]
    [InlineData("extra-reference")]
    [InlineData("reference-unit")]
    [InlineData("reference-material")]
    [InlineData("snapshot-change")]
    [InlineData("empty-plan")]
    [InlineData("plan-cohort")]
    [InlineData("plan-mode")]
    [InlineData("plan-repetition")]
    [InlineData("row-unit")]
    [InlineData("row-material")]
    [InlineData("prompt-hash")]
    [InlineData("system-prompt-hash")]
    [InlineData("system-prompt")]
    [InlineData("missing-receipt")]
    public async Task Scoring_rejects_changed_cohort_identity_before_creating_a_report(string change)
    {
        var output = await CompletedRun();
        var references = References(output);
        var refs = JsonNode.Parse(File.ReadAllText(references))!;
        var run = ReadNode(output, "run-manifest.json");
        var input = ReadNode(output, "input-manifest.json");
        var rows = ReadRows(output);
        switch (change)
        {
            case "missing-reference": refs["references"]!.AsArray().Clear(); rows.Clear(); break;
            case "extra-reference":
                var extra = refs["references"]![0]!.DeepClone(); extra["sampleId"] = "foreign";
                refs["references"]!.AsArray().Add(extra); break;
            case "reference-unit": refs["references"]![0]!["physicalUnit"] = "wrong-unit"; break;
            case "reference-material": refs["references"]![0]!["material"] = "wrong-material"; break;
            case "snapshot-change": input["samples"]![0]!["physicalUnit"] = "changed"; break;
            case "empty-plan": run["requests"]!.AsArray().Clear(); rows.Clear(); break;
            case "plan-cohort": run["requests"]![0]!["sampleId"] = "foreign"; rows.Clear(); break;
            case "plan-mode": run["requests"]![0]!["mode"] = "unregistered-mode"; rows.Clear(); break;
            case "plan-repetition": run["requests"]![0]!["repetition"] = 99; rows.Clear(); break;
            case "row-unit": rows[0]["physicalUnit"] = "changed"; break;
            case "row-material": rows[0]["material"] = "changed"; break;
            case "prompt-hash": rows[0]["result"]!["promptSha256"] = new string('0', 64); break;
            case "system-prompt-hash": run["systemPromptSha256"] = new string('0', 64); break;
            case "system-prompt": run["systemPrompt"] = "Different instructions"; break;
            case "missing-receipt": rows[0]["result"] = null; break;
        }
        File.WriteAllText(references, refs.ToJsonString());
        File.WriteAllText(Path.Combine(output, "run-manifest.json"), run.ToJsonString());
        File.WriteAllText(Path.Combine(output, "input-manifest.json"), input.ToJsonString());
        WriteRows(output, rows);
        var scorePath = Path.Combine(_root, "score.json");

        await Assert.ThrowsAsync<InvalidDataException>(() => Score(output, references, scorePath));

        Assert.False(File.Exists(scorePath));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("unknown")]
    [InlineData("image")]
    [InlineData("not-attempted-text")]
    [InlineData("unknown-status")]
    [InlineData("receipt-mismatch")]
    [InlineData("input-rejected-text")]
    public async Task Duplicate_unknown_and_changed_image_rows_are_rejected(string change)
    {
        var output = await CompletedRun();
        var references = References(output);
        var rows = ReadRows(output);
        if (change == "duplicate") rows.Add(rows[0].DeepClone());
        if (change == "unknown") rows[0]["sampleId"] = "unknown";
        if (change == "image") rows[0]["imageSha256"] = new string('f', 64);
        if (change == "not-attempted-text") { rows[0]["status"] = "NotAttempted"; rows[0]["result"] = null; }
        if (change == "input-rejected-text") { rows[0]["status"] = "InputRejected"; rows[0]["result"] = null; }
        if (change == "unknown-status") rows[0]["status"] = "InventedSuccess";
        if (change == "receipt-mismatch") rows[0]["result"]!["text"] = "different captured text";
        WriteRows(output, rows);
        var scorePath = Path.Combine(_root, "score.json");

        await Assert.ThrowsAsync<InvalidDataException>(() => Score(output, references, scorePath));

        Assert.False(File.Exists(scorePath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_rows_remain_deletions_and_score_output_is_never_overwritten(bool missingFile)
    {
        var output = await CompletedRun();
        var references = References(output);
        if (missingFile) File.Delete(Path.Combine(output, "results.jsonl"));
        else WriteRows(output, []);
        var scorePath = Path.Combine(_root, "score.json");
        Assert.Equal(0, await Score(output, references, scorePath));
        var bytes = File.ReadAllBytes(scorePath);
        var natural = JsonNode.Parse(bytes)!["reports"]![0]!["score"]!["overall"]!;
        Assert.Equal(1, natural["naturalLineCount"]!.GetValue<int>());
        Assert.Equal(2, natural["deletions"]!.GetValue<int>());
        await Assert.ThrowsAsync<IOException>(() => Score(output, references, scorePath));
        Assert.Equal(bytes, File.ReadAllBytes(scorePath));
    }

    [Fact]
    public async Task Truncated_nonempty_output_is_scored_without_hiding_insertions()
    {
        var manifest = Manifest();
        var output = Path.Combine(_root, "run");
        using var handler = new MockHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"model\":\"synthetic-model\",\"choices\":[{\"message\":{\"content\":\"αβ extra\"},\"finish_reason\":\"length\"}]}",
                Encoding.UTF8, "application/json"),
        }));
        Assert.Equal(3, await VlmBenchmarkCommand.RunAsync(RunArguments(manifest, output), handler));
        var references = References(output);
        var scorePath = Path.Combine(_root, "score.json");

        Assert.Equal(0, await Score(output, references, scorePath));

        var natural = JsonNode.Parse(File.ReadAllBytes(scorePath))!["reports"]![0]!["score"]!["overall"]!;
        Assert.Equal(8, natural["hypothesisScalars"]!.GetValue<int>());
        Assert.Equal(6, natural["insertions"]!.GetValue<int>());
        Assert.Equal(0, natural["validCompletedCount"]!.GetValue<int>());
        Assert.Equal(3, natural["microCer"]!.GetValue<double>());
    }

    [Fact]
    public async Task Large_escaped_but_bounded_model_text_is_retained_and_scored_in_full()
    {
        var text = new string('<', 500_000);
        var output = await CompletedRun(text);
        var references = References(output);
        var scorePath = Path.Combine(_root, "score.json");

        Assert.Equal(0, await Score(output, references, scorePath));

        var natural = JsonNode.Parse(File.ReadAllBytes(scorePath))!["reports"]![0]!["score"]!["overall"]!;
        Assert.Equal(500_000, natural["hypothesisScalars"]!.GetValue<int>());
        Assert.Equal(499_998, natural["insertions"]!.GetValue<int>());
    }

    [Theory]
    [InlineData("αβγδ", 6)]
    [InlineData("𝛂", 3)]
    public async Task Cumulative_text_budget_stops_before_a_malformed_trailing_row(string text, long budget)
    {
        var manifest = Manifest();
        var output = Path.Combine(_root, "run");
        using var handler = new MockHandler((_, _) => Task.FromResult(Completion(text)));
        Assert.Equal(0, await VlmBenchmarkCommand.RunAsync(RunArguments(manifest, output, 3), handler));
        var path = Path.Combine(output, "results.jsonl");
        var lines = File.ReadAllLines(path);
        // Each valid row fits individually. Their combined UTF-16 length exceeds the
        // private test cap; the invalid third row must never be deserialized.
        Assert.True(text.Length <= budget && 2 * text.Length > budget);
        lines[2] = "{malformed-json";
        File.WriteAllLines(path, lines);
        var preserved = File.ReadAllBytes(path);
        var scorePath = Path.Combine(_root, "score.json");

        var wrapper = Assert.Throws<TargetInvocationException>(() =>
            ScoreWithBudget(output, References(output), scorePath, budget));
        var error = Assert.IsType<InvalidDataException>(wrapper.InnerException);
        Assert.Contains("cumulative retained text", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(scorePath));
        Assert.Equal(preserved, File.ReadAllBytes(path));
    }

    [Fact]
    public async Task Exact_text_budget_keeps_every_hypothesis_while_ignoring_unneeded_row_diagnostics()
    {
        var manifest = Manifest();
        var input = JsonNode.Parse(File.ReadAllText(manifest))!;
        input["samples"]![0]!["physicalUnit"] = new string('u', 16_000);
        File.WriteAllText(manifest, input.ToJsonString());
        var output = Path.Combine(_root, "run");
        using var handler = new MockHandler((_, _) => Task.FromResult(Completion("αβ")));
        Assert.Equal(0, await VlmBenchmarkCommand.RunAsync(RunArguments(manifest, output, 3), handler));
        var rows = ReadRows(output);
        rows.RemoveAt(2);
        rows[0]["error"] = new string('d', 16_000);
        WriteRows(output, rows);
        var preserved = File.ReadAllBytes(Path.Combine(output, "results.jsonl"));
        var scorePath = Path.Combine(_root, "score.json");

        Assert.Equal(0, ScoreWithBudget(output, References(output), scorePath, 4));

        var score = JsonNode.Parse(File.ReadAllBytes(scorePath))!;
        Assert.Equal(2, score["recordedResults"]!.GetValue<int>());
        Assert.Equal(1, score["missingResults"]!.GetValue<int>());
        Assert.Equal([2, 2, 0], score["reports"]!.AsArray().Select(report =>
            report!["score"]!["overall"]!["hypothesisScalars"]!.GetValue<int>()));
        Assert.Equal(preserved, File.ReadAllBytes(Path.Combine(output, "results.jsonl")));
    }

    [Fact]
    public async Task Ocr_assisted_greek_prompt_receipts_score_with_the_transport_canonical_hash()
    {
        var manifest = Manifest();
        var input = JsonNode.Parse(File.ReadAllText(manifest))!;
        input["samples"]![0]!["baselineText"] = "ἀβ �";
        File.WriteAllText(manifest, input.ToJsonString());
        var output = Path.Combine(_root, "run");
        using var handler = new MockHandler((_, _) => Task.FromResult(Completion("αβ")));
        Assert.Equal(0, await VlmBenchmarkCommand.RunAsync(RunArguments(manifest, output, mode: "ocr-assisted"), handler));
        var scorePath = Path.Combine(_root, "score.json");

        Assert.Equal(0, await Score(output, References(output), scorePath));

        var report = JsonNode.Parse(File.ReadAllBytes(scorePath))!;
        Assert.Equal(0, report["reports"]![0]!["score"]!["overall"]!["microCer"]!.GetValue<double>());
    }

    [Fact]
    public async Task Completed_prose_remains_in_the_score_and_completion_is_only_a_transport_claim()
    {
        const string prose = "Here is the transcription: αβ";
        var output = await CompletedRun(prose);
        var references = References(output);
        var scorePath = Path.Combine(_root, "score.json");
        Assert.Equal(0, await Score(output, references, scorePath));
        var report = JsonNode.Parse(File.ReadAllBytes(scorePath))!;

        Assert.Equal("transport-protocol-only", report["completionScope"]!.GetValue<string>());
        Assert.Equal("pending", report["contentFormatAndGroundingReview"]!.GetValue<string>());
        foreach (var name in new[] { "run-manifest.json", "summary.json" })
            Assert.Equal("transport-protocol-only", ReadNode(output, name)["completionScope"]!.GetValue<string>());
        var natural = report["reports"]![0]!["score"]!["overall"]!;
        Assert.Equal(prose.EnumerateRunes().Count(), natural["hypothesisScalars"]!.GetValue<int>());
        Assert.Equal(prose.EnumerateRunes().Count() - 2, natural["insertions"]!.GetValue<int>());
        Assert.Equal(1, natural["validCompletedCount"]!.GetValue<int>());
    }

    [Fact]
    public async Task Provider_rejected_oversize_prompt_remains_a_scored_failure_without_a_network_call()
    {
        var manifest = Manifest();
        var input = JsonNode.Parse(File.ReadAllText(manifest))!;
        input["samples"]![0]!["baselineText"] = new string('α', 10_000);
        File.WriteAllText(manifest, input.ToJsonString());
        var output = Path.Combine(_root, "run");
        int calls = 0;
        using var handler = new MockHandler((_, _) =>
        {
            calls++;
            return Task.FromResult(Completion("unexpected"));
        });
        Assert.Equal(3, await VlmBenchmarkCommand.RunAsync(RunArguments(manifest, output, mode: "ocr-assisted"), handler));
        Assert.Equal(0, calls);
        Assert.Equal("InputRejected", ReadRows(output)[0]["status"]!.GetValue<string>());
        var scorePath = Path.Combine(_root, "score.json");

        Assert.Equal(0, await Score(output, References(output), scorePath));

        var natural = JsonNode.Parse(File.ReadAllBytes(scorePath))!["reports"]![0]!["score"]!["overall"]!;
        Assert.Equal(2, natural["deletions"]!.GetValue<int>());
        Assert.Equal(0, natural["validCompletedCount"]!.GetValue<int>());
    }

    private async Task<string> CompletedRun(string text = "αβ")
    {
        var manifest = Manifest();
        var output = Path.Combine(_root, "run");
        using var handler = new MockHandler((_, _) => Task.FromResult(Completion(text)));
        Assert.Equal(0, await VlmBenchmarkCommand.RunAsync(RunArguments(manifest, output), handler));
        return output;
    }

    private static string[] RunArguments(string manifest, string output, int repetitions = 1, string mode = "image-only") =>
        ["vlm", manifest, "--allow-provisional", "--model", "synthetic-model", "--out", output, "--repetitions", repetitions.ToString(), "--mode", mode];

    private static JsonNode ReadNode(string output, string name) => JsonNode.Parse(File.ReadAllText(Path.Combine(output, name)))!;
    private static List<JsonNode> ReadRows(string output) => File.ReadAllLines(Path.Combine(output, "results.jsonl"))
        .Select(line => JsonNode.Parse(line)!).ToList();
    private static void WriteRows(string output, List<JsonNode> rows) => File.WriteAllLines(Path.Combine(output, "results.jsonl"),
        rows.Select(row => row.ToJsonString(new JsonSerializerOptions { WriteIndented = false })));
    private string References(string output)
    {
        var input = ReadNode(output, "input-manifest.json");
        var path = Path.Combine(_root, "score-references.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            protocol = "VLM-BENCH-1", datasetId = input["datasetId"]!.GetValue<string>(), datasetVersion = 1,
            references = input["samples"]!.AsArray().Select(sample => new
            {
                sampleId = sample!["sampleId"]!.GetValue<string>(),
                physicalUnit = sample["physicalUnit"]!.GetValue<string>(), material = sample["material"]!.GetValue<string>(),
                referenceText = "αβ", controlKind = "naturalLine",
            }),
        }));
        return path;
    }
    private static Task<int> Score(string output, string references, string scorePath) => VlmBenchmarkCommand.RunAsync(
        ["vlm-score", output, "--references", references, "--out", scorePath]);
    private static int ScoreWithBudget(string output, string references, string scorePath, long codeUnits) =>
        (int)typeof(VlmBenchmarkCommand).GetMethod("Score", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [new[] { "vlm-score", output, "--references", references, "--out", scorePath }, codeUnits])!;
    private static HttpResponseMessage Completion(string text) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new
        {
            model = "synthetic-model", choices = new[] { new { message = new { content = text }, finish_reason = "stop" } },
        }, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }), Encoding.UTF8, "application/json"),
    };
    private sealed class MockHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => response(request, cancellationToken);
    }

    private string Manifest(string? hash = null, string purpose = "exploratory")
    {
        using var bitmap = new SKBitmap(30, 20);
        bitmap.Erase(SKColors.White);
        using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        var bytes = encoded.ToArray();
        File.WriteAllBytes(Path.Combine(_root, "crop.png"), bytes);
        var path = Path.Combine(_root, "manifest.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            protocol = "VLM-BENCH-1", datasetId = "synthetic", datasetVersion = 1,
            runPurpose = purpose, samples = new[] { new
            {
                sampleId = "sample-01", imagePath = "crop.png",
                imageSha256 = hash ?? Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)),
                physicalUnit = "unit-01", material = "synthetic", reviewState = "provisional-source-reference",
                exposure = "synthetic", rightsEvidenceRef = "generated-fixture", sourceGeometryRef = "whole-image",
            } },
        }));
        return path;
    }

    private (int Code, string Output) Run(params string[] args)
    {
        var original = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try { return (Program.Main(["--store", Path.Combine(_root, "unused.db"), .. args]), writer.ToString()); }
        finally { Console.SetOut(original); }
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
