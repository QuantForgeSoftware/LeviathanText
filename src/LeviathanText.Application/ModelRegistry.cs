using System.IO.Compression;
using System.Security.Cryptography;
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


/// <summary>A downloaded, checksum-verified registry package on local disk.</summary>
public sealed record DownloadedModel(string Id, string Directory, string Sha256, long EncodedBytes);

public static class ModelRegistryDownload
{
    public const long MaxEncodedBytes = 1L * 1024 * 1024 * 1024;
    public const long MaxExtractedBytes = 2L * 1024 * 1024 * 1024;

    /// <summary>
    /// Download and extract a registry package. A checksum is mandatory; paths inside the
    /// archive are confined to the target package directory and extraction is size-bounded.
    /// Package structure is not validated here; call the model loader afterward.
    /// </summary>
    public static async Task<DownloadedModel> DownloadAsync(
        HttpClient client,
        RemoteModelEntry entry,
        string targetRoot,
        RegistryTrustPolicy? trustPolicy = null,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetRoot);
        trustPolicy?.Validate(entry);
        if (string.IsNullOrWhiteSpace(entry.Sha256))
            throw new InvalidDataException("Registry downloads require a SHA-256 checksum.");
        Directory.CreateDirectory(targetRoot);

        var destination = Path.Combine(Path.GetFullPath(targetRoot), entry.Id);
        if (Directory.Exists(destination))
            throw new IOException($"Target package directory already exists: {destination}");
        Directory.CreateDirectory(destination);

        var archivePath = Path.Combine(Path.GetTempPath(), $"leviathan-registry-{Guid.NewGuid():N}.zip");
        try
        {
            using var response = await client.GetAsync(entry.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellation);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaxEncodedBytes)
                throw new InvalidDataException("Registry package exceeds the encoded-size limit.");
            await using (var source = await response.Content.ReadAsStreamAsync(cancellation))
            await using (var target = File.Create(archivePath))
            {
                var buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, cancellation)) > 0)
                {
                    total += read;
                    if (total > MaxEncodedBytes) throw new InvalidDataException("Registry package exceeds the encoded-size limit.");
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellation);
                }
            }

            var digest = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(archivePath, cancellation))).ToLowerInvariant();
            if (!string.Equals(digest, entry.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Registry package checksum does not match the registry entry.");

            using var archive = ZipFile.OpenRead(archivePath);
            long extracted = 0;
            foreach (var item in archive.Entries)
            {
                var path = Path.GetFullPath(Path.Combine(destination, item.FullName));
                if (!path.StartsWith(destination + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    throw new InvalidDataException($"Archive entry escapes the package directory: {item.FullName}");
                extracted += item.Length;
                if (extracted > MaxExtractedBytes) throw new InvalidDataException("Extracted package exceeds the size limit.");
                if (string.IsNullOrEmpty(item.Name))
                {
                    Directory.CreateDirectory(path);
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                item.ExtractToFile(path, overwrite: false);
            }
            return new DownloadedModel(entry.Id, destination, digest, new FileInfo(archivePath).Length);
        }
        catch
        {
            try { Directory.Delete(destination, recursive: true); } catch { /* best effort */ }
            throw;
        }
        finally
        {
            File.Delete(archivePath);
        }
    }
}
