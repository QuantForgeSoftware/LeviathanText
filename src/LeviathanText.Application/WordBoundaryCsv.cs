using System.Text;
using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application;

/// <summary>Export proposed L2 word-boundary positions without changing witness text.</summary>
public static class WordBoundaryCsv
{
    public static string Export(NativeProject project)
    {
        var builder = new StringBuilder("lineId,revisionId,layer,scalarPosition,before,after,description\n");
        foreach (var surface in project.Objects.SelectMany(o => o.Surfaces))
        {
            var revisions = surface.Revisions.ToDictionary(r => r.Id);
            foreach (var annotation in surface.Annotations.Where(a => a.Kind == AnnotationKind.WordBoundary))
            {
                if (!revisions.TryGetValue(annotation.AnchoredRevisionId, out var revision)) continue;
                int scalars = ScalarText.Count(revision.Text);
                int position = Math.Clamp(annotation.Range.Start, 0, scalars);
                var before = ScalarText.Slice(revision.Text, Math.Max(0, position - 12), position);
                var after = ScalarText.Slice(revision.Text, position, Math.Min(scalars, position + 12));
                builder.AppendLine(string.Join(',', new[]
                {
                    Quote(revision.ScopeId.ToString()), Quote(revision.Id.ToString()),
                    Quote(revision.Layer.ToString()),
                    position.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Quote(before), Quote(after), Quote(annotation.Description ?? ""),
                }));
            }
        }
        return builder.ToString();
    }

    private static string Quote(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
