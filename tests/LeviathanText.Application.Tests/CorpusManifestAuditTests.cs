using LeviathanText.Application;

namespace LeviathanText.Application.Tests;

public sealed class CorpusManifestAuditTests
{
    private const string Valid = """
        {
          "schema": "leviathantext.corpus-manifest",
          "schemaContractVersion": "C0-0.4-draft",
          "manifestVersion": "manifest-v1",
          "changeLog": [{ "date": "2026-10-19", "change": "ok" }],
          "sources": [{
            "source_id": "S1", "source_version": "1", "url_doi": "https://doi.org/10.0/x",
            "retrieval_date": "2026-10-19", "content_checksums": "sha256 verified",
            "material_track": "papyrus", "license_image": "cc-by-4.0",
            "license_text": "cc-by-4.0", "license_annotation": "cc-by-4.0",
            "license_evidence": "publisher page", "attribution": "Museum X",
            "allowed_intended_use": "dev",
            "units_evidence": { "counts": { "pages": 1 }, "provenance": "inspected" }
          }]
        }
        """;

    [Fact]
    public void Valid_manifest_has_no_issues()
    {
        Assert.Empty(CorpusManifestAudit.Audit(Valid));
    }

    [Fact]
    public void Missing_required_fields_and_schema_are_errors()
    {
        var issues = CorpusManifestAudit.Audit("""
            { "schema": "other", "sources": [{ "source_id": "S1", "license_image": "unverified" }] }
            """);

        Assert.Contains(issues, i => i.Field == "schema" && i.Severity == CorpusManifestAudit.Severity.Error);
        Assert.Contains(issues, i => i.Field == "schemaContractVersion");
        Assert.Contains(issues, i => i.Field == "url_doi");
        Assert.Contains(issues, i => i.Field == "license_image" && i.Severity == CorpusManifestAudit.Severity.Warning);
    }

    [Fact]
    public void Extended_audit_requires_lineage_licence_and_profile_fields()
    {
        var extended = Valid.Replace("""
            "allowed_intended_use": "dev",
            """, """
            "allowed_intended_use": "dev",
            "source_commit": "abc123", "object_ids": ["obj-1"], "parent_image_lineage": "capture-1",
            "label_granularity": "line", "language": "grc", "period": "roman", "material": "papyrus",
            "transcription_profile": "profile-v1", "license_code": "MIT", "license_weight": "cc-by-4.0",
            "evidence_url": "https://example.test/evidence", "unresolved_questions": "none",
            """);
        Assert.Empty(CorpusManifestAudit.Audit(extended, extended: true));

        var issues = CorpusManifestAudit.Audit(Valid, extended: true);
        Assert.Contains(issues, issue => issue.Field == "object_ids" && issue.Severity == CorpusManifestAudit.Severity.Error);
        Assert.Contains(issues, issue => issue.Field == "transcription_profile");
        Assert.Contains(issues, issue => issue.Field == "license_weight");
    }

    [Fact]
    public void Empty_sources_are_an_error()
    {
        var issues = CorpusManifestAudit.Audit("""
            { "schema": "leviathantext.corpus-manifest", "schemaContractVersion": "C0-0.4-draft", "sources": [] }
            """);
        Assert.Contains(issues, i => i.Field == "sources" && i.Severity == CorpusManifestAudit.Severity.Error);
    }
}
