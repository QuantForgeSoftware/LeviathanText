using System.Text.Json;
using LeviathanText.Contracts;
using LeviathanText.Contracts.Serialization;

namespace LeviathanText.Contracts.Tests;

public class SerializationTests
{
    private static readonly AgentRef Author = new() { Id = "annotator-7", IsModel = false };

    private static TextRevision SampleRevision() => new()
    {
        Id = new EntityId(EntityKind.TranscriptionRevision, "r1"),
        ScopeId = new EntityId(EntityKind.Line, "line17"),
        Layer = TextLayer.L1Diplomatic,
        ParentRevisionId = new EntityId(EntityKind.TranscriptionRevision, "r0"),
        Author = Author,
        CreatedAt = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero),
        Text = "Κ\u0305ϲ̅ ὑμῶν",
    };

    [Fact]
    public void TextRevision_roundtrips_exactly()
    {
        var original = SampleRevision();
        var json = ContractJson.ToJson(original);
        var parsed = ContractJson.FromJson<TextRevision>(json);
        Assert.Equal(original, parsed);
    }

    [Fact]
    public void EntityId_serializes_as_string()
    {
        var json = ContractJson.ToJson(SampleRevision().ScopeId);
        Assert.Equal("\"ln-line17\"", json);
    }

    [Fact]
    public void ScalarRange_serializes_as_half_open_object()
    {
        var json = ContractJson.ToJson(new ScalarRange(2, 7));
        Assert.Equal("""{"start":2,"end":7}""", json);
        Assert.Equal(new ScalarRange(2, 7), ContractJson.FromJson<ScalarRange>(json));
    }

    [Fact]
    public void ScalarRange_rejects_unknown_members()
    {
        Assert.Throws<JsonException>(() => ContractJson.FromJson<ScalarRange>("""{"start":1,"end":2,"middle":3}"""));
    }

    [Fact]
    public void Confidence_serializes_none_as_string()
    {
        Assert.Equal("\"none\"", ContractJson.ToJson(Confidence.None));
        var json = ContractJson.ToJson(Confidence.Of(0.75, ConfidenceScale.Calibrated));
        Assert.Equal("""{"value":0.75,"scale":"calibrated"}""", json);
        Assert.Equal(Confidence.Of(0.75, ConfidenceScale.Calibrated), ContractJson.FromJson<Confidence>(json));
        Assert.Equal(Confidence.None, ContractJson.FromJson<Confidence>("\"none\""));
    }

    [Fact]
    public void Confidence_rejects_invalid_string()
    {
        Assert.Throws<JsonException>(() => ContractJson.FromJson<Confidence>("\"uncertain\""));
    }

    [Fact]
    public void Annotation_roundtrips_with_optional_fields()
    {
        var annotation = new Annotation
        {
            Id = new EntityId(EntityKind.Annotation, "a9"),
            Kind = AnnotationKind.Lost,
            AnchoredRevisionId = new EntityId(EntityKind.TranscriptionRevision, "r1"),
            Range = new ScalarRange(0, 0),
            GapExtent = GapExtent.Estimated,
            EstimatedScalarCount = 5,
        };
        var parsed = ContractJson.FromJson<Annotation>(ContractJson.ToJson(annotation));
        Assert.Equal(annotation, parsed);
    }

    [Fact]
    public void AlternativeReading_annotation_roundtrips()
    {
        var annotation = new Annotation
        {
            Id = new EntityId(EntityKind.Annotation, "a2"),
            Kind = AnnotationKind.AlternativeReading,
            AnchoredRevisionId = new EntityId(EntityKind.TranscriptionRevision, "r1"),
            Range = new ScalarRange(7, 8),
            AlternativeText = "ρ",
            HandLabel = "unattributed",
        };
        var parsed = ContractJson.FromJson<Annotation>(ContractJson.ToJson(annotation));
        Assert.Equal(annotation, parsed);
    }

    [Fact]
    public void Unknown_annotation_kind_is_a_hard_error()
    {
        var annotation = new Annotation
        {
            Id = new EntityId(EntityKind.Annotation, "a1"),
            Kind = AnnotationKind.Unclear,
            AnchoredRevisionId = new EntityId(EntityKind.TranscriptionRevision, "r1"),
            Range = new ScalarRange(0, 1),
        };
        var json = ContractJson.ToJson(annotation).Replace("\"unclear\"", "\"bogus\"");
        Assert.Throws<JsonException>(() => ContractJson.FromJson<Annotation>(json));
    }

    [Fact]
    public void Integer_enum_values_are_rejected()
    {
        Assert.Throws<JsonException>(() => ContractJson.FromJson<Annotation>(
            """{"id":"ann-a1","kind":3,"anchoredRevisionId":"rev-r1","range":{"start":0,"end":1}}"""));
    }

    [Fact]
    public void LeviathanError_roundtrips_and_uses_camel_case_codes()
    {
        var error = new LeviathanError
        {
            Code = ErrorCode.StaleAnnotationAnchor,
            Scope = "ann-a9",
            Details = new Dictionary<string, string> { ["reason"] = "composed cluster" },
            RemedyHint = RemedyHint.RebaseRevision,
            Message = "rebase required",
        };
        var json = ContractJson.ToJson(error);
        Assert.Contains("\"staleAnnotationAnchor\"", json);
        Assert.Contains("\"rebaseRevision\"", json);
        Assert.Equal(error, ContractJson.FromJson<LeviathanError>(json));
    }

    [Fact]
    public void StageRun_roundtrips_including_stage_artifacts()
    {
        var checksum = new Checksum { Algorithm = "sha256", Digest = new string('a', 64) };
        var stage = new StageRun
        {
            Id = new EntityId(EntityKind.Job, "s1"),
            Stage = StageName.Layout,
            ContractVersion = ContractVersion.Current,
            Inputs = [new StageArtifact(new EntityId(EntityKind.Derivative, "d1"), checksum)],
            Outputs = [new StageArtifact(new EntityId(EntityKind.Region, "r1"), checksum)],
            Model = new ModelRef { PackageId = "pkg", Version = "1", IoContractHash = "h" },
            ConfigHash = "cfg",
            Status = StageStatus.Succeeded,
            LayoutSourceUsed = LayoutSource.Automatic,
            WallTime = TimeSpan.FromSeconds(3),
        };
        var parsed = ContractJson.FromJson<StageRun>(ContractJson.ToJson(stage));

        Assert.Equal(stage.Id, parsed.Id);
        Assert.Equal(stage.Stage, parsed.Stage);
        Assert.Equal(stage.ContractVersion, parsed.ContractVersion);
        Assert.Equal(stage.Inputs, parsed.Inputs);
        Assert.Equal(stage.Outputs, parsed.Outputs);
        Assert.Equal(stage.Model, parsed.Model);
        Assert.Equal(stage.Status, parsed.Status);
        Assert.Equal(stage.LayoutSourceUsed, parsed.LayoutSourceUsed);
        Assert.Equal(stage.WallTime, parsed.WallTime);
    }

    [Fact]
    public void Null_parent_revision_survives_roundtrip()
    {
        var revision = SampleRevision() with { ParentRevisionId = null };
        var parsed = ContractJson.FromJson<TextRevision>(ContractJson.ToJson(revision));
        Assert.Null(parsed.ParentRevisionId);
        Assert.Equal(revision, parsed);
    }
}
