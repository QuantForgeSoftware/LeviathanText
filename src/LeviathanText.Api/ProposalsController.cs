using LeviathanText.AI;
using LeviathanText.Contracts;
using LeviathanText.Contracts.Serialization;
using LeviathanText.Core.Revisions;
using Microsoft.AspNetCore.Mvc;

namespace LeviathanText.Api;

/// <summary>
/// AI proposal endpoints (interchange-and-api.md §2.3), now backed by B's server-side
/// services: the client names a revision + scalar offsets and NEVER supplies evidence
/// (resolved from the revision chain); providers resolve through the registry so
/// switching is configuration, not code; bulk acceptance requires the pure preview and
/// reports blocked items instead of overwriting.
/// </summary>
[ApiController]
[Route("/api/v1")]
public sealed class ProposalsController(
    RevisionChain chain,
    AiProviderRegistry registry,
    BulkProposalAcceptanceService bulk,
    JobEventBus bus) : ControllerBase
{
    [HttpGet("providers")]
    public IActionResult Providers() => Ok(new
    {
        providers = registry.ProviderIds.Select(id => new
        {
            configRef = id,
            settings = registry.SettingsFor(id) with { CredentialEnvironmentVariable = null }, // never leak credential names
        }),
        note = "credentials resolve server-side only; the browser never sees or sends them",
    });

    [HttpPost("proposals")]
    public async Task<IActionResult> Propose([FromBody] AiProposalRequest request, CancellationToken cancellation)
    {
        var service = new AiProposalService(registry.ProviderFor(request.ProviderConfigRef));
        var outcome = await service.ProposeAsync(chain, request, cancellation).ConfigureAwait(false);

        if (outcome.Failure is { } failure)
        {
            bus.Publish(request.TargetRevisionId, "proposal.failed", failure.Reason);
            return Ok(new { failure });
        }
        bus.Publish(request.TargetRevisionId, "proposal.created", ContractJson.ToJson(outcome.Proposal));
        return Ok(ContractJson.ToJson(outcome.Proposal));
    }

    [HttpPost("proposals/bulk-preview")]
    public IActionResult BulkPreview([FromBody] BulkRequestBody body) =>
        Ok(ContractJson.ToJson(bulk.Preview(body.Proposals)));

    [HttpPost("proposals/bulk-accept")]
    public IActionResult BulkAccept([FromBody] BulkRequestBody body)
    {
        var preview = bulk.Preview(body.Proposals);
        if (!preview.IsFullyApplicable)
            return Conflict(new
            {
                error = "bulk-acceptance-blocked",
                preview,
                remedy = "resolve blocked proposals (reject/expire them) or accept individually",
            });

        var result = bulk.Accept(body.Proposals, new AgentRef { Id = body.AcceptorId, IsModel = false });
        bus.Publish(body.Proposals[0].Request.TargetRevisionId, "proposal.bulk-accepted", ContractJson.ToJson(result));
        return Ok(ContractJson.ToJson(result));
    }

    public sealed record BulkRequestBody(IReadOnlyList<AiProposal> Proposals, string AcceptorId);
}
