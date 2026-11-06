using LeviathanText.Contracts;
using LeviathanText.Recognition;
using Xunit;

namespace LeviathanText.ML.Tests;

/// <summary>Pure mapping/Unicode controls only; no models, tensors, fitting or inference.</summary>
public sealed class LabelCodecInvariantTests
{
    [Fact]
    public void Greek_musical_scalar_is_one_unit_in_string_constructor_and_extension()
    {
        const string musical = "\U0001d200";
        var codec = new LabelCodec(musical, strict: true);
        Assert.Equal(1, codec.Count);
        Assert.Equal([1], codec.Encode(musical));
        Assert.Equal(musical, codec.Decode([1]));
        var extended = new LabelCodec("α").AddLabels(musical);
        Assert.Equal(2, extended.Count);
        Assert.Equal([1, 2], extended.Encode("α" + musical));
        Assert.Equal("α" + musical, extended.Decode([1, 2]));
    }

    [Fact]
    public void Empty_text_key_is_rejected_without_attempting_nonprogress_encoding()
    {
        Assert.Throws<ArgumentException>(() => Codec(("", [1])));
    }

    [Fact]
    public void Empty_unit_sequence_is_an_argument_error_not_an_indexing_failure()
    {
        Assert.Throws<ArgumentException>(() => Codec(("α", [])));
    }

    [Fact]
    public void Caller_mapping_and_unit_lists_cannot_change_the_constructed_codec()
    {
        var units = new List<int> { 1 };
        var mapping = new Dictionary<string, IReadOnlyList<int>> { ["α"] = units };
        var codec = new LabelCodec(mapping, strict: true);
        units[0] = 9;
        mapping["α"] = [11];
        mapping["β"] = [12];
        Assert.Equal(1, codec.Count);
        Assert.Equal([1], codec.Encode("α"));
        Assert.Equal("α", codec.Decode([1]));
        Assert.Throws<LeviathanException>(() => codec.Decode([9]));
    }

    [Fact]
    public void Public_mapping_and_nested_unit_collection_refuse_mutation()
    {
        var codec = Codec(("α", [1]));
        var mapping = Assert.IsAssignableFrom<IDictionary<string, IReadOnlyList<int>>>(codec.CharacterToLabels);
        Assert.Throws<NotSupportedException>(() => mapping["β"] = [2]);
        var units = Assert.IsAssignableFrom<IList<int>>(codec.CharacterToLabels["α"]);
        Assert.Throws<NotSupportedException>(() => units[0] = 8);
        Assert.Equal([1], codec.Encode("α"));
        Assert.Equal("α", codec.Decode([1]));
    }

    [Fact]
    public void Non_strict_unknown_scalar_does_not_match_its_low_surrogate_half()
    {
        var codec = Codec(("α", [1]), ("\ude00", [2]));
        Assert.Equal([1], codec.Encode("\U0001f600α"));
        Assert.Equal([2], codec.Encode("\ude00")); // Existing isolated-half preservation policy.
    }

    [Fact]
    public void Explicit_isolated_surrogate_keys_do_not_split_a_valid_scalar()
    {
        var codec = Codec(("\ud83d", [1]), ("\ude00", [2]), ("α", [3]));
        Assert.Equal([3], codec.Encode("\U0001f600α"));
        Assert.Equal([1, 3, 2], codec.Encode("\ud83dα\ude00"));
    }

    [Fact]
    public void Newly_shared_incoming_units_get_one_identity_across_sequences()
    {
        var old = Codec(("α", [1]));
        var incoming = Codec(("α", [10]), ("β", [20, 30]), ("γ", [30, 40]));
        var (merged, removed) = old.Merge(incoming);
        Assert.Empty(removed);
        Assert.Equal([1], merged.CharacterToLabels["α"]);
        Assert.Equal([2, 3], merged.CharacterToLabels["β"]);
        Assert.Equal([3, 4], merged.CharacterToLabels["γ"]);
        Assert.Equal(4, merged.MaxLabel);
        Assert.Equal("αβγ", merged.Decode(merged.Encode("αβγ")));
    }

    [Fact]
    public void New_incoming_numeric_collision_does_not_reuse_retained_old_identity()
    {
        var (merged, _) = Codec(("α", [10])).Merge(Codec(("α", [99]), ("β", [10])));
        Assert.Equal([1], merged.CharacterToLabels["α"]);
        Assert.Equal([2], merged.CharacterToLabels["β"]);
        Assert.Equal("αβ", merged.Decode([1, 2]));
    }

    [Fact]
    public void Common_sequences_align_shared_units_and_only_exclusive_old_units_are_removed()
    {
        var old = Codec(("α", [10, 20]), ("β", [20, 30]), ("δ", [40]));
        var incoming = Codec(("α", [7, 8]), ("γ", [8, 9]));
        var (merged, removed) = old.Merge(incoming);
        Assert.Equal(new HashSet<int> { 30, 40 }, removed);
        Assert.Equal([1, 2], merged.CharacterToLabels["α"]);
        Assert.Equal([2, 3], merged.CharacterToLabels["γ"]);
        Assert.Equal([10, 20], old.CharacterToLabels["α"]);
        Assert.Equal([8, 9], incoming.CharacterToLabels["γ"]);
    }

    [Fact]
    public void Incompatible_common_sequence_lengths_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => Codec(("α", [1])).Merge(Codec(("α", [10, 20]))));
    }

    [Fact]
    public void One_incoming_unit_cannot_collapse_two_retained_old_units()
    {
        var old = Codec(("α", [1, 2]), ("β", [3, 4]));
        var incoming = Codec(("α", [10, 20]), ("β", [10, 30]));
        Assert.Throws<ArgumentException>(() => old.Merge(incoming));
    }

    [Fact]
    public void One_retained_old_unit_cannot_split_into_two_incoming_units()
    {
        var old = Codec(("α", [1, 2]), ("β", [1, 3]));
        var incoming = Codec(("α", [10, 20]), ("β", [11, 30]));
        Assert.Throws<ArgumentException>(() => old.Merge(incoming));
    }

    [Fact]
    public void Merge_assigns_sorted_fresh_units_independently_of_dictionary_enumeration()
    {
        var old = Codec(("α", [1]));
        var (merged, _) = old.Merge(Codec(("α", [10]), ("γ", [99]), ("β", [30])));
        Assert.Equal([2], merged.CharacterToLabels["β"]);
        Assert.Equal([3], merged.CharacterToLabels["γ"]);
        Assert.Equal(3, merged.MaxLabel);
    }

    [Fact]
    public void Compatible_nonpositive_unit_ids_remain_supported_and_old_strict_policy_is_preserved()
    {
        var old = new LabelCodec(new Dictionary<string, IReadOnlyList<int>> { ["α"] = [-2, 0], ["β"] = [0, 10] }, strict: true);
        var (merged, _) = old.Merge(Codec(("α", [99, 101]), ("β", [101, 102])));
        Assert.True(merged.Strict);
        Assert.Equal([1, 2], merged.CharacterToLabels["α"]);
        Assert.Equal([2, 3], merged.CharacterToLabels["β"]);
        Assert.Equal("αβ", merged.Decode(merged.Encode("αβ")));
        Assert.Throws<LeviathanException>(() => merged.Encode("?"));
    }

    private static LabelCodec Codec(params (string Text, int[] Units)[] entries) => new(
        entries.ToDictionary(entry => entry.Text, entry => (IReadOnlyList<int>)entry.Units));
}
