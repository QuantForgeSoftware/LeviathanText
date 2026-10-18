using LeviathanText.Contracts;
using LeviathanText.Core.Stages;

namespace LeviathanText.Core.Tests;

public class StageValidationTests
{
    private static readonly Checksum Sum1 = new() { Algorithm = "sha256", Digest = new string('a', 64) };
    private static readonly Checksum Sum2 = new() { Algorithm = "sha256", Digest = new string('b', 64) };
    private static readonly ModelRef Model = new() { PackageId = "rec", Version = "1", IoContractHash = "h1" };

    private static StageRun SucceededRun() => new()
    {
        Id = new EntityId(EntityKind.Job, "s1"),
        Stage = StageName.Recognition,
        ContractVersion = ContractVersion.Current,
        Inputs = [new StageArtifact(new EntityId(EntityKind.Line, "ln1"), Sum1)],
        Outputs = [new StageArtifact(new EntityId(EntityKind.TranscriptionRevision, "rev1"), Sum2)],
        Model = Model,
        ConfigHash = "cfg1",
        Status = StageStatus.Succeeded,
    };

    [Fact]
    public void Unchanged_inputs_and_bindings_stay_valid()
    {
        var run = SucceededRun();
        var current = StageValidation.ExpectationsOf(run);
        Assert.True(StageValidation.IsValid(run, current));
        Assert.Equal(StageStatus.Succeeded, StageValidation.Classify(run, current).Status);
    }

    [Fact]
    public void Changed_input_checksum_invalidates()
    {
        var run = SucceededRun();
        var current = new StageValidation.Expectation(
            [new StageArtifact(new EntityId(EntityKind.Line, "ln1"), Sum2)], // re-derivative
            Model, run.ConfigHash, run.ContractVersion);

        var classified = StageValidation.Classify(run, current);
        Assert.Equal(StageStatus.Invalidated, classified.Status);
        Assert.Contains("input-checksum-changed", classified.Failure!.Details!["reasons"]);
        Assert.Equal(RemedyHint.ReRunStage, classified.Failure.RemedyHint);
    }

    [Fact]
    public void Changed_model_invalidates()
    {
        var run = SucceededRun();
        var otherModel = Model with { Version = "2" };
        var current = new StageValidation.Expectation(run.Inputs, otherModel, run.ConfigHash, run.ContractVersion);
        var classified = StageValidation.Classify(run, current);
        Assert.Equal(StageStatus.Invalidated, classified.Status);
        Assert.Contains("model-changed", classified.Failure!.Details!["reasons"]);
    }

    [Theory]
    [InlineData("config")]
    [InlineData("contract")]
    public void Changed_bindings_invalidate(string what)
    {
        var run = SucceededRun();
        var current = what switch
        {
            "config" => new StageValidation.Expectation(run.Inputs, run.Model, "cfg2", run.ContractVersion),
            _ => new StageValidation.Expectation(run.Inputs, run.Model, run.ConfigHash, "C0-0.9"),
        };
        var classified = StageValidation.Classify(run, current);
        Assert.Equal(StageStatus.Invalidated, classified.Status);
    }

    [Fact]
    public void Manual_stage_runs_with_null_model_stay_valid()
    {
        var run = SucceededRun() with { Model = null, LayoutSourceUsed = LayoutSource.Manual };
        var current = new StageValidation.Expectation(run.Inputs, null, run.ConfigHash, run.ContractVersion);
        Assert.True(StageValidation.IsValid(run, current));
    }

    [Fact]
    public void Null_model_run_invalidates_when_model_appears()
    {
        var run = SucceededRun() with { Model = null };
        var current = new StageValidation.Expectation(run.Inputs, Model, run.ConfigHash, run.ContractVersion);
        Assert.False(StageValidation.IsValid(run, current));
    }

    [Fact]
    public void Input_set_order_is_irrelevant()
    {
        var run = SucceededRun() with
        {
            Inputs =
            [
                new StageArtifact(new EntityId(EntityKind.Line, "ln1"), Sum1),
                new StageArtifact(new EntityId(EntityKind.Line, "ln2"), Sum2),
            ],
        };
        var current = new StageValidation.Expectation(
        [
            new StageArtifact(new EntityId(EntityKind.Line, "ln2"), Sum2),
            new StageArtifact(new EntityId(EntityKind.Line, "ln1"), Sum1),
        ], run.Model, run.ConfigHash, run.ContractVersion);
        Assert.True(StageValidation.IsValid(run, current));
    }
}
