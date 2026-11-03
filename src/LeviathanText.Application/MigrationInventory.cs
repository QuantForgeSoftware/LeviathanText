using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LeviathanText.Application;

/// <summary>
/// Read-only inventory of an existing collection tree for the Kraken→LeviathanText migration
/// work (project-plan §8). It classifies public-format inputs by content, records sizes and
/// SHA-256 digests, and reports unclassified files. It never writes to, moves or normalizes
/// the source tree; importing and comparison are separate authorized steps.
/// </summary>
public static class MigrationInventory
{
    public const int DefaultMaxFiles = 20_000;
    public const long MaxHashBytes = 512L * 1024 * 1024;

    public sealed record FileEntry(
        string RelativePath,
        string Kind,
        long Bytes,
        string? Sha256,
        string? Note = null);

    public sealed record Report(
        DateTimeOffset ScannedAt,
        string Root,
        int FileCount,
        long TotalBytes,
        IReadOnlyDictionary<string, int> Counts,
        IReadOnlyList<FileEntry> Files,
        IReadOnlyList<string> Notes);

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".tif", ".tiff", ".bmp", ".gif", ".webp",
    };

    private static readonly HashSet<string> ModelWeightExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".safetensors", ".mlmodel", ".onnx", ".pt", ".pth", ".ckpt", ".best", ".failure",
    };

    private static readonly HashSet<string> ScriptExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".sh", ".ps1", ".py", ".js", ".ts", ".bat", ".cmd",
    };

    public static Report Scan(string root, int maxFiles = DefaultMaxFiles, bool computeHashes = true)
    {
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException($"Inventory root is not readable: {root}");
        if (maxFiles <= 0) throw new ArgumentOutOfRangeException(nameof(maxFiles));

        var entries = new List<FileEntry>();
        var notes = new List<string>();
        int skipped = 0;
        foreach (var path in EnumerateFiles(root))
        {
            if (entries.Count >= maxFiles)
            {
                notes.Add($"stopped after {maxFiles} files; the remaining files were not inventoried");
                break;
            }
            FileInfo info;
            try
            {
                info = new FileInfo(path);
                if (!info.Exists) continue;
                if (info.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    skipped++;
                    continue;
                }
            }
            catch (IOException ex)
            {
                notes.Add($"{Relative(root, path)}: {ex.Message}");
                continue;
            }

            string? hash = null;
            if (computeHashes && info.Length <= MaxHashBytes)
            {
                try
                {
                    using var stream = File.OpenRead(path);
                    hash = Convert.ToHexStringLower(SHA256.HashData(stream));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    notes.Add($"{Relative(root, path)}: cannot hash ({ex.Message})");
                }
            }
            else if (computeHashes)
            {
                notes.Add($"{Relative(root, path)}: larger than {MaxHashBytes} bytes; not hashed");
            }

            var kind = Classify(path, info.Length, out var note);
            entries.Add(new FileEntry(Relative(root, path), kind, info.Length, hash, note));
        }

        if (skipped > 0) notes.Add($"skipped {skipped} symbolic link(s)");
        foreach (var duplicate in entries
                     .Where(entry => entry.Sha256 is { Length: 64 })
                     .GroupBy(entry => entry.Sha256!, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
            notes.Add($"duplicate content {duplicate.Key[..12]}: " +
                      string.Join(", ", duplicate.Select(entry => entry.RelativePath).Take(10)));
        var counts = entries
            .GroupBy(e => e.Kind, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        return new Report(DateTimeOffset.UtcNow, Path.GetFullPath(root), entries.Count,
            entries.Sum(e => e.Bytes), counts, entries, notes);
    }

    public static string ToJson(Report report) => JsonSerializer.Serialize(report, JsonOptions);

    public static string ToCsv(Report report)
    {
        var builder = new StringBuilder("relativePath,kind,bytes,sha256,note\n");
        foreach (var file in report.Files)
        {
            builder.AppendLine(string.Join(',', new[]
            {
                Quote(file.RelativePath), Quote(file.Kind),
                file.Bytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Quote(file.Sha256 ?? ""), Quote(file.Note ?? ""),
            }));
        }
        return builder.ToString();
    }

    private static string Quote(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private static IEnumerable<string> EnumerateFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            string[] files, directories;
            try
            {
                files = Directory.GetFiles(directory);
                directories = Directory.GetDirectories(directory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }
            foreach (var file in files.OrderBy(f => f, StringComparer.Ordinal)) yield return file;
            foreach (var child in directories.OrderBy(d => d, StringComparer.Ordinal))
            {
                try
                {
                    // Never follow a symbolic-link directory out of the requested tree.
                    if (new DirectoryInfo(child).Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    continue;
                }
                pending.Push(child);
            }
        }
    }

    private static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');

    private static string Classify(string path, long bytes, out string? note)
    {
        note = null;
        var extension = Path.GetExtension(path);
        if (ImageExtensions.Contains(extension)) return "image";
        if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)) return "pdf";
        if (ModelWeightExtensions.Contains(extension))
        {
            note = "model weight/checkpoint; compatibility is not guaranteed";
            return "model-weight";
        }
        if (ScriptExtensions.Contains(extension)) return "script";
        if (extension is ".zip" or ".tar" or ".gz" or ".tgz" or ".7z" or ".bz2")
        {
            note = "archive; extract to a separate directory before importing";
            return "archive";
        }
        if (extension is ".md" or ".rst" or ".adoc" or ".tex")
            return "documentation";
        if (extension is ".xsd" or ".rng" or ".rnc" or ".dtd")
            return "schema";
        if (extension is ".yaml" or ".yml" or ".toml" or ".ini" or ".cfg" or ".conf" or ".sample")
            return "config";
        if (extension is ".arrow" or ".parquet" or ".csv" or ".tsv" or ".jsonl")
            return "dataset";
        if (extension == ".svg") return "image";
        if (extension is ".css" or ".scss") return "style";
        if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
            return ClassifyJson(path);
        if (extension.Equals(".xml", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".html", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".htm", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".hocr", StringComparison.OrdinalIgnoreCase))
            return ClassifyMarkup(path);
        if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase)) return "text";
        if (bytes == 0)
        {
            note = "empty file";
            return "other";
        }
        return "other";
    }

    private static string ClassifyJson(string path)
    {
        var text = ReadPrefix(path, 32 * 1024);
        if (text.Contains("\"@context\"", StringComparison.Ordinal)
            || text.Contains("\"type\": \"Manifest\"", StringComparison.Ordinal)
            || text.Contains("\"type\":\"Manifest\"", StringComparison.Ordinal))
            return "iiif-manifest";
        if (text.Contains("\"architecture\"", StringComparison.Ordinal)
            || text.Contains("\"modelId\"", StringComparison.Ordinal))
            return "model-metadata";
        return "json";
    }

    private static string ClassifyMarkup(string path)
    {
        var text = ReadPrefix(path, 32 * 1024);
        if (text.Contains("PcGts", StringComparison.Ordinal)
            || text.Contains("pagecontent", StringComparison.Ordinal)) return "page-xml";
        if (text.Contains("<alto", StringComparison.OrdinalIgnoreCase)
            || text.Contains("alto.org", StringComparison.OrdinalIgnoreCase)) return "alto-xml";
        if (text.Contains("<TEI", StringComparison.Ordinal)
            || text.Contains("tei-c.org", StringComparison.Ordinal)) return "tei-epidoc";
        if (text.Contains("ocr_page", StringComparison.Ordinal)
            || text.Contains("ocrx_word", StringComparison.Ordinal)) return "hocr";
        return "markup";
    }

    private static string ReadPrefix(string path, int maxBytes)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var buffer = new byte[Math.Min(maxBytes, checked((int)Math.Min(stream.Length, maxBytes)))];
            int read = stream.Read(buffer, 0, buffer.Length);
            return Encoding.UTF8.GetString(buffer, 0, read);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "";
        }
    }
}
