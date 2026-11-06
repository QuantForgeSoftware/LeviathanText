using LeviathanText.AI;
using LeviathanText.Application;
using LeviathanText.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace LeviathanText.Api;

/// <summary>Native-project AI proposals with durable issued records and explicit L2 acceptance.</summary>
[ApiController]
[Route("/api/v1")]
public sealed class ProposalsController(
    NativeAiProposalService native,
    NativeAiProjectResolver projects,
    AiProviderRegistry registry) : ControllerBase
{
    [HttpGet("providers")]
    public IActionResult Providers() => Ok(new
    {
        providers = registry.ProviderIds.Select(id => new
        {
            configRef = id,
            settings = registry.SettingsFor(id) with { CredentialEnvironmentVariable = null },
        }),
        note = "credentials resolve server-side only; the browser never sees or sends them",
    });

    [HttpPost("proposals")]
    public async Task<IActionResult> Propose([FromBody] AiProposalRequest request,
        [FromQuery] string? projectId, CancellationToken cancellation)
    {
        try
        {
            var project = projects.ForRevision(HttpContext, request.TargetRevisionId, projectId);
            var prepared = native.Prepare(project, request);
            var provider = registry.ProviderFor(request.ProviderConfigRef);
            var outcome = await provider.ProposeAsync(prepared.Request, cancellation).ConfigureAwait(false);
            var current = projects.ReloadAuthorized(HttpContext, prepared.ProjectId);
            if (outcome.Failure is { } failure)
            {
                return Ok(new { failure });
            }
            var issued = native.Issue(current, prepared, outcome.Proposal
                ?? throw Invalid("Provider returned neither a proposal nor a failure."));
            projects.RequireAccess(HttpContext, current.Id);
            return Ok(issued.Record.Proposal);
        }
        catch (NativeAiAccessDeniedException) { return Denied(); }
    }

    [HttpPost("proposals/{proposalId}/accept")]
    public IActionResult Accept(string proposalId, [FromBody] AcceptRequestBody body)
    {
        try
        {
            var id = EntityId.Parse(proposalId, EntityKind.Proposal);
            if (body.Proposal is null || body.Proposal.Id != id)
                throw Invalid("The route and supplied proposal identity must agree.");
            var project = projects.ForProposals(HttpContext, [id], body.ProjectId);
            var accepted = native.Accept(project, [id], Acceptor(body.AcceptorId), [body.Proposal]);
            projects.RequireAccess(HttpContext, project.Id);
            var result = accepted.Acceptances[0];
            return Ok(new
            {
                proposal = result.Accepted, revisionId = result.EditorialRevision.Id,
                receipt = result.Receipt, alreadyAccepted = result.AlreadyAccepted,
            });
        }
        catch (NativeAiAccessDeniedException) { return Denied(); }
    }

    [HttpPost("proposals/bulk-preview")]
    public IActionResult BulkPreview([FromBody] BulkRequestBody body)
    {
        try
        {
            var supplied = RequireProposals(body.Proposals);
            var ids = supplied.Select(p => p.Id).ToArray();
            var project = projects.ForProposals(HttpContext, ids, body.ProjectId);
            var preview = native.Preview(project, ids, supplied);
            projects.RequireAccess(HttpContext, project.Id);
            return Ok(preview);
        }
        catch (NativeAiAccessDeniedException) { return Denied(); }
    }

    [HttpPost("proposals/bulk-accept")]
    public IActionResult BulkAccept([FromBody] BulkRequestBody body)
    {
        try
        {
            var supplied = RequireProposals(body.Proposals);
            var ids = supplied.Select(p => p.Id).ToArray();
            var project = projects.ForProposals(HttpContext, ids, body.ProjectId);
            var preview = native.Preview(project, ids, supplied);
            projects.RequireAccess(HttpContext, project.Id);
            if (!preview.IsFullyApplicable)
                return Conflict(new { error = "bulk-acceptance-blocked", preview, remedy = "resolve blocked proposals before accepting" });
            var accepted = native.Accept(project, ids, Acceptor(body.AcceptorId), supplied);
            projects.RequireAccess(HttpContext, project.Id);
            return Ok(accepted);
        }
        catch (NativeAiAccessDeniedException) { return Denied(); }
    }

    private AgentRef Acceptor(string supplied) =>
        new() { Id = MutationAttribution.Actor(HttpContext, supplied), IsModel = false };

    private ObjectResult Denied() => StatusCode(StatusCodes.Status403Forbidden,
        new { code = "forbidden", message = "Current account or project access does not permit this native AI operation." });

    private static IReadOnlyList<AiProposal> RequireProposals(IReadOnlyList<AiProposal>? proposals)
    {
        if (proposals is null || proposals.Count is < 1 or > NativeAiProposalService.MaxBatchSize
            || proposals.Any(p => p is null))
            throw Invalid($"A native proposal batch requires between 1 and {NativeAiProposalService.MaxBatchSize} issued proposal records.");
        return proposals;
    }

    private static LeviathanException Invalid(string message) => new(new LeviathanError
    {
        Code = ErrorCode.StageContractViolation, Scope = "native-ai", Message = message,
    });

    public sealed record AcceptRequestBody(AiProposal Proposal, string AcceptorId, string? ProjectId = null);
    public sealed record BulkRequestBody(IReadOnlyList<AiProposal> Proposals, string AcceptorId, string? ProjectId = null);
}
