using LeviathanText.Contracts;
using LeviathanText.Core.Revisions;
using LeviathanText.Core.Unicode;

namespace LeviathanText.AI;

/// <summary>
/// Turns a client's span-level request into a provider call with the authoritative
/// diplomatic text of the target revision (interchange-and-api.md §2.3: the client names
/// a revision and scalar offsets; it does not supply the evidence). Any
/// <see cref="AiProposalRequest.SourceText"/> on the incoming request is discarded, so a
/// caller cannot smuggle prompt content past the revision contract.
/// </summary>
public sealed class AiProposalService
{
    public AiProposalService(IAiProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        Provider = provider;
    }

    public IAiProvider Provider { get; }

    public async Task<ProposalOutcome> ProposeAsync(
        RevisionChain chain,
        AiProposalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentNullException.ThrowIfNull(request);

        var revision = chain.Get(request.TargetRevisionId)
            ?? throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StaleAnnotationAnchor,
                Scope = request.TargetRevisionId.ToString(),
                RemedyHint = RemedyHint.RebaseRevision,
                Message = $"Unknown target revision {request.TargetRevisionId}.",
            });

        var scalarLength = ScalarText.ScalarLength(revision.Text);
        if (request.Range.Start < 0 || request.Range.End > scalarLength || request.Range.End < request.Range.Start)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = request.TargetRevisionId.ToString(),
                Details = new Dictionary<string, string>
                {
                    ["range"] = $"{request.Range.Start}..{request.Range.End}",
                    ["scalarLength"] = scalarLength.ToString(),
                },
                Message = "The requested scalar range is outside the target revision.",
            });

        var sourceText = ScalarText.Slice(revision.Text, request.Range.Start, request.Range.End);
        var enriched = request with { SourceText = sourceText };

        return await Provider.ProposeAsync(enriched, cancellationToken).ConfigureAwait(false);
    }
}
