using LeviathanText.Recognition;
using Xunit;

namespace LeviathanText.ML.Tests;

public class TokenizerCoverageTests
{
    [Fact]
    public void Greek_text_with_full_coverage_routes_to_greek()
    {
        var report = TokenizerCoverage.Inspect(new LabelSet(["α", "β"]), "αβ");
        Assert.Empty(report.MissingGraphemes);
        Assert.False(report.MixedScript);
        Assert.Equal("greek", report.SuggestedRoute);
        Assert.Equal(2, report.Counts[TextScript.Greek]);
    }

    [Fact]
    public void Mixed_greek_and_latin_routes_to_mixed_script()
    {
        var report = TokenizerCoverage.Inspect(new LabelSet(["α", "a"]), "αa");
        Assert.True(report.MixedScript);
        Assert.Contains("mixed-script", report.SuggestedRoute);
        Assert.Equal(1, report.Counts[TextScript.Greek]);
        Assert.Equal(1, report.Counts[TextScript.Latin]);
    }

    [Fact]
    public void Common_punctuation_does_not_make_text_mixed_script()
    {
        var report = TokenizerCoverage.Inspect(new LabelSet(["α", " ", "1"]), "α 1");
        Assert.False(report.MixedScript);
        Assert.Equal(1, report.Counts[TextScript.Greek]);
        Assert.Equal(2, report.Counts[TextScript.Common]);
    }

    [Fact]
    public void Unknown_grapheme_is_reported_not_silently_dropped()
    {
        var report = TokenizerCoverage.Inspect(new LabelSet(["α"]), "α🙂");
        Assert.Contains("🙂", report.MissingGraphemes);
        Assert.Equal(1, report.Counts[TextScript.Unknown]);
    }
}
