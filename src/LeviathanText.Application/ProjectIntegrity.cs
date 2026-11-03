using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application;

/// <summary>
/// Read-only integrity audit for persisted native projects. It reports duplicate IDs,
/// dangling references and annotation/revision mismatches without rewriting the document.
/// Recovery remains an explicit copy-and-review operation using backups and revision history.
/// </summary>
public static class ProjectIntegrity
{
    public sealed record Issue(string Code, string Scope, string Message);

    public static IReadOnlyList<Issue> Audit(NativeProject project)
    {
        var issues = new List<Issue>();
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        void Register(EntityId id, string scope)
        {
            var key = id.ToString();
            if (ids.TryGetValue(key, out var first))
                issues.Add(new Issue("duplicate-id", scope, $"ID {key} is also used by {first}."));
            else
                ids[key] = scope;
        }

        foreach (var obj in project.Objects)
        {
            Register(obj.Id, "object");
            var objectSurfaceIds = obj.Surfaces.Select(s => s.Id).ToHashSet();
            foreach (var fragment in obj.Fragments)
            {
                Register(fragment.Id, $"object {obj.Id}");
                if (fragment.SurfaceIds.Count == 0)
                    issues.Add(new Issue("empty-fragment", fragment.Id.ToString(),
                        "Fragment references no surfaces."));
                foreach (var surfaceId in fragment.SurfaceIds)
                    if (!objectSurfaceIds.Contains(surfaceId))
                        issues.Add(new Issue("dangling-fragment-surface", fragment.Id.ToString(),
                            $"Fragment surface {surfaceId} does not belong to object {obj.Id}."));
            }
            foreach (var surface in obj.Surfaces)
            {
                Register(surface.Id, "surface");
                var captures = surface.Captures.Select(c => c.Id).ToHashSet();
                var regions = surface.Regions.Select(r => r.Id).ToHashSet();
                var lines = surface.Lines.Select(l => l.Id).ToHashSet();
                // Duplicates are findings, not reasons for the audit to throw: keep the first
                // instance for downstream reference checks.
                var revisions = surface.Revisions.GroupBy(r => r.Id).ToDictionary(g => g.Key, g => g.First());
                var linesById = surface.Lines.GroupBy(l => l.Id).ToDictionary(g => g.Key, g => g.First());
                foreach (var capture in surface.Captures) Register(capture.Id, $"surface {surface.Id}");
                foreach (var region in surface.Regions)
                {
                    Register(region.Id, $"surface {surface.Id}");
                    if (!captures.Contains(region.FrameRef))
                        issues.Add(new Issue("dangling-frame", region.Id.ToString(),
                            "Region frame is not a capture on this surface."));
                }
                foreach (var duplicateSource in surface.Lines
                             .Where(line => line.Tags.TryGetValue("source-id", out var value) && value.Count > 0)
                             .GroupBy(line => line.Tags["source-id"][0], StringComparer.Ordinal)
                             .Where(group => group.Count() > 1))
                    issues.Add(new Issue("duplicate-source-id", surface.Id.ToString(),
                        $"Source line id '{duplicateSource.Key}' occurs {duplicateSource.Count()} times on this surface."));

                foreach (var line in surface.Lines)
                {
                    Register(line.Id, $"surface {surface.Id}");
                    if (!regions.Contains(line.RegionId))
                        issues.Add(new Issue("dangling-region", line.Id.ToString(),
                            "Line region does not exist on this surface."));
                    if (!captures.Contains(line.FrameRef))
                        issues.Add(new Issue("dangling-frame", line.Id.ToString(),
                            "Line frame is not a capture on this surface."));
                    if (line.DiplomaticRevisionId is { } pointer
                        && (!revisions.TryGetValue(pointer, out var revision)
                            || revision.ScopeId != line.Id
                            || revision.Layer != TextLayer.L1Diplomatic))
                        issues.Add(new Issue("dangling-line-revision", line.Id.ToString(),
                            "Diplomatic revision pointer is missing, wrong-scope or not L1."));
                }

                foreach (var revision in surface.Revisions)
                {
                    Register(revision.Id, $"surface {surface.Id}");
                    bool scopeExists = revision.ScopeId == surface.Id || lines.Contains(revision.ScopeId);
                    if (!scopeExists)
                        issues.Add(new Issue("dangling-revision-scope", revision.Id.ToString(),
                            "Revision scope is neither this surface nor one of its lines."));
                    if (revision.ParentRevisionId is { } parent)
                    {
                        if (!revisions.TryGetValue(parent, out var parentRevision))
                            issues.Add(new Issue("dangling-parent", revision.Id.ToString(),
                                "Parent revision is missing."));
                        else if (parentRevision.ScopeId != revision.ScopeId || parentRevision.Layer != revision.Layer)
                            issues.Add(new Issue("parent-scope-mismatch", revision.Id.ToString(),
                                "Parent revision has a different scope or layer."));
                    }
                    int scalars = LeviathanText.Core.Unicode.ScalarText.ScalarLength(revision.Text);
                    foreach (var annotation in surface.Annotations.Where(a => a.AnchoredRevisionId == revision.Id))
                        if (annotation.Range.End > scalars)
                            issues.Add(new Issue("annotation-out-of-range", annotation.Id.ToString(),
                                $"Annotation range ends at {annotation.Range.End}, text has {scalars} scalars."));
                }

                foreach (var annotation in surface.Annotations)
                {
                    Register(annotation.Id, $"surface {surface.Id}");
                    if (!revisions.ContainsKey(annotation.AnchoredRevisionId))
                        issues.Add(new Issue("dangling-annotation-anchor", annotation.Id.ToString(),
                            "Annotation revision is missing."));
                    if (annotation.ExpansionRevisionId is { } expansion && !revisions.ContainsKey(expansion))
                        issues.Add(new Issue("dangling-expansion", annotation.Id.ToString(),
                            "Expansion revision is missing."));
                }
                foreach (var retraction in surface.AnnotationRetractions)
                {
                    if (!surface.Annotations.Any(a => a.Id == retraction.AnnotationId))
                        issues.Add(new Issue("dangling-retraction", retraction.Id,
                            "Retracted annotation is missing."));
                }
                foreach (var review in surface.ReviewDecisions)
                {
                    if (!linesById.ContainsKey(review.LineId))
                        issues.Add(new Issue("dangling-review-line", review.Id, "Reviewed line is missing."));
                    if (!revisions.ContainsKey(review.RevisionId))
                        issues.Add(new Issue("dangling-review-revision", review.Id, "Reviewed revision is missing."));
                    if (review.CorrectedRevisionId is { } corrected && !revisions.ContainsKey(corrected))
                        issues.Add(new Issue("dangling-review-correction", review.Id,
                            "Corrected revision is missing."));
                }
            }
        }
        return issues;
    }

    public static string ToJson(IReadOnlyList<Issue> issues) =>
        System.Text.Json.JsonSerializer.Serialize(issues, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        });

    public static string ToCsv(IReadOnlyList<Issue> issues)
    {
        var builder = new System.Text.StringBuilder("code,scope,message\n");
        foreach (var issue in issues)
            builder.AppendLine($"{Quote(issue.Code)},{Quote(issue.Scope)},{Quote(issue.Message)}");
        return builder.ToString();
    }

    private static string Quote(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
