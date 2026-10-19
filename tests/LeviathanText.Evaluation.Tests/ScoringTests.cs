using LeviathanText.Contracts;
using LeviathanText.Core.Unicode;
using LeviathanText.Evaluation;
using Xunit;

namespace LeviathanText.Evaluation.Tests;

/// <summary>Scoring semantics from examples.md §1 and todo §7.</summary>
public class ScoringTests
{
    [Fact]
    public void IdenticalText_ScoresZero()
    {
        var score = RecognitionScorer.Score("Κϲ χάριτος", "Κϲ χάριτος");
        Assert.Equal(0, score.ScalarDistance);
        Assert.Equal(0.0, score.ScalarCer);
        Assert.Equal(0, score.MarkErrors.Total);
    }

    [Fact]
    public void CanonicalEquivalents_CompareEqual()
    {
        // Precomposed U+03AC vs decomposed alpha + combining acute.
        var score = RecognitionScorer.Score("ά", "α\u0301");
        Assert.Equal(0, score.ScalarDistance);
        Assert.Equal(0.0, score.ScalarCer);
        Assert.Equal(1, score.ReferenceScalarLength); // NFC storage form
    }

    [Fact]
    public void MissingAccent_IsAMarkDeletion_NotABaseError()
    {
        var score = RecognitionScorer.Score("τῆς", "της");
        Assert.True(score.MarkErrors.Deletions >= 1, $"expected a mark deletion, got {score.MarkErrors}");
        Assert.Equal(0, score.BaseErrors.Substitutions);
        Assert.Equal(0, score.BaseErrors.Deletions);
    }

    [Fact]
    public void SuppliedAccentOnUnmarkedText_IsAMarkInsertion()
    {
        var score = RecognitionScorer.Score("της", "τῆς");
        Assert.True(score.MarkErrors.Insertions >= 1, $"expected a mark insertion, got {score.MarkErrors}");
    }

    [Fact]
    public void GraphemeErrorRate_CountsClustersNotScalars()
    {
        var score = RecognitionScorer.Score("άβ", "αβ");
        Assert.Equal(1, score.GraphemeDistance);
        Assert.Equal(0.5, score.GraphemeCer);
    }

    [Fact]
    public void Uncertainty_ExcludesSpansFromMainScore_AndReportsCoverage()
    {
        var ranges = new List<ScalarRange> { new(1, 3) };

        // An error inside the excluded span does not count...
        var inside = UncertaintyScorer.Score("αβγδ", "αΩγδ", ranges);
        Assert.Equal(0, inside.ScalarDistance);
        Assert.Equal(0.5, inside.Coverage);

        // ...but an insertion (no reference anchor) always counts.
        var insertion = UncertaintyScorer.Score("αβγδ", "αβXγδ", ranges);
        Assert.Equal(1, insertion.ScalarDistance);
        Assert.Equal(0.5, insertion.ScalarCer);
    }

    [Fact]
    public void MalformedSurrogateHandling_MatchesCoreScalarSegmentation()
    {
        // A dangling high surrogate between two ordinary letters.
        var text = "α\uD834β";
        Assert.Equal(ScalarText.ScalarLength(text), CanonicalText.ToScalars(text).Count);
        Assert.Equal(text, CanonicalText.FromScalars(CanonicalText.ToScalars(text)));

        // A valid surrogate pair stays a single scalar.
        var pair = "α\U0001F600β"; // emoji as one astral code point
        Assert.Equal(ScalarText.ScalarLength(pair), CanonicalText.ToScalars(pair).Count);
        Assert.Equal(pair, CanonicalText.FromScalars(CanonicalText.ToScalars(pair)));
    }

    [Fact]
    public void PageScore_CountsMissedLinesAndBlankInsertions()
    {
        var lines = new (LineTruth, LinePrediction?)[]
        {
            (new LineTruth("l1", "αβ"), new LinePrediction("l1", "αβ")),
            (new LineTruth("l2", "", IsBlank: true), new LinePrediction("l2", "γ")),   // hallucination on blank
            (new LineTruth("l3", "αβγ"), new LinePrediction("l3", null)),               // missed line
        };

        var page = PageScorer.Evaluate(lines);
        Assert.Equal(3, page.LineCount);
        Assert.Equal(5, page.ReferenceScalarLength);
        Assert.Equal(1, page.BlankRegionInsertions);
        Assert.Equal(1, page.MissedLines);
        Assert.Equal(4, page.ScalarDistance);
        Assert.Equal(4.0 / 6.0, page.ScalarCer, precision: 10);
    }
}
