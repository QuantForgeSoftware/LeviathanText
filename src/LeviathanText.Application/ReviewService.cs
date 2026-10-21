using LeviathanText.Contracts;
using LeviathanText.Core.Unicode;
using LeviathanText.Formats;

namespace LeviathanText.Application;

/// <summary>
/// Double-review and adjudication ledger (todo DATA-03 readiness). Decisions are append-only
/// and anchored to the exact reviewed revision; a "correct" decision creates a new L1
/// revision and records both the reviewed and corrected revision ids. Two reviewers who
/// disagree make a line an adjudication candidate — the tooling never forces agreement.
/// </summary>
public sealed class ReviewService(IProjectStore store)
{
    public sealed record LineReview(
        EntityId LineId,
        EntityId? SurfaceId,
        string Text,
        int Decisions,
        IReadOnlyList<string> Reviewers,
        IReadOnlyList<ReviewOutcome> Outcomes,
        ReviewOutcome? LatestOutcome,
        bool DoubleReviewed,
        bool NeedsAdjudication);

    public sealed record Summary(
        int TranscribedLines,
        int ReviewedLines,
        int DoubleReviewedLines,
        int DisputedLines,
        int AcceptedLines,
        int CorrectedLines,
        int NeedsAdjudication,
        IReadOnlyList<LineReview> Lines);

    public (NativeProject Project, NativeSurface Surface, ReviewDecision Decision, TextRevision? Correction)
        Record(NativeProject project, EntityId lineId, string reviewer, ReviewOutcome outcome,
            string? note = null, string? correctionText = null)
    {
        if (string.IsNullOrWhiteSpace(reviewer))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = lineId.ToString(),
                Message = "A reviewer identity is required.",
            });

        var (foundProject, surface, line) = FindLine(project, lineId);
        var revisionId = line.DiplomaticRevisionId
            ?? throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = lineId.ToString(),
                Message = "Line has no diplomatic revision to review.",
            });
        if (surface.Revisions.All(r => r.Id != revisionId))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = revisionId.ToString(),
                Message = "The line's current revision is missing from the surface.",
            });

        if (outcome == ReviewOutcome.Correct && string.IsNullOrWhiteSpace(correctionText))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = lineId.ToString(),
                Message = "A correction decision must carry the corrected text.",
            });
        if (outcome != ReviewOutcome.Correct && !string.IsNullOrWhiteSpace(correctionText))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = lineId.ToString(),
                Message = "Only a correction decision may carry corrected text.",
            });

        TextRevision? correction = null;
        if (outcome == ReviewOutcome.Correct)
        {
            correction = new TextRevision
            {
                Id = new EntityId(EntityKind.TranscriptionRevision, Guid.NewGuid().ToString("N")[..12]),
                ScopeId = lineId,
                Layer = TextLayer.L1Diplomatic,
                ParentRevisionId = revisionId,
                Author = new AgentRef { Id = reviewer, IsModel = false },
                CreatedAt = DateTimeOffset.UtcNow,
                Text = correctionText!,
            };
        }

        var decision = new ReviewDecision
        {
            Id = $"rv-{Guid.NewGuid().ToString("N")[..12]}",
            LineId = lineId,
            RevisionId = revisionId,
            Reviewer = reviewer,
            Outcome = outcome,
            At = DateTimeOffset.UtcNow,
            Note = string.IsNullOrWhiteSpace(note) ? null : note,
            CorrectedRevisionId = correction?.Id,
        };

        var updatedSurface = surface with
        {
            Revisions = correction is null ? surface.Revisions : [.. surface.Revisions, correction],
            Lines = correction is null
                ? surface.Lines
                : [.. surface.Lines.Select(l => l.Id == lineId ? l with { DiplomaticRevisionId = correction.Id } : l)],
            ReviewDecisions = [.. surface.ReviewDecisions, decision],
        };
        foundProject = WithSurface(foundProject, updatedSurface);
        store.SaveProject(foundProject);
        return (foundProject, updatedSurface, decision, correction);
    }

    public Summary Summarize(NativeProject project, EntityId? surfaceId = null)
    {
        var lines = new List<LineReview>();
        int transcribed = 0, reviewed = 0, doubleReviewed = 0, disputed = 0, accepted = 0, corrected = 0, needsAdjudication = 0;

        foreach (var surface in project.Objects.SelectMany(o => o.Surfaces))
        {
            if (surfaceId is { } wanted && surface.Id != wanted) continue;
            var revisions = surface.Revisions.ToDictionary(r => r.Id);

            foreach (var line in surface.Lines)
            {
                if (line.DiplomaticRevisionId is not { } rid || !revisions.TryGetValue(rid, out var current))
                    continue;
                transcribed++;

                var decisions = surface.ReviewDecisions
                    .Where(d => d.LineId == line.Id)
                    .OrderBy(d => d.At).ThenBy(d => d.Id, StringComparer.Ordinal)
                    .ToList();
                var reviewers = decisions.Select(d => d.Reviewer).Distinct(StringComparer.Ordinal).ToList();
                var outcomes = decisions.Select(d => d.Outcome).ToList();
                var latest = decisions.Count == 0 ? (ReviewOutcome?)null : decisions[^1].Outcome;
                bool isDouble = reviewers.Count >= 2;
                bool isDisputed = latest == ReviewOutcome.Dispute;
                bool disagreement = isDouble && outcomes.Distinct().Count() > 1;

                if (decisions.Count > 0) reviewed++;
                if (isDouble) doubleReviewed++;
                if (isDisputed) disputed++;
                if (latest == ReviewOutcome.Accept) accepted++;
                if (latest == ReviewOutcome.Correct) corrected++;
                if (disagreement) needsAdjudication++;

                lines.Add(new LineReview(line.Id, surface.Id, current.Text, decisions.Count,
                    reviewers, outcomes, latest, isDouble, disagreement));
            }
        }

        return new Summary(transcribed, reviewed, doubleReviewed, disputed, accepted, corrected, needsAdjudication, lines);
    }

    /// <summary>Adjudication CSV: only lines whose reviewers disagree, with every decision.</summary>
    public string ExportAdjudicationCsv(NativeProject project)
    {
        var summary = Summarize(project);
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("surface_id,line_id,current_text,reviewers,decisions,latest_outcome");

        foreach (var surface in project.Objects.SelectMany(o => o.Surfaces))
        {
            var revisions = surface.Revisions.ToDictionary(r => r.Id);
            foreach (var review in summary.Lines.Where(l => l.SurfaceId == surface.Id && l.NeedsAdjudication))
            {
                var decisions = surface.ReviewDecisions
                    .Where(d => d.LineId == review.LineId)
                    .OrderBy(d => d.At)
                    .Select(d => $"{d.Reviewer}={d.Outcome}" + (string.IsNullOrEmpty(d.Note) ? "" : $"({d.Note})"))
                    .ToList();
                var text = review.Text;
                builder.Append(Escape(surface.Id.ToString())).Append(',')
                    .Append(Escape(review.LineId.ToString())).Append(',')
                    .Append(Escape(text)).Append(',')
                    .Append(Escape(string.Join('|', review.Reviewers))).Append(',')
                    .Append(Escape(string.Join('|', decisions))).Append(',')
                    .Append(review.LatestOutcome?.ToString() ?? "")
                    .AppendLine();
            }
        }
        return builder.ToString();
    }

    private static string Escape(string value) =>
        value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r')
            ? '"' + value.Replace("\"", "\"\"") + '"'
            : value;

    private static (NativeProject Project, NativeSurface Surface, NativeLine Line) FindLine(
        NativeProject project, EntityId lineId)
    {
        foreach (var obj in project.Objects)
            foreach (var surface in obj.Surfaces)
                foreach (var line in surface.Lines)
                    if (line.Id == lineId)
                        return (project, surface, line);
        throw new LeviathanException(new LeviathanError
        {
            Code = ErrorCode.InputMissing,
            Scope = lineId.ToString(),
            Message = "No such line in the project.",
        });
    }

    private static NativeProject WithSurface(NativeProject project, NativeSurface surface) =>
        project with
        {
            Objects = [.. project.Objects.Select(o => o.Surfaces.Any(s => s.Id == surface.Id)
                ? o with { Surfaces = [.. o.Surfaces.Select(s => s.Id == surface.Id ? surface : s)] }
                : o)],
        };
}
