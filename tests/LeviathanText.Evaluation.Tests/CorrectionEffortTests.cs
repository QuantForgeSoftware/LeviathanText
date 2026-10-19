using LeviathanText.Contracts;
using LeviathanText.Evaluation;
using Xunit;

namespace LeviathanText.Evaluation.Tests;

/// <summary>
/// Correction-effort and unsupported-addition scoring for AI-assisted review
/// (acceptance-plan.md §5). Times are supplied by the harness; this scorer never
/// invents one.
/// </summary>
public class CorrectionEffortTests
{
    private static SuggestionAudit Suggestion(
        SuggestionKind kind,
        SuggestionDisposition disposition,
        string proposed,
        string source,
        ScalarRange? target = null) => new(kind, disposition, proposed, source, target);

    [Fact]
    public void Raw_and_reviewed_output_are_scored_separately()
    {
        var score = CorrectionEffortScorer.Score(
            reference: "κυρίου",
            raw: "κυριου",
            final: "κυρίου",
            suggestions:
            [
                Suggestion(SuggestionKind.Accentuation, SuggestionDisposition.Accepted, "κυρίου", "κυριου", new ScalarRange(0, 6)),
            ],
            time: new CorrectionTime(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(4)));

        Assert.Equal(1, score.ScalarEditsWithoutAssistance);
        Assert.Equal(1, score.ScalarEditsWithAssistance);
        Assert.Equal(0.0, score.FinalCer);                    // the accepted suggestion fixed the line
        Assert.Equal(1.0 / 6.0, score.RawCer, 10);
        Assert.Equal(1.0 / 6.0, score.CerImprovement, 10);
        Assert.Equal(0.6, score.Time!.Reduction, 10);         // labor gain is recorded as time, not scalar edits
        Assert.Equal(1, score.AcceptedSuggestions);
        Assert.Equal(6, score.AcceptedSuggestionScalars);
        Assert.Equal(0, score.UnsupportedAddedScalars);
        Assert.Equal(1, score.SuppliedRestorationScalars);    // the combining mark the task was allowed to add
    }

    [Fact]
    public void Candidate_reading_addition_with_no_source_support_is_unsupported()
    {
        var score = CorrectionEffortScorer.Score(
            reference: "κυρίου",
            raw: "κυρίου",
            final: "κυρίου",
            suggestions:
            [
                Suggestion(SuggestionKind.CandidateReading, SuggestionDisposition.Accepted, "κυρίουι", "κυρίου"),
            ]);

        Assert.Equal(1, score.UnsupportedAddedScalars);
        Assert.Equal(0, score.SuppliedRestorationScalars);
    }

    [Fact]
    public void Expansion_and_restoration_additions_are_marked_supplied_not_unsupported()
    {
        var expansion = CorrectionEffortScorer.Score(
            "κυρίου", "κυρίου", "κυρίου",
            [Suggestion(SuggestionKind.Expansion, SuggestionDisposition.Accepted, "κυρίου", "κυ")]);
        var restoration = CorrectionEffortScorer.Score(
            "κυρίου", "κυρίου", "κυρίου",
            [Suggestion(SuggestionKind.Restoration, SuggestionDisposition.Accepted, "κυρίου", "■")]);

        Assert.Equal(0, expansion.UnsupportedAddedScalars);
        Assert.True(expansion.SuppliedRestorationScalars > 0);
        Assert.Equal(0, restoration.UnsupportedAddedScalars);
        Assert.True(restoration.SuppliedRestorationScalars > 0);
    }

    [Theory]
    [InlineData("κυρι ου", 0, 1)]   // a space is expected word division
    [InlineData("κυριοu", 1, 0)]    // a letter change is not
    public void Word_division_only_separators_are_supported(string proposed, int unsupportedAdded, int suppliedAdded)
    {
        var score = CorrectionEffortScorer.Score(
            "κυριου", "κυριου", "κυριου",
            [Suggestion(SuggestionKind.WordDivision, SuggestionDisposition.Accepted, proposed, "κυριου")]);

        Assert.Equal(unsupportedAdded, score.UnsupportedAddedScalars);
        Assert.Equal(suppliedAdded, score.SuppliedRestorationScalars);
        Assert.Equal(1, Assert.Single(score.Findings).AddedScalars);
    }

    [Fact]
    public void Word_division_letter_replacement_is_also_an_unsupported_substitution()
    {
        var score = CorrectionEffortScorer.Score(
            "κυριου", "κυριου", "κυριου",
            [Suggestion(SuggestionKind.WordDivision, SuggestionDisposition.Accepted, "κυριοu", "κυριου")]);

        Assert.Equal(1, score.UnsupportedSubstitutedScalars);
    }

    [Fact]
    public void Accentuation_additions_are_supported()
    {
        var score = CorrectionEffortScorer.Score(
            "κυρίου", "κυρίου", "κυρίου",
            [Suggestion(SuggestionKind.Accentuation, SuggestionDisposition.Accepted, "κυρίου", "κυριου")]);

        Assert.Equal(0, score.UnsupportedAddedScalars);
        Assert.Equal(1, score.SuppliedRestorationScalars);
    }

    [Fact]
    public void Accepted_candidate_reading_that_replaces_a_correct_witness_span_is_flagged()
    {
        var score = CorrectionEffortScorer.Score(
            reference: "κυριου",
            raw: "κυριου",     // the raw reading already matched the reference
            final: "κυρίου",
            suggestions:
            [
                Suggestion(SuggestionKind.CandidateReading, SuggestionDisposition.Accepted, "κυρίου", "κυριου", new ScalarRange(0, 6)),
            ]);

        Assert.Equal(1, score.WitnessVariantOverwrites);   // accepted change diverged from the correct witness reading
        Assert.Equal(1, score.UnsupportedAddedScalars);    // and added a mark the visible source does not show
    }

    [Fact]
    public void Rejected_and_pending_suggestions_are_counted_but_do_not_change_unsupported_totals()
    {
        var score = CorrectionEffortScorer.Score(
            "κυρίου", "κυρίου", "κυρίου",
            [
                Suggestion(SuggestionKind.CandidateReading, SuggestionDisposition.Rejected, "κυριο", "κυρίου"),
                Suggestion(SuggestionKind.CandidateReading, SuggestionDisposition.Proposed, "κυριο", "κυρίου"),
                Suggestion(SuggestionKind.Restoration, SuggestionDisposition.Expired, "κυρίου", "■"),
            ]);

        Assert.Equal(0, score.AcceptedSuggestions);
        Assert.Equal(1, score.RejectedSuggestions);
        Assert.Equal(2, score.PendingSuggestions);
        Assert.Equal(0, score.UnsupportedAddedScalars);
        Assert.Equal(0, score.SuppliedRestorationScalars);
    }

    [Fact]
    public void Aggregate_is_micro_over_lines()
    {
        var first = CorrectionEffortScorer.Score("κυρίου", "κυριου", "κυρίου", time: new CorrectionTime(TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(1)));
        var second = CorrectionEffortScorer.Score("λόγος", "λογος", "λόγος", time: new CorrectionTime(TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(4)));

        var aggregate = CorrectionEffortScorer.Aggregate([first, second]);

        Assert.Equal(11, aggregate.ReferenceScalars);
        Assert.Equal(2, aggregate.ScalarEditsWithoutAssistance);
        Assert.Equal(2, aggregate.ScalarEditsWithAssistance);
        Assert.Equal(2.0 / 11.0, aggregate.RawCer, 10);
        Assert.Equal(0.0, aggregate.FinalCer);
        Assert.Equal(2.0 / 11.0, aggregate.CerImprovement, 10);
        Assert.Equal(TimeSpan.FromMinutes(6), aggregate.Time!.Baseline);
        Assert.Equal(1.0 / 6.0, aggregate.Time.Reduction, 10);
    }

    [Fact]
    public void Aggregate_rejects_an_empty_sequence()
    {
        Assert.Throws<ArgumentException>(() => CorrectionEffortScorer.Aggregate([]));
    }
}
