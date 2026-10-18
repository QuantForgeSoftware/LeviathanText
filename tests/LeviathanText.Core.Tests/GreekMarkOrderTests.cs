using LeviathanText.Core.Unicode;

namespace LeviathanText.Core.Tests;

public class GreekMarkOrderTests
{
    private const string BreathingThenAcute = "α\u0313\u0301";   // profile order
    private const string AcuteThenBreathing = "α\u0301\u0313";   // reversed input

    [Fact]
    public void Reversed_mark_order_is_reordered_to_profile_order()
    {
        Assert.Equal(BreathingThenAcute, GreekMarkOrder.Reorder(AcuteThenBreathing));
    }

    [Fact]
    public void Profile_order_is_stable()
    {
        Assert.Equal(BreathingThenAcute, GreekMarkOrder.Reorder(BreathingThenAcute));
    }

    [Fact]
    public void Reorder_is_idempotent()
    {
        var once = GreekMarkOrder.Reorder(AcuteThenBreathing);
        Assert.Equal(once, GreekMarkOrder.Reorder(once));
    }

    [Fact]
    public void Ranks_follow_breathing_diaeresis_accent_subscript()
    {
        // Feed every mark in reverse rank order; expect breathing, diaeresis, acute, subscript.
        var scrambled = "α\u0345\u0301\u0308\u0313";
        var ordered = GreekMarkOrder.Reorder(scrambled);
        Assert.Equal("α\u0313\u0308\u0301\u0345", ordered);
    }

    [Fact]
    public void Unranked_marks_keep_relative_order_after_ranked()
    {
        // U+0304 macron is not in the rank table; it must not displace ranked marks.
        var input = "α\u0304\u0301";
        var reordered = GreekMarkOrder.Reorder(input);
        Assert.Equal("α\u0301\u0304", reordered);
    }

    [Fact]
    public void Text_without_marks_is_unchanged()
    {
        Assert.Equal("ΕΑΝΤΙϹΑΛΛΟΓΡΑΦΗ", GreekMarkOrder.Reorder("ΕΑΝΤΙϹΑΛΛΟΓΡΑΦΗ"));
        Assert.Equal("", GreekMarkOrder.Reorder(""));
    }

    [Fact]
    public void Base_prefix_is_untouched()
    {
        // Multi-char clusters (ZWJ sequences) pass through without reordering bases.
        var family = "\uD83D\uDC68\u200D\uD83D\uDC69";
        Assert.Equal(family, GreekMarkOrder.Reorder(family));
    }
}
