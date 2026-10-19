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
    private int _logicalRequests;

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

    /// <summary>Conservative worst-case reservation before a request leaves the machine.</summary>
    public void EnsureBudget(int promptCharacters)
    {
        var estimatedInputTokens = Math.Max(1, promptCharacters / 4) + 16;
        _budget?.EnsureAffordable(estimatedInputTokens, _settings.MaxOutputTokens);
    }

    public void Charge(int inputTokens, int outputTokens) => _budget?.Charge(inputTokens, outputTokens);

    /// <summary>
    /// Sends a request and returns the success body. Transient statuses (408/429/5xx),
    /// timeouts and network errors are retried; all other statuses fail immediately.
    /// </summary>
    public async Task<string> SendJsonAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestFactory);

        if (_settings.MaxRequests > 0
            && Interlocked.Increment(ref _logicalRequests) > _settings.MaxRequests)
            throw new AiProviderException(
                AiFailureReasons.RequestBudgetExceeded,
                detail: $"The configured limit of {_settings.MaxRequests} provider calls has been reached.");

        for (var attempt = 1; ; attempt++)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_settings.Timeout);
            HttpResponseMessage? response = null;
            TimeSpan? retryAfter = null;

            try
            {
                using var request = requestFactory();
                ApplyAuthorization(request);

                response = await _client
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token)
                    .ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                    return await ReadBoundedAsync(response.Content, _settings.MaxResponseBytes, timeoutCts.Token).ConfigureAwait(false);

                retryAfter = RetryAfter(response);

                if (IsRetryable(response.StatusCode) && attempt < _settings.MaxAttempts)
                {
                    await _delay(Backoff(attempt, retryAfter), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var excerpt = await TryExcerptAsync(response.Content, timeoutCts.Token).ConfigureAwait(false);
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

    private void ApplyAuthorization(HttpRequestMessage request)
    {
        if (string.IsNullOrWhiteSpace(_settings.CredentialEnvironmentVariable)) return;

        var secret = _credentials.Resolve(_settings.CredentialEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(secret))
            throw new AiProviderException(
                AiFailureReasons.ConfigurationInvalid,
                detail: $"Credential environment variable {_settings.CredentialEnvironmentVariable} is not set.");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        if (!string.IsNullOrWhiteSpace(_settings.Organization))
            request.Headers.TryAddWithoutValidation("OpenAI-Organization", _settings.Organization);
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

    private async Task<string?> TryExcerptAsync(HttpContent content, CancellationToken cancellationToken)
    {
        try
        {
            if (content.Headers.ContentLength is { } declared && declared > 8 * 1024 * 1024) return null;

            await using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var buffer = new byte[ErrorExcerptBytes];
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            return SanitizeForRecord(Encoding.UTF8.GetString(buffer, 0, read));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    internal string? SanitizeForRecord(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;

        var cleaned = new string(text.Where(c => !char.IsControl(c) || c is '\n' or '\t').ToArray());
        var secret = string.IsNullOrWhiteSpace(_settings.CredentialEnvironmentVariable)
            ? null
            : _credentials.Resolve(_settings.CredentialEnvironmentVariable);
        if (!string.IsNullOrEmpty(secret) && secret.Length >= 4)
            cleaned = cleaned.Replace(secret, "[redacted]", StringComparison.Ordinal);

        return cleaned.Length <= 240 ? cleaned : cleaned[..240];
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
