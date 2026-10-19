using LeviathanText.Contracts;
using LeviathanText.Core.Unicode;
using LeviathanText.Formats;

namespace LeviathanText.Application;

/// <summary>
/// Project-level Greek transcription-profile report (QA aid, never a normalizer). Shared by
/// the CLI, the API and the export bundle so they cannot drift.
/// </summary>
public static class ProfileReport
{
    public sealed record Issue(string LineId, int ScalarOffset, string Category, string Scalar);

    public sealed record SurfaceReport(
        string SurfaceId,
        int Lines,
        int Flagged,
        IReadOnlyList<Issue> Issues);

    public sealed record Report(
        string Profile,
        string Version,
        IReadOnlyList<SurfaceReport> Surfaces)
    {
        public int FlaggedTotal => Surfaces.Sum(s => s.Flagged);
    }

    public static Report Build(NativeProject project, int maxIssuesPerSurface = 20)
    {
        ArgumentNullException.ThrowIfNull(project);

        var surfaces = new List<SurfaceReport>();
        foreach (var surface in project.Objects.SelectMany(o => o.Surfaces))
        {
            var revisions = surface.Revisions.ToDictionary(r => r.Id);
            int lines = 0, flagged = 0;
            var issues = new List<Issue>();

            foreach (var line in surface.Lines)
            {
                if (line.DiplomaticRevisionId is not { } rid || !revisions.TryGetValue(rid, out var revision))
                    continue;

                lines++;
                var inspection = GreekTextProfile.Inspect(revision.Text);
                if (inspection.IsClean) continue;

                flagged++;
                foreach (var issue in inspection.Issues.Take(maxIssuesPerSurface))
                    issues.Add(new Issue(line.Id.ToString(), issue.ScalarOffset, issue.Category.ToString(), issue.Scalar));
            }

            surfaces.Add(new SurfaceReport(surface.Id.ToString(), lines, flagged, issues));
        }

        return new Report(GreekTextProfile.ProfileId, GreekTextProfile.Version, surfaces);
    }
}
