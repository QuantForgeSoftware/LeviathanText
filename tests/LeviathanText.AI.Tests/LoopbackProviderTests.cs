using System.Net;
using System.Net.Sockets;
using System.Text;
using LeviathanText.AI;
using LeviathanText.Contracts;
using Xunit;

namespace LeviathanText.AI.Tests;

/// <summary>
/// Real HTTP over loopback (no external network): proves URL construction, auth headers,
/// request JSON, response parsing and retry behavior through an actual socket and
/// <see cref="HttpClient"/>, not only an injected message handler.
/// </summary>
public class LoopbackProviderTests
{
    private static readonly EntityId Target = new(EntityKind.TranscriptionRevision, "r1");

    private static AiProposalRequest Request() => new()
    {
        TargetRevisionId = Target,
        Range = new ScalarRange(0, 6),
        Task = AiTaskKind.CandidateReading,
        ProviderConfigRef = "loopback",
        PromptVersion = "greek-ai-1",
        SourceText = "κυριου",
    };

    private static AiProviderSettings OpenAiSettings(string baseUrl) => new()
    {
        ProviderId = "loopback-openai",
        Host = AiProviderHost.Hosted,
        Protocol = AiWireProtocol.OpenAiChatCompletions,
        Endpoint = new Uri($"{baseUrl}/v1"),
        ModelId = "greek-model",
        CredentialEnvironmentVariable = "LOOPBACK_KEY",
        MaxAttempts = 1,
        MaxOutputTokens = 64,
    };

    private static AiProviderSettings OllamaSettings(string baseUrl) => new()
    {
        ProviderId = "loopback-ollama",
        Host = AiProviderHost.Local,
        Protocol = AiWireProtocol.OllamaChat,
        Endpoint = new Uri(baseUrl),
        ModelId = "greek-local",
        MaxAttempts = 2,
        BaseRetryDelay = TimeSpan.Zero,
        MaxOutputTokens = 64,
    };

    private static StaticCredentialSource Credentials() =>
        new(new Dictionary<string, string> { ["LOOPBACK_KEY"] = "loopback-secret" });

    [Fact]
    public async Task OpenAi_chat_round_trips_over_a_real_socket()
    {
        await using var server = new Loopback(
            (200, """{"choices":[{"message":{"content":"κυρίου"}}],"usage":{"prompt_tokens":20,"completion_tokens":2}}"""));

        var provider = new AiHttpProvider(
            OpenAiSettings(server.BaseUrl), new HttpClient(), Credentials(),
            (_, _) => Task.CompletedTask);

        var outcome = await new SchemaValidatingAiProvider(provider).ProposeAsync(Request());

        Assert.True(outcome.Succeeded);
        Assert.Equal("κυρίου", outcome.Proposal!.ProposedText);
        var request = Assert.Single(server.Requests);
        Assert.Equal("POST", request.Method);
        Assert.Equal("/v1/chat/completions", request.Path);
        Assert.Equal("Bearer loopback-secret", request.Authorization);
        Assert.Contains("\"model\":\"greek-model\"", request.Body);
        Assert.Contains("κυριου", request.Body);
    }

    [Fact]
    public async Task Ollama_discovery_and_chat_work_without_a_key()
    {
        await using var server = new Loopback(
            (200, """{"models":[{"name":"greek-local","model":"greek-local","size":10}]}"""),
            (200, """{"message":{"content":"κυρίου"},"prompt_eval_count":20,"eval_count":2}"""));

        var settings = OllamaSettings(server.BaseUrl);
        var catalog = new AiHttpModelCatalog(settings, new HttpClient(), delay: (_, _) => Task.CompletedTask);
        var provider = new AiHttpProvider(settings, new HttpClient(), delay: (_, _) => Task.CompletedTask);

        var models = await catalog.ListModelsAsync();
        var outcome = await new SchemaValidatingAiProvider(provider).ProposeAsync(Request());

        Assert.Equal("greek-local", Assert.Single(models).Id);
        Assert.True(outcome.Succeeded);
        Assert.Collection(server.Requests,
            first => { Assert.Equal("GET", first.Method); Assert.Equal("/api/tags", first.Path); Assert.Null(first.Authorization); },
            second => { Assert.Equal("POST", second.Method); Assert.Equal("/api/chat", second.Path); Assert.Null(second.Authorization); });
    }

    [Fact]
    public async Task Transient_500_is_retried_over_a_real_socket()
    {
        await using var server = new Loopback(
            (500, """{"error":"temporary"}"""),
            (200, """{"message":{"content":"κυρίου"},"eval_count":2}"""));

        var settings = OllamaSettings(server.BaseUrl);
        var provider = new AiHttpProvider(settings, new HttpClient(), delay: (_, _) => Task.CompletedTask);

        var outcome = await new SchemaValidatingAiProvider(provider).ProposeAsync(Request());

        Assert.True(outcome.Succeeded);
        Assert.Equal(2, server.Requests.Count);
        Assert.All(server.Requests, request => Assert.Equal("/api/chat", request.Path));
    }

    /// <summary>Minimal loopback HTTP server serving a fixed sequence of responses.</summary>
    private sealed class Loopback : IAsyncDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly Task _loop;
        private readonly int _port;

        public Loopback(params (int Status, string Json)[] responses)
        {
            _port = FreePort();
            BaseUrl = $"http://127.0.0.1:{_port}";
            _listener.Prefixes.Add(BaseUrl + "/");
            _listener.Start();
            _loop = Task.Run(async () =>
            {
                foreach (var (status, json) in responses)
                {
                    var context = await _listener.GetContextAsync();
                    var body = await new StreamReader(context.Request.InputStream, Encoding.UTF8).ReadToEndAsync();
                    lock (Requests)
                    {
                        Requests.Add(new RecordedRequest(
                            context.Request.HttpMethod,
                            context.Request.Url?.AbsolutePath ?? "",
                            context.Request.Headers["Authorization"],
                            body));
                    }

                    var bytes = Encoding.UTF8.GetBytes(json);
                    context.Response.StatusCode = status;
                    context.Response.ContentType = "application/json";
                    context.Response.ContentLength64 = bytes.Length;
                    await context.Response.OutputStream.WriteAsync(bytes);
                    context.Response.Close();
                }
            });
        }

        public string BaseUrl { get; }

        public List<RecordedRequest> Requests { get; } = [];

        public sealed record RecordedRequest(string Method, string Path, string? Authorization, string Body);

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            try { await _loop.ConfigureAwait(false); } catch (HttpListenerException) { } catch (ObjectDisposedException) { }
            _listener.Close();
        }

        private static int FreePort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }
    }
}
