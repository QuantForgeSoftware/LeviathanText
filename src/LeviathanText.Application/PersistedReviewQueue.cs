using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application;

/// <summary>
/// Review queue derived from persisted L0 revisions (mean raw confidence stored on each).
/// Unlike the in-memory queue returned by a recognition call, this survives process restarts
/// and is reproducible; empty outputs rank first, then lowest confidence, then missing
/// confidence.
/// </summary>
public static class PersistedReviewQueue
{
    public sealed record Entry(
        EntityId LineId,
        EntityId RevisionId,
        double? MeanConfidence,
        bool EmptyOutput,
        string Text);

    public static IReadOnlyList<Entry> Build(NativeProject project, EntityId? surfaceId = null)
    {
        var entries = new List<Entry>();
        foreach (var surface in project.Objects.SelectMany(o => o.Surfaces))
        {
            if (surfaceId is { } wanted && surface.Id != wanted) continue;

            foreach (var line in surface.Lines)
            {
                var latest = surface.Revisions
                    .Where(r => r.Layer == TextLayer.L0RawRecognition && r.ScopeId == line.Id)
                    .OrderByDescending(r => r.CreatedAt)
                    .FirstOrDefault();
                if (latest is null) continue;

                entries.Add(new Entry(
                    line.Id,
                    latest.Id,
                    latest.Confidence.IsNone ? null : latest.Confidence.Value,
                    string.IsNullOrWhiteSpace(latest.Text),
                    latest.Text));
            }
        }

        return [.. entries
            .OrderBy(e => e.EmptyOutput ? 0 : 1)
            .ThenBy(e => e.MeanConfidence ?? double.NegativeInfinity)
            .ThenBy(e => e.Text.Length)];
    }
}
