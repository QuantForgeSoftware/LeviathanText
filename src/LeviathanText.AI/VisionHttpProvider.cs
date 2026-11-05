using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace LeviathanText.AI;

/// <summary>
/// Opt-in, loopback-only image inference using OpenAI-compatible chat completions.
/// One attempt and one in-flight call per instance; no text-only fallback or revision writes.
/// A supplied handler must disable automatic redirects. The provider owns that handler.
/// </summary>
public sealed class VisionHttpProvider : IDisposable
{
    public const int MaxRequestBytes = 12 * 1024 * 1024;
    public const int MaxResponseBytes = 1024 * 1024;
    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };
    private readonly AiProviderSettings _settings;
    private readonly HttpClient _client;
    private readonly IAiCredentialSource _credentials;
    private int _inFlight;
    private int _requests;

    public VisionHttpProvider(AiProviderSettings settings, HttpMessageHandler? handler = null,
        IAiCredentialSource? credentials = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        if (settings.Host != AiProviderHost.Local || settings.Protocol != AiWireProtocol.OpenAiChatCompletions
            || !settings.Endpoint.IsLoopback || settings.Endpoint.UserInfo.Length != 0
            || settings.Endpoint.Query.Length != 0 || settings.Endpoint.Fragment.Length != 0
            || settings.MaxAttempts != 1 || settings.Timeout > TimeSpan.FromSeconds(120)
            || settings.MaxResponseBytes > MaxResponseBytes || settings.MaxSpendUsd is not null)
            throw new AiProviderException(AiFailureReasons.ConfigurationInvalid,
                detail: "Vision inference requires a loopback OpenAI endpoint, one attempt, timeout <=120s, response <=1 MiB and no spend cap.");
        _settings = settings;
        _credentials = credentials ?? new EnvironmentCredentialSource();
        _client = new HttpClient(handler ?? new SocketsHttpHandler { AllowAutoRedirect = false })
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }

    public async Task<VisionInferenceResult> InferAsync(VisionInferenceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var started = DateTimeOffset.UtcNow;
        var clock = Stopwatch.StartNew();
        var promptHash = (long)(request.SystemPrompt?.Length ?? 0) + (request.UserPrompt?.Length ?? 0) <= _settings.MaxPromptCharacters
            ? Hash(JsonSerializer.SerializeToUtf8Bytes(new[] { request.SystemPrompt, request.UserPrompt }, PayloadOptions)) : string.Empty;
        var result = new VisionInferenceResult
        {
            SampleId = request.SampleId,
            ImageSha256 = request.Image?.Sha256 ?? string.Empty,
            PromptSha256 = promptHash,
            PromptVersion = request.PromptVersion,
            ProviderId = _settings.ProviderId,
            ModelId = _settings.ModelId,
            StartedAt = started,
            Status = VisionInferenceStatus.InputRejected,
        };
        VisionInferenceResult Finish(VisionInferenceResult value) => value with { ElapsedMilliseconds = clock.Elapsed.TotalMilliseconds };
        if (cancellationToken.IsCancellationRequested)
            return Finish(result with { Status = VisionInferenceStatus.Cancelled });
        if (string.IsNullOrWhiteSpace(request.SampleId) || request.SampleId.Length > 256
            || string.IsNullOrWhiteSpace(request.PromptVersion) || request.PromptVersion.Length > 256
            || request.Image is null || string.IsNullOrWhiteSpace(request.SystemPrompt) || request.UserPrompt is null
            || (long)request.SystemPrompt.Length + request.UserPrompt.Length > _settings.MaxPromptCharacters)
            return Finish(result with { Detail = "Required input is absent or the prompt/identity length limit was exceeded." });
        if (!Enum.IsDefined(request.ResponseFormat))
            return Finish(result with { Detail = "The requested vision response format is unsupported." });
        if (Interlocked.CompareExchange(ref _inFlight, 1, 0) != 0)
            return Finish(result with { Status = VisionInferenceStatus.Busy, Detail = "Only one in-flight vision request is permitted." });

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_settings.Timeout);
        using var raw = new MemoryStream();
        var parser = new VisionResponseParser();
        string? secret = null;
        var successHttp = false;
        var isSse = false;
        var isJson = false;
        var bodyComplete = false;
        try
        {
            if (_settings.MaxRequests > 0 && _requests >= _settings.MaxRequests)
                return Finish(result with { Detail = "The configured request budget is exhausted." });
            if (!string.IsNullOrWhiteSpace(_settings.CredentialEnvironmentVariable))
            {
                secret = _credentials.Resolve(_settings.CredentialEnvironmentVariable);
                if (string.IsNullOrWhiteSpace(secret))
                    return Finish(result with { Status = VisionInferenceStatus.ConfigurationError, Detail = "The configured credential is unavailable." });
            }

            var encodingClock = Stopwatch.StartNew();
            var payload = new Dictionary<string, object?>
            {
                ["model"] = _settings.ModelId,
                ["messages"] = new object[]
                {
                    new { role = "system", content = request.SystemPrompt },
                    new { role = "user", content = new object[]
                    {
                        new { type = "text", text = request.UserPrompt },
                        new { type = "image_url", image_url = new { url = request.Image.ToDataUri() } },
                    } },
                },
                ["temperature"] = 0,
                ["top_p"] = 1,
                ["stream"] = true,
                [_settings.UseMaxCompletionTokens ? "max_completion_tokens" : "max_tokens"] = _settings.MaxOutputTokens,
            };
            if (request.Seed is { } seed) payload["seed"] = seed;
            if (request.ResponseFormat == VisionResponseFormat.TranscriptionJsonV1)
                payload["response_format"] = new
                {
                    type = "json_schema",
                    json_schema = new
                    {
                        name = "transcription_v1",
                        strict = true,
                        schema = new
                        {
                            type = "object",
                            properties = new { transcription = new { type = "string" } },
                            required = new[] { "transcription" },
                            additionalProperties = false,
                        },
                    },
                };
            var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, PayloadOptions);
            result = result with
            {
                RequestSha256 = Hash(bytes),
                RequestBytes = bytes.Length,
                EncodingMilliseconds = encodingClock.Elapsed.TotalMilliseconds,
            };
            if (bytes.Length > MaxRequestBytes)
                return Finish(result with { Detail = "Encoded request exceeds 12 MiB." });

            using var message = new HttpRequestMessage(HttpMethod.Post, AiHttpTransport.Combine(_settings.Endpoint, "chat/completions"))
            {
                Content = new ByteArrayContent(bytes),
            };
            message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            if (secret is not null) message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
            deadline.Token.ThrowIfCancellationRequested();
            _requests++;
            using var response = await _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
            result = result with { HttpStatusCode = (int)response.StatusCode };
            successHttp = response.IsSuccessStatusCode;
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            isSse = successHttp && string.Equals(mediaType, "text/event-stream", StringComparison.OrdinalIgnoreCase);
            isJson = successHttp && string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase);

            await using var body = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
            var chunk = new byte[8192];
            var decoder = new UTF8Encoding(false, true).GetDecoder();
            var characters = new char[Encoding.UTF8.GetMaxCharCount(chunk.Length)];
            while (true)
            {
                // Read at most one byte beyond the cap so an unknown-length body remains bounded.
                var remaining = _settings.MaxResponseBytes - (int)raw.Length;
                var read = await body.ReadAsync(chunk.AsMemory(0, Math.Min(chunk.Length, remaining + 1)), deadline.Token).ConfigureAwait(false);
                if (read == 0) { bodyComplete = true; break; }
                result = result with { FirstByteMilliseconds = result.FirstByteMilliseconds ?? clock.Elapsed.TotalMilliseconds };
                var retain = Math.Min(read, remaining);
                raw.Write(chunk, 0, retain);
                if (isSse && retain > 0)
                {
                    var count = decoder.GetChars(chunk, 0, retain, characters, 0, false);
                    parser.FeedSse(characters.AsSpan(0, count));
                    if (parser.Text.Length > 0 && result.FirstTokenMilliseconds is null)
                        result = result with { FirstTokenMilliseconds = clock.Elapsed.TotalMilliseconds };
                }
                if (read > retain)
                {
                    result = result with { Status = VisionInferenceStatus.ResponseTooLarge, Detail = "Response exceeded the configured byte limit; its bounded prefix is retained." };
                    return Finish(Complete());
                }
            }
            if (isSse)
            {
                var count = decoder.GetChars([], 0, 0, characters, 0, true);
                parser.FeedSse(characters.AsSpan(0, count));
                parser.EndSse();
            }
            else if (isJson)
            {
                var jsonBytes = raw.ToArray();
                if (VisionResponseParser.ValidUtf8Prefix(jsonBytes).Length != jsonBytes.Length)
                    throw new DecoderFallbackException("Response contains invalid UTF-8.");
                parser.ReadJson(jsonBytes);
            }

            var status = !successHttp ? VisionInferenceStatus.HttpError
                : !isSse && !isJson ? VisionInferenceStatus.InvalidOutput
                : parser.FinishReason == "length" ? VisionInferenceStatus.Truncated
                : parser.IsComplete(isSse) ? VisionInferenceStatus.Completed : VisionInferenceStatus.InvalidOutput;
            result = result with { Status = status };
            return Finish(Complete());
        }
        catch (OperationCanceledException)
        {
            result = result with { Status = cancellationToken.IsCancellationRequested ? VisionInferenceStatus.Cancelled : VisionInferenceStatus.Timeout };
            return Finish(Complete());
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            result = result with { Status = VisionInferenceStatus.TransportError, Detail = "The connection failed; captured response bytes are retained." };
            return Finish(Complete());
        }
        catch (Exception ex) when (ex is JsonException or DecoderFallbackException)
        {
            result = result with { Status = VisionInferenceStatus.InvalidOutput, Detail = "The response was not valid UTF-8 or the expected completion format." };
            return Finish(Complete());
        }
        catch (FormatException)
        {
            result = result with { Status = VisionInferenceStatus.ConfigurationError, Detail = "The configured authorization header is invalid." };
            return Finish(Complete());
        }
        finally { Volatile.Write(ref _inFlight, 0); }

        VisionInferenceResult Complete()
        {
            var capturedBytes = raw.ToArray();
            // A decoder can reject a whole received chunk after valid events in that chunk.
            // Reparse the valid byte prefix once so those events and a torn content string
            // survive every failure path without inventing a replacement Unicode scalar.
            var validPrefix = VisionResponseParser.ValidUtf8Prefix(capturedBytes);
            parser = new VisionResponseParser();
            if (isJson) parser.ReadJson(validPrefix.ToArray());
            if (isSse)
            {
                parser.FeedSse(Encoding.UTF8.GetString(validPrefix));
                if (bodyComplete) parser.EndSse();
                else parser.ReadPartialSse();
            }
            var wire = Encoding.UTF8.GetString(capturedBytes);
            var text = successHttp ? parser.Text.ToString() : string.Empty;
            // Model content can itself be JSON. Inspect that one additional bounded
            // layer before an offline consumer decodes an escaped credential from it.
            // Redact the whole content on a hit rather than repairing/re-encoding JSON.
            var contentEcho = secret is not null
                && VisionResponseParser.ContainsDecodedSecret(Encoding.UTF8.GetBytes(text), secret);
            var redacted = secret is not null && (wire.Contains(secret, StringComparison.Ordinal) || text.Contains(secret, StringComparison.Ordinal)
                || (parser.ModelId?.Contains(secret, StringComparison.Ordinal) ?? false) || (parser.FinishReason?.Contains(secret, StringComparison.Ordinal) ?? false)
                || VisionResponseParser.ContainsDecodedSecret(capturedBytes, secret) || contentEcho);
            if (secret is not null)
            {
                // A credential may be JSON escaped or split across SSE deltas. Redacting
                // the whole wire representation avoids retaining an alternate encoding.
                if (redacted) wire = "[redacted: response echoed the configured credential]";
                text = contentEcho ? "[redacted: model content echoed the configured credential]"
                    : text.Replace(secret, "[redacted]", StringComparison.Ordinal);
            }
            return result with
            {
                Text = text,
                RawResponse = wire,
                RawResponseSha256 = Hash(capturedBytes),
                CapturedResponseBytes = (int)raw.Length,
                RawResponseComplete = bodyComplete,
                CredentialsRedacted = redacted,
                FinishReason = secret is null ? parser.FinishReason : parser.FinishReason?.Replace(secret, "[redacted]", StringComparison.Ordinal),
                ResponseModelId = secret is null ? parser.ModelId : parser.ModelId?.Replace(secret, "[redacted]", StringComparison.Ordinal),
                InputTokens = parser.InputTokens,
                OutputTokens = parser.OutputTokens,
            };
        }
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    public void Dispose() => _client.Dispose();
}
