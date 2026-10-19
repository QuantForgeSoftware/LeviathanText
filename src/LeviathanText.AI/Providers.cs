using LeviathanText.Contracts;

namespace LeviathanText.AI;

/// <summary>
/// Provider-neutral adapter (todo §4). Adapters are swappable across local and hosted
/// providers; the interface accepts the same request shape for every task.
/// </summary>
public interface IAiProvider
{
    string ProviderId { get; }

    Task<ProposalOutcome> ProposeAsync(AiProposalRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Low-level raw text generator, e.g. one HTTP call to a model.</summary>
public interface IRawAiProvider
{
    string ProviderId { get; }
    string ModelId { get; }

    Task<string?> GenerateRawAsync(AiProposalRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Validates a provider's raw response against the proposal schema before it becomes a
/// proposal. Failures are recorded, never surfaced as partial text (interchange §2.3).
/// </summary>
public sealed class SchemaValidatingAiProvider(IRawAiProvider inner, int maxTextLength = 4096) : IAiProvider
{
    public string ProviderId => inner.ProviderId;

    public async Task<ProposalOutcome> ProposeAsync(AiProposalRequest request, CancellationToken cancellationToken = default)
    {
        string? raw;
        try
        {
            raw = await inner.GenerateRawAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ProposalOutcome.Failed(new AiProposalFailure
            {
                ProviderId = inner.ProviderId,
                Reason = $"provider-error:{ex.GetType().Name}",
            });
        }

        var reason = Validate(raw, maxTextLength);
        if (reason is not null)
            return ProposalOutcome.Failed(new AiProposalFailure
            {
                ProviderId = inner.ProviderId,
                Reason = reason,
                RawResponseExcerpt = Excerpt(raw),
            });

        return ProposalOutcome.Ok(new AiProposal
        {
            Id = ProposalIds.New(),
            Request = request,
            ProposedText = raw!,
            ProviderId = inner.ProviderId,
            ModelId = inner.ModelId,
            PromptVersion = request.PromptVersion,
            Confidence = Confidence.Of(0.5, ConfidenceScale.RawModelProbability),
            CreatedAt = DateTimeOffset.UtcNow,
        });
    }

    internal static string? Validate(string? raw, int maxTextLength)
    {
        if (raw is null) return "null-response";
        if (raw.Length == 0) return "empty-response";
        if (raw.Length > maxTextLength) return "response-too-long";
        if (raw.Any(char.IsControl)) return "control-characters";
        if (raw.All(char.IsWhiteSpace)) return "whitespace-only";
        return null;
    }

    private static string? Excerpt(string? raw)
    {
        if (raw is null) return null;
        var cleaned = new string(raw.Where(c => !char.IsControl(c)).ToArray());
        return cleaned.Length <= 200 ? cleaned : cleaned[..200];
    }
}

/// <summary>Mints proposal identifiers from the accepted Proposal entity kind (B-CCR-7).</summary>
public static class ProposalIds
{
    public static EntityId New() => new(EntityKind.Proposal, Guid.NewGuid().ToString("N"));
}

/// <summary>Raw variant of the scripted double, for use under the schema-validating wrapper.</summary>
public sealed class RawScriptedAiProvider(string providerId, string modelId, Func<AiProposalRequest, string> generator) : IRawAiProvider
{
    public string ProviderId => providerId;

    public string ModelId => modelId;

    public Task<string?> GenerateRawAsync(AiProposalRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(generator(request));
}

/// <summary>Deterministic in-process test double. Used to exercise proposal handling without a
/// network call; its identity clearly marks it as not a real model.</summary>
public sealed class ScriptedAiProvider(string providerId, string modelId, Func<AiProposalRequest, string> generator) : IAiProvider
{
    public string ProviderId => providerId;

    public Task<ProposalOutcome> ProposeAsync(AiProposalRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var text = generator(request);
        return Task.FromResult(ProposalOutcome.Ok(new AiProposal
        {
            Id = ProposalIds.New(),
            Request = request,
            ProposedText = text,
            ProviderId = providerId,
            ModelId = modelId,
            PromptVersion = request.PromptVersion,
            Confidence = Confidence.Of(0.9, ConfidenceScale.RawModelProbability),
            CreatedAt = DateTimeOffset.UtcNow,
        }));
    }

}
