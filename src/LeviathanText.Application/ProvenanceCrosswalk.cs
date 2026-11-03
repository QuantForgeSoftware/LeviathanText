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

    public sealed record LineRow(
        string ObjectId,
        string SurfaceId,
        string LineId,
        string LineSourceId,
        int? LineNumber,
        string RevisionId,
        string ImageSha256,
        string Tags);

    public sealed record FragmentRow(
        string ObjectId,
        string FragmentId,
        string FragmentRefs,
        string Metadata,
        string SurfaceIds);

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

    public static IReadOnlyList<LineRow> BuildLines(NativeProject project)
    {
        var rows = new List<LineRow>();
        foreach (var obj in project.Objects)
        {
            foreach (var surface in obj.Surfaces)
            {
                var checksum = surface.Captures.FirstOrDefault()?.Frame.ImageChecksum.Digest ?? "";
                foreach (var line in surface.Lines)
                {
                    rows.Add(new LineRow(
                        obj.Id.ToString(),
                        surface.Id.ToString(),
                        line.Id.ToString(),
                        line.Tags.TryGetValue("source-id", out var source) && source.Count > 0 ? source[0] : "",
                        line.LineNumber,
                        line.DiplomaticRevisionId?.ToString() ?? "",
                        checksum,
                        string.Join('|', line.Tags.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                            .Select(pair => $"{pair.Key}={string.Join(';', pair.Value)}"))));
                }
            }
        }
        return rows;
    }

    public static IReadOnlyList<FragmentRow> BuildFragments(NativeProject project)
    {
        var rows = new List<FragmentRow>();
        foreach (var obj in project.Objects)
            foreach (var fragment in obj.Fragments)
                rows.Add(new FragmentRow(
                    obj.Id.ToString(),
                    fragment.Id.ToString(),
                    FormatRefs(fragment.ExternalRefs),
                    FormatRefs(fragment.Metadata),
                    string.Join('|', fragment.SurfaceIds.Select(s => s.ToString()))));
        return rows;
    }

    public static string ToFragmentCsv(IReadOnlyList<FragmentRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("objectId,fragmentId,fragmentRefs,metadata,surfaceIds");
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(',', new[]
            {
                Quote(row.ObjectId), Quote(row.FragmentId), Quote(row.FragmentRefs),
                Quote(row.Metadata), Quote(row.SurfaceIds),
            }));
        }
        return builder.ToString();
    }

    public static string ToLineCsv(IReadOnlyList<LineRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("objectId,surfaceId,lineId,lineSourceId,lineNumber,revisionId,imageSha256,tags");
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(',', new[]
            {
                Quote(row.ObjectId),
                Quote(row.SurfaceId),
                Quote(row.LineId),
                Quote(row.LineSourceId),
                row.LineNumber?.ToString(CultureInfo.InvariantCulture) ?? "",
                Quote(row.RevisionId),
                Quote(row.ImageSha256),
                Quote(row.Tags),
            }));
        }
        return builder.ToString();
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
