using System.Text.Json;

namespace LeviathanText.Application;

/// <summary>
/// Corpus-ledger audit (todo §5 / DATA-01): checks the declared schema and the fields that
/// must never be missing when a source is used. This does not verify rights — it verifies that
/// the ledger says what it is required to say. Missing/unverified values are reported.
/// </summary>
public static class CorpusManifestAudit
{
    public const string ExpectedSchema = "leviathantext.corpus-manifest";

    public enum Severity { Error, Warning }

    public sealed record Issue(string SourceId, string Field, Severity Severity, string Message);

    private static readonly string[] RequiredSourceFields =
    [
        "source_id", "source_version", "url_doi", "retrieval_date", "content_checksums",
        "material_track", "license_image", "license_text", "license_annotation",
        "license_evidence", "attribution", "allowed_intended_use",
    ];

    public static IReadOnlyList<Issue> Audit(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var issues = new List<Issue>();

        if (!root.TryGetProperty("schema", out var schema) || schema.GetString() != ExpectedSchema)
            issues.Add(new Issue("(manifest)", "schema", Severity.Error,
                $"Expected schema '{ExpectedSchema}'."));

        if (!root.TryGetProperty("schemaContractVersion", out _))
            issues.Add(new Issue("(manifest)", "schemaContractVersion", Severity.Error,
                "Missing schemaContractVersion (field names must be consistent across ledgers)."));

        if (!root.TryGetProperty("changeLog", out var changeLog) || changeLog.GetArrayLength() == 0)
            issues.Add(new Issue("(manifest)", "changeLog", Severity.Warning, "Change log is empty."));

        if (!root.TryGetProperty("sources", out var sources) || sources.GetArrayLength() == 0)
        {
            issues.Add(new Issue("(manifest)", "sources", Severity.Error, "No sources recorded."));
            return issues;
        }

        foreach (var source in sources.EnumerateArray())
        {
            var id = source.TryGetProperty("source_id", out var sid) ? sid.GetString() ?? "(source)" : "(source)";
            foreach (var field in RequiredSourceFields)
            {
                if (!source.TryGetProperty(field, out var value)
                    || value.ValueKind is JsonValueKind.Null
                    || (value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString())))
                    issues.Add(new Issue(id, field, Severity.Error, "Required source field is missing or empty."));
            }

            foreach (var licenseField in new[] { "license_image", "license_text", "license_annotation" })
                if (source.TryGetProperty(licenseField, out var license)
                    && license.GetString() is { } text
                    && text.Contains("unverified", StringComparison.OrdinalIgnoreCase))
                    issues.Add(new Issue(id, licenseField, Severity.Warning,
                        "License is recorded as unverified; do not rely on it for redistribution."));

            if (source.TryGetProperty("units_evidence", out var evidence)
                && (!evidence.TryGetProperty("counts", out _) || !evidence.TryGetProperty("provenance", out _)))
                issues.Add(new Issue(id, "units_evidence", Severity.Warning,
                    "Units evidence should carry counts and provenance."));
        }

        return issues;
    }
}
