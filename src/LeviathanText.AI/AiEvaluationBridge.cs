using LeviathanText.Evaluation;

namespace LeviathanText.AI;

/// <summary>
/// Bridges recorded proposals to the EVAL scorers without making Evaluation depend on the
/// provider package. The source span stored on each request is the evidence text resolved
/// by <see cref="AiProposalService"/>; a proposal with no stored source cannot support an
/// addition and is scored accordingly.
/// </summary>
public static class AiEvaluationBridge
{
    public static SuggestionKind ToSuggestionKind(AiTaskKind kind) => kind switch
    {
        AiTaskKind.CandidateReading => SuggestionKind.CandidateReading,
        AiTaskKind.WordDivision => SuggestionKind.WordDivision,
        AiTaskKind.Expansion => SuggestionKind.Expansion,
        AiTaskKind.Accentuation => SuggestionKind.Accentuation,
        AiTaskKind.Restoration => SuggestionKind.Restoration,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown proposal task kind."),
    };

    public static SuggestionDisposition ToDisposition(ProposalStatus status) => status switch
    {
        ProposalStatus.Proposed => SuggestionDisposition.Proposed,
        ProposalStatus.Accepted => SuggestionDisposition.Accepted,
        ProposalStatus.Rejected => SuggestionDisposition.Rejected,
        ProposalStatus.Expired => SuggestionDisposition.Expired,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown proposal status."),
    };

    public static SuggestionAudit ToAudit(AiProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        return new SuggestionAudit(
            ToSuggestionKind(proposal.Request.Task),
            ToDisposition(proposal.Status),
            proposal.ProposedText,
            proposal.Request.SourceText);
    }

    /// <summary>Score one reviewed line/session using the proposals recorded for it.</summary>
    public static CorrectionEffortScore ScoreSession(
        string reference,
        string raw,
        string final,
        IEnumerable<AiProposal> proposals,
        CorrectionTime? time = null)
    {
        ArgumentNullException.ThrowIfNull(proposals);
        // Translation/extraction are advisory, not text-replacement suggestions; including
        // them in correction-effort scoring would misstate the edit burden.
        var editable = proposals.Where(proposal =>
            proposal.Request.Task is not (AiTaskKind.Translation or AiTaskKind.StructuredExtraction
                or AiTaskKind.LexicalAnnotation or AiTaskKind.MorphologicalAnnotation));
        return CorrectionEffortScorer.Score(reference, raw, final, editable.Select(ToAudit).ToArray(), time);
    }
}
