using System.Net;
using System.Text;
using LeviathanText.Contracts;
using Xunit;

namespace LeviathanText.AI.Tests;

public sealed class ProviderBudgetConcurrencyTests
{
    private static AiProviderSettings Settings => new()
    {
        ProviderId = "budget-fixture", Host = AiProviderHost.Local,
        Protocol = AiWireProtocol.OpenAiChatCompletions,
        Endpoint = new Uri("http://example.test/v1"), ModelId = "fixture",
        MaxOutputTokens = 1, MaxAttempts = 1, BaseRetryDelay = TimeSpan.Zero,
        Price = new AiTokenPrice(0, 1_000_000m), MaxSpendUsd = 1m,
    };
    private static AiProposalRequest Request => new()
    {
        TargetRevisionId = new EntityId(EntityKind.TranscriptionRevision, "fixture"),
        Range = new ScalarRange(0, 1), Task = AiTaskKind.CandidateReading,
        SourceText = "α", PromptVersion = "fixture", ProviderConfigRef = "budget-fixture",
    };
    private static HttpResponseMessage Reply(string json) => new(HttpStatusCode.OK)
        { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    private const string Success = """{"choices":[{"message":{"content":"α"}}],"usage":{"prompt_tokens":1,"completion_tokens":1}}""";

    [Fact]
    public async Task Repeated_registry_resolution_cannot_reset_call_caps()
    {
        var handler = new Handler(() => Task.FromResult(Reply(Success)));
        using var client = new HttpClient(handler);
        var registry = new AiProviderRegistry([Settings with { MaxSpendUsd = null, Price = null, MaxRequests = 1 }], client);
        await registry.RawProviderFor("budget-fixture").GenerateRawAsync(Request);
        var error = await Assert.ThrowsAsync<AiProviderException>(() =>
            registry.RawProviderFor("budget-fixture").GenerateRawAsync(Request));
        Assert.Equal(AiFailureReasons.RequestBudgetExceeded, error.Reason);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Concurrent_calls_reserve_the_available_allowance_before_network()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new Handler(async () => { entered.TrySetResult(); await release.Task; return Reply(Success); });
        using var client = new HttpClient(handler);
        var provider = new AiHttpProvider(Settings, client);
        var first = provider.GenerateRawAsync(Request);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var second = provider.GenerateRawAsync(Request);
            var error = await Assert.ThrowsAsync<AiProviderException>(() => second.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.Equal(AiFailureReasons.SpendBudgetExceeded, error.Reason);
            Assert.Equal(1, handler.Calls);
            Assert.Equal(0m, provider.RemainingSpendUsd);
        }
        finally { release.TrySetResult(); }
        Assert.Equal("α", await first);
        Assert.Equal(1m, provider.SpentUsd);
    }

    [Fact]
    public async Task Retry_cannot_reuse_the_same_reserved_allowance()
    {
        var handler = new Handler(() => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        using var client = new HttpClient(handler);
        var provider = new AiHttpProvider(Settings with { MaxAttempts = 2 }, client);
        var error = await Assert.ThrowsAsync<AiProviderException>(() => provider.GenerateRawAsync(Request));
        Assert.Equal(AiFailureReasons.SpendBudgetExceeded, error.Reason);
        Assert.Equal(1, handler.Calls);
        Assert.Equal(1m, provider.SpentUsd);
    }

    [Theory]
    [InlineData("{}")] // A response may have incurred cost even if its content is unusable.
    [InlineData("{\"choices\":[{\"message\":{\"content\":\"α\"}}]}")]
    public async Task Missing_usage_or_unusable_response_conservatively_consumes_the_attempt(string json)
    {
        var handler = new Handler(() => Task.FromResult(Reply(json)));
        using var client = new HttpClient(handler);
        var provider = new AiHttpProvider(Settings, client);
        try { await provider.GenerateRawAsync(Request); }
        catch (AiProviderException error) { Assert.Equal(AiFailureReasons.MalformedResponse, error.Reason); }
        Assert.Equal(1m, provider.SpentUsd);
        var blocked = await Assert.ThrowsAsync<AiProviderException>(() => provider.GenerateRawAsync(Request));
        Assert.Equal(AiFailureReasons.SpendBudgetExceeded, blocked.Reason);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Reported_usage_releases_unused_allowance_but_never_hides_an_overrun()
    {
        var calls = 0;
        var handler = new Handler(() => Task.FromResult(Reply(Interlocked.Increment(ref calls) == 1
            ? """{"choices":[{"message":{"content":"α"}}],"usage":{"prompt_tokens":1,"completion_tokens":0}}"""
            : """{"choices":[{"message":{"content":"α"}}],"usage":{"prompt_tokens":1,"completion_tokens":3}}""")));
        using var client = new HttpClient(handler);
        var provider = new AiHttpProvider(Settings, client);
        await provider.GenerateRawAsync(Request);
        Assert.Equal(0m, provider.SpentUsd);
        Assert.Equal(1m, provider.RemainingSpendUsd);
        await provider.GenerateRawAsync(Request);
        Assert.Equal(3m, provider.SpentUsd); // Reported bills are not clamped to the estimate.
        Assert.Equal(0m, provider.RemainingSpendUsd);
        var error = await Assert.ThrowsAsync<AiProviderException>(() => provider.GenerateRawAsync(Request));
        Assert.Equal(AiFailureReasons.SpendBudgetExceeded, error.Reason);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Successful_retry_keeps_unknown_prior_attempt_cost()
    {
        var calls = 0;
        var handler = new Handler(() => Task.FromResult(Interlocked.Increment(ref calls) == 1
            ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
            : Reply("""{"choices":[{"message":{"content":"α"}}],"usage":{"prompt_tokens":1,"completion_tokens":0}}""")));
        using var client = new HttpClient(handler);
        var provider = new AiHttpProvider(Settings with { MaxAttempts = 2, MaxSpendUsd = 2m }, client);
        await provider.GenerateRawAsync(Request);
        Assert.Equal(2, handler.Calls);
        Assert.Equal(1m, provider.SpentUsd);
        Assert.Equal(1m, provider.RemainingSpendUsd);
    }

    [Fact]
    public async Task Credential_failure_before_dispatch_consumes_no_spend_allowance()
    {
        var handler = new Handler(() => Task.FromResult(Reply(Success)));
        using var client = new HttpClient(handler);
        var provider = new AiHttpProvider(Settings with { CredentialEnvironmentVariable = "FIXTURE_KEY" }, client,
            new StaticCredentialSource(new Dictionary<string, string>()));
        var error = await Assert.ThrowsAsync<AiProviderException>(() => provider.GenerateRawAsync(Request));
        Assert.Equal(AiFailureReasons.ConfigurationInvalid, error.Reason);
        Assert.Equal(0, handler.Calls);
        Assert.Equal(0m, provider.SpentUsd);
        Assert.Equal(1m, provider.RemainingSpendUsd);
    }

    [Fact]
    public void Reservation_disposal_is_idempotent_and_never_releases_another_request()
    {
        var budget = new AiSpendBudget(new AiTokenPrice(0, 1_000_000m), 2m);
        using var first = budget.CreateReservation(0, 1);
        using var second = budget.CreateReservation(0, 1);
        first.ReserveAttempt(); first.MarkDispatched(); second.ReserveAttempt(); second.MarkDispatched();
        first.Dispose(); first.Dispose();
        Assert.Equal(1m, budget.SpentUsd);
        Assert.Equal(0m, budget.RemainingUsd);
        second.Settle(0, 0);
        Assert.Equal(1m, budget.SpentUsd);
        Assert.Equal(1m, budget.RemainingUsd);
    }

    [Fact]
    public async Task Precancelled_call_consumes_neither_spend_nor_call_allowance()
    {
        var handler = new Handler(() => Task.FromResult(Reply(Success)));
        using var client = new HttpClient(handler);
        var provider = new AiHttpProvider(Settings with { MaxRequests = 1 }, client);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.GenerateRawAsync(Request, cancellation.Token));
        Assert.Equal(0, handler.Calls);
        Assert.Equal(0m, provider.SpentUsd);
        Assert.Equal(1m, provider.RemainingSpendUsd);
        await provider.GenerateRawAsync(Request);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Locally_denied_concurrent_call_does_not_consume_logical_call_allowance()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new Handler(async () =>
        {
            entered.TrySetResult(); await release.Task;
            return Reply("""{"choices":[{"message":{"content":"α"}}],"usage":{"prompt_tokens":1,"completion_tokens":0}}""");
        });
        using var client = new HttpClient(handler);
        var provider = new AiHttpProvider(Settings with { MaxRequests = 2 }, client);
        var first = provider.GenerateRawAsync(Request);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var denied = await Assert.ThrowsAsync<AiProviderException>(() => provider.GenerateRawAsync(Request));
            Assert.Equal(AiFailureReasons.SpendBudgetExceeded, denied.Reason);
        }
        finally { release.TrySetResult(); }
        await first;
        await provider.GenerateRawAsync(Request);
        Assert.Equal(2, handler.Calls);
        Assert.Equal(0m, provider.SpentUsd);
        Assert.Equal(1m, provider.RemainingSpendUsd);
        var capped = await Assert.ThrowsAsync<AiProviderException>(() => provider.GenerateRawAsync(Request));
        Assert.Equal(AiFailureReasons.RequestBudgetExceeded, capped.Reason);
        Assert.Equal(1m, provider.RemainingSpendUsd); // Unsent holds are released.
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Known_usage_is_charged_even_when_content_is_unusable(bool ollama)
    {
        var body = ollama
            ? """{"message":{"content":""},"done_reason":"length","prompt_eval_count":1,"eval_count":10}"""
            : """{"usage":{"prompt_tokens":1,"completion_tokens":10}}""";
        var handler = new Handler(() => Task.FromResult(Reply(body)));
        using var client = new HttpClient(handler);
        var provider = new AiHttpProvider(Settings with
        {
            MaxSpendUsd = 10m,
            Protocol = ollama ? AiWireProtocol.OllamaChat : AiWireProtocol.OpenAiChatCompletions,
        }, client);
        var error = await Assert.ThrowsAsync<AiProviderException>(() => provider.GenerateRawAsync(Request));
        Assert.Equal(ollama ? AiFailureReasons.ResponseTruncated : AiFailureReasons.MalformedResponse, error.Reason);
        Assert.Equal(10m, provider.SpentUsd);
        var blocked = await Assert.ThrowsAsync<AiProviderException>(() => provider.GenerateRawAsync(Request));
        Assert.Equal(AiFailureReasons.SpendBudgetExceeded, blocked.Reason);
        Assert.Equal(1, handler.Calls);
    }

    private sealed class Handler(Func<Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        private int _calls;
        public int Calls => Volatile.Read(ref _calls);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            return response();
        }
    }
}
