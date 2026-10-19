using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application;

/// <summary>Read-only project counts for dashboards and handoffs (no derived accuracy claims).</summary>
public static class ProjectStats
{
    public sealed record Counts(
        int Objects,
        int Surfaces,
        int Captures,
        int Regions,
        int Lines,
        int TranscribedLines,
        int LinesWithL0,
        int L0Revisions,
        int L2Revisions,
        int Annotations,
        int FlaggedLines,
        IReadOnlyDictionary<string, int> AnnotationsByKind);

    public static Counts Build(NativeProject project)
    {
        var surfaces = project.Objects.SelectMany(o => o.Surfaces).ToList();
        var flagged = ProfileReport.Build(project).FlaggedTotal;
        var annotations = surfaces.SelectMany(s => s.Annotations).ToList();

        return new Counts(
            project.Objects.Count,
            surfaces.Count,
            surfaces.Sum(s => s.Captures.Count),
            surfaces.Sum(s => s.Regions.Count),
            surfaces.Sum(s => s.Lines.Count),
            surfaces.Sum(s => s.Lines.Count(l => l.DiplomaticRevisionId is not null)),
            surfaces.Sum(s => s.Lines.Count(l => l.DiplomaticRevisionId is { } id
                && s.Revisions.Any(r => r.Id == id && r.Layer == TextLayer.L1Diplomatic))),
            surfaces.Sum(s => s.Revisions.Count(r => r.Layer == TextLayer.L0RawRecognition)),
            surfaces.Sum(s => s.Revisions.Count(r => r.Layer == TextLayer.L2Editorial)),
            annotations.Count,
            flagged,
            annotations.GroupBy(a => a.Kind.ToString()).ToDictionary(g => g.Key, g => g.Count()));
    }
}
