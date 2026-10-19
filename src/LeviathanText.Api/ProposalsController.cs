using LeviathanText.AI;
using LeviathanText.Contracts;
using LeviathanText.Contracts.Serialization;
using LeviathanText.Core.Revisions;
using Microsoft.AspNetCore.Mvc;

namespace LeviathanText.Api;

/// <summary>
/// AI proposal endpoints (interchange-and-api.md §2.3): tasks are visually separate,
/// provenance travels on every proposal, acceptance creates an L2 revision (L1 untouched),
/// and malformed provider responses become recorded failures — never partial proposals.
/// Providers in this scaffold are the labeled test double and the schema-validating
/// wrapper; real hosted/local adapters require user configuration (todo §2).
/// </summary>
[ApiController]
[Route("/api/v1/proposals")]
public sealed class ProposalsController(RevisionChain chain, JobEventBus bus) : ControllerBase
{
    private static readonly RawScriptedAiProvider Raw = new("scripted-double", "labeled-test-double",
        request => $"[proposal:{request.Task}]");

    private static readonly SchemaValidatingAiProvider Provider = new(Raw, maxTextLength: 2000);

    [HttpPost]
    public async Task<IActionResult> Propose([FromBody] AiProposalRequest request, CancellationToken cancellation)
    {
        var outcome = await Provider.ProposeAsync(request, cancellation).ConfigureAwait(false);
        if (outcome.Failure is { } failure)
        {
            bus.Publish(request.TargetRevisionId, "proposal.failed", failure.Reason);
            return Ok(new { failure });
        }
        bus.Publish(request.TargetRevisionId, "proposal.created", ContractJson.ToJson(outcome.Proposal));
        return Ok(ContractJson.ToJson(outcome.Proposal));
    }

    [HttpPost("{id}/accept")]
    public IActionResult Accept(string id, [FromBody] AcceptRequestBody body)
    {
        var acceptor = new AgentRef { Id = body.AcceptorId, IsModel = false };
        var (accepted, editorial) = new ProposalAcceptanceService(chain).Accept(body.Proposal, acceptor);
        return Ok(ContractJson.ToJson(new
        {
            acceptedProposalId = accepted.Id.ToString(),
            editorialRevisionId = editorial.Id.ToString(),
            layer = editorial.Layer.ToString(),
        }));
    }

    public sealed record AcceptRequestBody(AiProposal Proposal, string AcceptorId);
}
