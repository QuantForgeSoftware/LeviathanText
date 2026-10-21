using LeviathanText.Contracts;
using LeviathanText.Recognition;
using Xunit;

namespace LeviathanText.ML.Tests;

/// <summary>Behavioral parity scenarios adapted from Kraken test_codec.py.</summary>
public class LabelCodecTests
{
    private static readonly LabelCodec O2O = new("ab");
    private static readonly LabelCodec M2O = new(new[] { "aaa", "aa", "a", "b" });
    private static readonly LabelCodec O2M = new(new Dictionary<string, IReadOnlyList<int>>
    {
        ["a"] = [10, 11, 12], ["b"] = [12, 45, 80],
    });
    private static readonly LabelCodec M2M = new(new Dictionary<string, IReadOnlyList<int>>
    {
        ["aaa"] = [10, 11, 12], ["aa"] = [9, 9], ["a"] = [11], ["bb"] = [15], ["b"] = [12],
    });

    [Fact]
    public void One_to_one_encodes_and_decodes()
    {
        Assert.Equal([1, 1, 1, 2, 1, 2, 2, 1, 1, 1, 2, 2, 2, 2], O2O.Encode("aaababbaaabbbb"));
        Assert.Equal("aaababbaaabbbb", O2O.Decode(O2O.Encode("aaababbaaabbbb")));
    }

    [Fact]
    public void Many_to_one_greedily_uses_longest_code()
    {
        Assert.Equal([3, 4, 1, 4, 4, 3, 4, 4, 4, 4], M2O.Encode("aaababbaaabbbb"));
        Assert.Equal("aaababbaaabbbb", M2O.Decode(M2O.Encode("aaababbaaabbbb")));
    }

    [Fact]
    public void One_to_many_and_many_to_many_roundtrip()
    {
        Assert.Equal([10, 11, 12, 10, 11, 12, 10, 11, 12, 12, 45, 80, 12, 45, 80],
            O2M.Encode("aaabb"));
        Assert.Equal("aaabb", O2M.Decode(O2M.Encode("aaabb")));
        Assert.Equal("aaabbbaaabbbb", M2M.Decode(M2M.Encode("aaabbbaaabbbb")));
    }

    [Fact]
    public void Unknown_sequences_are_skipped_non_strict_and_rejected_strict()
    {
        Assert.Equal([1, 2], new LabelCodec("ab").Encode("a?b"));
        var strict = new LabelCodec("ab", strict: true);
        var ex = Assert.Throws<LeviathanException>(() => strict.Encode("a?b"));
        Assert.Equal(ErrorCode.UnknownLabel, ex.Error.Code);
        Assert.Throws<LeviathanException>(() => strict.Decode([1, 99]));
    }

    [Fact]
    public void Add_labels_assigns_new_indices_and_keeps_existing_order()
    {
        var extended = O2O.AddLabels("cde");
        Assert.Equal(5, extended.Count);
        Assert.Equal([3, 4, 1, 1, 2, 1, 5], extended.Encode("cdaabae"));

        var list = O2O.AddLabels(new[] { "cd", "e" });
        Assert.Equal(4, list.Count);
        Assert.Equal([3, 1, 1, 2, 1, 4], list.Encode("cdaabae"));
    }

    [Fact]
    public void Merge_removes_unused_labels_and_adds_new_ones()
    {
        var (merged, removed) = O2O.Merge(new LabelCodec("acde"));
        Assert.Equal(new HashSet<int> { 2 }, removed);
        Assert.Equal(new[] { 1 }, merged.CharacterToLabels["a"]);
        Assert.Equal(new[] { 2 }, merged.CharacterToLabels["c"]);
        Assert.Equal(new[] { 3 }, merged.CharacterToLabels["d"]);
        Assert.Equal(new[] { 4 }, merged.CharacterToLabels["e"]);

        var (added, none) = O2O.Merge(new LabelCodec("abcde"));
        Assert.Empty(none);
        Assert.Equal(5, added.Count);
    }

    [Fact]
    public void Invalid_prefix_free_mappings_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => new LabelCodec(new Dictionary<string, IReadOnlyList<int>>
        {
            ["a"] = [1], ["ab"] = [1, 2],
        }));
    }
}
