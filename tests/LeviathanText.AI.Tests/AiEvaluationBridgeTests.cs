using LeviathanText.AI;
using LeviathanText.Contracts;
using LeviathanText.Evaluation;
using Xunit;

namespace LeviathanText.AI.Tests;

/// <summary>Proposal records feed the EVAL correction-effort scorer without a package cycle.</summary>
public class AiEvaluationBridgeTests
{
    private static AiProposal Proposal(AiTaskKind task, ProposalStatus status, string text, string source) => new()
    {
        Id = ProposalIds.New(),
        Request = new AiProposalRequest
        {
            TargetRevisionId = new EntityId(EntityKind.TranscriptionRevision, "r1"),
            Range = new ScalarRange(0, 6),
            Task = task,
            ProviderConfigRef = "local/test",
            PromptVersion = "p1",
            SourceText = source,
        },
        ProposedText = text,
        ProviderId = "stub",
        ModelId = "stub-1",
        PromptVersion = "p1",
        Confidence = Confidence.Of(0.5, ConfidenceScale.RawModelProbability),
        Status = status,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    [Theory]
    [InlineData(AiTaskKind.CandidateReading, SuggestionKind.CandidateReading)]
    [InlineData(AiTaskKind.WordDivision, SuggestionKind.WordDivision)]
    [InlineData(AiTaskKind.Expansion, SuggestionKind.Expansion)]
    [InlineData(AiTaskKind.Accentuation, SuggestionKind.Accentuation)]
    [InlineData(AiTaskKind.Restoration, SuggestionKind.Restoration)]
    public void Task_kinds_map_one_to_one(AiTaskKind source, SuggestionKind expected) =>
        Assert.Equal(expected, AiEvaluationBridge.ToSuggestionKind(source));

    [Theory]
    [InlineData(ProposalStatus.Proposed, SuggestionDisposition.Proposed)]
    [InlineData(ProposalStatus.Accepted, SuggestionDisposition.Accepted)]
    [InlineData(ProposalStatus.Rejected, SuggestionDisposition.Rejected)]
    [InlineData(ProposalStatus.Expired, SuggestionDisposition.Expired)]
    public void Statuses_map_one_to_one(ProposalStatus source, SuggestionDisposition expected) =>
        Assert.Equal(expected, AiEvaluationBridge.ToDisposition(source));

    [Fact]
    public void Session_scoring_uses_the_recorded_source_and_status()
    {
        var accepted = Proposal(AiTaskKind.Accentuation, ProposalStatus.Accepted, "κυρίου", "κυριου");
        var rejected = Proposal(AiTaskKind.CandidateReading, ProposalStatus.Rejected, "κυριο", "κυριου");

        var score = AiEvaluationBridge.ScoreSession(
            reference: "κυρίου",
            raw: "κυριου",
            final: "κυρίου",
            proposals: [accepted, rejected],
            time: new CorrectionTime(TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(1)));

        Assert.Equal(1, score.AcceptedSuggestions);
        Assert.Equal(1, score.RejectedSuggestions);
        Assert.Equal(1, score.SuppliedRestorationScalars);
        Assert.Equal(0, score.UnsupportedAddedScalars);
        Assert.Equal(0.0, score.FinalCer);
        Assert.Equal(0.5, score.Time!.Reduction, 10);
    }

    [Fact]
    public void Unknown_enum_values_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AiEvaluationBridge.ToSuggestionKind((AiTaskKind)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => AiEvaluationBridge.ToDisposition((ProposalStatus)99));
    }
}
