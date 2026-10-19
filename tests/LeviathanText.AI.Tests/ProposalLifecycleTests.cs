using LeviathanText.AI;
using LeviathanText.Contracts;
using LeviathanText.Core.Revisions;
using Xunit;

namespace LeviathanText.AI.Tests;

/// <summary>
/// Proposal lifecycle and mandated bulk-acceptance dry run (interchange-and-api.md §2.3):
/// a preview never mutates the chain, and a batch reports blocked proposals instead of
/// overwriting its own earlier output.
/// </summary>
public class ProposalLifecycleTests
{
    private static readonly AgentRef Human = new() { Id = "u1", IsModel = false };

    private static AiProposal ProposalFor(EntityId targetRevision, string text = "κυρίου") =>
        new()
        {
            Id = ProposalIds.New(),
            Request = new AiProposalRequest
            {
                TargetRevisionId = targetRevision,
                Range = new ScalarRange(0, 6),
                Task = AiTaskKind.Expansion,
                ProviderConfigRef = "local/test",
                PromptVersion = "p1",
            },
            ProposedText = text,
            ProviderId = "stub",
            ModelId = "stub-1",
            PromptVersion = "p1",
            Confidence = Confidence.Of(0.5, ConfidenceScale.RawModelProbability),
            CreatedAt = DateTimeOffset.UtcNow,
        };

    private static TextRevision AppendL1(RevisionChain chain, EntityId scope, string text, EntityId? parent = null) =>
        chain.Append(scope, TextLayer.L1Diplomatic, text, Human, parentRevisionId: parent);

    [Fact]
    public void Lifecycle_transitions_only_apply_to_proposed_records()
    {
        var revision = new EntityId(EntityKind.TranscriptionRevision, "r1");
        var proposed = ProposalFor(revision);

        Assert.Equal(ProposalStatus.Rejected, ProposalLifecycle.Reject(proposed).Status);
        Assert.Equal(ProposalStatus.Expired, ProposalLifecycle.Expire(proposed).Status);

        var accepted = proposed with { Status = ProposalStatus.Accepted };
        var ex = Assert.Throws<LeviathanException>(() => ProposalLifecycle.Reject(accepted));
        Assert.Equal(ErrorCode.StageContractViolation, ex.Error.Code);
    }

    [Fact]
    public void Empty_batch_is_rejected()
    {
        var chain = new RevisionChain();
        var service = new BulkProposalAcceptanceService(chain);

        Assert.Throws<ArgumentException>(() => service.Preview([]));
    }

    [Fact]
    public void Dry_run_never_mutates_the_chain()
    {
        var chain = new RevisionChain();
        var scope = new EntityId(EntityKind.Surface, "s1");
        var revision = AppendL1(chain, scope, "κυριου");
        var proposals = new[] { ProposalFor(revision.Id) };

        var preview = new BulkProposalAcceptanceService(chain).Preview(proposals);

        Assert.Single(preview.Items);
        Assert.True(preview.Items[0].CanAccept);
        Assert.Null(chain.Current(scope, TextLayer.L2Editorial));
    }

    [Fact]
    public void Second_proposal_on_the_same_chain_is_blocked_in_preview()
    {
        var chain = new RevisionChain();
        var scope = new EntityId(EntityKind.Surface, "s1");
        var revision = AppendL1(chain, scope, "κυριου");
        var proposals = new[] { ProposalFor(revision.Id, "κυρίου"), ProposalFor(revision.Id, "κυριοῦ") };

        var preview = new BulkProposalAcceptanceService(chain).Preview(proposals);

        Assert.True(preview.Items[0].CanAccept);
        Assert.False(preview.Items[1].CanAccept);
        Assert.Equal(BulkAcceptanceBlocker.ConflictsWithEarlierInBatch, preview.Items[1].Blocker);
        Assert.Equal(1, preview.AcceptableCount);
        Assert.False(preview.IsFullyApplicable);
    }

    [Fact]
    public void Preview_flags_unknown_non_proposed_and_superseded_targets()
    {
        var chain = new RevisionChain();
        var scope = new EntityId(EntityKind.Surface, "s1");
        var r1 = AppendL1(chain, scope, "κυριου");
        var r2 = AppendL1(chain, scope, "κυριου ", parent: r1.Id);

        var unknown = ProposalFor(new EntityId(EntityKind.TranscriptionRevision, "missing"));
        var superseded = ProposalFor(r1.Id);
        var alreadyAccepted = ProposalFor(r2.Id) with { Status = ProposalStatus.Accepted };

        var preview = new BulkProposalAcceptanceService(chain).Preview([unknown, superseded, alreadyAccepted]);

        Assert.Equal(BulkAcceptanceBlocker.UnknownTargetRevision, preview.Items[0].Blocker);
        Assert.Equal(BulkAcceptanceBlocker.TargetSuperseded, preview.Items[1].Blocker);
        Assert.Equal(BulkAcceptanceBlocker.NotProposed, preview.Items[2].Blocker);
    }

    [Fact]
    public void Batch_accept_applies_the_first_and_reports_the_blocked_one()
    {
        var chain = new RevisionChain();
        var scope = new EntityId(EntityKind.Surface, "s1");
        var revision = AppendL1(chain, scope, "κυριου");
        var proposals = new[] { ProposalFor(revision.Id, "κυρίου"), ProposalFor(revision.Id, "κυριοῦ") };

        var result = new BulkProposalAcceptanceService(chain).Accept(proposals, Human);

        Assert.Equal(1, result.AcceptedCount);
        Assert.Equal(ProposalStatus.Accepted, result.Outcomes[0].Status);
        Assert.NotNull(result.Outcomes[0].EditorialRevisionId);
        Assert.Equal(BulkAcceptanceBlocker.ConflictsWithEarlierInBatch, result.Outcomes[1].Blocker);

        Assert.Equal("κυρίου", chain.Current(scope, TextLayer.L2Editorial)!.Text);
        Assert.Equal("κυριου", chain.Current(scope, TextLayer.L1Diplomatic)!.Text); // L1 untouched
    }

    [Fact]
    public void Batch_accept_applies_independent_chains()
    {
        var chain = new RevisionChain();
        var first = AppendL1(chain, new EntityId(EntityKind.Surface, "s1"), "κυριου");
        var second = AppendL1(chain, new EntityId(EntityKind.Surface, "s2"), "λογος");
        var proposals = new[] { ProposalFor(first.Id, "κυρίου"), ProposalFor(second.Id, "λόγος") };

        var result = new BulkProposalAcceptanceService(chain).Accept(proposals, Human);

        Assert.Equal(2, result.AcceptedCount);
    }
}
