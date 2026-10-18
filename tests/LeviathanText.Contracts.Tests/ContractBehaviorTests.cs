using LeviathanText.Contracts;

namespace LeviathanText.Contracts.Tests;

public class ErrorTests
{
    [Fact]
    public void Error_toString_contains_code_scope_and_remedy()
    {
        var error = new LeviathanError
        {
            Code = ErrorCode.FrameUnregistered,
            Scope = "surf-1",
            RemedyHint = RemedyHint.RegisterCaptures,
        };
        var text = error.ToString();
        Assert.Contains("FrameUnregistered", text);
        Assert.Contains("surf-1", text);
        Assert.Contains("RegisterCaptures", text);
    }

    [Fact]
    public void Exception_carries_the_contract_error()
    {
        var error = new LeviathanError { Code = ErrorCode.RevisionConflict, Scope = "ln-1", Message = "stale" };
        var ex = new LeviathanException(error);
        Assert.Same(error, ex.Error);
        Assert.Equal("stale", ex.Message);
    }

    [Fact]
    public void Error_codes_come_from_the_closed_vocabulary()
    {
        // Compile-time vocabulary check: all codes used in tests must be defined members.
        var defined = Enum.GetValues<ErrorCode>();
        Assert.Contains(ErrorCode.StaleAnnotationAnchor, defined);
        Assert.Contains(ErrorCode.LossyExportRequiredReport, defined);
        Assert.Contains(ErrorCode.UnknownLabel, defined);
    }
}

public class StageRunTests
{
    private static readonly Checksum Checksum = new() { Algorithm = "sha256", Digest = new string('a', 64) };

    [Fact]
    public void Manual_layout_stage_records_its_source_and_absent_confidence()
    {
        var stage = new StageRun
        {
            Id = new EntityId(EntityKind.Job, "s1"),
            Stage = StageName.Layout,
            ContractVersion = ContractVersion.Current,
            Inputs = [(new EntityId(EntityKind.Derivative, "d1"), Checksum)],
            Outputs = [(new EntityId(EntityKind.Region, "r1"), Checksum)],
            Model = null, // manual: no model
            ConfigHash = "cfg",
            Status = StageStatus.Succeeded,
            LayoutSourceUsed = LayoutSource.Manual,
        };
        Assert.Equal(LayoutSource.Manual, stage.LayoutSourceUsed);
        Assert.Null(stage.Model);
        // manual layout must not fabricate a confidence value (domain-and-pipeline.md §4)
        Assert.True(Confidence.None.IsNone);
    }

    [Fact]
    public void Failed_stage_carries_closed_vocabulary_failure()
    {
        var stage = new StageRun
        {
            Id = new EntityId(EntityKind.Job, "s2"),
            Stage = StageName.Recognition,
            Inputs = [],
            Outputs = [],
            Model = new ModelRef { PackageId = "pkg", Version = "1", IoContractHash = "h" },
            ConfigHash = "cfg",
            Status = StageStatus.Failed,
            Failure = new LeviathanError { Code = ErrorCode.ModelIncompatible, Scope = "mdl-pkg" },
        };
        Assert.Equal(StageStatus.Failed, stage.Status);
        Assert.Equal(ErrorCode.ModelIncompatible, stage.Failure!.Code);
    }

    [Fact]
    public void Checksum_renders_algorithm_and_digest()
    {
        Assert.Equal("sha256:" + new string('a', 64), Checksum.ToString());
    }
}

public class ManifestVocabularyTests
{
    [Fact]
    public void LabelGranularity_distinguishes_character_boxes_from_line_labels()
    {
        Assert.NotEqual(LabelGranularity.CharacterBox, LabelGranularity.Line);
        Assert.NotEqual(LabelGranularity.Glyph, LabelGranularity.Line);
        Assert.NotEqual(LabelGranularity.None, LabelGranularity.Region);
    }

    [Fact]
    public void PairingStatus_keeps_unsynced_files_distinct_from_paired()
    {
        Assert.NotEqual(PairingStatus.Paired, PairingStatus.UnpairedUnsynced);
        Assert.NotEqual(PairingStatus.ImageOnly, PairingStatus.UnpairedUnsynced);
    }

    [Fact]
    public void AnnotationCompleteness_treats_unknown_as_unknown()
    {
        Assert.NotEqual(AnnotationCompleteness.Unknown, AnnotationCompleteness.Complete);
        Assert.NotEqual(AnnotationCompleteness.Unknown, AnnotationCompleteness.Partial);
    }

    [Fact]
    public void ProfileRef_carries_version_and_preproject_flag()
    {
        var profile = new TranscriptionProfileRef { ProfileId = "greek-diplomatic", Version = "1.0-draft", PreProject = true };
        Assert.True(profile.PreProject);
    }
}
