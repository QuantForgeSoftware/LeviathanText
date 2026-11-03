using System.IO.Compression;
using System.Net;
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
    /// <summary>Create a transport whose redirects remain visible to registry trust checks.</summary>
    public static HttpClient CreateHttpClient() => new(new HttpClientHandler { AllowAutoRedirect = false });

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
            if (entry is null) throw new InvalidDataException("Registry index contains a null entry.");
            ValidateModelId(entry.Id);
            if (!ids.Add(entry.Id))
                throw new InvalidDataException("Registry entries need unique, non-empty ids.");
            if (string.IsNullOrWhiteSpace(entry.Name)) throw new InvalidDataException($"Registry entry '{entry.Id}' has no name.");
            if (entry.Task is not ("recognition" or "segmentation" or "reading_order"))
                throw new InvalidDataException($"Registry entry '{entry.Id}' has an unsupported task '{entry.Task}'.");
            if (entry.DownloadUrl is null || !entry.DownloadUrl.IsAbsoluteUri
                || entry.DownloadUrl.Scheme is not ("http" or "https"))
                throw new InvalidDataException($"Registry entry '{entry.Id}' has an invalid download URL.");
            // Format 1 is legacy preprocessing; format 2 declares a typed preprocessing profile.
            if (entry.PackageFormatVersion is < 1 or > 2)
                throw new InvalidDataException($"Registry entry '{entry.Id}' has an unsupported package format.");
            if (entry.Sha256 is { } sha && (sha.Length != 64 || !sha.All(Uri.IsHexDigit)))
                throw new InvalidDataException($"Registry entry '{entry.Id}' has an invalid SHA-256 digest.");
        }
        return new RemoteModelIndex(raw, source);
    }

    /// <summary>
    /// Fetch an index, validating each redirect before following it. Caller-provided clients
    /// must disable automatic redirects; <see cref="CreateHttpClient"/> supplies a suitable transport.
    /// </summary>
    public static async Task<RemoteModelIndex> FetchAsync(
        HttpClient client, Uri indexUri, CancellationToken cancellation = default,
        RegistryTrustPolicy? trustPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(indexUri);
        if (!indexUri.IsAbsoluteUri || indexUri.Scheme is not ("http" or "https"))
            throw new ArgumentException("Registry index URL must be absolute HTTP(S).", nameof(indexUri));
        // ResponseHeadersRead releases HttpClient's timeout at the headers. Keep the
        // original whole-index timeout while checking redirects before reading bodies.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        if (client.Timeout != Timeout.InfiniteTimeSpan) timeout.CancelAfter(client.Timeout);
        using var response = await GetWithValidatedRedirectsAsync(client, indexUri, trustPolicy, timeout.Token);
        response.EnsureSuccessStatusCode();
        return Parse(await response.Content.ReadAsStringAsync(timeout.Token), indexUri.ToString());
    }

    internal static async Task<HttpResponseMessage> GetWithValidatedRedirectsAsync(
        HttpClient client, Uri uri, RegistryTrustPolicy? trustPolicy, CancellationToken cancellation)
    {
        const int maxRedirects = 5;
        for (var redirects = 0; ; redirects++)
        {
            if (!uri.IsAbsoluteUri || uri.Scheme is not ("http" or "https"))
                throw new InvalidDataException("Registry request URL must be absolute HTTP(S).");
            trustPolicy?.ValidateIndex(uri);
            var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellation);
            if (response.StatusCode is not (HttpStatusCode.MovedPermanently or HttpStatusCode.Found
                or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect))
                return response;

            using (response)
            {
                if (redirects >= maxRedirects)
                    throw new InvalidDataException("Registry request exceeded the redirect limit.");
                var location = response.Headers.Location;
                if (location is null || !Uri.TryCreate(uri, location, out var next))
                    throw new InvalidDataException("Registry redirect has no valid destination.");
                uri = next;
            }
        }
    }

    internal static void ValidateModelId(string id)
    {
        // Validate on every OS, so a Windows absolute path cannot become a literal filename
        // on Unix and an accepted ID keeps its meaning if a registry is used on another OS.
        if (string.IsNullOrWhiteSpace(id) || id is "." or ".." || Path.IsPathRooted(id)
            || id.Any(c => char.IsControl(c) || "/\\:*?\"<>|".Contains(c))
            || id.EndsWith('.') || id.EndsWith(' '))
            throw new InvalidDataException("Registry model id must be a safe, single filename component.");

        var stem = id.Split('.')[0];
        if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase) || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase)
            || stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && "123456789¹²³".Contains(stem[3]))
            throw new InvalidDataException("Registry model id cannot be a reserved device name.");
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
    /// Caller-provided clients must disable automatic redirects; use <see cref="ModelRegistry.CreateHttpClient"/>.
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
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(targetRoot));
        var destination = PackageDestination(root, entry.Id);
        trustPolicy?.Validate(entry);
        if (string.IsNullOrWhiteSpace(entry.Sha256))
            throw new InvalidDataException("Registry downloads require a SHA-256 checksum.");
        if (Directory.Exists(destination) || File.Exists(destination))
            throw new IOException($"Target package directory already exists: {destination}");

        // Only this invocation owns the staging directory. The destination is published once,
        // after verification and extraction, and is never removed by another pull's cleanup.
        var staging = Path.Combine(root, $".registry-download-{Guid.NewGuid():N}");
        var package = Path.Combine(staging, "package");
        var archivePath = Path.Combine(staging, "download.zip");
        try
        {
            Directory.CreateDirectory(package);
            using var response = await ModelRegistry.GetWithValidatedRedirectsAsync(
                client, entry.DownloadUrl, trustPolicy, cancellation);
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

            using (var archive = ZipFile.OpenRead(archivePath))
            {
                long extracted = 0;
                foreach (var item in archive.Entries)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var path = Path.GetFullPath(Path.Combine(package, item.FullName));
                    if (!path.StartsWith(package + Path.DirectorySeparatorChar, StringComparison.Ordinal))
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
            }
            cancellation.ThrowIfCancellationRequested();
            var encodedBytes = new FileInfo(archivePath).Length;
            Directory.Move(package, destination);
            return new DownloadedModel(entry.Id, destination, digest, encodedBytes);
        }
        finally
        {
            try { Directory.Delete(staging, recursive: true); } catch { /* best effort; never delete the destination */ }
        }
    }

    private static string PackageDestination(string root, string id)
    {
        ModelRegistry.ValidateModelId(id);
        var destination = Path.GetFullPath(Path.Combine(root, id));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(Path.GetDirectoryName(destination), root, comparison))
            throw new InvalidDataException("Registry model id must name a direct child of the target directory.");
        return destination;
    }
}
