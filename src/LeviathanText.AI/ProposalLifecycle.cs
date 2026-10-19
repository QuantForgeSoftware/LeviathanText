using LeviathanText.Contracts;
using LeviathanText.Core.Revisions;

namespace LeviathanText.AI;

/// <summary>Why a proposal cannot be accepted.</summary>
public enum BulkAcceptanceBlocker
{
    None,
    NotProposed,
    UnknownTargetRevision,
    TargetSuperseded,
    ConflictsWithEarlierInBatch,
}

/// <summary>Dry-run verdict for one proposal in a batch.</summary>
public sealed record BulkAcceptanceItem(EntityId ProposalId, bool CanAccept, BulkAcceptanceBlocker Blocker, string? Detail = null);

/// <summary>
/// Dry-run preview (interchange-and-api.md §2.3: bulk acceptance requires a dry-run
/// preview first). Nothing is written to the revision chain by a preview.
/// </summary>
public sealed record BulkAcceptancePreview(IReadOnlyList<BulkAcceptanceItem> Items)
{
    public int AcceptableCount => Items.Count(i => i.CanAccept);

    public bool IsFullyApplicable => Items.All(i => i.CanAccept);
}

/// <summary>One batch item's applied outcome.</summary>
public sealed record BulkAcceptanceOutcome(EntityId ProposalId, ProposalStatus Status, EntityId? EditorialRevisionId, BulkAcceptanceBlocker Blocker);

/// <summary>Result of applying a batch; blocked items are reported, never silently dropped.</summary>
public sealed record BulkAcceptanceResult(IReadOnlyList<BulkAcceptanceOutcome> Outcomes)
{
    public int AcceptedCount => Outcomes.Count(o => o.Blocker == BulkAcceptanceBlocker.None);
}

/// <summary>
/// Proposal lifecycle transitions and batch acceptance. Rejection/expiry only change the
/// proposal record; acceptance alone creates an L2 revision, so L1 is never touched.
/// </summary>
public static class ProposalLifecycle
{
    public static AiProposal Reject(AiProposal proposal) =>
        proposal.Status == ProposalStatus.Proposed
            ? proposal with { Status = ProposalStatus.Rejected }
            : throw InvalidTransition(proposal, ProposalStatus.Rejected);

    public static AiProposal Expire(AiProposal proposal) =>
        proposal.Status == ProposalStatus.Proposed
            ? proposal with { Status = ProposalStatus.Expired }
            : throw InvalidTransition(proposal, ProposalStatus.Expired);

    private static LeviathanException InvalidTransition(AiProposal proposal, ProposalStatus target) =>
        new(new LeviathanError
        {
            Code = ErrorCode.StageContractViolation,
            Scope = proposal.Id.ToString(),
            Details = new Dictionary<string, string> { ["from"] = proposal.Status.ToString(), ["to"] = target.ToString() },
            Message = $"Cannot move proposal {proposal.Id} from {proposal.Status} to {target}.",
        });
}

/// <summary>
/// Batch acceptance with a mandatory dry-run: each proposal is independently validated,
/// and a proposal that would target a revision superseded by an earlier acceptance in the
/// same batch is reported as blocked instead of overwriting the batch's own output.
/// </summary>
public sealed class BulkProposalAcceptanceService(RevisionChain chain)
{
    private readonly RevisionChain _chain = chain ?? throw new ArgumentNullException(nameof(chain));

    public BulkAcceptancePreview Preview(IReadOnlyList<AiProposal> proposals)
    {
        ArgumentNullException.ThrowIfNull(proposals);
        if (proposals.Count == 0)
            throw new ArgumentException("At least one proposal is required for bulk acceptance.", nameof(proposals));

        var items = new List<BulkAcceptanceItem>(proposals.Count);
        var provisionallyAcceptedChains = new HashSet<(EntityId Scope, TextLayer Layer)>();

        foreach (var proposal in proposals)
        {
            var (blocker, detail) = Validate(proposal, provisionallyAcceptedChains);
            items.Add(new BulkAcceptanceItem(proposal.Id, blocker == BulkAcceptanceBlocker.None, blocker, detail));

            if (blocker == BulkAcceptanceBlocker.None && _chain.Get(proposal.Request.TargetRevisionId) is { } target)
                provisionallyAcceptedChains.Add((target.ScopeId, TextLayer.L2Editorial));
        }

        return new BulkAcceptancePreview(items);
    }

    public BulkAcceptanceResult Accept(IReadOnlyList<AiProposal> proposals, AgentRef acceptor, DateTimeOffset? at = null)
    {
        ArgumentNullException.ThrowIfNull(proposals);
        ArgumentNullException.ThrowIfNull(acceptor);

        var preview = Preview(proposals);
        var acceptance = new ProposalAcceptanceService(_chain);
        var outcomes = new List<BulkAcceptanceOutcome>(proposals.Count);
        var acceptedAt = at;

        for (var i = 0; i < proposals.Count; i++)
        {
            var proposal = proposals[i];
            var item = preview.Items[i];
            if (!item.CanAccept)
            {
                outcomes.Add(new BulkAcceptanceOutcome(proposal.Id, proposal.Status, null, item.Blocker));
                continue;
            }

            var (accepted, editorial) = acceptance.Accept(proposal, acceptor, acceptedAt);
            outcomes.Add(new BulkAcceptanceOutcome(accepted.Id, accepted.Status, editorial.Id, BulkAcceptanceBlocker.None));
        }

        return new BulkAcceptanceResult(outcomes);
    }

    private (BulkAcceptanceBlocker Blocker, string? Detail) Validate(
        AiProposal proposal,
        HashSet<(EntityId Scope, TextLayer Layer)> provisionallyAcceptedChains)
    {
        if (proposal.Status != ProposalStatus.Proposed)
            return (BulkAcceptanceBlocker.NotProposed, $"Proposal is {proposal.Status}, not Proposed.");

        var target = _chain.Get(proposal.Request.TargetRevisionId);
        if (target is null)
            return (BulkAcceptanceBlocker.UnknownTargetRevision, $"Unknown target revision {proposal.Request.TargetRevisionId}.");

        if (provisionallyAcceptedChains.Contains((target.ScopeId, TextLayer.L2Editorial)))
            return (BulkAcceptanceBlocker.ConflictsWithEarlierInBatch,
                "An earlier proposal in this batch already accepts into the same surface's L2 chain; rebase this request.");

        var current = _chain.Current(target.ScopeId, target.Layer);
        if (current is null || current.Id != target.Id)
            return (BulkAcceptanceBlocker.TargetSuperseded, "The target revision is no longer current; rebase before accepting.");

        return (BulkAcceptanceBlocker.None, null);
    }
}
