using System.Security.Cryptography;
using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application;

/// <summary>
/// Resolves a capture to local image bytes for analysis stages. Local paths pass
/// through; http(s) URLs are fetched once into a checksummed cache (bounded size) and
/// the capture is rewritten to the local path with the recorded checksum, so later
/// stages work offline and provenance is explicit.
/// </summary>
public sealed class ImageResolver(IProjectStore store, string cacheDirectory, HttpClient? http = null)
{
    public const long MaxBytes = 64L * 1024 * 1024;

    public sealed record Result(NativeProject Project, NativeSurface Surface, NativeCapture Capture);

    public Result EnsureLocal(NativeProject project, EntityId surfaceId)
    {
        var surface = project.Objects.SelectMany(o => o.Surfaces).FirstOrDefault(s => s.Id == surfaceId)
            ?? throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = surfaceId.ToString(),
                Message = "No such surface in the project.",
            });

        var capture = surface.Captures.FirstOrDefault()
            ?? throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = surfaceId.ToString(),
                Message = "Surface has no capture.",
            });

        if (File.Exists(capture.ImageLocation))
            return new Result(project, surface, capture);

        if (!Uri.TryCreate(capture.ImageLocation, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = capture.ImageLocation,
                Message = "Capture image is neither a readable local file nor an http(s) URL.",
            });

        Directory.CreateDirectory(cacheDirectory);
        using var ownedClient = http is null ? new HttpClient { Timeout = TimeSpan.FromSeconds(60) } : null;
        var client = http ?? ownedClient!;
        using var response = client.SendAsync(new HttpRequestMessage(HttpMethod.Get, uri))
            .GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();

        using var buffer = new MemoryStream();
        using (var stream = response.Content.ReadAsStream())
        {
            var chunk = new byte[81920];
            int read;
            while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
            {
                if (buffer.Length + read > MaxBytes)
                    throw new LeviathanException(new LeviathanError
                    {
                        Code = ErrorCode.RejectedArchive,
                        Scope = capture.ImageLocation,
                        Details = new Dictionary<string, string> { ["maxBytes"] = MaxBytes.ToString() },
                        Message = "Remote image exceeds the configured size limit.",
                    });
                buffer.Write(chunk, 0, read);
            }
        }

        var bytes = buffer.ToArray();
        var digest = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var extension = (response.Content.Headers.ContentType?.MediaType) switch
        {
            "image/png" => ".png",
            "image/tiff" or "image/tif" => ".tif",
            _ => ".jpg",
        };
        var path = Path.Combine(cacheDirectory, digest + extension);
        if (!File.Exists(path)) File.WriteAllBytes(path, bytes);

        var updatedCapture = capture with
        {
            ImageLocation = path,
            Frame = capture.Frame with { ImageChecksum = new Checksum { Algorithm = "sha256", Digest = digest } },
        };
        var updatedSurface = surface with
        {
            Captures = [.. surface.Captures.Select(c => c.Id == capture.Id ? updatedCapture : c)],
        };
        var updatedProject = project with
        {
            Objects = [.. project.Objects.Select(o => o.Surfaces.Any(s => s.Id == surfaceId)
                ? o with { Surfaces = [.. o.Surfaces.Select(s => s.Id == surfaceId ? updatedSurface : s)] }
                : o)],
        };
        store.SaveProject(updatedProject);
        return new Result(updatedProject, updatedSurface, updatedCapture);
    }
}
