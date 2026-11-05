using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LeviathanText.Cli.Tests;

public sealed class VlmCredentialTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lt-vlm-auth", Guid.NewGuid().ToString("N"));
    private readonly string _name = "LEVIATHAN_TEST_TOKEN_" + Guid.NewGuid().ToString("N");
    private const string Dummy = "dummy-sentinel-token-for-tests";
    public VlmCredentialTests() => Directory.CreateDirectory(_root);
    private string Manifest()
    {
        using var image = new Image<L8>(2, 1, new L8(255)); var path = Path.Combine(_root, "image.png"); image.SaveAsPng(path);
        string manifest = Path.Combine(_root, "manifest.json");
        File.WriteAllText(manifest, JsonSerializer.Serialize(new
        {
            protocol = "VLM-BENCH-1", datasetId = "auth-mock", datasetVersion = 1, runPurpose = "exploratory",
            samples = new[] { new { sampleId = "line", imagePath = path, imageSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))),
                physicalUnit = "unit", material = "synthetic", reviewState = "synthetic", exposure = "generated", rightsEvidenceRef = "generated", sourceGeometryRef = "whole" } },
        }));
        return manifest;
    }
    private string[] Args(string output, string? credential = null, string format = "plain-text", int repetitions = 1) =>
        ["vlm", Manifest(), "--model", "auth-model", "--out", output, "--repetitions", repetitions.ToString(), "--response-format", format,
            .. credential is null ? Array.Empty<string>() : new[] { "--credential-env", credential }];
    private static HttpResponseMessage Completion(string text) => new(HttpStatusCode.OK)
    { Content = new StringContent(JsonSerializer.Serialize(new { model = "auth-model", choices = new[] { new { message = new { content = text }, finish_reason = "stop" } } }), Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => callback(request, token); }
    private async Task<JsonObject> Score(string output)
    {
        string references = Path.Combine(_root, "references.json"), score = Path.Combine(_root, Guid.NewGuid() + ".score.json");
        File.WriteAllText(references, JsonSerializer.Serialize(new
        {
            protocol = "VLM-BENCH-1", datasetId = "auth-mock", datasetVersion = 1,
            references = new[] { new { sampleId = "line", physicalUnit = "unit", material = "synthetic", referenceText = "αβ", controlKind = "NaturalLine" } },
        }));
        Assert.Equal(0, await VlmBenchmarkCommand.RunAsync(["vlm-score", output, "--references", references, "--out", score]));
        return JsonNode.Parse(File.ReadAllBytes(score))!.AsObject();
    }
    private static JsonObject Row(string output) => JsonNode.Parse(File.ReadAllLines(Path.Combine(output, "results.jsonl"))[0])!.AsObject();

    [Fact]
    public async Task Explicit_environment_reference_sends_a_header_only_dummy_credential()
    {
        Environment.SetEnvironmentVariable(_name, Dummy); int calls = 0;
        using var handler = new Handler(async (request, token) =>
        {
            calls++; Assert.Equal("Bearer " + Dummy, request.Headers.Authorization!.ToString());
            Assert.DoesNotContain(Dummy, await request.Content!.ReadAsStringAsync(token)); return Completion("αβ");
        });
        string output = Path.Combine(_root, "run");
        Assert.Equal(0, await VlmBenchmarkCommand.RunAsync(Args(output, _name), handler));
        Assert.Equal(1, calls);
        Assert.All(Directory.EnumerateFiles(output), p => Assert.DoesNotContain(Dummy, File.ReadAllText(p)));
    }
    [Fact]
    public async Task Omitted_reference_remains_keyless()
    {
        Environment.SetEnvironmentVariable(_name, Dummy);
        using var handler = new Handler((request, _) =>
        { Assert.Null(request.Headers.Authorization); return Task.FromResult(Completion("αβ")); });
        Assert.Equal(0, await VlmBenchmarkCommand.RunAsync(Args(Path.Combine(_root, "keyless")), handler));
    }

    [Theory]
    [InlineData("plain-text")]
    [InlineData("transcription-json-v1")]
    public async Task Changing_only_the_dummy_credential_does_not_change_body_or_format_audit(string format)
    {
        var bodies = new List<byte[]>(); var rows = new List<JsonObject>();
        foreach (var tokenValue in new[] { Dummy, Dummy + "-other" })
        {
            Environment.SetEnvironmentVariable(_name, tokenValue);
            using var handler = new Handler(async (request, token) =>
            {
                Assert.Equal("Bearer " + tokenValue, request.Headers.Authorization!.ToString());
                bodies.Add(await request.Content!.ReadAsByteArrayAsync(token));
                return Completion(format == "plain-text" ? "αβ" : "{\"transcription\":\"αβ\"}");
            });
            string output = Path.Combine(_root, Guid.NewGuid().ToString());
            Assert.Equal(0, await VlmBenchmarkCommand.RunAsync(Args(output, _name, format), handler));
            rows.Add(Row(output));
            var run = JsonNode.Parse(File.ReadAllText(Path.Combine(output, "run-manifest.json")))!;
            Assert.Equal("bearer-environment", run["authentication"]!["mode"]!.GetValue<string>());
            Assert.Equal(_name, run["authentication"]!["environmentVariable"]!.GetValue<string>());
            Assert.Null((await Score(output))["credentialRedaction"]);
            Assert.All(Directory.EnumerateFiles(output), p => Assert.DoesNotContain(tokenValue, File.ReadAllText(p)));
        }
        Assert.Equal(bodies[0], bodies[1]);
        Assert.Equal(rows[0]["result"]!["requestSha256"]!.GetValue<string>(), rows[1]["result"]!["requestSha256"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(rows[0]["requestFormatAudit"], rows[1]["requestFormatAudit"]));
    }

    [Theory]
    [InlineData("plain-text", null)]
    [InlineData("plain-text", "")]
    [InlineData("plain-text", "   ")]
    [InlineData("transcription-json-v1", null)]
    [InlineData("transcription-json-v1", "")]
    [InlineData("transcription-json-v1", "   ")]
    public async Task Missing_or_blank_key_keeps_every_configuration_failure_without_a_request(string format, string? value)
    {
        Environment.SetEnvironmentVariable(_name, value); int calls = 0;
        using var handler = new Handler((_, _) => { calls++; return Task.FromResult(Completion("unexpected")); });
        string output = Path.Combine(_root, "missing-key");
        Assert.Equal(3, await VlmBenchmarkCommand.RunAsync(Args(output, _name, format, 3), handler));
        Assert.Equal(0, calls);
        var rows = File.ReadAllLines(Path.Combine(output, "results.jsonl")).Select(line => JsonNode.Parse(line)!).ToArray();
        Assert.Equal(3, rows.Length);
        Assert.All(rows, row => { Assert.Equal("ConfigurationError", row["status"]!.GetValue<string>()); Assert.Null(row["requestFormatAudit"]); });
        var score = await Score(output); Assert.Equal(3, score["recordedResults"]!.GetValue<int>());
        Assert.All(score["reports"]!.AsArray(), report =>
        {
            string key = format == "plain-text" ? "score" : "strictDerivedScore";
            Assert.Equal(2, report![key]!["overall"]!["deletions"]!.GetValue<int>());
            Assert.Equal(0, report[key]!["overall"]!["validCompletedCount"]!.GetValue<int>());
            if (format != "plain-text") Assert.False(report["contentFormatAssessment"]![0]!["captured"]!.GetValue<bool>());
        });
    }

    [Theory]
    [InlineData("plain-text", "literal")]
    [InlineData("plain-text", "outer-escaped")]
    [InlineData("transcription-json-v1", "literal")]
    [InlineData("transcription-json-v1", "outer-escaped")]
    [InlineData("transcription-json-v1", "nested")]
    public async Task Echo_redaction_is_disclosed_and_no_secret_is_reconstructed_in_any_saved_file(string format, string kind)
    {
        Environment.SetEnvironmentVariable(_name, Dummy);
        string modelText = format == "plain-text" ? Dummy : JsonSerializer.Serialize(new { transcription = Dummy });
        if (kind == "nested") modelText = "{\"transcription\":\"\\u0064ummy-sentinel-token-for-tests\"}";
        string wire = JsonSerializer.Serialize(new { model = "auth-model", choices = new[] { new { message = new { content = modelText }, finish_reason = "stop" } } });
        if (kind == "outer-escaped") wire = wire.Replace(Dummy, "\\u0064ummy-sentinel-token-for-tests", StringComparison.Ordinal);
        using var handler = new Handler((request, _) =>
        {
            Assert.Equal("Bearer " + Dummy, request.Headers.Authorization!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(wire, Encoding.UTF8, "application/json") });
        });
        string output = Path.Combine(_root, "echo");
        Assert.Equal(0, await VlmBenchmarkCommand.RunAsync(Args(output, _name, format), handler));
        Assert.True(Row(output)["result"]!["credentialsRedacted"]!.GetValue<bool>());
        var score = await Score(output);
        Assert.Equal(1, score["credentialRedaction"]!["redactedResultCount"]!.GetValue<int>());
        Assert.Equal(1, score["reports"]![0]!["credentialsRedactedCount"]!.GetValue<int>());
        Assert.Contains("not byte-exact", score["credentialRedaction"]!["textFidelity"]!.GetValue<string>());
        Assert.Equal(1, score["plannedRequests"]!.GetValue<int>());
        foreach (var path in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories).Where(p => p.EndsWith(".json", StringComparison.Ordinal) || p.EndsWith(".jsonl", StringComparison.Ordinal)))
        {
            string content = File.ReadAllText(path);
            Assert.DoesNotContain(Dummy, content); Assert.DoesNotContain("\\u0064ummy-sentinel-token-for-tests", content);
        }
    }

    [Theory]
    [InlineData("plain-text")]
    [InlineData("transcription-json-v1")]
    public async Task Unauthorized_response_is_one_failed_attempt_without_keyless_fallback(string format)
    {
        Environment.SetEnvironmentVariable(_name, Dummy); int calls = 0;
        using var handler = new Handler((request, _) =>
        {
            calls++; Assert.Equal("Bearer " + Dummy, request.Headers.Authorization!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            { Content = new StringContent(JsonSerializer.Serialize(new { error = Dummy }), Encoding.UTF8, "application/json") });
        });
        string output = Path.Combine(_root, "unauthorized");
        Assert.Equal(3, await VlmBenchmarkCommand.RunAsync(Args(output, _name, format), handler));
        Assert.Equal(1, calls); Assert.Equal("HttpError", Row(output)["status"]!.GetValue<string>());
        Assert.Equal("", Row(output)["text"]!.GetValue<string>());
        Assert.Equal(1, (await Score(output))["credentialRedaction"]!["redactedResultCount"]!.GetValue<int>());
        Assert.All(Directory.EnumerateFiles(output), p => Assert.DoesNotContain(Dummy, File.ReadAllText(p)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1BAD")]
    [InlineData("BAD-NAME")]
    [InlineData("BAD=VALUE")]
    [InlineData("BAD\nNAME")]
    [InlineData("BAD NAME")]
    [InlineData("../TOKEN")]
    [InlineData("ΤΟΚΕΝ")]
    public async Task Invalid_reference_has_a_generic_error_before_output_or_http(string name)
    {
        int calls = 0; using var handler = new Handler((_, _) => { calls++; return Task.FromResult(Completion("")); });
        string output = Path.Combine(_root, "invalid");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => VlmBenchmarkCommand.RunAsync(Args(output, name), handler));
        Assert.Equal("Credential environment-variable reference is invalid.", error.Message);
        Assert.Equal(0, calls); Assert.False(Directory.Exists(output));
    }

    [Fact]
    public async Task Long_reference_is_rejected_and_offline_commands_do_not_accept_credentials()
    {
        string output = Path.Combine(_root, "invalid");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => VlmBenchmarkCommand.RunAsync(Args(output, new string('A', 129))));
        Assert.Equal("Credential environment-variable reference is invalid.", error.Message); Assert.False(Directory.Exists(output));
        foreach (var command in new[] { "vlm-validate", "vlm-score" })
            await Assert.ThrowsAsync<ArgumentException>(() => VlmBenchmarkCommand.RunAsync([command, "not-read", "--credential-env", _name]));
    }

    [Theory]
    [InlineData("mode")]
    [InlineData("reference")]
    [InlineData("extra")]
    [InlineData("duplicate")]
    [InlineData("escaped-duplicate")]
    [InlineData("root-duplicate")]
    [InlineData("null")]
    [InlineData("wrong-kind")]
    public async Task Malformed_authentication_metadata_is_rejected_offline(string mutation)
    {
        Environment.SetEnvironmentVariable(_name, Dummy);
        using var handler = new Handler((_, _) => Task.FromResult(Completion("αβ")));
        string output = Path.Combine(_root, "mutated");
        Assert.Equal(0, await VlmBenchmarkCommand.RunAsync(Args(output, _name), handler));
        Environment.SetEnvironmentVariable(_name, null);
        string path = Path.Combine(output, "run-manifest.json"); var run = JsonNode.Parse(File.ReadAllText(path))!;
        switch (mutation)
        {
            case "mode": run["authentication"]!["mode"] = "automatic"; break;
            case "reference": run["authentication"]!["environmentVariable"] = "BAD=VALUE"; break;
            case "extra": run["authentication"]!["extra"] = true; break;
            case "null": run["authentication"] = null; break;
            case "wrong-kind": run["authentication"]!["environmentVariable"] = 123; break;
        }
        string text = run.ToJsonString();
        if (mutation == "duplicate") text = text.Replace("\"mode\":\"bearer-environment\"", "\"mode\":\"unknown\",\"mode\":\"bearer-environment\"", StringComparison.Ordinal);
        if (mutation == "escaped-duplicate") text = text.Replace("\"environmentVariable\":", "\"\\u0065nvironmentVariable\":\"BAD=VALUE\",\"environmentVariable\":", StringComparison.Ordinal);
        if (mutation == "root-duplicate") text = text.Replace("\"authentication\":", "\"authentication\":null,\"authentication\":", StringComparison.Ordinal);
        File.WriteAllText(path, text);
        await Assert.ThrowsAsync<InvalidDataException>(() => Score(output));
    }
    public void Dispose()
    {
        Environment.SetEnvironmentVariable(_name, null);
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
