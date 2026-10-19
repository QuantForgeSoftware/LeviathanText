using LeviathanText.AI;
using LeviathanText.Contracts;
using LeviathanText.Core.Revisions;
using Xunit;

namespace LeviathanText.AI.Tests;

/// <summary>Proposal handling and acceptance against the C0 revision contract.</summary>
public class ProposalTests
{
    private static AiProposalRequest Request(EntityId targetRevision) => new()
    {
        TargetRevisionId = targetRevision,
        Range = new ScalarRange(0, 2),
        Task = AiTaskKind.Expansion,
        ProviderConfigRef = "local/test-config",
        PromptVersion = "p1",
    };

    [Fact]
    public async Task ScriptedProvider_ReturnsProposalWithProvenance()
    {
        var provider = new ScriptedAiProvider("local-stub", "stub-1", _ => "κυρίου");
        var target = new EntityId(EntityKind.TranscriptionRevision, "r1");

        var outcome = await provider.ProposeAsync(Request(target));

        Assert.True(outcome.Succeeded);
        var proposal = outcome.Proposal!;
        Assert.Equal("κυρίου", proposal.ProposedText);
        Assert.Equal("local-stub", proposal.ProviderId);
        Assert.Equal("stub-1", proposal.ModelId);
        Assert.Equal("p1", proposal.PromptVersion);
        Assert.Equal(ProposalStatus.Proposed, proposal.Status);
        Assert.Equal(ConfidenceScale.RawModelProbability, proposal.Confidence.Scale);
    }

    [Theory]
    [InlineData("", "empty-response")]
    [InlineData(null, "null-response")]
    [InlineData("bad\u0007text", "control-characters")]
    public async Task SchemaValidatingProvider_RecordsMalformedResponses(string? raw, string expectedReason)
    {
        var provider = new SchemaValidatingAiProvider(new RawStub(raw));
        var outcome = await provider.ProposeAsync(Request(new EntityId(EntityKind.TranscriptionRevision, "r1")));

        Assert.False(outcome.Succeeded);
        Assert.Equal(expectedReason, outcome.Failure!.Reason);
        Assert.Null(outcome.Proposal);
    }

    [Fact]
    public async Task Acceptance_CreatesEditorialLayer_AndLeavesDiplomaticTextIntact()
    {
        var chain = new RevisionChain();
        var scope = new EntityId(EntityKind.Surface, "s1");
        var human = new AgentRef { Id = "u1", IsModel = false };
        var l1 = chain.Append(scope, TextLayer.L1Diplomatic, "Κϲ", human);

        var provider = new ScriptedAiProvider("local-stub", "stub-1", _ => "κυρίου");
        var proposal = (await provider.ProposeAsync(Request(l1.Id))).Proposal!;

        var service = new ProposalAcceptanceService(chain);
        var (accepted, editorial) = service.Accept(proposal, human);

        Assert.Equal(ProposalStatus.Accepted, accepted.Status);
        Assert.Equal("κυρίου", editorial.Text);
        Assert.Equal(TextLayer.L2Editorial, editorial.Layer);
        Assert.Equal("Κϲ", chain.Current(scope, TextLayer.L1Diplomatic)!.Text); // L1 untouched
    }

    [Fact]
    public async Task Acceptance_RejectsProposalTargetingASupersededRevision()
    {
        var chain = new RevisionChain();
        var scope = new EntityId(EntityKind.Surface, "s1");
        var human = new AgentRef { Id = "u1", IsModel = false };
        var r1 = chain.Append(scope, TextLayer.L1Diplomatic, "Κϲ", human);
        chain.Append(scope, TextLayer.L1Diplomatic, "Κϲ ", human, parentRevisionId: r1.Id);

        var provider = new ScriptedAiProvider("local-stub", "stub-1", _ => "κυρίου");
        var proposal = (await provider.ProposeAsync(Request(r1.Id))).Proposal!;

        var service = new ProposalAcceptanceService(chain);
        var ex = Assert.Throws<LeviathanException>(() => service.Accept(proposal, human));
        Assert.Equal(ErrorCode.StaleAnnotationAnchor, ex.Error.Code);
    }

    private sealed class RawStub(string? raw) : IRawAiProvider
    {
        public string ProviderId => "raw-stub";
        public string ModelId => "raw-1";
        public Task<string?> GenerateRawAsync(AiProposalRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(raw);
    }
}
