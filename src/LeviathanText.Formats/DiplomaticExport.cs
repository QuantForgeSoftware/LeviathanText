using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Formats;

/// <summary>
/// Mandatory loss reporting for lossy exports (interchange-and-api.md §1.3): every
/// lossy format export carries a machine-checkable report of what was dropped or
/// flattened. Export without a loss report for a lossy format is a contract violation.
/// </summary>
public sealed record LossEntry(string Category, int Count, string Description);

public sealed record LossReport
{
    public required string Format { get; init; }
    public required string ContractVersion { get; init; }
    public required IReadOnlyList<LossEntry> Entries { get; init; }

    public bool IsLossless => Entries.Count == 0;

    public override string ToString() =>
        $"loss-report[{Format}] v{ContractVersion}: {string.Join("; ", Entries.Select(e => $"{e.Category}={e.Count} ({e.Description})"))}";
}

/// <summary>
/// Diplomatic plain-text export: L1 text of all lines of a surface, joined in reading
/// order with newlines (todo Phase 1: native JSON and diplomatic UTF-8 text).
/// Plain text cannot carry geometry, uncertainty, alternatives or provenance — the
/// returned <see cref="LossReport"/> enumerates exactly what was dropped.
/// </summary>
public static class DiplomaticExport
{
    public static (string Text, LossReport Report) Export(NativeSurface surface, IReadOnlyList<TextRevision> revisions)
    {
        var revisionById = revisions.ToDictionary(r => r.Id);
        var linesInOrder = surface.Lines.OrderBy(l => surface.Regions.FirstOrDefault(r => r.Id == l.RegionId)?.ReadingOrderIndex ?? int.MaxValue);

        var parts = new List<string>();
        foreach (var line in linesInOrder)
        {
            if (line.DiplomaticRevisionId is { } revId && revisionById.TryGetValue(revId, out var revision))
                parts.Add(revision.Text);
            else
                parts.Add($"[{line.Id}: not-yet-transcribed]");
        }

        var entries = new List<LossEntry>
        {
            new("geometry", surface.Lines.Count(l => l.Baseline.Count > 0) + surface.Regions.Count(r => r.Polygon.Count > 0),
                "polygons and baselines are not representable in plain text"),
            new("uncertainty", Active(surface).Count(a => a.Kind is AnnotationKind.Unclear or AnnotationKind.Illegible or AnnotationKind.Lost),
                "unclear/illegible/lost spans are dropped; bracket conventions are NOT substituted"),
            new("alternatives", Active(surface).Count(a => a.Kind == AnnotationKind.AlternativeReading),
                "alternative readings are dropped; only the transcribed reading survives"),
            new("editorial-layer", CountEditorial(revisions), "L2 editorial readings are a separate layer and are not exported"),
            new("provenance", revisions.Count, "revision history and authorship are not representable in plain text"),
        };

        var report = new LossReport
        {
            Format = "diplomatic-plain-text",
            ContractVersion = ContractVersion.Current,
            Entries = [.. entries.Where(e => e.Count > 0)],
        };
        return (string.Join("\n", parts), report);
    }

    private static int CountEditorial(IReadOnlyList<TextRevision> revisions) =>
        revisions.Count(r => r.Layer == TextLayer.L2Editorial);

    private static IEnumerable<Annotation> Active(NativeSurface surface)
    {
        var retracted = surface.AnnotationRetractions.Select(r => r.AnnotationId).ToHashSet();
        return surface.Annotations.Where(a => !retracted.Contains(a.Id));
    }
}
