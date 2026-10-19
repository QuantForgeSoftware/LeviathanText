using System.Net;
using System.Text;
using LeviathanText.AI;
using LeviathanText.Contracts;
using Xunit;

namespace LeviathanText.AI.Tests;

/// <summary>Provider switching by opaque config reference; no credentials in the registry.</summary>
public class AiProviderRegistryTests
{
    private static AiProviderSettings Local() => new()
    {
        ProviderId = "local/ollama",
        Host = AiProviderHost.Local,
        Protocol = AiWireProtocol.OllamaChat,
        Endpoint = new Uri("http://127.0.0.1:11434"),
        ModelId = "greek-local",
        MaxAttempts = 1,
    };

    private static AiProviderSettings Hosted() => new()
    {
        ProviderId = "hosted/openai",
        Host = AiProviderHost.Hosted,
        Protocol = AiWireProtocol.OpenAiChatCompletions,
        Endpoint = new Uri("https://api.example.test/v1"),
        ModelId = "greek-model",
        CredentialEnvironmentVariable = "LEVIATHAN_AI_KEY",
        MaxAttempts = 1,
    };

    [Fact]
    public void Resolves_local_and_hosted_providers_by_config_reference()
    {
        var registry = new AiProviderRegistry([Hosted(), Local()], new HttpClient(new OfflineHandler()));

        Assert.Equal(["hosted/openai", "local/ollama"], registry.ProviderIds);
        Assert.Equal(AiProviderHost.Local, registry.SettingsFor("local/ollama").Host);
        Assert.Equal("local/ollama", registry.ProviderFor("local/ollama").ProviderId);
        Assert.Equal("greek-local", registry.RawProviderFor("local/ollama").ModelId);
        Assert.Equal(AiProviderHost.Hosted, registry.SettingsFor("hosted/openai").Host);
    }

    [Fact]
    public void Unknown_reference_fails_with_the_closed_input_missing_error()
    {
        var registry = new AiProviderRegistry([Local()], new HttpClient(new OfflineHandler()));

        var ex = Assert.Throws<LeviathanException>(() => registry.ProviderFor("missing/provider"));

        Assert.Equal(ErrorCode.InputMissing, ex.Error.Code);
        Assert.Contains("local/ollama", ex.Error.Details!["known"]);
    }

    [Fact]
    public void Duplicate_provider_ids_are_rejected()
    {
        var client = new HttpClient(new OfflineHandler());
        Assert.Throws<ArgumentException>(() => new AiProviderRegistry([Local(), Local()], client));
    }

    [Fact]
    public async Task Model_catalog_uses_the_selected_provider()
    {
        var handler = new ModelListHandler();
        var credentials = new StaticCredentialSource(new Dictionary<string, string> { ["LEVIATHAN_AI_KEY"] = "test-key" });
        var registry = new AiProviderRegistry([Local(), Hosted()], new HttpClient(handler), credentials);

        var models = await registry.ModelCatalogFor("hosted/openai").ListModelsAsync();

        Assert.Equal("greek-model", Assert.Single(models).Id);
        Assert.Equal("https://api.example.test/v1/models", handler.LastUri);
    }

    private sealed class OfflineHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The registry must not call a provider during resolution.");
    }

    private sealed class ModelListHandler : HttpMessageHandler
    {
        public string? LastUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":[{"id":"greek-model"}]}""", Encoding.UTF8, "application/json"),
            });
        }
    }
}
