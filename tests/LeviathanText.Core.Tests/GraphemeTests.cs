using LeviathanText.Core.Unicode;

namespace LeviathanText.Core.Tests;

public class GraphemeTests
{
    private const string DecomposedAnthropos = "\u03B1\u0301\u03BD\u03B8\u03C1\u03C9\u03C0\u03BF\u03C2"; // α+́ ν θ ρ ω π ο ς
    private const string FamilyEmoji = "\uD83D\uDC68\u200D\uD83D\uDC69\u200D\uD83D\uDC67";               // ZWJ sequence
    private const string OverlinedAlpha = "\u03B1\u0305";                                                // α + overline

    [Fact]
    public void Combining_marks_stay_in_their_base_cluster()
    {
        // α+́ is ONE cluster; then ν θ ρ ω π ο ς
        Assert.Equal(8, Graphemes.Count(DecomposedAnthropos));
    }

    [Fact]
    public void Zwj_emoji_sequence_is_one_cluster()
    {
        Assert.Equal(1, Graphemes.Count(FamilyEmoji));
    }

    [Fact]
    public void Overline_is_part_of_the_letter_cluster()
    {
        Assert.Equal(1, Graphemes.Count(OverlinedAlpha));
        Assert.Equal(2, ScalarText.ScalarLength(OverlinedAlpha));
    }

    [Fact]
    public void ClusterStartScalars_end_at_scalar_length()
    {
        var text = "α\u0301β" + "\uD835\uDD38";
        var starts = Graphemes.ClusterStartScalars(text);
        Assert.Equal([0, 2, 3, 4], starts); // α+́ = scalars 0..1, β = 2, 𝔸 = 3, end = 4
    }

    [Fact]
    public void Empty_text_has_no_clusters()
    {
        Assert.Equal(0, Graphemes.Count(""));
        Assert.Equal([0], Graphemes.ClusterStartScalars(""));
    }
}
