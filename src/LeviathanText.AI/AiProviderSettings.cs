using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace LeviathanText.AI;

/// <summary>Whether a provider runs on the user's machine or over the network.</summary>
public enum AiProviderHost
{
    Local,
    Hosted,
}

/// <summary>Wire protocol of a provider. Both are documented, provider-neutral HTTP shapes.</summary>
public enum AiWireProtocol
{
    /// <summary>OpenAI-compatible <c>{base}/chat/completions</c> (hosted or local gateway).</summary>
    OpenAiChatCompletions,

    /// <summary>Ollama-native <c>{base}/api/chat</c>.</summary>
    OllamaChat,
}

/// <summary>
/// Published token prices used for a conservative local spend cap. Prices are supplied
/// by the user/configuration; this type makes no claim about a provider's current rates.
/// </summary>
public sealed record AiTokenPrice(decimal InputUsdPerMillionTokens, decimal OutputUsdPerMillionTokens)
{
    public decimal Cost(int inputTokens, int outputTokens) =>
        (inputTokens * InputUsdPerMillionTokens + outputTokens * OutputUsdPerMillionTokens) / 1_000_000m;
}

/// <summary>
/// Configuration for one provider/model. Credentials are referenced by environment
/// variable name and resolved at call time; the secret itself is never copied into a
/// proposal, failure record, log message or handoff (interchange-and-api.md §2.3).
/// </summary>
public sealed record AiProviderSettings
{
    public required string ProviderId { get; init; }
    public required AiProviderHost Host { get; init; }
    public required AiWireProtocol Protocol { get; init; }

    /// <summary>
    /// API base, not the full chat URL. For OpenAI-compatible endpoints include the
    /// version segment (for example <c>https://api.openai.com/v1</c>); Ollama uses
    /// <c>http://localhost:11434</c>.
    /// </summary>
    public required Uri Endpoint { get; init; }

    public required string ModelId { get; init; }

    /// <summary>Environment variable holding the key. Optional for keyless local servers.</summary>
    public string? CredentialEnvironmentVariable { get; init; }

    public string? Organization { get; init; }

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>Upper bound sent to the model and reserved by the spend cap.</summary>
    public int MaxOutputTokens { get; init; } = 512;

    /// <summary>
    /// OpenAI-compatible endpoints differ: older/local servers accept <c>max_tokens</c>,
    /// newer hosted models require <c>max_completion_tokens</c>. Default keeps the
    /// broadly compatible field.
    /// </summary>
    public bool UseMaxCompletionTokens { get; init; }

    /// <summary>Hard cap on a response body; larger bodies become a recorded failure.</summary>
    public int MaxResponseBytes { get; init; } = 1_048_576;

    /// <summary>Hard cap on the combined system + user prompt characters.</summary>
    public int MaxPromptCharacters { get; init; } = 20_000;

    /// <summary>Total attempts, including the first request (1 = no retries).</summary>
    public int MaxAttempts { get; init; } = 3;

    public TimeSpan BaseRetryDelay { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Required when <see cref="MaxSpendUsd"/> is set.</summary>
    public AiTokenPrice? Price { get; init; }

    /// <summary>Optional session cap. Checked conservatively before each request.</summary>
    public decimal? MaxSpendUsd { get; init; }

    /// <summary>Optional cap on logical provider calls (0 = unlimited); retries do not consume extra calls.</summary>
    public int MaxRequests { get; init; }

    /// <summary>
    /// Ollama-only: send <c>think</c> to enable/disable a reasoning model's thinking phase.
    /// Null leaves the server default. A thinking model with a small token budget can
    /// spend it all on thinking and emit no content.
    /// </summary>
    public bool? OllamaThink { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProviderId)) Invalid("providerId is required.");
        if (string.IsNullOrWhiteSpace(ModelId)) Invalid("modelId is required.");
        if (Endpoint is null || !Endpoint.IsAbsoluteUri) Invalid("endpoint must be an absolute URI.");
        if (Endpoint.Scheme is not ("http" or "https")) Invalid("endpoint must use http or https.");
        if (Timeout <= TimeSpan.Zero || Timeout > TimeSpan.FromMinutes(10)) Invalid("timeout must be between 0 and 10 minutes.");
        if (MaxOutputTokens is < 1 or > 131_072) Invalid("maxOutputTokens must be between 1 and 131072.");
        if (MaxResponseBytes is < 1024 or > 64 * 1024 * 1024) Invalid("maxResponseBytes must be between 1 KiB and 64 MiB.");
        if (MaxPromptCharacters is < 256 or > 1_000_000) Invalid("maxPromptCharacters must be between 256 and 1000000.");
        if (MaxAttempts is < 1 or > 5) Invalid("maxAttempts must be between 1 and 5.");
        if (BaseRetryDelay < TimeSpan.Zero || BaseRetryDelay > TimeSpan.FromSeconds(30)) Invalid("baseRetryDelay must be between 0 and 30 seconds.");
        if (MaxRequests < 0) Invalid("maxRequests must be non-negative.");
        if (OllamaThink is not null && Protocol != AiWireProtocol.OllamaChat)
            Invalid("ollamaThink is only valid for the Ollama protocol.");
        if (MaxSpendUsd is { } spend)
        {
            if (spend < 0) Invalid("maxSpendUsd must be non-negative.");
            if (Price is null) Invalid("price is required when maxSpendUsd is set.");
            if (Price.InputUsdPerMillionTokens < 0 || Price.OutputUsdPerMillionTokens < 0) Invalid("token prices must be non-negative.");
        }
    }

    [DoesNotReturn]
    private void Invalid(string message) =>
        throw new AiProviderException(AiFailureReasons.ConfigurationInvalid, detail: $"{ProviderId}: {message}");
}

/// <summary>Closed failure vocabulary for provider adapters.</summary>
public static class AiFailureReasons
{
    public const string ConfigurationInvalid = "configuration-invalid";
    public const string AuthenticationFailed = "authentication-failed";
    public const string RateLimited = "rate-limited";
    public const string ProviderUnavailable = "provider-unavailable";
    public const string RequestInvalid = "request-invalid";
    public const string RequestFailed = "request-failed";
    public const string Timeout = "timeout";
    public const string ResponseTooLarge = "response-too-large";
    public const string MalformedResponse = "malformed-response";
    public const string SpendBudgetExceeded = "spend-budget-exceeded";
    public const string PromptTooLarge = "prompt-too-large";
    public const string RequestBudgetExceeded = "request-budget-exceeded";
    public const string ResponseTruncated = "response-truncated";
}

/// <summary>
/// A provider failure with a reason that is safe to record. The excerpt is bounded,
/// control-character-stripped and credential-redacted; it never contains partial
/// proposal text that could be mistaken for a reading.
/// </summary>
public sealed class AiProviderException(string reason, string? redactedExcerpt = null, string? detail = null, Exception? inner = null)
    : Exception(detail ?? reason, inner)
{
    public string Reason { get; } = reason;
    public string? RedactedExcerpt { get; } = redactedExcerpt;
}

/// <summary>Resolves a credential reference to a secret at call time.</summary>
public interface IAiCredentialSource
{
    string? Resolve(string reference);
}

/// <summary>Reads credentials from the process environment. Never persists them.</summary>
public sealed class EnvironmentCredentialSource : IAiCredentialSource
{
    public string? Resolve(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return null;
        var value = Environment.GetEnvironmentVariable(reference);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

/// <summary>Deterministic credential source for tests; never use with a real secret.</summary>
public sealed class StaticCredentialSource(IReadOnlyDictionary<string, string> values) : IAiCredentialSource
{
    public string? Resolve(string reference) =>
        values.TryGetValue(reference, out var value) ? value : null;
}

/// <summary>
/// Session spend accounting. The preflight reserves the worst-case output tokens so a
/// misconfigured model cannot overshoot the cap before usage is known.
/// </summary>
public sealed class AiSpendBudget
{
    private readonly object _gate = new();
    private readonly AiTokenPrice _price;
    private readonly decimal _limitUsd;
    private decimal _spentUsd;

    public AiSpendBudget(AiTokenPrice price, decimal limitUsd)
    {
        if (limitUsd < 0) throw new ArgumentOutOfRangeException(nameof(limitUsd));
        _price = price;
        _limitUsd = limitUsd;
    }

    public decimal SpentUsd { get { lock (_gate) return _spentUsd; } }

    public decimal RemainingUsd { get { lock (_gate) return Math.Max(0, _limitUsd - _spentUsd); } }

    public void EnsureAffordable(int maxInputTokens, int maxOutputTokens)
    {
        var estimate = _price.Cost(maxInputTokens, maxOutputTokens);
        lock (_gate)
        {
            if (_spentUsd + estimate > _limitUsd)
                throw new AiProviderException(
                    AiFailureReasons.SpendBudgetExceeded,
                    detail: string.Create(CultureInfo.InvariantCulture,
                        $"Estimated ${estimate:0.######} would exceed the remaining ${(_limitUsd - _spentUsd):0.######} budget."));
        }
    }

    public decimal Charge(int inputTokens, int outputTokens)
    {
        var cost = _price.Cost(Math.Max(0, inputTokens), Math.Max(0, outputTokens));
        lock (_gate) _spentUsd += cost;
        return cost;
    }
}
