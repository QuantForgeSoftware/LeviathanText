using LeviathanText.Contracts;

namespace LeviathanText.AI;

/// <summary>
/// Visually separate proposal tasks (interchange-and-api.md §2.3, todo §4).
/// A restoration must never be conflated with a visual reading.
/// </summary>
public enum AiTaskKind
{
    CandidateReading,
    WordDivision,
    Expansion,
    Accentuation,
    Restoration,
}

/// <summary>Acceptance lifecycle of a proposal (interchange-and-api.md §2.3).</summary>
public enum ProposalStatus
{
    Proposed,
    Accepted,
    Rejected,
    Expired,
}

/// <summary>
/// A request to a provider. Credentials are never part of this record: the provider
/// adapter resolves <see cref="ProviderConfigRef"/> against server-side secret storage.
/// </summary>
public sealed record AiProposalRequest
{
    public required EntityId TargetRevisionId { get; init; }
    public required ScalarRange Range { get; init; }
    public required AiTaskKind Task { get; init; }

    /// <summary>Opaque reference to a server-side provider/model/prompt configuration.</summary>
    public required string ProviderConfigRef { get; init; }

    public required string PromptVersion { get; init; }
}

/// <summary>
/// A single proposal with full provenance. The proposal id is a string because the C0
/// identity hierarchy has no Proposal entity kind; a dedicated kind is requested
/// (B-CCR-7).
/// </summary>
public sealed record AiProposal
{
    public required string Id { get; init; }
    public required AiProposalRequest Request { get; init; }
    public required string ProposedText { get; init; }
    public required string ProviderId { get; init; }
    public required string ModelId { get; init; }
    public required string PromptVersion { get; init; }

    /// <summary>Always carries its scale; model probability is not scholarly certainty.</summary>
    public required Confidence Confidence { get; init; }

    public ProposalStatus Status { get; init; } = ProposalStatus.Proposed;
    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// A malformed or failed provider attempt. Only a bounded, redacted excerpt of the raw
/// response is retained; a failed attempt never surfaces partial text as a proposal.
/// </summary>
public sealed record AiProposalFailure
{
    public required string ProviderId { get; init; }
    public required string Reason { get; init; }

    /// <summary>Bounded excerpt (control characters stripped), or null.</summary>
    public string? RawResponseExcerpt { get; init; }
}

/// <summary>Result of a provider call: either a validated proposal or a recorded failure.</summary>
public sealed record ProposalOutcome
{
    public AiProposal? Proposal { get; init; }
    public AiProposalFailure? Failure { get; init; }

    public bool Succeeded => Proposal is not null;

    public static ProposalOutcome Ok(AiProposal proposal) => new() { Proposal = proposal };

    public static ProposalOutcome Failed(AiProposalFailure failure) => new() { Failure = failure };
}
