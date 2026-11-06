using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application;

public sealed record CaptureRelocationEntry(string ObjectId, string SurfaceId, string CaptureId,
    string PreviousLocation, string Location, string Sha256, string Verification);
public sealed record CaptureRelocationReport(string ProjectId, string OldRoot, string NewRoot,
    bool Applied, IReadOnlyList<CaptureRelocationEntry> Captures);

/// <summary>
/// Rebinds an explicitly selected local image tree without changing capture identity or geometry.
/// Every candidate is byte-verified before one optimistic save containing the relocation receipt.
/// Image files are never copied or changed; the caller controls the moved tree's lifetime.
/// </summary>
public sealed class CaptureRelocation(IProjectStore store)
{
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public CaptureRelocationReport Rebind(EntityId projectId, string oldRoot, string newRoot, bool dryRun = false)
    {
        var oldPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(oldRoot));
        var newPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(newRoot));
        if (!Directory.Exists(newPath)) throw new DirectoryNotFoundException("New image root does not exist.");
        var oldCanonical = CanonicalPath(oldPath);
        var newCanonical = CanonicalPath(newPath);
        if (Within(oldCanonical, newCanonical) || Within(newCanonical, oldCanonical))
            throw new ArgumentException("Old and new image roots must be separate, non-overlapping trees.");
        var project = store.LoadProject(projectId) ?? throw new ArgumentException("Project does not exist.");
        if (project.Version <= 0)
            throw new InvalidOperationException("Capture relocation requires a versioned persisted project.");
        var entries = new List<CaptureRelocationEntry>();
        var objects = new List<NativeObject>();
        foreach (var obj in project.Objects)
        {
            var start = entries.Count;
            var surfaces = obj.Surfaces.Select(surface => surface with
            {
                Captures = surface.Captures.Select(capture =>
                {
                    // Relative paths have no stable project-root contract; remote references stay remote.
                    if (!Path.IsPathFullyQualified(capture.ImageLocation)) return capture;
                    var source = Path.GetFullPath(capture.ImageLocation);
                    if (!Within(oldPath, source) || source.Equals(oldPath, PathComparison)) return capture;
                    if (!Within(oldCanonical, CanonicalPath(source)))
                        throw new IOException("Source capture escapes the selected image root through a link.");
                    var destination = Path.GetFullPath(Path.Combine(newPath, Path.GetRelativePath(oldPath, source)));
                    if (!Within(newCanonical, CanonicalPath(destination)))
                        throw new IOException("Destination capture escapes the selected image root through a link.");
                    var expected = ExpectedDigest(capture, source, out var verification);
                    if (!Hash(destination).Equals(expected, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException($"Moved image checksum mismatch for capture {capture.Id}.");
                    entries.Add(new(obj.Id.ToString(), surface.Id.ToString(), capture.Id.ToString(),
                        capture.ImageLocation, destination, expected, verification));
                    return capture with { ImageLocation = destination };
                }).ToArray(),
            }).ToArray();
            if (entries.Count == start) { objects.Add(obj); continue; }
            var receipt = JsonSerializer.Serialize(new
            {
                schema = "leviathan.capture-relocation.v1", projectId = projectId.ToString(),
                projectVersion = project.Version, at = DateTimeOffset.UtcNow,
                oldRoot = oldPath, newRoot = newPath, captures = entries.Skip(start).ToArray(),
            });
            if (Encoding.UTF8.GetByteCount(receipt) > TeiXml.MaxAttachmentBytes)
                throw new InvalidDataException("Relocation receipt exceeds the attachment byte limit; select a smaller tree.");
            objects.Add(obj with
            {
                Surfaces = surfaces,
                Attachments = [.. obj.Attachments, new NativeAttachment
                {
                    Id = $"relocation-{Guid.NewGuid():N}", Name = "capture-relocation.json",
                    MediaType = "application/json", Role = "capture-relocation",
                    Content = receipt, Sha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(receipt))),
                }],
            });
        }
        if (!dryRun && entries.Count > 0) store.SaveProject(project with { Objects = objects });
        return new(projectId.ToString(), oldPath, newPath, !dryRun && entries.Count > 0, entries);
    }

    private static string ExpectedDigest(NativeCapture capture, string source, out string verification)
    {
        var checksum = capture.Frame.ImageChecksum;
        if (checksum.Algorithm.Equals("sha256", StringComparison.OrdinalIgnoreCase))
        {
            if (checksum.Digest.Length != 64 || !checksum.Digest.All(char.IsAsciiHexDigit))
                throw new InvalidDataException($"Invalid recorded SHA-256 for capture {capture.Id}.");
            var digest = checksum.Digest.ToLowerInvariant();
            if (File.Exists(source) && !Hash(source).Equals(digest, StringComparison.Ordinal))
                throw new InvalidDataException($"Original image checksum mismatch for capture {capture.Id}.");
            verification = "recorded-sha256";
            return digest;
        }
        if (!checksum.Algorithm.Equals("unavailable", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Unsupported recorded checksum for capture {capture.Id}; SHA-256 is required.");
        if (!File.Exists(source))
            throw new InvalidDataException($"Capture {capture.Id} has neither a recorded SHA-256 nor a readable original.");
        verification = "original-bytes";
        return Hash(source);
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static bool Within(string root, string path) => path.Equals(root, PathComparison)
        || path.StartsWith(Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar, PathComparison);

    private static string CanonicalPath(string path)
    {
        FileSystemInfo entry = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
        if (entry.LinkTarget is not null)
            return CanonicalPath(entry.ResolveLinkTarget(true)?.FullName
                ?? throw new IOException("Cannot resolve image path link."));
        var parent = Path.GetDirectoryName(path);
        return parent is null ? path : Path.Combine(CanonicalPath(parent), Path.GetFileName(path));
    }
}
