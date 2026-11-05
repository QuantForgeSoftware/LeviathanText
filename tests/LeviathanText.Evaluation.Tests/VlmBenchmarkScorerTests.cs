using LeviathanText.Evaluation;
using Xunit;

namespace LeviathanText.Evaluation.Tests;

public sealed class VlmBenchmarkScorerTests
{
    [Fact]
    public void NaturalAccuracyExcludesGroundingControlsAndTheirSyntheticDenominators()
    {
        var result = VlmBenchmarkScorer.ScoreRun(
            [Reference("line", "α"), Reference("blank", "", VlmControlKind.NoCharacterTraces)],
            [Output("line", "α"), Output("blank", "β")]);

        Assert.Equal(0, result.Overall.MicroCer);
        Assert.Equal(1, result.Overall.ReferenceScalars);
        Assert.Equal(1, result.Overall.NaturalLineCount);
        Assert.Equal(VlmControlOutcome.Fail, result.Rows[1].ControlOutcome);
        Assert.Equal(1, result.Rows[1].Insertions);
        Assert.Null(result.Rows[1].ScalarCer);
        Assert.Equal(1, Assert.Single(result.Controls).Failed);
    }

    [Fact]
    public void TimeoutOnBlankCannotPassAsSuccessfulEmptyResponse()
    {
        var result = VlmBenchmarkScorer.ScoreRun(
            [Reference("blank", "", VlmControlKind.NoCharacterTraces)],
            [new("blank", VlmCompletionStatus.Timeout, null)]);

        var row = Assert.Single(result.Rows);
        Assert.True(row.ExactTextMatch);
        Assert.False(row.ValidCompleted);
        Assert.Equal(VlmControlOutcome.Fail, row.ControlOutcome);
        Assert.Null(result.Overall.MicroCer);
        Assert.Null(result.Overall.ValidCompletionCoverage);
        Assert.Equal(0, result.Overall.NaturalLineCount);
    }

    [Fact]
    public void CanonicalNfcEquivalencePreservesCaseSigmaAccentsAndWhitespace()
    {
        var result = VlmBenchmarkScorer.ScoreRun(
            [Reference("canonical", "ά"), Reference("literal", "Σ ϲά")],
            [Output("canonical", "α\u0301"), Output("literal", "σςα")]);

        Assert.Equal(0, result.Rows[0].ScalarDistance);
        Assert.Equal(1, result.Rows[0].ReferenceScalars);
        Assert.Equal(4, result.Rows[1].ScalarDistance);
        Assert.Equal(1, result.Rows[1].Deletions);
        Assert.Equal(3, result.Rows[1].Substitutions);
        Assert.Equal(VlmBenchmarkScorer.PolicyVersion, result.PolicyVersion);
    }

    [Fact]
    public void SupplementaryGreekCharacterIsOneScalar()
    {
        var row = Assert.Single(VlmBenchmarkScorer.ScoreRun(
            [Reference("astral", "α\U00010140β")], [Output("astral", "αβ")]).Rows);

        Assert.Equal(3, row.ReferenceScalars);
        Assert.Equal(2, row.HypothesisScalars);
        Assert.Equal(1, row.Deletions);
        Assert.Equal(1.0 / 3, row.ScalarCer);
    }

    [Theory]
    [InlineData(VlmCompletionStatus.InvalidOutput)]
    [InlineData(VlmCompletionStatus.Truncated)]
    [InlineData(VlmCompletionStatus.Timeout)]
    [InlineData(VlmCompletionStatus.TransportError)]
    [InlineData(VlmCompletionStatus.Cancelled)]
    public void FailedNonemptyOutputRetainsEveryInsertedScalar(VlmCompletionStatus status)
    {
        var result = VlmBenchmarkScorer.ScoreRun(
            [Reference("line", "α")], [new("line", status, "αβγδ")]);

        Assert.Equal(3, result.Overall.Insertions);
        Assert.Equal(3, result.Overall.MicroCer);
        Assert.Equal(4, result.Overall.OutputReferenceLengthRatio);
        Assert.Equal(0, result.Overall.ValidCompletionCoverage);
        Assert.Equal(1, result.Overall.NonemptyOutputCoverage);
        Assert.Equal(status, Assert.Single(result.Rows).Status);
    }

    [Fact]
    public void InvalidProseAndWhitespaceAreNotExtractedOrTrimmed()
    {
        const string raw = "\nα\nExplanation: βγ\n";
        var result = VlmBenchmarkScorer.ScoreRun([Reference("line", "α")],
            [new("line", VlmCompletionStatus.InvalidOutput, raw)]);

        Assert.Equal(raw.Length, result.Overall.HypothesisScalars);
        Assert.Equal(raw.Length - 1, result.Overall.Insertions);
        Assert.Equal(raw.Length - 1, result.Overall.MicroCer);
    }

    [Fact]
    public void MissingAttemptsRemainInReferenceAndCoverageDenominators()
    {
        var result = VlmBenchmarkScorer.ScoreRun(
            [Reference("one", "αβ"), Reference("two", "γδε")], [Output("one", "αβ")]);

        Assert.Equal(5, result.Overall.ReferenceScalars);
        Assert.Equal(3, result.Overall.Deletions);
        Assert.Equal(0.6, result.Overall.MicroCer);
        Assert.Equal(0.5, result.Overall.ValidCompletionCoverage);
        Assert.Equal(0.5, result.Overall.NonemptyOutputCoverage);
        Assert.Equal(0.5, result.Overall.ExactLineAccuracy);
        Assert.Equal(VlmCompletionStatus.NotAttempted, result.Rows[1].Status);
    }

    [Fact]
    public void CompleteEmptyNaturalAbstentionStillDeletesWholeReference()
    {
        var result = VlmBenchmarkScorer.ScoreRun([Reference("line", "αβ")], [Output("line", "")]);
        Assert.Equal(2, result.Overall.Deletions);
        Assert.Equal(1, result.Overall.MicroCer);
        Assert.Equal(1, result.Overall.ValidCompletionCoverage);
        Assert.Equal(0, result.Overall.NonemptyOutputCoverage);
    }

    [Fact]
    public void NullCompletionIsInvalidButExplicitEmptyCompletionPassesBlankControl()
    {
        var result = VlmBenchmarkScorer.ScoreRun(
            [Reference("null", "", VlmControlKind.NoCharacterTraces),
             Reference("empty", "", VlmControlKind.NoCharacterTraces)],
            [new("null", VlmCompletionStatus.Completed, null), Output("empty", "")]);

        Assert.Equal(VlmCompletionStatus.InvalidOutput, result.Rows[0].Status);
        Assert.Equal(VlmControlOutcome.Fail, result.Rows[0].ControlOutcome);
        Assert.Equal(VlmControlOutcome.Pass, result.Rows[1].ControlOutcome);
    }

    [Theory]
    [InlineData(VlmCompletionStatus.InvalidOutput)]
    [InlineData(VlmCompletionStatus.Truncated)]
    [InlineData(VlmCompletionStatus.Timeout)]
    [InlineData(VlmCompletionStatus.TransportError)]
    [InlineData(VlmCompletionStatus.Cancelled)]
    [InlineData(VlmCompletionStatus.InputRejected)]
    public void FailedEmptyControlsCannotPass(VlmCompletionStatus status)
    {
        var row = Assert.Single(VlmBenchmarkScorer.ScoreRun(
            [Reference("blank", "", VlmControlKind.NoCharacterTraces)],
            [new("blank", status, "")]).Rows);
        Assert.Equal(VlmControlOutcome.Fail, row.ControlOutcome);
    }

    [Fact]
    public void WhitespaceOnBlankIsAnInsertionAndControlFailure()
    {
        var row = Assert.Single(VlmBenchmarkScorer.ScoreRun(
            [Reference("blank", "", VlmControlKind.NoCharacterTraces)], [Output("blank", " \n")]).Rows);
        Assert.Equal(2, row.Insertions);
        Assert.Equal(VlmControlOutcome.Fail, row.ControlOutcome);
    }

    [Theory]
    [InlineData("◊◊", VlmControlOutcome.Pass)]
    [InlineData("◊", VlmControlOutcome.Fail)]
    [InlineData("α◊", VlmControlOutcome.Fail)]
    [InlineData("", VlmControlOutcome.Fail)]
    public void UnreadableTracesRequireFrozenMarkerPattern(string hypothesis, VlmControlOutcome expected)
    {
        var row = Assert.Single(VlmBenchmarkScorer.ScoreRun(
            [new("trace", "u", "papyrus", "◊◊", VlmControlKind.UnreadableTraces, "◊")],
            [Output("trace", hypothesis)]).Rows);
        Assert.Equal(expected, row.ControlOutcome);
        Assert.Null(row.ScalarCer);
    }

    [Fact]
    public void MaskedCounterfactualRequiresHumanAuditEvenWhenTextMatches()
    {
        var row = Assert.Single(VlmBenchmarkScorer.ScoreRun(
            [Reference("masked", "α◊", VlmControlKind.MaskedCounterfactual)],
            [Output("masked", "α◊")]).Rows);
        Assert.True(row.ExactTextMatch);
        Assert.Equal(VlmControlOutcome.NeedsManualReview, row.ControlOutcome);
    }

    [Fact]
    public void GroupsUsePhysicalUnitsRatherThanCountingLongLinesAsIndependentUnits()
    {
        var result = VlmBenchmarkScorer.ScoreRun(
            [new("long", "unit-a", "papyrus", new string('α', 100)),
             new("short", "unit-b", "inscription", "β")],
            [Output("long", ""), Output("short", "β")]);

        Assert.Equal(100.0 / 101, result.Overall.MicroCer);
        Assert.Equal(0.5, result.Overall.MacroPhysicalUnitCer);
        Assert.Equal(2, result.Overall.PhysicalUnitCount);
        Assert.Equal(1, result.ByMaterial.Single(x => x.Key == "papyrus").Score.MicroCer);
        Assert.Equal(0, result.ByMaterial.Single(x => x.Key == "inscription").Score.MicroCer);
        Assert.Equal(2, result.ByPhysicalUnit.Count);
    }

    [Fact]
    public void ExactFailedTextDoesNotCountAsValidCompletion()
    {
        var result = VlmBenchmarkScorer.ScoreRun([Reference("line", "α")],
            [new("line", VlmCompletionStatus.Truncated, "α")]);
        Assert.Equal(0, result.Overall.MicroCer);
        Assert.Equal(1, result.Overall.ExactLineAccuracy);
        Assert.Equal(0, result.Overall.ValidCompletionCoverage);
    }

    [Fact]
    public void DuplicateUnknownAndCrossRepetitionOutputsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => VlmBenchmarkScorer.ScoreRun(
            [Reference("same", "α"), Reference("same", "β")], []));
        Assert.Throws<ArgumentException>(() => VlmBenchmarkScorer.ScoreRun(
            [Reference("line", "α")], [Output("line", "α"), Output("line", "β")]));
        Assert.Throws<ArgumentException>(() => VlmBenchmarkScorer.ScoreRun(
            [Reference("line", "α")], [Output("other", "α")]));
    }

    [Fact]
    public void InvalidReferenceControlContractsCannotInventGroundTruth()
    {
        Assert.Throws<ArgumentException>(() => VlmBenchmarkScorer.ScoreRun([Reference("blank", "")], []));
        Assert.Throws<ArgumentException>(() => VlmBenchmarkScorer.ScoreRun(
            [Reference("blank", "α", VlmControlKind.NoCharacterTraces)], []));
        Assert.Throws<ArgumentException>(() => VlmBenchmarkScorer.ScoreRun(
            [new("trace", "u", "papyrus", "α", VlmControlKind.UnreadableTraces, "α")], []));
        Assert.Throws<ArgumentException>(() => VlmBenchmarkScorer.ScoreRun(
            [new("trace", "u", "papyrus", "◊α", VlmControlKind.UnreadableTraces, "◊")], []));
        Assert.Throws<ArgumentException>(() => VlmBenchmarkScorer.ScoreRun(
            [new("trace", "u", "papyrus", "◊", VlmControlKind.UnreadableTraces)], []));
        Assert.Throws<ArgumentException>(() => VlmBenchmarkScorer.ScoreRun(
            [Reference("line", "α")], [new("line", VlmCompletionStatus.NotAttempted, "α")]));
    }

    [Fact]
    public void EmptyReleaseHasUndefinedMetricsRatherThanPerfectAccuracy()
    {
        var result = VlmBenchmarkScorer.ScoreRun([], []);
        Assert.Empty(result.Rows);
        Assert.Null(result.Overall.MicroCer);
        Assert.Null(result.Overall.MacroPhysicalUnitCer);
        Assert.Null(result.Overall.ExactLineAccuracy);
        Assert.Null(result.Overall.ValidCompletionCoverage);
    }

    [Fact]
    public void EditCountsMatchExistingDeterministicAlignmentOnAmbiguousPaths()
    {
        var texts = new List<string> { "" };
        for (int size = 1; size <= 4; size++)
            for (int bits = 0; bits < (1 << size); bits++)
                texts.Add(new string(Enumerable.Range(0, size).Select(i => (bits & (1 << i)) == 0 ? 'α' : 'β').ToArray()));
        foreach (string reference in texts.Where(text => text.Length > 0))
        foreach (string hypothesis in texts)
        {
            var expected = EditScript.Compute(CanonicalText.ToScalars(reference), CanonicalText.ToScalars(hypothesis));
            var row = Assert.Single(VlmBenchmarkScorer.ScoreRun([Reference("s", reference)], [Output("s", hypothesis)]).Rows);
            Assert.Equal(expected.Substitutions, row.Substitutions);
            Assert.Equal(expected.Deletions, row.Deletions);
            Assert.Equal(expected.Insertions, row.Insertions);
        }
    }

    [Fact]
    public void LongInvalidGenerationIsScoredCompletelyWithoutQuadraticTracebackStorage()
    {
        string hypothesis = "α" + new string('β', 50_000);
        var result = VlmBenchmarkScorer.ScoreRun([Reference("s", "α")],
            [new("s", VlmCompletionStatus.InvalidOutput, hypothesis)]);
        Assert.Equal(50_000, result.Overall.Insertions);
        Assert.Equal(50_000, result.Overall.MicroCer);
    }

    [Fact]
    public void ExcessiveSingleAlignmentIsRejectedWithoutPublishingPartialScores()
    {
        // 4001 x4001 initialized cells exceeds the declared 16-million-cell run budget.
        // This bounded reproduction finishes on the old implementation; it does not
        // attempt the trillion-cell adversarial workload the guard prevents.
        Assert.Throws<ArgumentException>(() => VlmBenchmarkScorer.ScoreRun(
            [Reference("s", new string('α', 4000))], [Output("s", new string('β', 4000))]));
    }

    [Fact]
    public void WorkBudgetAppliesToEntireRunRatherThanEveryRowSeparately()
    {
        var references = Enumerable.Range(0, 3).Select(i => Reference($"s{i}", new string('α', 2500)));
        var outputs = Enumerable.Range(0, 3).Select(i => Output($"s{i}", new string('β', 2500)));
        Assert.Throws<ArgumentException>(() => VlmBenchmarkScorer.ScoreRun(references, outputs));
    }

    [Fact]
    public void EmptyReferenceControlsStillConsumeInitializationBudget()
    {
        var references = Enumerable.Range(0, 1000).Select(i => Reference($"blank{i}", "", VlmControlKind.NoCharacterTraces));
        var outputs = Enumerable.Range(0, 1000).Select(i => Output($"blank{i}", new string('α', 17_000)));
        Assert.Throws<ArgumentException>(() => VlmBenchmarkScorer.ScoreRun(references, outputs));
    }

    [Fact]
    public void ExcessiveReferenceScalarsAreRejectedEvenWithNoOutput()
        => Assert.Throws<ArgumentException>(() => VlmBenchmarkScorer.ScoreRun(
            [Reference("s", new string('α', 8193))], []));

    [Fact]
    public void HypothesisBeyondTransportSizedScalarBoundIsRejectedRatherThanTruncated()
        => Assert.Throws<ArgumentException>(() => VlmBenchmarkScorer.ScoreRun(
            [Reference("s", "α")], [Output("s", new string('β', 1_048_577))]));

    [Fact]
    public void ExcessiveSampleCountIsRejectedBeforeAlignment()
        => Assert.Throws<ArgumentException>(() => VlmBenchmarkScorer.ScoreRun(
            Enumerable.Range(0, 1001).Select(i => Reference($"s{i}", "α")), []));

    [Fact]
    public void TransportSizedFailureRemainsFullyScoredWhenItsEditWorkIsSmall()
    {
        var result = VlmBenchmarkScorer.ScoreRun([Reference("s", "αβ")],
            [new("s", VlmCompletionStatus.InvalidOutput, new string('<', 1_048_576))]);
        Assert.Equal(1_048_576, result.Overall.HypothesisScalars);
        Assert.Equal(1_048_574, result.Overall.Insertions);
        Assert.Equal(2, result.Overall.Substitutions);
    }

    private static VlmBenchmarkReference Reference(string id, string text, VlmControlKind kind = VlmControlKind.NaturalLine)
        => new(id, "unit", "papyrus", text, kind);

    private static VlmBenchmarkOutput Output(string id, string text)
        => new(id, VlmCompletionStatus.Completed, text);
}
