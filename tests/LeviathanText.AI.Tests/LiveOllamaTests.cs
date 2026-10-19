using System.Diagnostics;
using LeviathanText.AI;
using LeviathanText.Contracts;
using Xunit;
using Xunit.Abstractions;

namespace LeviathanText.AI.Tests;

/// <summary>
/// Opt-in smoke test against a real local Ollama server. Disabled by default so CI and
/// offline runs never touch the network; set <c>LEVIATHAN_OLLAMA_LIVE=1</c> to run.
/// The model name can be overridden with <c>LEVIATHAN_OLLAMA_MODEL</c>. No credentials
/// are used or recorded.
/// </summary>
public sealed class LiveOllamaTests(ITestOutputHelper output)
{
    [SkippableFact]
    public async Task Local_ollama_server_answers_a_real_request()
    {
        Skip.IfNot(Environment.GetEnvironmentVariable("LEVIATHAN_OLLAMA_LIVE") == "1",
            "set LEVIATHAN_OLLAMA_LIVE=1 to run the live local-provider smoke test");

        var model = Environment.GetEnvironmentVariable("LEVIATHAN_OLLAMA_MODEL") ?? "deepseek-v4.1-flash:cloud";
        var settings = new AiProviderSettings
        {
            ProviderId = "live-ollama",
            Host = AiProviderHost.Local,
            Protocol = AiWireProtocol.OllamaChat,
            Endpoint = new Uri("http://127.0.0.1:11434"),
            ModelId = model,
            Timeout = TimeSpan.FromSeconds(120),
            MaxAttempts = 1,
            MaxOutputTokens = 256,
            OllamaThink = false, // avoid spending the whole budget on the thinking phase
        };

        var catalog = new AiHttpModelCatalog(settings, new HttpClient());
        var models = await catalog.ListModelsAsync();
        Assert.Contains(models, m => m.Id == model);

        var request = new AiProposalRequest
        {
            TargetRevisionId = new EntityId(EntityKind.TranscriptionRevision, "live-smoke"),
            Range = new ScalarRange(0, 6),
            Task = AiTaskKind.CandidateReading,
            ProviderConfigRef = settings.ProviderId,
            PromptVersion = "live-smoke-1",
            SourceText = "κυριου",
        };

        var clock = Stopwatch.StartNew();
        var outcome = await new SchemaValidatingAiProvider(
            new AiHttpProvider(settings, new HttpClient())).ProposeAsync(request);
        clock.Stop();

        output.WriteLine($"model={model}; succeeded={outcome.Succeeded}; " +
                         $"reason={outcome.Failure?.Reason ?? "none"}; elapsed={clock.Elapsed.TotalSeconds:0.##}s; " +
                         $"textLength={outcome.Proposal?.ProposedText.Length ?? 0}");

        Assert.True(outcome.Succeeded, $"live provider failed: {outcome.Failure?.Reason}");
        Assert.False(string.IsNullOrWhiteSpace(outcome.Proposal!.ProposedText));
    }
}
