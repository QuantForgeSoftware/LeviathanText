using System.Text.Json;

namespace LeviathanText.Application;

/// <summary>One model advertised by a remote registry. Download is deliberately not part
/// of this type: a registry entry is metadata, not trust.</summary>
public sealed record RemoteModelEntry(
    string Id,
    string Name,
    string Task,
    Uri DownloadUrl,
    string? Sha256 = null,
    int PackageFormatVersion = 1);

public sealed record RemoteModelIndex(IReadOnlyList<RemoteModelEntry> Models, string? Source);

/// <summary>
/// Parses and fetches a small HTTP registry index. The index must be a JSON array of
/// model entries; unknown/malformed entries are rejected rather than silently skipped.
/// HTTPS is preferred but HTTP is accepted for a local test registry.
/// </summary>
public static class ModelRegistry
{
    public static RemoteModelIndex Parse(string json, string? source = null)
    {
        ArgumentNullException.ThrowIfNull(json);
        List<RemoteModelEntry>? raw;
        try
        {
            raw = JsonSerializer.Deserialize<List<RemoteModelEntry>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Registry index is not valid JSON.", ex);
        }
        if (raw is null) throw new InvalidDataException("Registry index is empty.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in raw)
        {
            if (string.IsNullOrWhiteSpace(entry.Id) || !ids.Add(entry.Id))
                throw new InvalidDataException("Registry entries need unique, non-empty ids.");
            if (string.IsNullOrWhiteSpace(entry.Name)) throw new InvalidDataException($"Registry entry '{entry.Id}' has no name.");
            if (entry.Task is not ("recognition" or "segmentation" or "reading_order"))
                throw new InvalidDataException($"Registry entry '{entry.Id}' has an unsupported task '{entry.Task}'.");
            if (entry.DownloadUrl is null || !entry.DownloadUrl.IsAbsoluteUri
                || entry.DownloadUrl.Scheme is not ("http" or "https"))
                throw new InvalidDataException($"Registry entry '{entry.Id}' has an invalid download URL.");
            if (entry.PackageFormatVersion is < 1 or > 1)
                throw new InvalidDataException($"Registry entry '{entry.Id}' has an unsupported package format.");
            if (entry.Sha256 is { } sha && (sha.Length != 64 || !sha.All(Uri.IsHexDigit)))
                throw new InvalidDataException($"Registry entry '{entry.Id}' has an invalid SHA-256 digest.");
        }
        return new RemoteModelIndex(raw, source);
    }

    public static async Task<RemoteModelIndex> FetchAsync(
        HttpClient client, Uri indexUri, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(indexUri);
        if (!indexUri.IsAbsoluteUri || indexUri.Scheme is not ("http" or "https"))
            throw new ArgumentException("Registry index URL must be absolute HTTP(S).", nameof(indexUri));
        using var response = await client.GetAsync(indexUri, cancellation);
        response.EnsureSuccessStatusCode();
        return Parse(await response.Content.ReadAsStringAsync(cancellation), indexUri.ToString());
    }
}
