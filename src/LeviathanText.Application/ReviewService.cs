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
            string? note = null, string? correctionText = null, EntityId? reviewedRevisionId = null)
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
        var current = surface.Revisions.Where(revision => revision.Id == revisionId).Take(2).ToArray();
        if (current.Length != 1 || current[0].ScopeId != lineId || current[0].Layer != TextLayer.L1Diplomatic
            || project.Objects.SelectMany(obj => obj.Surfaces).SelectMany(page => page.Revisions)
                .Count(revision => revision.Id == revisionId) != 1)
            throw InvalidTarget(revisionId, "The current diplomatic revision is ambiguous or belongs to another line/layer; no review was recorded.");
        if (reviewedRevisionId is { } requested && requested != revisionId)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.RevisionConflict,
                Scope = lineId.ToString(),
                Details = new Dictionary<string, string>
                {
                    ["requestedRevision"] = requested.ToString(),
                    ["currentRevision"] = revisionId.ToString(),
                },
                RemedyHint = RemedyHint.RebaseRevision,
                Message = "Another writer changed this line after it was opened for review; reload before recording a decision.",
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
        foundProject = foundProject with { Version = store.SaveProject(foundProject) };
        return (foundProject, updatedSurface, decision, correction);
    }

    /// <summary>
    /// Review status describes only the current diplomatic revision. Correction counts
    /// describe provenance of that revision, independently of reviews of its new text.
    /// Historical decisions remain in the append-only surface ledger.
    /// </summary>
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

                var decisions = CurrentDecisions(surface, line.Id, rid);
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
                if (surface.ReviewDecisions.Any(d => d.LineId == line.Id &&
                    d.Outcome == ReviewOutcome.Correct && d.CorrectedRevisionId == rid)) corrected++;
                if (disagreement) needsAdjudication++;

                lines.Add(new LineReview(line.Id, surface.Id, current.Text, decisions.Count,
                    reviewers, outcomes, latest, isDouble, disagreement));
            }
        }

        return new Summary(transcribed, reviewed, doubleReviewed, disputed, accepted, corrected, needsAdjudication, lines);
    }

    /// <summary>Adjudication CSV: only current-revision disagreements, with their current-revision decisions.</summary>
    public string ExportAdjudicationCsv(NativeProject project)
    {
        var summary = Summarize(project);
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("surface_id,line_id,current_text,reviewers,decisions,latest_outcome");

        foreach (var surface in project.Objects.SelectMany(o => o.Surfaces))
        {
            foreach (var review in summary.Lines.Where(l => l.SurfaceId == surface.Id && l.NeedsAdjudication))
            {
                var revisionId = surface.Lines.Single(line => line.Id == review.LineId).DiplomaticRevisionId!.Value;
                var decisions = CurrentDecisions(surface, review.LineId, revisionId)
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

    private static List<ReviewDecision> CurrentDecisions(NativeSurface surface, EntityId lineId, EntityId revisionId) =>
        surface.ReviewDecisions.Where(d => d.LineId == lineId && d.RevisionId == revisionId)
            .OrderBy(d => d.At).ThenBy(d => d.Id, StringComparer.Ordinal).ToList();

    private static string Escape(string value) =>
        value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r')
            ? '"' + value.Replace("\"", "\"\"") + '"'
            : value;

    private static (NativeProject Project, NativeSurface Surface, NativeLine Line) FindLine(
        NativeProject project, EntityId lineId)
    {
        var matches = project.Objects.SelectMany(obj => obj.Surfaces)
            .SelectMany(surface => surface.Lines.Where(line => line.Id == lineId).Select(line => (Surface: surface, Line: line)))
            .Take(2).ToArray();
        if (matches.Length > 1)
            throw InvalidTarget(lineId, "The line identity is ambiguous; no review was recorded.");
        if (matches.Length == 1)
        {
            if (project.Objects.SelectMany(obj => obj.Surfaces).Count(surface => surface.Id == matches[0].Surface.Id) != 1)
                throw InvalidTarget(matches[0].Surface.Id, "The surface identity is ambiguous; no review was recorded.");
            return (project, matches[0].Surface, matches[0].Line);
        }
        throw new LeviathanException(new LeviathanError
        {
            Code = ErrorCode.InputMissing,
            Scope = lineId.ToString(),
            Message = "No such line in the project.",
        });
    }

    private static LeviathanException InvalidTarget(EntityId id, string message) => new(new LeviathanError
    {
        Code = ErrorCode.StageContractViolation, Scope = id.ToString(), Message = message,
        Details = new Dictionary<string, string> { ["reason"] = "ambiguous-review-target" },
    });

    private static NativeProject WithSurface(NativeProject project, NativeSurface surface) =>
        project with
        {
            Objects = [.. project.Objects.Select(o => o.Surfaces.Any(s => s.Id == surface.Id)
                ? o with { Surfaces = [.. o.Surfaces.Select(s => s.Id == surface.Id ? surface : s)] }
                : o)],
        };
}
