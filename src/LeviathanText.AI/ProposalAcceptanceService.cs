using LeviathanText.Contracts;
using LeviathanText.Core.Revisions;

namespace LeviathanText.AI;

/// <summary>
/// Turns an accepted proposal into an L2 editorial revision, leaving L1 untouched
/// (interchange-and-api.md §2.3). Reuses the C0 revision semantics so a stale target
/// fails loudly instead of being silently re-anchored.
/// </summary>
public sealed class ProposalAcceptanceService(RevisionChain chain)
{
    public (AiProposal Accepted, TextRevision EditorialRevision) Accept(
        AiProposal proposal, AgentRef acceptor, DateTimeOffset? at = null)
    {
        if (proposal.Status != ProposalStatus.Proposed)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = proposal.Id,
                Message = $"Proposal {proposal.Id} is {proposal.Status}; only Proposed proposals can be accepted.",
            });

        var target = chain.Get(proposal.Request.TargetRevisionId)
            ?? throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StaleAnnotationAnchor,
                Scope = proposal.Id,
                RemedyHint = RemedyHint.RebaseRevision,
                Message = $"Unknown target revision {proposal.Request.TargetRevisionId}.",
            });

        var current = chain.Current(target.ScopeId, target.Layer);
        if (current is null || current.Id != target.Id)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StaleAnnotationAnchor,
                Scope = proposal.Id,
                Details = new Dictionary<string, string> { ["currentRevision"] = current?.Id.ToString() ?? "none" },
                RemedyHint = RemedyHint.RebaseRevision,
                Message = "Proposal targets a superseded revision; rebase before accepting.",
            });

        var modelAuthor = new AgentRef
        {
            Id = $"ai:{proposal.ProviderId}/{proposal.ModelId}",
            IsModel = true,
            ModelIdentity = $"{proposal.ProviderId}/{proposal.ModelId}",
        };

        // Recording the accepted reading as an L2 revision keeps L1 intact.
        var editorial = chain.Append(target.ScopeId, TextLayer.L2Editorial, proposal.ProposedText, acceptor, createdAt: at);

        _ = modelAuthor; // provenance is carried on the proposal record; the L2 revision records the acceptor
        return (proposal with { Status = ProposalStatus.Accepted }, editorial);
    }
}
