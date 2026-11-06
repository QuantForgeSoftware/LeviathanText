using LeviathanText.Core.Text;

namespace LeviathanText.Core.Tests;

public sealed class AnchoredTextMergeTests
{
    [Theory]
    [InlineData("ααβ", true, "αβ")]
    [InlineData("βαα", false, "ααα")]
    public void Exact_local_source_span_is_preserved_in_repeated_text(string remote, bool success, string expected)
    {
        var result = ThreeWayMerge.MergeAnchoredReplacement("ααα", new(0, 2, "α"), remote);
        Assert.Equal(success, result.Success);
        Assert.Equal(expected, result.Text);
    }

    [Theory]
    [InlineData("αα")]
    [InlineData("αααα")]
    public void Ambiguous_remote_deletion_or_insertion_is_refused(string remote)
    {
        var result = ThreeWayMerge.MergeAnchoredReplacement("ααα", new(0, 1, "Ω"), remote);
        Assert.False(result.Success);
        Assert.Equal("ααα", result.Text);
    }

    [Fact]
    public void Equal_composed_text_does_not_prove_ambiguous_remote_source_alignment()
    {
        Assert.False(ThreeWayMerge.MergeAnchoredReplacement("ααα", new(0, 2, "α"), "αα").Success);
    }

    [Fact]
    public void Repeated_source_with_no_remote_change_uses_direct_anchored_replacement()
    {
        var result = ThreeWayMerge.MergeAnchoredReplacement("ααα", new(0, 2, "α"), "ααα");
        Assert.True(result.Success);
        Assert.Equal("αα", result.Text);
    }

    [Theory]
    [InlineData("abXcd", "AbXcd")]
    [InlineData("acd", "Acd")]
    [InlineData("aXcd", "AXcd")]
    public void Uniquely_mapped_remote_edit_outside_local_span_is_preserved(string remote, string expected)
    {
        var result = ThreeWayMerge.MergeAnchoredReplacement("abcd", new(0, 1, "A"), remote);
        Assert.True(result.Success);
        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public void Remote_insertion_inside_local_source_span_conflicts()
    {
        Assert.False(ThreeWayMerge.MergeAnchoredReplacement("abcd", new(1, 3, "Q"), "abXcd").Success);
    }

    [Fact]
    public void Even_unchanged_local_reading_keeps_its_explicit_span_intent()
    {
        Assert.False(ThreeWayMerge.MergeAnchoredReplacement("abc", new(0, 2, "ab"), "aXc").Success);
    }

    [Fact]
    public void Identical_uniquely_aligned_replacements_are_applied_once()
    {
        var result = ThreeWayMerge.MergeAnchoredReplacement("abc", new(0, 1, "A"), "Abc");
        Assert.True(result.Success);
        Assert.Equal("Abc", result.Text);
    }

    [Fact]
    public void Equal_uniquely_aligned_result_can_keep_unchanged_characters_inside_the_declared_span()
    {
        var result = ThreeWayMerge.MergeAnchoredReplacement("abc", new(0, 2, "aB"), "aBc");
        Assert.True(result.Success);
        Assert.Equal("aBc", result.Text);
    }

    [Fact]
    public void All_optimal_paths_including_skip_ties_are_counted()
    {
        // Two substitutions and a deletion/insertion pair are both optimal.
        Assert.False(ThreeWayMerge.MergeAnchoredReplacement("ab", new(0, 1, "X"), "ba").Success);
    }

    [Fact]
    public void Anchors_use_astral_and_combining_scalars_without_normalization()
    {
        var result = ThreeWayMerge.MergeAnchoredReplacement("α𐅀β\u0301γ", new(1, 2, "Ω"), "α𐅀β\u0301X");
        Assert.True(result.Success);
        Assert.Equal("αΩβ\u0301X", result.Text);
    }

    [Fact]
    public void Nontrivial_remote_alignment_above_cell_budget_is_refused()
    {
        var source = new string('a', 2001);
        var result = ThreeWayMerge.MergeAnchoredReplacement(source, new(0, 1, "X"), new string('a', 2000));
        Assert.False(result.Success);
        Assert.Equal(source, result.Text);
    }

    [Fact]
    public void Invalid_anchor_is_rejected_before_mapping()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ThreeWayMerge.MergeAnchoredReplacement("abc", new(0, 4, "X"), "abc"));
    }
}
