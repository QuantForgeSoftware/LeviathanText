using System.Text;
using LeviathanText.Contracts;

namespace LeviathanText.Application;

/// <summary>
/// Bounded migration importer: creates a new project and imports the public document formats
/// discovered by <see cref="MigrationInventory"/>. It never modifies the source tree, never
/// fetches remote images, and reports every skipped/failed file instead of guessing a pairing.
/// </summary>
public static class MigrationImport
{
    public sealed record Row(
        string Source,
        string Kind,
        string Status,
        string? ObjectId,
        string? SurfaceId,
        string Note);

    public static (EntityId ProjectId, IReadOnlyList<Row> Rows) Import(
        ProjectService projects, InterchangeService interchange, string tree, string projectName,
        int limit = 0)
    {
        var inventory = MigrationInventory.Scan(tree, maxFiles: Math.Max(1, limit > 0 ? limit * 10 : 20_000),
            computeHashes: false);
        var documents = inventory.Files
            .Where(file => file.Kind is "page-xml" or "tei-epidoc" or "alto-xml" or "hocr")
            .ToList();
        if (limit > 0) documents = [.. documents.Take(limit)];

        var project = projects.Create(projectName);
        var rows = new List<Row>();
        foreach (var document in documents)
        {
            var path = Path.Combine(tree, document.RelativePath);
            try
            {
                switch (document.Kind)
                {
                    case "page-xml":
                    {
                        var (imported, unsupported) = interchange.ImportPage(project, path);
                        project = projects.Load(project.Id)!;
                        rows.Add(new Row(document.RelativePath, document.Kind, "imported",
                            imported.Id.ToString(), imported.Surfaces[0].Id.ToString(),
                            $"{unsupported.Count} unsupported note(s)"));
                        break;
                    }
                    case "tei-epidoc":
                    {
                        var image = ResolveImage(path);
                        if (image is null)
                        {
                            rows.Add(new Row(document.RelativePath, document.Kind, "skipped", null, null,
                                "no local image with the record stem"));
                            break;
                        }
                        var (imported, notes, _) = interchange.ImportTei(project, path, image);
                        project = projects.Load(project.Id)!;
                        rows.Add(new Row(document.RelativePath, document.Kind, "imported",
                            imported.Id.ToString(), imported.Surfaces[0].Id.ToString(),
                            $"{notes.Count} import note(s)"));
                        break;
                    }
                    case "alto-xml":
                    case "hocr":
                    {
                        var image = ResolveImage(path);
                        if (image is null)
                        {
                            rows.Add(new Row(document.RelativePath, document.Kind, "skipped", null, null,
                                "no local image with the record stem"));
                            break;
                        }
                        var (imported, unsupported) = interchange.ImportLegacyLayout(project, path, image,
                            document.Kind == "hocr" ? "hocr" : "alto");
                        project = projects.Load(project.Id)!;
                        rows.Add(new Row(document.RelativePath, document.Kind, "imported",
                            imported.Id.ToString(), imported.Surfaces[0].Id.ToString(),
                            $"{unsupported.Count} unsupported note(s)"));
                        break;
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                rows.Add(new Row(document.RelativePath, document.Kind, "failed", null, null, ex.Message));
            }
        }
        return (project.Id, rows);
    }

    public static string ToCsv(IReadOnlyList<Row> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("source,kind,status,objectId,surfaceId,note");
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(',', new[]
            {
                Quote(row.Source),
                Quote(row.Kind),
                Quote(row.Status),
                Quote(row.ObjectId ?? ""),
                Quote(row.SurfaceId ?? ""),
                Quote(row.Note),
            }));
        }
        return builder.ToString();
    }

    public static string Summary(IReadOnlyList<Row> rows) =>
        string.Join(", ", rows.GroupBy(row => row.Status, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Key}={group.Count()}"));

    private static string? ResolveImage(string documentPath)
    {
        var directory = Path.GetDirectoryName(documentPath)!;
        var stem = Path.GetFileNameWithoutExtension(documentPath);
        var candidates = new List<string>();
        foreach (var extension in new[] { ".png", ".jpg", ".jpeg", ".tif", ".tiff" })
        {
            candidates.Add(Path.Combine(directory, stem + extension));
            candidates.Add(Path.Combine(directory, "images", stem + extension));
            candidates.Add(Path.Combine(Path.GetDirectoryName(directory)!, "images", stem + extension));
        }
        return candidates.FirstOrDefault(File.Exists);
    }

    private static string Quote(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
