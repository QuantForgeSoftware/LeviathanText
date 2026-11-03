using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application;

/// <summary>
/// Read-only cross-object overlap audit for corpus lineage (DATA-02): repeated external
/// identifiers and repeated image checksums are reported so alternate views, fragments,
/// crops or editions can be kept in one split. It does not merge or delete anything.
/// </summary>
public static class CorpusOverlap
{
    public sealed record Overlap(string Kind, string Value, string Entities, string Note);

    public static IReadOnlyList<Overlap> Audit(NativeProject project)
    {
        var rows = new List<Overlap>();

        foreach (var group in project.Objects
                     .SelectMany(obj => obj.ExternalRefs.Select(pair => (obj, pair)))
                     .Where(item => !string.IsNullOrWhiteSpace(item.pair.Value))
                     .GroupBy(item => $"{item.pair.Key}={item.pair.Value}", StringComparer.Ordinal)
                     .Where(group => group.Select(item => item.obj.Id).Distinct().Count() > 1))
            rows.Add(new Overlap("external-ref", group.Key,
                string.Join(", ", group.Select(item => item.obj.Id).Distinct()),
                "The same source identifier is attached to multiple objects; review object/fragment lineage."));

        foreach (var group in project.Objects
                     .SelectMany(obj => obj.Fragments)
                     .SelectMany(fragment => fragment.ExternalRefs.Select(pair => (fragment, pair)))
                     .Where(item => !string.IsNullOrWhiteSpace(item.pair.Value))
                     .GroupBy(item => $"{item.pair.Key}={item.pair.Value}", StringComparer.Ordinal)
                     .Where(group => group.Select(item => item.fragment.Id).Distinct().Count() > 1))
            rows.Add(new Overlap("fragment-ref", group.Key,
                string.Join(", ", group.Select(item => item.fragment.Id).Distinct()),
                "The same source identifier is attached to multiple fragments; review physical lineage."));

        foreach (var group in project.Objects
                     .SelectMany(obj => obj.Surfaces)
                     .SelectMany(surface => surface.Captures)
                     .Where(capture => !string.IsNullOrWhiteSpace(capture.Frame.ImageChecksum.Digest))
                     .GroupBy(capture => capture.Frame.ImageChecksum.Digest, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Select(capture => capture.Id).Distinct().Count() > 1))
            rows.Add(new Overlap("image-checksum", group.Key[..Math.Min(16, group.Key.Length)],
                string.Join(", ", group.Select(capture => capture.Id).Distinct()),
                "The same image bytes are used by multiple captures; keep them in one physical split."));

        foreach (var group in project.Objects
                     .SelectMany(obj => obj.Surfaces)
                     .SelectMany(surface => surface.Lines
                         .Where(line => line.Tags.TryGetValue("source-id", out var value) && value.Count > 0)
                         .Select(line => (surface, line)))
                     .GroupBy(item => item.line.Tags["source-id"][0], StringComparer.Ordinal)
                     .Where(group => group.Select(item => item.line.Id).Distinct().Count() > 1))
            rows.Add(new Overlap("line-source-id", group.Key,
                string.Join(", ", group.Select(item => $"{item.surface.Id}/{item.line.Id}").Distinct()),
                "The same source line id is attached to multiple native lines; deduplicate import lineage."));

        foreach (var group in project.Objects
                     .SelectMany(obj => obj.Surfaces)
                     .SelectMany(surface => surface.ExternalRefs.Select(pair => (surface, pair)))
                     .Where(item => !string.IsNullOrWhiteSpace(item.pair.Value))
                     .GroupBy(item => $"{item.pair.Key}={item.pair.Value}", StringComparer.Ordinal)
                     .Where(group => group.Select(item => item.surface.Id).Distinct().Count() > 1))
            rows.Add(new Overlap("surface-ref", group.Key,
                string.Join(", ", group.Select(item => item.surface.Id).Distinct()),
                "The same surface source identifier is attached to multiple surfaces."));

        return rows;
    }

    public static string ToJson(IReadOnlyList<Overlap> rows) =>
        System.Text.Json.JsonSerializer.Serialize(rows, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        });

    public static string ToCsv(IReadOnlyList<Overlap> rows)
    {
        var builder = new System.Text.StringBuilder("kind,value,entities,note\n");
        foreach (var row in rows)
            builder.AppendLine(string.Join(',', new[]
            {
                Quote(row.Kind), Quote(row.Value), Quote(row.Entities), Quote(row.Note),
            }));
        return builder.ToString();
    }

    private static string Quote(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
