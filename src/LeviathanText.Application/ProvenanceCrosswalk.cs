using System.Globalization;
using System.Text;
using LeviathanText.Formats;

namespace LeviathanText.Application;

/// <summary>
/// Read-only object/surface/capture/line provenance crosswalk (project-plan §3/§8). It joins
/// native ids to retained external/source identifiers and image hashes so an import can be
/// audited without treating source ids as primary keys. It does not merge or renumber anything.
/// </summary>
public static class ProvenanceCrosswalk
{
    public sealed record Row(
        string ObjectId,
        string ObjectRefs,
        string SurfaceId,
        string SurfaceRefs,
        string CaptureId,
        string ImageLocation,
        string ImageSha256,
        int Lines,
        string LineSourceIds);

    public static IReadOnlyList<Row> Build(NativeProject project)
    {
        var rows = new List<Row>();
        foreach (var obj in project.Objects)
        {
            var objectRefs = FormatRefs(obj.ExternalRefs);
            foreach (var surface in obj.Surfaces)
            {
                var capture = surface.Captures.FirstOrDefault();
                var sourceIds = string.Join('|', surface.Lines
                    .Select(line => line.Tags.TryGetValue("source-id", out var value) && value.Count > 0
                        ? value[0]
                        : "")
                    .Where(id => id.Length > 0));
                rows.Add(new Row(
                    obj.Id.ToString(),
                    objectRefs,
                    surface.Id.ToString(),
                    FormatRefs(surface.ExternalRefs),
                    capture?.Id.ToString() ?? "",
                    capture?.ImageLocation ?? "",
                    capture?.Frame.ImageChecksum.Digest ?? "",
                    surface.Lines.Count,
                    sourceIds));
            }
        }
        return rows;
    }

    public static string ToCsv(IReadOnlyList<Row> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("objectId,objectRefs,surfaceId,surfaceRefs,captureId,imageLocation,imageSha256,lineCount,lineSourceIds");
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(',', new[]
            {
                Quote(row.ObjectId),
                Quote(row.ObjectRefs),
                Quote(row.SurfaceId),
                Quote(row.SurfaceRefs),
                Quote(row.CaptureId),
                Quote(row.ImageLocation),
                Quote(row.ImageSha256),
                row.Lines.ToString(CultureInfo.InvariantCulture),
                Quote(row.LineSourceIds),
            }));
        }
        return builder.ToString();
    }

    private static string FormatRefs(IReadOnlyDictionary<string, string> refs) =>
        string.Join('|', refs.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={pair.Value}"));

    private static string Quote(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
