using System.Net;
using System.Text;
using LeviathanText.Contracts;
using Xunit;

namespace LeviathanText.AI.Tests;

public sealed class CredentialRotationRedactionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Credential_echo_cannot_become_a_proposal_or_generic_schema_failure_excerpt(bool structured, bool encoded)
    {
        const string key = "model-content-private";
        var source = new MutableCredentials(key);
        var escaped = string.Concat(key.Select(character => $"\\u{(int)character:x4}"));
        var content = encoded ? "{\"secret\":\"" + escaped + "\"}" : structured ? "not-json " + key : key;
        using var client = new HttpClient(new Handler((_, _) =>
        {
            source.Value = "unused-rotated-key";
            return Task.FromResult(Reply(System.Text.Json.JsonSerializer.Serialize(new
            {
                choices = new[] { new { message = new { content } } },
            })));
        }));
        var provider = new SchemaValidatingAiProvider(new AiHttpProvider(Settings, client, source));

        var outcome = await provider.ProposeAsync(Request with
        {
            Task = structured ? AiTaskKind.StructuredExtraction : AiTaskKind.CandidateReading,
        });

        Assert.False(outcome.Succeeded);
        Assert.Null(outcome.Proposal);
        Assert.Equal(AiFailureReasons.MalformedResponse, outcome.Failure!.Reason);
        Assert.Equal("[redacted]", outcome.Failure.RawResponseExcerpt);
        Assert.DoesNotContain(key, System.Text.Json.JsonSerializer.Serialize(outcome));
        Assert.DoesNotContain(escaped, System.Text.Json.JsonSerializer.Serialize(outcome));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Encoded_credential_echo_suppresses_the_excerpt_after_rotation(bool parseFailure)
    {
        const string key = "encoded-request-private";
        var escaped = string.Concat(key.Select(character => $"\\u{(int)character:x4}"));
        var source = new MutableCredentials(key);
        using var client = new HttpClient(new Handler((_, _) =>
        {
            source.Value = "unused-next-key";
            return Task.FromResult(Reply("{\"echo\":\"" + escaped + "\"}",
                parseFailure ? HttpStatusCode.OK : HttpStatusCode.Unauthorized));
        }));

        var error = await Assert.ThrowsAsync<AiProviderException>(() => new AiHttpProvider(Settings, client, source).GenerateRawAsync(Request));

        Assert.Equal("[redacted]", error.RedactedExcerpt);
        Assert.Equal(1, source.Resolutions);
    }

    [Fact]
    public async Task Short_exact_credential_is_redacted_and_long_control_character_excerpt_stays_bounded()
    {
        var source = new MutableCredentials("xyz");
        using var client = new HttpClient(new Handler((_, _) =>
            Task.FromResult(Reply("xyz\u0001\n" + new string('q', 400), HttpStatusCode.Unauthorized))));

        var error = await Assert.ThrowsAsync<AiProviderException>(() => new AiHttpProvider(Settings, client, source).GenerateRawAsync(Request));

        Assert.StartsWith("[redacted]\n", error.RedactedExcerpt);
        Assert.DoesNotContain("xyz", error.RedactedExcerpt);
        Assert.DoesNotContain('\u0001', error.RedactedExcerpt!);
        Assert.Equal(240, error.RedactedExcerpt!.Length);
    }

    [Fact]
    public async Task Typed_response_context_closes_on_dispose_and_legacy_convenience_still_returns_exact_body()
    {
        const string key = "typed-response-private";
        var source = new MutableCredentials(key);
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(Reply("body " + key))));
        var transport = new AiHttpTransport(Settings, client, source);
        var response = await transport.SendJsonWithContextAsync(() => new(HttpMethod.Post, "http://example.test"));
        Assert.Equal("body " + key, response.Body);
        source.Value = "rotated-away";
        Assert.Equal("body [redacted]", response.SanitizeForRecord(response.Body));
        response.Dispose();
        response.Dispose();
        Assert.Throws<ObjectDisposedException>(() => response.Body);
        Assert.Throws<ObjectDisposedException>(() => response.SanitizeForRecord(response.Body));
        Assert.Equal("body " + key, await transport.SendJsonAsync(() => new(HttpMethod.Post, "http://example.test")));
    }

    [Fact]
    public async Task All_five_retry_keys_are_redacted_without_leaving_a_longer_keys_suffix()
    {
        string[] keys = ["prefix-private", "prefix-private-extended", "third-private", "fourth-private", "fifth-private"];
        var source = new MutableCredentials(keys[0]);
        int calls = 0;
        using var client = new HttpClient(new Handler((message, _) =>
        {
            var index = calls++;
            Assert.Equal(keys[index], message.Headers.Authorization!.Parameter);
            source.Value = index < keys.Length - 1 ? keys[index + 1] : "unused-final-key";
            return Task.FromResult(Reply(string.Join(" ", keys), HttpStatusCode.InternalServerError));
        }));
        var provider = new AiHttpProvider(Settings with { MaxAttempts = 5 }, client, source, (_, _) => Task.CompletedTask);

        var error = await Assert.ThrowsAsync<AiProviderException>(() => provider.GenerateRawAsync(Request));

        Assert.All(keys, key => Assert.DoesNotContain(key, error.RedactedExcerpt));
        Assert.DoesNotContain("-extended", error.RedactedExcerpt);
        Assert.Equal(5, source.Resolutions);
    }

    private static AiProviderSettings Settings => new()
    {
        ProviderId = "rotation", ModelId = "fixture", Host = AiProviderHost.Local,
        Protocol = AiWireProtocol.OpenAiChatCompletions, Endpoint = new("http://example.test/v1"),
        CredentialEnvironmentVariable = "FIXTURE_KEY", MaxAttempts = 1, BaseRetryDelay = TimeSpan.Zero,
    };
    private static AiProposalRequest Request => new()
    {
        TargetRevisionId = new(EntityKind.TranscriptionRevision, "fixture"), Range = new(0, 1),
        Task = AiTaskKind.CandidateReading, SourceText = "α", PromptVersion = "fixture", ProviderConfigRef = "rotation",
    };
    private static HttpResponseMessage Reply(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rotated_credential_is_redacted_from_transport_and_post_response_parse_failures(bool parseFailure)
    {
        const string oldKey = "old-request-credential", nextKey = "new-environment-credential";
        var source = new MutableCredentials(oldKey);
        using var client = new HttpClient(new Handler((message, _) =>
        {
            Assert.Equal("Bearer", message.Headers.Authorization!.Scheme);
            Assert.Equal(oldKey, message.Headers.Authorization.Parameter);
            source.Value = nextKey;
            return Task.FromResult(Reply("{\"error\":\"Bearer " + oldKey + "\"}",
                parseFailure ? HttpStatusCode.OK : HttpStatusCode.Unauthorized));
        }));
        var provider = new AiHttpProvider(Settings, client, source);

        var error = await Assert.ThrowsAsync<AiProviderException>(() => provider.GenerateRawAsync(Request));

        Assert.Equal(parseFailure ? AiFailureReasons.MalformedResponse : AiFailureReasons.AuthenticationFailed, error.Reason);
        Assert.DoesNotContain(oldKey, error.RedactedExcerpt);
        Assert.Contains("[redacted]", error.RedactedExcerpt);
        Assert.InRange(error.RedactedExcerpt!.Length, 1, 240);
        Assert.Equal(1, source.Resolutions);
    }

    [Fact]
    public async Task Retry_context_redacts_all_attempt_credentials_in_final_parse_error()
    {
        const string first = "retry-first-credential", second = "retry-second-credential";
        var source = new MutableCredentials(first);
        int calls = 0;
        using var client = new HttpClient(new Handler((message, _) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                Assert.Equal(first, message.Headers.Authorization!.Parameter);
                source.Value = second;
                return Task.FromResult(Reply("{}", HttpStatusCode.InternalServerError));
            }
            Assert.Equal(second, message.Headers.Authorization!.Parameter);
            source.Value = "third-unused-credential";
            return Task.FromResult(Reply("{\"error\":\"" + first + " " + second + "\"}"));
        }));
        var provider = new AiHttpProvider(Settings with { MaxAttempts = 2 }, client, source, (_, _) => Task.CompletedTask);

        var error = await Assert.ThrowsAsync<AiProviderException>(() => provider.GenerateRawAsync(Request));

        Assert.Equal(AiFailureReasons.MalformedResponse, error.Reason);
        Assert.DoesNotContain(first, error.RedactedExcerpt);
        Assert.DoesNotContain(second, error.RedactedExcerpt);
        Assert.Equal(2, source.Resolutions);
    }

    [Fact]
    public async Task Cached_adapter_concurrent_calls_keep_credential_contexts_separate()
    {
        const string first = "request-one-private", second = "request-two-private";
        var source = new MutableCredentials(first);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new HttpClient(new Handler(async (message, _) =>
        {
            var used = message.Headers.Authorization!.Parameter;
            if (used == first) { entered.TrySetResult(); await release.Task; }
            // The other call's key is an unrelated canary: it must not be retained in this call's context.
            return Reply("{\"error\":\"used=" + used + ";canary=" + (used == first ? second : first) + "\"}");
        }));
        var registry = new AiProviderRegistry([Settings], client, source);
        var a = registry.RawProviderFor("rotation").GenerateRawAsync(Request);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            source.Value = second;
            var b = await Assert.ThrowsAsync<AiProviderException>(() => registry.RawProviderFor("rotation").GenerateRawAsync(Request));
            Assert.DoesNotContain(second, b.RedactedExcerpt);
            Assert.Contains(first, b.RedactedExcerpt);
        }
        finally { release.TrySetResult(); }
        var errorA = await Assert.ThrowsAsync<AiProviderException>(() => a);
        Assert.DoesNotContain(first, errorA.RedactedExcerpt);
        Assert.Contains(second, errorA.RedactedExcerpt);
        Assert.Equal(2, source.Resolutions);
    }

    [Fact]
    public async Task Model_catalog_parse_errors_use_the_dispatched_credential()
    {
        const string key = "catalog-request-private";
        var source = new MutableCredentials(key);
        using var client = new HttpClient(new Handler((_, _) =>
        {
            source.Value = "unused-new-key";
            return Task.FromResult(Reply("{\"error\":\"" + key + "\"}"));
        }));
        var catalog = new AiHttpModelCatalog(Settings, client, source);

        var error = await Assert.ThrowsAsync<AiProviderException>(() => catalog.ListModelsAsync());

        Assert.DoesNotContain(key, error.RedactedExcerpt);
        Assert.Contains("[redacted]", error.RedactedExcerpt);
        Assert.Equal(1, source.Resolutions);
    }

    [Fact]
    public async Task Ollama_truncation_parse_error_keeps_its_credential_context()
    {
        const string key = "ollama-request-private";
        var source = new MutableCredentials(key);
        using var client = new HttpClient(new Handler((_, _) =>
        {
            source.Value = "new-unused-ollama-key";
            return Task.FromResult(Reply("{\"message\":{\"content\":\"\"},\"done_reason\":\"length\",\"echo\":\"" + key + "\"}"));
        }));
        var provider = new AiHttpProvider(Settings with { Protocol = AiWireProtocol.OllamaChat }, client, source);

        var error = await Assert.ThrowsAsync<AiProviderException>(() => provider.GenerateRawAsync(Request));

        Assert.Equal(AiFailureReasons.ResponseTruncated, error.Reason);
        Assert.DoesNotContain(key, error.RedactedExcerpt);
        Assert.Contains("[redacted]", error.RedactedExcerpt);
        Assert.Equal(1, source.Resolutions);
    }

    private sealed class MutableCredentials(string value) : IAiCredentialSource
    {
        private int _resolutions;
        public string Value { get; set; } = value;
        public int Resolutions => Volatile.Read(ref _resolutions);
        public string Resolve(string reference) { Interlocked.Increment(ref _resolutions); return Value; }
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
