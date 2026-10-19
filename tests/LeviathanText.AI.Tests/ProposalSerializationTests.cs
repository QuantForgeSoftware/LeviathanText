using System.Text.Json;
using LeviathanText.AI;
using LeviathanText.Contracts;
using LeviathanText.Contracts.Serialization;
using Xunit;

namespace LeviathanText.AI.Tests;

/// <summary>
/// Proposal records on the wire: camelCase, camelCase string enums, scalar ranges,
/// confidence and provenance all survive a round trip, and unknown enum strings are a
/// hard error rather than a silently defaulted status.
/// </summary>
public class ProposalSerializationTests
{
    private static AiProposal Proposal() => new()
    {
        Id = ProposalIds.New(),
        Request = new AiProposalRequest
        {
            TargetRevisionId = new EntityId(EntityKind.TranscriptionRevision, "r7"),
            Range = new ScalarRange(2, 6),
            Task = AiTaskKind.Expansion,
            ProviderConfigRef = "local/ollama-greek",
            PromptVersion = "greek-ai-1",
            SourceText = "κυριου",
            Instruction = "keep the lacuna",
        },
        ProposedText = "κυρίου",
        ProviderId = "local-ollama",
        ModelId = "greek-local",
        PromptVersion = "greek-ai-1",
        Confidence = Confidence.Of(0.62, ConfidenceScale.RawModelProbability),
        CreatedAt = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero),
    };

    [Fact]
    public void Proposal_round_trips_with_camelCase_string_enums()
    {
        var json = ContractJson.ToJson(Proposal());

        using (var document = JsonDocument.Parse(json))
        {
            var request = document.RootElement.GetProperty("request");
            Assert.Equal("κυριου", request.GetProperty("sourceText").GetString());
            Assert.Equal("expansion", request.GetProperty("task").GetString());
            Assert.Equal(2, request.GetProperty("range").GetProperty("start").GetInt32());
            Assert.Equal("proposed", document.RootElement.GetProperty("status").GetString());
            Assert.Equal("rawModelProbability", document.RootElement.GetProperty("confidence").GetProperty("scale").GetString());
        }

        Assert.Contains("\"task\":\"expansion\"", json);
        Assert.Contains("\"range\":{\"start\":2,\"end\":6}", json);

        var read = ContractJson.FromJson<AiProposal>(json);
        Assert.Equal("κυρίου", read.ProposedText);
        Assert.Equal(AiTaskKind.Expansion, read.Request.Task);
        Assert.Equal("κυριου", read.Request.SourceText);
        Assert.Equal(new ScalarRange(2, 6), read.Request.Range);
        Assert.Equal(ConfidenceScale.RawModelProbability, read.Confidence.Scale);
        Assert.Equal(0.62, read.Confidence.Value, 10);
        Assert.Equal(ProposalStatus.Proposed, read.Status);
        Assert.Equal(new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero), read.CreatedAt);
    }

    [Fact]
    public void Unknown_task_string_is_rejected()
    {
        var json = ContractJson.ToJson(Proposal()).Replace("\"expansion\"", "\"mind-reading\"");

        Assert.Throws<JsonException>(() => ContractJson.FromJson<AiProposal>(json));
    }

    [Fact]
    public void Failure_record_round_trips_with_a_bounded_excerpt()
    {
        var failure = new AiProposalFailure
        {
            ProviderId = "hosted-openai",
            Reason = AiFailureReasons.MalformedResponse,
            RawResponseExcerpt = "not json",
        };

        var read = ContractJson.FromJson<AiProposalFailure>(ContractJson.ToJson(failure));

        Assert.Equal(failure, read);
    }
}
