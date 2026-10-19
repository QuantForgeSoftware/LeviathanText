using LeviathanText.AI;
using LeviathanText.Contracts;
using LeviathanText.Core.Revisions;
using Xunit;

namespace LeviathanText.AI.Tests;

/// <summary>
/// Server-side request enrichment: the source span always comes from the authoritative
/// revision, client-supplied text is ignored, and bad spans fail with the closed
/// vocabulary instead of reaching a provider.
/// </summary>
public class AiProposalServiceTests
{
    private static readonly EntityId Scope = new(EntityKind.Surface, "s1");
    private static readonly AgentRef Human = new() { Id = "u1", IsModel = false };

    private static AiProposalRequest Request(EntityId revision, int start, int end, string? injectedSource = null) => new()
    {
        TargetRevisionId = revision,
        Range = new ScalarRange(start, end),
        Task = AiTaskKind.Expansion,
        ProviderConfigRef = "local/test",
        PromptVersion = "p1",
        SourceText = injectedSource ?? string.Empty,
    };

    [Fact]
    public async Task Resolves_source_span_from_the_revision_and_ignores_client_text()
    {
        var chain = new RevisionChain();
        var revision = chain.Append(Scope, TextLayer.L1Diplomatic, "αβγδε", Human);
        AiProposalRequest? seen = null;
        var provider = new ScriptedAiProvider("stub", "stub-1", request => { seen = request; return "βγ"; });
        var service = new AiProposalService(provider);

        var outcome = await service.ProposeAsync(chain, Request(revision.Id, 1, 3, injectedSource: "IGNORE ME"));

        Assert.True(outcome.Succeeded);
        Assert.Equal("βγ", seen!.SourceText);
        Assert.Equal("βγ", outcome.Proposal!.ProposedText);
    }

    [Fact]
    public async Task Scalar_offsets_handle_combining_marks_without_splitting_a_cluster()
    {
        var chain = new RevisionChain();
        var revision = chain.Append(Scope, TextLayer.L1Diplomatic, "α\u0301β", Human); // 3 scalars, no precomposition
        AiProposalRequest? seen = null;
        var provider = new ScriptedAiProvider("stub", "stub-1", request => { seen = request; return request.SourceText; });

        await new AiProposalService(provider).ProposeAsync(chain, Request(revision.Id, 0, 2));

        Assert.Equal("α\u0301", seen!.SourceText);
    }

    [Fact]
    public async Task Unknown_revision_fails_with_rebase_hint()
    {
        var chain = new RevisionChain();
        var provider = new ScriptedAiProvider("stub", "stub-1", _ => "x");

        var ex = await Assert.ThrowsAsync<LeviathanException>(() =>
            new AiProposalService(provider).ProposeAsync(chain, Request(new EntityId(EntityKind.TranscriptionRevision, "missing"), 0, 0)));

        Assert.Equal(ErrorCode.StaleAnnotationAnchor, ex.Error.Code);
        Assert.Equal(RemedyHint.RebaseRevision, ex.Error.RemedyHint);
    }

    [Fact]
    public async Task Out_of_range_span_fails_before_the_provider_is_called()
    {
        var chain = new RevisionChain();
        var revision = chain.Append(Scope, TextLayer.L1Diplomatic, "αβγ", Human);
        var called = false;
        var provider = new ScriptedAiProvider("stub", "stub-1", _ => { called = true; return "x"; });

        var ex = await Assert.ThrowsAsync<LeviathanException>(() =>
            new AiProposalService(provider).ProposeAsync(chain, Request(revision.Id, 2, 99)));

        Assert.Equal(ErrorCode.StageContractViolation, ex.Error.Code);
        Assert.False(called);
    }

    [Fact]
    public async Task Proposal_from_resolved_source_can_be_accepted_into_L2()
    {
        var chain = new RevisionChain();
        var revision = chain.Append(Scope, TextLayer.L1Diplomatic, "κυριου", Human);
        var provider = new SchemaValidatingAiProvider(
            new RawScriptedAiProvider("local-stub", "stub-1", _ => "κυρίου"));
        var service = new AiProposalService(provider);

        var outcome = await service.ProposeAsync(chain, Request(revision.Id, 0, 6));
        var (accepted, editorial) = new ProposalAcceptanceService(chain).Accept(outcome.Proposal!, Human);

        Assert.Equal(ProposalStatus.Accepted, accepted.Status);
        Assert.Equal("κυρίου", editorial.Text);
        Assert.Equal(TextLayer.L2Editorial, editorial.Layer);
        Assert.Equal("κυριου", chain.Current(Scope, TextLayer.L1Diplomatic)!.Text);
    }
}
