namespace LeviathanText.Application;

/// <summary>
/// Opt-in trust policy for remote registry entries. The default is deny-all: a caller must
/// explicitly allow a host, and downloads still require a SHA-256 and HTTPS unless a local
/// test explicitly relaxes that rule.
/// </summary>
public sealed record RegistryTrustPolicy(
    IReadOnlySet<string> AllowedHosts,
    bool RequireHttps = true,
    bool RequireChecksum = true)
{
    public static RegistryTrustPolicy DenyAll { get; } = new(
        new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    public static RegistryTrustPolicy Allow(params string[] hosts) => new(
        hosts.ToHashSet(StringComparer.OrdinalIgnoreCase));

    public void Validate(RemoteModelEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (AllowedHosts.Count == 0)
            throw new InvalidOperationException("Remote registry access is not trusted; allow a host explicitly.");
        if (entry.DownloadUrl is null || !entry.DownloadUrl.IsAbsoluteUri)
            throw new InvalidDataException("Registry entry has no absolute download URL.");
        if (RequireHttps && entry.DownloadUrl.Scheme != Uri.UriSchemeHttps)
            throw new InvalidDataException("Registry downloads require HTTPS under the current trust policy.");
        if (!AllowedHosts.Any(host => HostMatches(entry.DownloadUrl.Host, host)))
            throw new InvalidDataException($"Registry host '{entry.DownloadUrl.Host}' is not in the allow-list.");
        if (RequireChecksum && string.IsNullOrWhiteSpace(entry.Sha256))
            throw new InvalidDataException("Registry download requires a SHA-256 checksum.");
    }

    private static bool HostMatches(string host, string allowed) =>
        allowed.StartsWith("*.", StringComparison.Ordinal)
            ? host.EndsWith(allowed[1..], StringComparison.OrdinalIgnoreCase)
              && host.Length > allowed.Length - 1
            : string.Equals(host, allowed, StringComparison.OrdinalIgnoreCase);
}
