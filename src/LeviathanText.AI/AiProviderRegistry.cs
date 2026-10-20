using LeviathanText.Contracts;

namespace LeviathanText.AI;

/// <summary>
/// Resolves an opaque <see cref="AiProposalRequest.ProviderConfigRef"/> to a concrete
/// local or hosted adapter, so provider switching is a configuration change rather than
/// a code change (project-plan §4; Phase 2 gate). Credentials resolve through the shared
/// <see cref="IAiCredentialSource"/> and never live in the registry.
/// </summary>
public sealed class AiProviderRegistry
{
    private readonly Dictionary<string, AiProviderSettings> _settings;
    private readonly HttpClient _client;
    private readonly IAiCredentialSource? _credentials;
    private readonly Func<TimeSpan, CancellationToken, Task>? _delay;

    public AiProviderRegistry(
        IEnumerable<AiProviderSettings> settings,
        HttpClient client,
        IAiCredentialSource? credentials = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(client);

        _client = client;
        _credentials = credentials;
        _delay = delay;
        _settings = new Dictionary<string, AiProviderSettings>(StringComparer.Ordinal);

        foreach (var entry in settings)
        {
            entry.Validate();
            if (!_settings.TryAdd(entry.ProviderId, entry))
                throw new ArgumentException($"Duplicate provider id '{entry.ProviderId}'.", nameof(settings));
        }
    }

    public IReadOnlyList<string> ProviderIds => _settings.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray();

    public AiProviderSettings SettingsFor(string providerConfigRef) =>
        _settings.TryGetValue(providerConfigRef, out var settings)
            ? settings
            : throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = providerConfigRef,
                Details = new Dictionary<string, string> { ["known"] = string.Join(',', ProviderIds) },
                Message = $"No configured provider matches '{providerConfigRef}'.",
            });

    /// <summary>Schema-validating provider, the only shape the proposal API should consume.</summary>
    public IAiProvider ProviderFor(string providerConfigRef) =>
        new SchemaValidatingAiProvider(RawProviderFor(providerConfigRef));

    /// <summary>Raw adapter for callers that need the unvalidated text path (tests/tools).</summary>
    public AiHttpProvider RawProviderFor(string providerConfigRef) =>
        new(SettingsFor(providerConfigRef), _client, _credentials, _delay);

    public IAiModelCatalog ModelCatalogFor(string providerConfigRef) =>
        new AiHttpModelCatalog(SettingsFor(providerConfigRef), _client, _credentials, _delay);
}
