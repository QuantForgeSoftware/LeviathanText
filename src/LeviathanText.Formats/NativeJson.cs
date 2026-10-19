using LeviathanText.Contracts;
using LeviathanText.Contracts.Serialization;

namespace LeviathanText.Formats;

/// <summary>
/// Native JSON persistence (todo Phase 1: native object/surface/capture persistence).
/// Uses the shared contract conventions (camelCase, strict enums). Documents are
/// versioned by the contract version they were written under.
/// </summary>
public static class NativeJson
{
    /// <summary>Envelope member carrying the contract version; loaders validate it.</summary>
    public sealed record Envelope(string ContractVersion, NativeProject Project);

    public static string Save(NativeProject project) =>
        ContractJson.ToJson(new Envelope(ContractVersion.Current, project));

    /// <summary>
    /// Load a project. Refuses documents written under a different contract version
    /// (contract-version-unsupported is the closed-vocabulary code for this).
    /// </summary>
    public static NativeProject Load(string json)
    {
        var envelope = ContractJson.FromJson<Envelope>(json);
        if (envelope.ContractVersion != ContractVersion.Current)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.ProfileVersionUnsupported,
                Scope = "native-project",
                Details = new Dictionary<string, string>
                {
                    ["documentVersion"] = envelope.ContractVersion,
                    ["supportedVersion"] = ContractVersion.Current,
                },
                RemedyHint = RemedyHint.None,
                Message = "Native project was written under a different contract version; conversion must be explicit.",
            });
        return envelope.Project;
    }
}
