using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkiaSharp;

namespace LeviathanText.Cli.Tests;

public sealed class VlmStructuredOutputTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lt-vlm-json", Guid.NewGuid().ToString("N"));
    public VlmStructuredOutputTests() => Directory.CreateDirectory(_root);
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static JsonObject Node(string directory, string name) => JsonNode.Parse(File.ReadAllBytes(Path.Combine(directory, name)))!.AsObject();
    private static JsonObject Row(string directory) => JsonNode.Parse(File.ReadAllLines(Path.Combine(directory, "results.jsonl"))[0])!.AsObject();
    private static void WriteRow(string directory, JsonObject row) => File.WriteAllText(Path.Combine(directory, "results.jsonl"), row.ToJsonString() + "\n");
    private string Manifest()
    {
        using var bitmap = new SKBitmap(30, 20); bitmap.Erase(SKColors.White);
        using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100); byte[] bytes = encoded.ToArray();
        File.WriteAllBytes(Path.Combine(_root, "crop.png"), bytes);
        var path = Path.Combine(_root, "manifest.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            protocol = "VLM-BENCH-1", datasetId = "json-fixture", datasetVersion = 1, runPurpose = "exploratory",
            samples = new[] { new { sampleId = "line-1", imagePath = "crop.png", imageSha256 = Hash(bytes), physicalUnit = "unit-1",
                material = "synthetic", reviewState = "synthetic", exposure = "generated", rightsEvidenceRef = "generated", sourceGeometryRef = "whole", baselineText = "αβ" } },
        }));
        return path;
    }
    private string References(string text = "αβ", bool blank = false)
    {
        var path = Path.Combine(_root, Guid.NewGuid() + ".references.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            protocol = "VLM-BENCH-1", datasetId = "json-fixture", datasetVersion = 1,
            references = new[] { new { sampleId = "line-1", physicalUnit = "unit-1", material = "synthetic", referenceText = text,
                controlKind = blank ? "NoCharacterTraces" : "NaturalLine" } },
        }));
        return path;
    }
    private string[] Args(string output, string? format, int repetitions = 1, string mode = "image-only") =>
        ["vlm", Manifest(), "--model", "fixture-model", "--out", output, "--mode", mode, "--repetitions", repetitions.ToString(),
            .. format is null ? Array.Empty<string>() : new[] { "--response-format", format }];
    private static HttpResponseMessage Completion(string text, string finish = "stop") => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new { model = "fixture-model", choices = new[] { new { message = new { content = text }, finish_reason = finish } } }), Encoding.UTF8, "application/json"),
    };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> run) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => run(request, token);
    }
    private async Task<string> Run(string text, string? format = "transcription-json-v1", string finish = "stop", int repetitions = 1, string mode = "image-only")
    {
        string output = Path.Combine(_root, Guid.NewGuid().ToString());
        using var handler = new Handler((_, _) => Task.FromResult(Completion(text, finish)));
        Assert.Equal(finish == "stop" ? 0 : 3, await VlmBenchmarkCommand.RunAsync(Args(output, format, repetitions, mode), handler));
        return output;
    }
    private async Task<JsonObject> Score(string directory, string text = "αβ", bool blank = false)
    {
        var path = Path.Combine(_root, Guid.NewGuid() + ".score.json");
        Assert.Equal(0, await VlmBenchmarkCommand.RunAsync(["vlm-score", directory, "--references", References(text, blank), "--out", path]));
        return JsonNode.Parse(File.ReadAllBytes(path))!.AsObject();
    }
    [Fact]
    public async Task Structured_option_supports_a_valid_greek_object_with_a_separate_decoded_score()
    {
        var output = await Run("{\"transcription\":\"αβ\"}");
        var score = await Score(output);
        Assert.Equal("VLM-TRANSCRIPTION-JSON-1", score["outputContractVersion"]!.GetValue<string>());
        Assert.Equal(0, score["reports"]![0]!["strictDerivedScore"]!["overall"]!["microCer"]!.GetValue<double>());
        Assert.True(score["reports"]![0]!["rawRepresentationScore"]!["overall"]!["microCer"]!.GetValue<double>() > 0);
    }
    [Fact]
    public async Task Legacy_default_still_scores_the_complete_plain_text()
    {
        var output = await Run("αβ", format: null);
        var score = await Score(output);
        Assert.Null(score["outputContractVersion"]);
        Assert.Equal(0, score["reports"]![0]!["score"]!["overall"]!["microCer"]!.GetValue<double>());
        Assert.Null(Row(output)["requestFormatAudit"]);
    }

    [Theory]
    [InlineData("image-only")]
    [InlineData("ocr-assisted")]
    public async Task Explicit_plain_text_matches_default_bytes_and_structured_audit_matches_actual_bytes(string mode)
    {
        var bodies = new List<byte[]>(); var outputs = new List<string>();
        foreach (var format in new string?[] { null, "plain-text", "transcription-json-v1" })
        {
            var output = Path.Combine(_root, Guid.NewGuid().ToString()); outputs.Add(output);
            using var handler = new Handler(async (request, token) =>
            { bodies.Add(await request.Content!.ReadAsByteArrayAsync(token)); return Completion("{\"transcription\":\"αβ\"}"); });
            Assert.Equal(0, await VlmBenchmarkCommand.RunAsync(Args(output, format, mode: mode), handler));
        }
        Assert.Equal(bodies[0], bodies[1]);
        Assert.Equal(VlmBenchmarkCommand.PromptVersion, Node(outputs[0], "run-manifest.json")["promptVersion"]!.GetValue<string>());
        Assert.Null(Node(outputs[1], "run-manifest.json")["responseFormat"]);
        Assert.Null(Row(outputs[1])["requestFormatAudit"]);
        var run = Node(outputs[2], "run-manifest.json"); var row = Row(outputs[2]); var audit = row["requestFormatAudit"]!;
        using var wire = JsonDocument.Parse(bodies[2]);
        Assert.Equal(run["responseSchemaJson"]!.GetValue<string>(), wire.RootElement.GetProperty("response_format").GetRawText());
        Assert.Equal(Hash(bodies[2]), audit["requestSha256"]!.GetValue<string>());
        Assert.Equal(bodies[2].Length, audit["requestBytes"]!.GetValue<int>());
        Assert.Equal(row["result"]!["requestSha256"]!.GetValue<string>(), audit["requestSha256"]!.GetValue<string>());
        Assert.Equal(run["responseSchemaSha256"]!.GetValue<string>(), audit["schemaSha256"]!.GetValue<string>());
        Assert.NotEqual(VlmBenchmarkCommand.PromptVersion, run["promptVersion"]!.GetValue<string>());
        Assert.Equal("transcription-json-v1", audit["format"]!.GetValue<string>());
        Assert.Equal(0, (await Score(outputs[2]))["reports"]![0]!["strictDerivedScore"]!["overall"]!["microCer"]!.GetValue<double>());
    }

    [Theory]
    [InlineData("unsupported")]
    [InlineData("")]
    public async Task Unknown_format_fails_before_output_or_http(string format)
    {
        var output = Path.Combine(_root, "no-output"); int calls = 0;
        using var handler = new Handler((_, _) => { calls++; return Task.FromResult(Completion("")); });
        await Assert.ThrowsAsync<ArgumentException>(() => VlmBenchmarkCommand.RunAsync(Args(output, format), handler));
        Assert.Equal(0, calls); Assert.False(Directory.Exists(output));
    }

    [Theory]
    [InlineData("{\"text\":\"αβ\"}")]
    [InlineData("{\"transcription\":7}")]
    [InlineData("{\"transcription\":null}")]
    [InlineData("{\"transcription\":\"αβ\",\"extra\":true}")]
    [InlineData("{\"transcription\":\"αβ\",\"transcription\":\"αβ\"}")]
    [InlineData("{\"transcription\":\"αβ\",\"\\u0074ranscription\":\"αβ\"}")]
    [InlineData("[\"αβ\"]")]
    [InlineData("```json\n{\"transcription\":\"αβ\"}\n```")]
    [InlineData("{\"transcription\":\"αβ\"} trailing")]
    [InlineData("{\"transcription\":\"αβ")]
    [InlineData("{\"transcription\":\"\\uD800\"}")]
    public async Task Invalid_schema_preserves_all_raw_text_and_is_not_a_derived_success(string text)
    {
        var output = await Run(text); var score = await Score(output); var report = score["reports"]![0]!;
        Assert.Equal(text, Row(output)["text"]!.GetValue<string>());
        Assert.Equal("Completed", Row(output)["status"]!.GetValue<string>());
        Assert.Equal(0, report["schemaValidCount"]!.GetValue<int>());
        Assert.Equal(0, report["strictDerivedScore"]!["overall"]!["validCompletedCount"]!.GetValue<int>());
        Assert.Equal(report["rawRepresentationScore"]!["overall"]!["hypothesisScalars"]!.GetValue<int>(),
            report["strictDerivedScore"]!["overall"]!["hypothesisScalars"]!.GetValue<int>());
        foreach (var field in new[] { "insertions", "deletions", "substitutions" })
            Assert.Equal(report["rawRepresentationScore"]!["overall"]![field]!.GetValue<int>(), report["strictDerivedScore"]!["overall"]![field]!.GetValue<int>());
        Assert.False(report["contentFormatAssessment"]![0]!["schemaValid"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Decoded_unicode_is_exact_while_the_score_is_explicitly_NFC()
    {
        const string text = "α\u0313\u0301𐅀 ";
        var output = await Run(JsonSerializer.Serialize(new { transcription = text }));
        var report = (await Score(output, text))["reports"]![0]!;
        Assert.Equal(text, report["contentFormatAssessment"]![0]!["decodedTranscription"]!.GetValue<string>());
        Assert.Equal(0, report["strictDerivedScore"]!["overall"]!["microCer"]!.GetValue<double>());
        Assert.Equal("VLM-NFC-SCALAR-1", report["strictDerivedScore"]!["policyVersion"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("{\"transcription\":\"\"}", "stop", true)]
    [InlineData("{\"transcription\":\"α\"}", "stop", false)]
    [InlineData("", "stop", false)]
    [InlineData("{\"transcription\":\"\"}", "length", false)]
    public async Task Blank_success_requires_completed_valid_empty_transcription(string text, string finish, bool succeeds)
    {
        var output = await Run(text, finish: finish); var score = await Score(output, "", blank: true);
        Assert.Equal(succeeds ? 1 : 0, score["reports"]![0]!["strictDerivedScore"]!["controls"]![0]!["passed"]!.GetValue<int>());
    }

    [Theory]
    [InlineData("{\"transcription\":\"αβ\"}", true)]
    [InlineData("{\"transcription\":\"αβ", false)]
    public async Task Truncated_text_is_retained_and_never_promoted_to_completed(string text, bool schemaValid)
    {
        var output = await Run(text, finish: "length"); var report = (await Score(output))["reports"]![0]!;
        Assert.Equal(text, Row(output)["text"]!.GetValue<string>());
        Assert.Equal("Truncated", Row(output)["status"]!.GetValue<string>());
        Assert.Equal(schemaValid ? 1 : 0, report["schemaValidCount"]!.GetValue<int>());
        Assert.Equal(0, report["completedSchemaValidCount"]!.GetValue<int>());
        Assert.Equal(0, report["strictDerivedScore"]!["overall"]!["validCompletedCount"]!.GetValue<int>());
        Assert.True(report["strictDerivedScore"]!["overall"]!["hypothesisScalars"]!.GetValue<int>() > 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancellation_retains_full_plan_without_observed_blank_output(bool beforeHandler)
    {
        var output = Path.Combine(_root, "cancelled"); using var cancellation = new CancellationTokenSource();
        int calls = 0;
        using var handler = new Handler((_, token) =>
        { calls++; cancellation.Cancel(); token.ThrowIfCancellationRequested(); return Task.FromResult(Completion("")); });
        if (beforeHandler) cancellation.Cancel();
        Assert.Equal(3, await VlmBenchmarkCommand.RunAsync(Args(output, "transcription-json-v1", 3), handler, cancellation.Token));
        Assert.Equal(beforeHandler ? 0 : 1, calls);
        var reports = (await Score(output))["reports"]!.AsArray(); Assert.Equal(3, reports.Count);
        Assert.All(reports, r =>
        {
            Assert.False(r!["contentFormatAssessment"]![0]!["captured"]!.GetValue<bool>());
            Assert.Equal(2, r["strictDerivedScore"]!["overall"]!["deletions"]!.GetValue<int>());
            Assert.Equal(1, r["strictDerivedScore"]!["overall"]!["naturalLineCount"]!.GetValue<int>());
        });
        if (beforeHandler) Assert.Null(Row(output)["requestFormatAudit"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_rows_or_file_keep_every_planned_denominator(bool missingFile)
    {
        var output = await Run("{\"transcription\":\"αβ\"}", repetitions: 3);
        var path = Path.Combine(output, "results.jsonl");
        if (missingFile) File.Delete(path); else File.WriteAllLines(path, File.ReadAllLines(path).Take(1));
        var score = await Score(output);
        Assert.Equal(missingFile ? 3 : 2, score["missingResults"]!.GetValue<int>());
        Assert.Equal(3, score["reports"]!.AsArray().Sum(r => r!["strictDerivedScore"]!["overall"]!["naturalLineCount"]!.GetValue<int>()));
    }

    [Theory]
    [InlineData("format")]
    [InlineData("version")]
    [InlineData("schema")]
    [InlineData("missing-contract")]
    [InlineData("audit-schema")]
    [InlineData("audit-hash")]
    [InlineData("audit-size")]
    [InlineData("missing-audit")]
    [InlineData("legacy-metadata")]
    public async Task Contradictory_format_or_request_metadata_is_rejected(string mutation)
    {
        var output = await Run("{\"transcription\":\"αβ\"}", format: mutation == "legacy-metadata" ? null : "transcription-json-v1");
        var run = Node(output, "run-manifest.json"); var row = Row(output);
        switch (mutation)
        {
            case "format": run["responseFormat"] = "plain-text"; break;
            case "version": run["promptVersion"] = VlmBenchmarkCommand.PromptVersion; break;
            case "schema": run["responseSchemaJson"] = "{}"; break;
            case "missing-contract": run.Remove("outputContractVersion"); break;
            case "audit-schema": row["requestFormatAudit"]!["schemaSha256"] = new string('0', 64); break;
            case "audit-hash": row["requestFormatAudit"]!["requestSha256"] = new string('0', 64); break;
            case "audit-size": row["requestFormatAudit"]!["requestBytes"] = 0; break;
            case "missing-audit": row.Remove("requestFormatAudit"); break;
            case "legacy-metadata": run["responseFormat"] = "transcription-json-v1"; break;
        }
        File.WriteAllText(Path.Combine(output, "run-manifest.json"), run.ToJsonString()); WriteRow(output, row);
        await Assert.ThrowsAsync<InvalidDataException>(() => Score(output));
    }

    [Theory]
    [InlineData("httpStatusCode", "200")]
    [InlineData("capturedResponseBytes", "1")]
    [InlineData("rawResponseComplete", "true")]
    [InlineData("firstByteMilliseconds", "1")]
    [InlineData("firstTokenMilliseconds", "1")]
    [InlineData("inputTokens", "0")]
    [InlineData("outputTokens", "0")]
    [InlineData("responseModelId", "\"model\"")]
    [InlineData("finishReason", "\"stop\"")]
    [InlineData("rawResponse", "null")]
    [InlineData("rawResponseSha256", "\"unexpected-body-digest\"")]
    public async Task Auditless_cancelled_receipt_cannot_contain_response_evidence(string field, string json)
    {
        var output = await Run("{\"transcription\":\"αβ\"}"); var row = Row(output); var result = row["result"]!.AsObject();
        MakeEarlyCancellation(row);
        result[field] = JsonNode.Parse(json); WriteRow(output, row);
        await Assert.ThrowsAsync<InvalidDataException>(() => Score(output));
    }
    private static void MakeEarlyCancellation(JsonObject row)
    {
        var result = row["result"]!.AsObject();
        row.Remove("requestFormatAudit"); row["status"] = "Cancelled"; row["text"] = "";
        result["status"] = "cancelled"; result["text"] = ""; result["rawResponse"] = ""; result["capturedResponseBytes"] = 0;
        result["rawResponseComplete"] = false; result["rawResponseSha256"] = Hash([]);
        foreach (var name in new[] { "httpStatusCode", "firstByteMilliseconds", "firstTokenMilliseconds", "inputTokens", "outputTokens", "responseModelId", "finishReason" }) result[name] = null;
    }
    [Fact]
    public async Task Early_cancellation_can_retain_encoded_request_and_empty_response_hash_without_claiming_capture()
    {
        var output = await Run("{\"transcription\":\"αβ\"}"); var row = Row(output);
        MakeEarlyCancellation(row); WriteRow(output, row);
        Assert.NotNull(row["result"]!["requestSha256"]);
        var report = (await Score(output))["reports"]![0]!;
        Assert.False(report["contentFormatAssessment"]![0]!["captured"]!.GetValue<bool>());
        Assert.Equal(2, report["strictDerivedScore"]!["overall"]!["deletions"]!.GetValue<int>());
    }

    [Fact]
    public async Task Raw_plus_decoded_retention_is_bounded_before_output_and_exact_budget_succeeds()
    {
        const string raw = "{\"transcription\":\"αβ\"}"; var output = await Run(raw);
        string refs = References(), rejected = Path.Combine(_root, "over.json"), accepted = Path.Combine(_root, "exact.json");
        var method = typeof(VlmBenchmarkCommand).GetMethod("Score", BindingFlags.NonPublic | BindingFlags.Static)!;
        var error = Assert.Throws<TargetInvocationException>(() => method.Invoke(null,
            [new[] { "vlm-score", output, "--references", refs, "--out", rejected }, (long)raw.Length]));
        Assert.IsType<InvalidDataException>(error.InnerException); Assert.False(File.Exists(rejected));
        Assert.Equal(0, (int)method.Invoke(null,
            [new[] { "vlm-score", output, "--references", refs, "--out", accepted }, (long)raw.Length + 2])!);
        Assert.Equal(raw, Row(output)["text"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("schemaSha256")]
    [InlineData("requestSha256")]
    [InlineData("escaped-schema")]
    [InlineData("run-plan")]
    public async Task Duplicate_nested_receipt_properties_cannot_hide_a_conflicting_identity(string name)
    {
        var output = await Run("{\"transcription\":\"αβ\"}"); var row = Row(output);
        if (name == "run-plan")
        {
            var runPath = Path.Combine(output, "run-manifest.json");
            var run = File.ReadAllText(runPath);
            File.WriteAllText(runPath, run.Replace("\"sampleId\": \"line-1\"", "\"sampleId\": \"foreign\", \"sampleId\": \"line-1\"", StringComparison.Ordinal));
            await Assert.ThrowsAsync<InvalidDataException>(() => Score(output));
            return;
        }
        bool escaped = name == "escaped-schema";
        if (escaped) name = "schemaSha256";
        var original = File.ReadAllText(Path.Combine(output, "results.jsonl"));
        var value = row["requestFormatAudit"]![name]!.GetValue<string>();
        var token = JsonSerializer.Serialize(name) + ":" + JsonSerializer.Serialize(value);
        Assert.Contains(token, original);
        File.WriteAllText(Path.Combine(output, "results.jsonl"), original.Replace(token,
            (escaped ? "\"\\u0073chemaSha256\"" : JsonSerializer.Serialize(name)) + ":\"conflicting-first-value\"," + token, StringComparison.Ordinal));
        await Assert.ThrowsAsync<InvalidDataException>(() => Score(output));
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
