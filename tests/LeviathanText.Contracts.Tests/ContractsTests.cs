using LeviathanText.Contracts;

namespace LeviathanText.Contracts.Tests;

public class IdentityTests
{
    [Theory]
    [InlineData(EntityKind.Line, "ln")]
    [InlineData(EntityKind.TranscriptionRevision, "rev")]
    [InlineData(EntityKind.PhysicalObject, "obj")]
    [InlineData(EntityKind.Surface, "surf")]
    [InlineData(EntityKind.Capture, "cap")]
    public void EntityId_roundtrips(EntityKind kind, string expectedPrefix)
    {
        var id = new EntityId(kind, "Ab1");
        Assert.Equal($"{expectedPrefix}-Ab1", id.ToString());
        var parsed = EntityId.Parse(id.ToString());
        Assert.Equal(kind, parsed.Kind);
        Assert.Equal("Ab1", parsed.Suffix);
    }

    [Fact]
    public void Parse_rejects_unknown_prefix()
    {
        Assert.Throws<FormatException>(() => EntityId.Parse("zz-1"));
    }

    [Fact]
    public void Parse_rejects_mismatched_expected_kind()
    {
        Assert.Throws<FormatException>(() => EntityId.Parse("ln-1", EntityKind.Capture));
    }

    [Fact]
    public void Constructor_rejects_invalid_suffix()
    {
        Assert.Throws<ArgumentException>(() => new EntityId(EntityKind.Line, "bad suffix!"));
        Assert.Throws<ArgumentException>(() => new EntityId(EntityKind.Line, ""));
    }

    [Fact]
    public void Distinct_kinds_are_never_equal()
    {
        Assert.NotEqual(new EntityId(EntityKind.Line, "a1"), new EntityId(EntityKind.Region, "a1"));
    }
}

public class ScalarRangeTests
{
    [Fact]
    public void Negative_start_throws() => Assert.Throws<ArgumentOutOfRangeException>(() => new ScalarRange(-1, 0));

    [Fact]
    public void End_before_start_throws() => Assert.Throws<ArgumentOutOfRangeException>(() => new ScalarRange(5, 3));

    [Fact]
    public void Empty_range_is_valid() => Assert.True(new ScalarRange(3, 3).IsEmpty);

    [Fact]
    public void Half_open_range_renders_bracket_notation()
    {
        Assert.Equal("[2,7)", new ScalarRange(2, 7).ToString());
    }
}

public class ConfidenceTests
{
    [Fact]
    public void Of_accepts_bounds()
    {
        Assert.Equal(0.5, Confidence.Of(0.5, ConfidenceScale.Calibrated).Value);
        Confidence.Of(0, ConfidenceScale.RawModelProbability);
        Confidence.Of(1, ConfidenceScale.AnnotatorUncertainty);
    }

    [Fact]
    public void Of_rejects_out_of_bounds() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Confidence.Of(1.5, ConfidenceScale.RawModelProbability));

    [Fact]
    public void None_is_absent_not_zero()
    {
        Assert.True(Confidence.None.IsNone);
        Assert.False(Confidence.Of(0, ConfidenceScale.RawModelProbability).IsNone);
        Assert.Equal("none", Confidence.None.ToString());
    }

    [Fact]
    public void Scale_is_part_of_identity()
    {
        Assert.NotEqual(Confidence.Of(0.5, ConfidenceScale.RawModelProbability), Confidence.Of(0.5, ConfidenceScale.Calibrated));
    }
}

public class ContractVersionTests
{
    [Fact]
    public void Compiled_types_target_the_published_draft()
    {
        Assert.Equal("C0-0.4-draft", ContractVersion.Current);
    }
}
