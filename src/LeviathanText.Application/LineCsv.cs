using System.Text;
using LeviathanText.Contracts;
using LeviathanText.Core.Unicode;
using LeviathanText.Formats;
using ScalarText = LeviathanText.Core.Unicode.ScalarText;

namespace LeviathanText.Application;

/// <summary>
/// Line-level CSV for review workflows outside the workspace: L1 text, layer/provenance,
/// geometry ids and a count of anchored annotations. CSV is lossy (no marks semantics, no
/// geometry); the native project remains authoritative.
/// </summary>
public static class LineCsv
{
    public static string Export(NativeProject project)
    {
        var builder = new StringBuilder();
        builder.AppendLine("line_id,surface_id,region_id,layer,revision_id,annotation_count,scalar_length,text");

        foreach (var surface in project.Objects.SelectMany(o => o.Surfaces))
        {
            var revisions = surface.Revisions.ToDictionary(r => r.Id);
            foreach (var line in surface.Lines)
            {
                if (line.DiplomaticRevisionId is not { } rid || !revisions.TryGetValue(rid, out var revision))
                {
                    Append(builder, line.Id, surface.Id, line.RegionId, TextLayer.L1Diplomatic,
                        "", 0, 0, "");
                    continue;
                }

                var annotations = surface.Annotations.Count(a => a.AnchoredRevisionId == revision.Id);
                Append(builder, line.Id, surface.Id, line.RegionId, revision.Layer,
                    revision.Id.ToString(), annotations, ScalarText.ScalarLength(revision.Text), revision.Text);
            }
        }

        return builder.ToString();
    }

    private static void Append(
        StringBuilder builder, EntityId lineId, EntityId surfaceId, EntityId regionId,
        TextLayer layer, string revisionId, int annotationCount, int scalarLength, string text)
    {
        builder.Append(Escape(lineId.ToString())).Append(',')
            .Append(Escape(surfaceId.ToString())).Append(',')
            .Append(Escape(regionId.ToString())).Append(',')
            .Append(layer).Append(',')
            .Append(revisionId).Append(',')
            .Append(annotationCount).Append(',')
            .Append(scalarLength).Append(',')
            .Append(Escape(text))
            .AppendLine();
    }

    private static string Escape(string value) =>
        value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r')
            ? '"' + value.Replace("\"", "\"\"") + '"'
            : value;
}
