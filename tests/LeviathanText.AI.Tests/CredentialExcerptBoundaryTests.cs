using System.Net;
using System.Text;
using LeviathanText.Contracts;
using Xunit;

namespace LeviathanText.AI.Tests;

public sealed class CredentialExcerptBoundaryTests
{
    [Fact]
    public async Task Unknown_length_short_reads_are_filled_before_sanitizing_a_split_credential()
    {
        const string key = "private-split-credential";
        var stream = new ChunkedStream(Encoding.UTF8.GetBytes("prefix " + key + " suffix"), chunk: 12);

        var error = await Failure(stream, key);

        Assert.Equal("prefix [redacted] suffix", error.RedactedExcerpt);
        Assert.True(stream.Reads > 1);
    }

    [Fact]
    public async Task Unknown_length_long_credential_crossing_limit_suppresses_the_whole_excerpt()
    {
        var key = "private-" + new string('k', 600);
        var stream = new ChunkedStream(Encoding.UTF8.GetBytes("diagnostic " + key + " suffix"), chunk: 512);

        var error = await Failure(stream, key);

        Assert.Equal("[redacted]", error.RedactedExcerpt);
        Assert.Equal(513, stream.BytesRead);
    }

    [Fact]
    public async Task Declared_oversized_credential_response_is_suppressed_without_reading_body()
    {
        var stream = new ChunkedStream(Encoding.UTF8.GetBytes("body not needed"), chunk: 3);

        var error = await Failure(stream, "private-key", declaredLength: 600);

        Assert.Equal("[redacted]", error.RedactedExcerpt);
        Assert.Equal(0, stream.BytesRead);
    }

    [Fact]
    public async Task Complete_exact_limit_response_is_sanitized_after_observing_eof()
    {
        const string key = "private-exact-key";
        var body = "prefix " + key + new string('q', 512 - 7 - key.Length);
        var stream = new ChunkedStream(Encoding.UTF8.GetBytes(body), chunk: 5);

        var error = await Failure(stream, key);

        Assert.StartsWith("prefix [redacted]", error.RedactedExcerpt);
        Assert.Equal(240, error.RedactedExcerpt!.Length);
        Assert.Equal(512, stream.BytesRead);
        Assert.True(stream.ObservedEof);
    }

    [Fact]
    public async Task Keyless_oversized_response_keeps_its_bounded_diagnostic_prefix()
    {
        var stream = new ChunkedStream(Encoding.UTF8.GetBytes("diagnostic details " + new string('q', 600)), chunk: 7);

        var error = await Failure(stream, key: null);

        Assert.StartsWith("diagnostic details ", error.RedactedExcerpt);
        Assert.Equal(240, error.RedactedExcerpt!.Length);
        Assert.Equal(513, stream.BytesRead);
    }

    private static async Task<AiProviderException> Failure(ChunkedStream stream, string? key, long? declaredLength = null)
    {
        var settings = new AiProviderSettings
        {
            ProviderId = "excerpt-boundary", ModelId = "fixture", Host = AiProviderHost.Local,
            Protocol = AiWireProtocol.OpenAiChatCompletions, Endpoint = new("http://example.test/v1"),
            CredentialEnvironmentVariable = key is null ? null : "FIXTURE_KEY", MaxAttempts = 1,
        };
        using var content = new StreamContent(stream);
        if (declaredLength is { } length) content.Headers.ContentLength = length;
        using var client = new HttpClient(new Handler(content));
        var provider = new AiHttpProvider(settings, client,
            new StaticCredentialSource(key is null ? new Dictionary<string, string>() : new() { ["FIXTURE_KEY"] = key }));
        var request = new AiProposalRequest
        {
            TargetRevisionId = new(EntityKind.TranscriptionRevision, "fixture"), Range = new(0, 1),
            Task = AiTaskKind.CandidateReading, SourceText = "α", PromptVersion = "fixture", ProviderConfigRef = settings.ProviderId,
        };
        var error = await Assert.ThrowsAsync<AiProviderException>(() => provider.GenerateRawAsync(request));
        Assert.Equal(AiFailureReasons.AuthenticationFailed, error.Reason);
        return error;
    }

    private sealed class Handler(HttpContent content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = content });
    }

    private sealed class ChunkedStream(byte[] bytes, int chunk) : MemoryStream(bytes, writable: false)
    {
        public override bool CanSeek => false;
        public int Reads { get; private set; }
        public int BytesRead { get; private set; }
        public bool ObservedEof { get; private set; }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Reads++;
            int read = base.Read(buffer.Span[..Math.Min(buffer.Length, chunk)]);
            BytesRead += read;
            ObservedEof |= read == 0;
            return ValueTask.FromResult(read);
        }
    }
}
