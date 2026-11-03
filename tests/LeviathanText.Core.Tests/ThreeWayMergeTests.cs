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
    public void Disjoint_random_edits_always_merge_to_both_edits()
    {
        var random = new Random(42);
        for (int iteration = 0; iteration < 100; iteration++)
        {
            var baseText = RandomUniqueText(random, random.Next(12, 40));
            int aStart = random.Next(0, baseText.Length);
            int aEnd = random.Next(aStart, baseText.Length + 1);
            int bStart = random.Next(aEnd, baseText.Length + 1);
            int bEnd = random.Next(bStart, baseText.Length + 1);
            var aText = RandomReplacement(random, baseText, random.Next(0, 5));
            var bText = RandomReplacement(random, baseText, random.Next(0, 5));

            var local = baseText[..aStart] + aText + baseText[aEnd..];
            var remote = baseText[..bStart] + bText + baseText[bEnd..];
            var expected = baseText[..aStart] + aText + baseText[aEnd..bStart] + bText + baseText[bEnd..];

            var merged = ThreeWayMerge.Merge(baseText, local, remote);

            Assert.True(merged.Success, $"iteration {iteration}: {baseText} / {local} / {remote}");
            Assert.Equal(expected, merged.Text);
        }
    }

    private static string RandomUniqueText(Random random, int length)
    {
        const string alphabet = "αβγδεζηθικλμνξοπρστυφχψωΑΒΓΔΕΖΗΘΙΚΛΜΝΞΟΠΡΣΤΥΦΧΨΩ0123456789";
        return new string(alphabet.OrderBy(_ => random.Next()).Take(length).ToArray());
    }

    private static string RandomReplacement(Random random, string baseText, int length)
    {
        const string alphabet = "αβγδεζηθικλμνξοπρστυφχψωΑΒΓΔΕΖΗΘΙΚΛΜΝΞΟΠΡΣΤΥΦΧΨΩ0123456789";
        var available = alphabet.Where(c => !baseText.Contains(c)).ToArray();
        return new string(Enumerable.Range(0, length)
            .Select(_ => available[random.Next(available.Length)]).ToArray());
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
