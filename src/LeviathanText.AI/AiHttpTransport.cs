using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace LeviathanText.AI;

/// <summary>
/// Shared HTTP plumbing for provider calls: authentication, bounded responses,
/// per-request timeouts, retry/backoff on transient failures and the spend cap.
/// Every failure is expressed as an <see cref="AiProviderException"/> with a closed
/// reason and a redacted excerpt, so a caller can record it without leaking secrets.
/// </summary>
public sealed class AiHttpTransport
{
    private const int ErrorExcerptBytes = 512;
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);

    private readonly AiProviderSettings _settings;
    private readonly HttpClient _client;
    private readonly IAiCredentialSource _credentials;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly AiSpendBudget? _budget;
    private long _logicalRequests;

    public AiHttpTransport(
        AiProviderSettings settings,
        HttpClient client,
        IAiCredentialSource? credentials = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(client);
        settings.Validate();
        if (settings.MaxSpendUsd is not null && settings.Price is null)
            throw new AiProviderException(AiFailureReasons.ConfigurationInvalid, detail: "price is required when maxSpendUsd is set.");

        _settings = settings;
        _client = client;
        _credentials = credentials ?? new EnvironmentCredentialSource();
        _delay = delay ?? Task.Delay;
        _budget = settings.MaxSpendUsd is { } limit ? new AiSpendBudget(settings.Price!, limit) : null;
    }

    public AiProviderSettings Settings => _settings;

    public bool HasSpendCap => _budget is not null;

    public decimal SpentUsd => _budget?.SpentUsd ?? 0m;

    public decimal? RemainingSpendUsd => _budget?.RemainingUsd;

    internal static Uri Combine(Uri baseUri, string relative)
    {
        var text = baseUri.AbsoluteUri;
        if (!text.EndsWith('/')) text += "/";
        return new Uri(new Uri(text), relative);
    }

    /// <summary>Legacy estimate preview; dispatch reservations hold the actual allowance.</summary>
    public void EnsureBudget(int promptCharacters)
    {
        var estimatedInputTokens = Math.Max(1, promptCharacters / 4) + 16;
        _budget?.EnsureAffordable(estimatedInputTokens, _settings.MaxOutputTokens);
    }

    public AiSpendBudget.Reservation? CreateBudgetReservation(int promptUtf8Bytes) =>
        _budget?.CreateReservation(checked(Math.Max(1, promptUtf8Bytes) + 16), _settings.MaxOutputTokens);

    public void Charge(int inputTokens, int outputTokens) => _budget?.Charge(inputTokens, outputTokens);

    /// <summary>
    /// Sends a request and returns the success body. Transient statuses (408/429/5xx),
    /// timeouts and network errors are retried; all other statuses fail immediately.
    /// </summary>
    public async Task<string> SendJsonAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken = default,
        AiSpendBudget.Reservation? budget = null)
    {
        using var response = await SendJsonWithContextAsync(requestFactory, cancellationToken, budget).ConfigureAwait(false);
        return response.Body;
    }

    /// <summary>Owns per-call redaction credentials until post-response parsing has finished.</summary>
    public sealed class JsonResponse : IDisposable
    {
        private readonly AiCredentialRedactionContext _credentials;
        private string? _body;
        internal JsonResponse(string body, AiCredentialRedactionContext credentials)
        {
            _body = body;
            _credentials = credentials;
        }
        public string Body => _body ?? throw new ObjectDisposedException(nameof(JsonResponse));
        public string? SanitizeForRecord(string? text) => _credentials.SanitizeForRecord(text);
        internal bool ContainsCredential(string text) => _credentials.ContainsCredential(text);
        public void Dispose()
        {
            _body = null;
            _credentials.Dispose();
        }
    }

    /// <summary>Preserves only this logical call's exact attempt credentials for bounded error redaction.</summary>
    public async Task<JsonResponse> SendJsonWithContextAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken = default,
        AiSpendBudget.Reservation? budget = null)
    {
        ArgumentNullException.ThrowIfNull(requestFactory);

        cancellationToken.ThrowIfCancellationRequested();
        var credentials = new AiCredentialRedactionContext();
        try
        {
            for (var attempt = 1; ; attempt++)
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(_settings.Timeout);
                HttpResponseMessage? response = null;
                TimeSpan? retryAfter = null;

                try
                {
                    using var request = requestFactory();
                    credentials.Remember(ApplyAuthorization(request));
                    timeoutCts.Token.ThrowIfCancellationRequested();
                    budget?.ReserveAttempt();
                    timeoutCts.Token.ThrowIfCancellationRequested();
                    if (attempt == 1 && _settings.MaxRequests > 0
                        && Interlocked.Increment(ref _logicalRequests) > _settings.MaxRequests)
                        throw new AiProviderException(AiFailureReasons.RequestBudgetExceeded,
                            detail: $"The configured limit of {_settings.MaxRequests} provider calls has been reached.");
                    budget?.MarkDispatched();

                    response = await _client
                        .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token)
                        .ConfigureAwait(false);

                    if (response.IsSuccessStatusCode)
                        return new JsonResponse(await ReadBoundedAsync(response.Content, _settings.MaxResponseBytes, timeoutCts.Token).ConfigureAwait(false), credentials);

                    retryAfter = RetryAfter(response);

                    if (IsRetryable(response.StatusCode) && attempt < _settings.MaxAttempts)
                    {
                        await _delay(Backoff(attempt, retryAfter), cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    var excerpt = await TryExcerptAsync(response.Content, credentials, timeoutCts.Token).ConfigureAwait(false);
                    throw new AiProviderException(ReasonFor(response.StatusCode), excerpt);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    if (attempt < _settings.MaxAttempts)
                    {
                        await _delay(Backoff(attempt, retryAfter), cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    throw new AiProviderException(AiFailureReasons.Timeout);
                }
                catch (HttpRequestException ex)
                {
                    if (attempt < _settings.MaxAttempts)
                    {
                        await _delay(Backoff(attempt, retryAfter), cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    throw new AiProviderException(AiFailureReasons.ProviderUnavailable, inner: ex);
                }
                finally
                {
                    response?.Dispose();
                }
            }
        }
        catch
        {
            credentials.Dispose();
            throw;
        }
    }

    private string? ApplyAuthorization(HttpRequestMessage request)
    {
        if (string.IsNullOrWhiteSpace(_settings.CredentialEnvironmentVariable)) return null;

        var secret = _credentials.Resolve(_settings.CredentialEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(secret))
            throw new AiProviderException(
                AiFailureReasons.ConfigurationInvalid,
                detail: $"Credential environment variable {_settings.CredentialEnvironmentVariable} is not set.");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        if (!string.IsNullOrWhiteSpace(_settings.Organization))
            request.Headers.TryAddWithoutValidation("OpenAI-Organization", _settings.Organization);
        return secret;
    }

    private async Task<string> ReadBoundedAsync(HttpContent content, int maxBytes, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is { } declared && declared > maxBytes)
            throw new AiProviderException(AiFailureReasons.ResponseTooLarge);

        await using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        var total = 0;

        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            total += read;
            if (total > maxBytes)
                throw new AiProviderException(AiFailureReasons.ResponseTooLarge);
            buffer.Write(chunk, 0, read);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private async Task<string?> TryExcerptAsync(HttpContent content, AiCredentialRedactionContext credentials, CancellationToken cancellationToken)
    {
        try
        {
            if (credentials.HasCredentials && content.Headers.ContentLength is > ErrorExcerptBytes)
                return "[redacted]";
            if (content.Headers.ContentLength is { } declared && declared > 8 * 1024 * 1024) return null;

            await using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var buffer = new byte[ErrorExcerptBytes + 1];
            int total = 0;
            while (total < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken).ConfigureAwait(false);
                if (read == 0) break;
                total += read;
            }
            // A bounded prefix can cut through a credential. Do not guess which
            // fragments are safe; suppress credential-bearing truncated responses.
            if (credentials.HasCredentials && total > ErrorExcerptBytes) return "[redacted]";
            return credentials.SanitizeForRecord(Encoding.UTF8.GetString(buffer, 0, Math.Min(total, ErrorExcerptBytes)));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    private TimeSpan Backoff(int attempt, TimeSpan? retryAfter)
    {
        if (retryAfter is { } explicitWait && explicitWait >= TimeSpan.Zero)
            return explicitWait > MaxBackoff ? MaxBackoff : explicitWait;

        var seconds = _settings.BaseRetryDelay.TotalSeconds * Math.Pow(2, Math.Max(0, attempt - 1));
        return TimeSpan.FromSeconds(Math.Min(seconds, MaxBackoff.TotalSeconds));
    }

    private static TimeSpan? RetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        if (header?.Delta is { } delta) return delta;
        if (header?.Date is { } date) return date - DateTimeOffset.UtcNow;
        return null;
    }

    private static bool IsRetryable(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)status >= 500;

    private static string ReasonFor(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => AiFailureReasons.AuthenticationFailed,
        HttpStatusCode.TooManyRequests => AiFailureReasons.RateLimited,
        HttpStatusCode.RequestTimeout => AiFailureReasons.Timeout,
        HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.UnprocessableEntity
            => AiFailureReasons.RequestInvalid,
        _ when (int)status >= 500 => AiFailureReasons.ProviderUnavailable,
        _ => AiFailureReasons.RequestFailed,
    };
}
