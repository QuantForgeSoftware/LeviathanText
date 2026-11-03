using LeviathanText.Core.Text;

namespace LeviathanText.Core.Tests;

public class ThreeWayMergeTests
{
    [Fact]
    public void Non_overlapping_edits_are_combined()
    {
        var result = ThreeWayMerge.Merge("αβγδε", "Αβγδε", "αβγδΕ");

        Assert.True(result.Success);
        Assert.Equal("ΑβγδΕ", result.Text);
    }

    [Fact]
    public void Identical_edits_are_accepted_once()
    {
        var result = ThreeWayMerge.Merge("αβγ", "αΒγ", "αΒγ");

        Assert.True(result.Success);
        Assert.Equal("αΒγ", result.Text);
    }

    [Fact]
    public void Overlapping_edits_report_a_conflict_instead_of_choosing()
    {
        var result = ThreeWayMerge.Merge("αβγ", "αΒγ", "αXγ");

        Assert.False(result.Success);
        Assert.Single(result.Conflicts);
        Assert.Equal("αβγ", result.Text);
        Assert.Contains("Β", result.Conflicts[0].Local);
        Assert.Contains("X", result.Conflicts[0].Remote);
    }

    [Fact]
    public void Insertions_at_the_same_scalar_boundary_conflict_when_they_differ()
    {
        var result = ThreeWayMerge.Merge("αβ", "αXβ", "αYβ");

        Assert.False(result.Success);
        Assert.Single(result.Conflicts);
    }

    [Fact]
    public void Unchanged_side_takes_the_other_side_verbatim()
    {
        Assert.Equal("αβγδ", ThreeWayMerge.Merge("αβ", "αβ", "αβγδ").Text);
        Assert.Equal("αβγδ", ThreeWayMerge.Merge("αβ", "αβγδ", "αβ").Text);
    }

    [Fact]
    public void Multiple_disjoint_hunks_merge_and_offsets_remain_scalar_based()
    {
        var baseText = "🙂αβγδε🙂";
        var local = "🙂Αβγδε🙂";
        var remote = "🙂αβγδΕ🙂";

        var result = ThreeWayMerge.Merge(baseText, local, remote);

        Assert.True(result.Success);
        Assert.Equal("🙂ΑβγδΕ🙂", result.Text);
    }

    [Fact]
    public void Adjacent_boundary_edits_do_not_count_as_overlap()
    {
        // Replacing [0,1) and inserting at 1 can both be applied.
        var result = ThreeWayMerge.Merge("αβ", "Αβ", "αXβ");

        Assert.True(result.Success);
        Assert.Equal("ΑXβ", result.Text);
    }
}
