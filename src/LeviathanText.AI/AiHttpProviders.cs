using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace LeviathanText.AI;

/// <summary>
/// Real provider adapter for OpenAI-compatible chat endpoints and Ollama-native chat.
/// A local server uses the same contract as a hosted one; the host flag is recorded,
/// not inferred. Credentials resolve through <see cref="IAiCredentialSource"/> and are
/// never part of a proposal or failure record.
/// </summary>
public sealed class AiHttpProvider : IRawAiProvider
{
    /// <summary>Sends raw Greek text instead of \\uXXXX escapes; still valid UTF-8 JSON.</summary>
    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    private readonly AiProviderSettings _settings;
    private readonly AiHttpTransport _transport;

    public AiHttpProvider(
        AiProviderSettings settings,
        HttpClient client,
        IAiCredentialSource? credentials = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _settings = settings;
        _transport = new AiHttpTransport(settings, client, credentials, delay);
    }

    public string ProviderId => _settings.ProviderId;

    public string ModelId => _settings.ModelId;

    public decimal SpentUsd => _transport.SpentUsd;

    public decimal? RemainingSpendUsd => _transport.RemainingSpendUsd;

    public async Task<string?> GenerateRawAsync(
        AiProposalRequest request,
        CancellationToken cancellationToken = default)
    {
        var prompt = AiPromptBuilder.Build(request);
        _transport.EnsureBudget(prompt.System.Length + prompt.User.Length);

        var uri = _settings.Protocol == AiWireProtocol.OpenAiChatCompletions
            ? AiHttpTransport.Combine(_settings.Endpoint, "chat/completions")
            : AiHttpTransport.Combine(_settings.Endpoint, "api/chat");

        var payload = BuildPayload(prompt);
        var body = await _transport
            .SendJsonAsync(() => new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            }, cancellationToken)
            .ConfigureAwait(false);

        var (content, inputTokens, outputTokens) = Parse(body, prompt.System.Length + prompt.User.Length);
        _transport.Charge(inputTokens, outputTokens);
        return content;
    }

    private string BuildPayload(AiPrompt prompt)
    {
        var messages = new object[]
        {
            new { role = "system", content = prompt.System },
            new { role = "user", content = prompt.User },
        };

        object payload = _settings.Protocol == AiWireProtocol.OpenAiChatCompletions
            ? new Dictionary<string, object?>
            {
                ["model"] = _settings.ModelId,
                ["messages"] = messages,
                [_settings.UseMaxCompletionTokens ? "max_completion_tokens" : "max_tokens"] = _settings.MaxOutputTokens,
                ["temperature"] = 0,
                ["stream"] = false,
            }
            : new
            {
                model = _settings.ModelId,
                messages,
                stream = false,
                options = new
                {
                    temperature = 0,
                    num_predict = _settings.MaxOutputTokens,
                },
            };

        return JsonSerializer.Serialize(payload, PayloadOptions);
    }

    private (string Content, int InputTokens, int OutputTokens) Parse(string json, int promptCharacters)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var contentElement = _settings.Protocol == AiWireProtocol.OpenAiChatCompletions
                ? ReadOpenAiContent(root)
                : ReadOllamaContent(root);

            if (contentElement.ValueKind != JsonValueKind.String)
                throw new JsonException("The response content is not a string.");

            var content = contentElement.GetString() ?? string.Empty;
            var inputTokens = EstimateTokens(promptCharacters);
            var outputTokens = EstimateTokens(content.Length);

            if (_settings.Protocol == AiWireProtocol.OpenAiChatCompletions)
            {
                if (root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
                {
                    inputTokens = ReadInt(usage, "prompt_tokens") ?? inputTokens;
                    outputTokens = ReadInt(usage, "completion_tokens") ?? outputTokens;
                }
            }
            else
            {
                inputTokens = ReadInt(root, "prompt_eval_count") ?? inputTokens;
                outputTokens = ReadInt(root, "eval_count") ?? outputTokens;
            }

            return (content, inputTokens, outputTokens);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            throw new AiProviderException(
                AiFailureReasons.MalformedResponse,
                _transport.SanitizeForRecord(json),
                inner: ex);
        }
    }

    private static JsonElement ReadOpenAiContent(JsonElement root)
    {
        if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array)
            throw new JsonException("Missing choices array.");

        var enumerator = choices.EnumerateArray();
        if (!enumerator.MoveNext()) throw new JsonException("Empty choices array.");
        var choice = enumerator.Current;

        if (choice.TryGetProperty("message", out var message)
            && message.ValueKind == JsonValueKind.Object
            && message.TryGetProperty("content", out var content))
            return content;

        // Some local OpenAI-compatible servers answer in completion shape.
        if (choice.TryGetProperty("text", out var text)) return text;

        throw new JsonException("Missing message content.");
    }

    private static JsonElement ReadOllamaContent(JsonElement root)
    {
        if (root.TryGetProperty("message", out var message)
            && message.ValueKind == JsonValueKind.Object
            && message.TryGetProperty("content", out var content))
            return content;

        throw new JsonException("Missing Ollama message content.");
    }

    private static int EstimateTokens(int characters) =>
        Math.Max(1, (int)Math.Ceiling(characters / 4.0));

    private static int? ReadInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number >= 0)
            return number;
        return null;
    }
}

/// <summary>One model advertised by a provider's discovery endpoint.</summary>
public sealed record AiModelInfo(string Id, string? Family = null, long? SizeBytes = null);

/// <summary>Capability discovery for local and hosted providers.</summary>
public interface IAiModelCatalog
{
    Task<IReadOnlyList<AiModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Lists models through the documented discovery endpoints: OpenAI-compatible
/// <c>{base}/models</c> and Ollama <c>{base}/api/tags</c>. Discovery proves reachability
/// and the model name only; it does not benchmark or endorse a model.
/// </summary>
public sealed class AiHttpModelCatalog : IAiModelCatalog
{
    private readonly AiProviderSettings _settings;
    private readonly AiHttpTransport _transport;

    public AiHttpModelCatalog(
        AiProviderSettings settings,
        HttpClient client,
        IAiCredentialSource? credentials = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _settings = settings;
        _transport = new AiHttpTransport(settings, client, credentials, delay);
    }

    public async Task<IReadOnlyList<AiModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        var uri = _settings.Protocol == AiWireProtocol.OpenAiChatCompletions
            ? AiHttpTransport.Combine(_settings.Endpoint, "models")
            : AiHttpTransport.Combine(_settings.Endpoint, "api/tags");

        var json = await _transport
            .SendJsonAsync(() => new HttpRequestMessage(HttpMethod.Get, uri), cancellationToken)
            .ConfigureAwait(false);

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var models = _settings.Protocol == AiWireProtocol.OpenAiChatCompletions
                ? ParseOpenAi(root)
                : ParseOllama(root);

            return models
                .Where(model => !string.IsNullOrWhiteSpace(model.Id))
                .DistinctBy(model => model.Id, StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            throw new AiProviderException(
                AiFailureReasons.MalformedResponse,
                _transport.SanitizeForRecord(json),
                inner: ex);
        }
    }

    private static IEnumerable<AiModelInfo> ParseOpenAi(JsonElement root)
    {
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            throw new JsonException("Missing data array.");

        foreach (var item in data.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            if (!item.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String) continue;
            yield return new AiModelInfo(id.GetString()!);
        }
    }

    private static IEnumerable<AiModelInfo> ParseOllama(JsonElement root)
    {
        if (!root.TryGetProperty("models", out var models) || models.ValueKind != JsonValueKind.Array)
            throw new JsonException("Missing models array.");

        foreach (var item in models.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;

            var id = ReadString(item, "model") ?? ReadString(item, "name");
            if (id is null) continue;

            long? size = item.TryGetProperty("size", out var sizeElement)
                && sizeElement.ValueKind == JsonValueKind.Number
                && sizeElement.TryGetInt64(out var bytes)
                    ? bytes
                    : null;

            string? family = null;
            if (item.TryGetProperty("details", out var details)
                && details.ValueKind == JsonValueKind.Object)
                family = ReadString(details, "family");

            yield return new AiModelInfo(id, family, size);
        }
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
