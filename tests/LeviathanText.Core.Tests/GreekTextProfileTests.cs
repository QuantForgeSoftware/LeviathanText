using LeviathanText.Core.Unicode;

namespace LeviathanText.Core.Tests;

public class GreekTextProfileTests
{
    [Fact]
    public void Polytonic_diplomatic_text_is_clean()
    {
        var report = GreekTextProfile.Inspect("α\u0301νθρωπος· καὶ ἐγένετο φῶς ͵αφλ᾽");
        Assert.True(report.IsClean);
        Assert.True(report.Counts[GreekTextProfile.Category.GreekLetter] > 10);
        Assert.Equal(1, report.Counts[GreekTextProfile.Category.Punctuation]);
    }

    [Fact]
    public void Latin_letters_are_flagged_but_not_rewritten()
    {
        var report = GreekTextProfile.Inspect("αβγ abc");
        Assert.False(report.IsClean);
        Assert.Contains(report.Issues, i => i.Category == GreekTextProfile.Category.LatinLetter && i.Scalar == "a");
        Assert.Equal(3, report.Counts[GreekTextProfile.Category.LatinLetter]);
    }

    [Fact]
    public void Leading_combining_mark_is_an_unpaired_mark()
    {
        var report = GreekTextProfile.Inspect("\u0301α");
        Assert.Contains(report.Issues, i => i.Category == GreekTextProfile.Category.UnpairedMark && i.ScalarOffset == 0);
    }

    [Fact]
    public void Unknown_symbols_are_reported_with_scalar_offsets()
    {
        var report = GreekTextProfile.Inspect("αβ😀");
        Assert.Contains(report.Issues, i => i.Category == GreekTextProfile.Category.Other && i.ScalarOffset == 2);
        Assert.Equal(3, report.ScalarCount);
    }

    [Fact]
    public void Editorial_brackets_and_numeral_signs_are_allowed()
    {
        var report = GreekTextProfile.Inspect("⟦α⟧ ͵αφλ᾽");
        Assert.True(report.IsClean);
        Assert.Equal(1, report.Counts.GetValueOrDefault(GreekTextProfile.Category.NumeralSign));
        Assert.Equal(2, report.Counts.GetValueOrDefault(GreekTextProfile.Category.EditorialBracket));
    }
}
