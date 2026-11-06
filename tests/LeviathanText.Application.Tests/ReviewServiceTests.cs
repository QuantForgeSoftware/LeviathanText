using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application.Tests;

public sealed class ReviewServiceTests
{
    [Fact]
    public void Replacing_reviewed_text_resets_current_review_counts_without_erasing_history()
    {
        var (project, lineId, _, service, store) = Setup();
        var (first, _, _, _) = service.Record(project, lineId, "reviewer-a", ReviewOutcome.Accept);
        var (second, reviewedSurface, _, _) = service.Record(first, lineId, "reviewer-b", ReviewOutcome.Accept);
        var text = service.Summarize(second).Lines.Single().Text;
        // Even identical bytes in a new revision require fresh revision-anchored review.
        var changed = Edit(store, second, lineId, text);

        var summary = service.Summarize(changed);

        Assert.Equal(0, summary.ReviewedLines);
        Assert.Equal(0, summary.AcceptedLines);
        Assert.Equal(0, summary.DoubleReviewedLines);
        var line = Assert.Single(summary.Lines);
        Assert.Equal(0, line.Decisions);
        Assert.Empty(line.Reviewers);
        Assert.Null(line.LatestOutcome);
        Assert.Equal(reviewedSurface.ReviewDecisions, changed.Objects[0].Surfaces[0].ReviewDecisions);
    }

    [Fact]
    public void Obsolete_disagreement_does_not_require_adjudication_of_replacement_text()
    {
        var (project, lineId, _, service, store) = Setup();
        var (first, _, _, _) = service.Record(project, lineId, "old-a", ReviewOutcome.Accept);
        var (second, _, _, _) = service.Record(first, lineId, "old-b", ReviewOutcome.Dispute, note: "old dispute");
        var changed = Edit(store, second, lineId, "νεα γραφη");

        var summary = service.Summarize(changed);

        Assert.Equal(0, summary.DisputedLines);
        Assert.Equal(0, summary.NeedsAdjudication);
        Assert.False(Assert.Single(summary.Lines).NeedsAdjudication);
        Assert.DoesNotContain("old-b", service.ExportAdjudicationCsv(changed));
        Assert.Equal(2, changed.Objects[0].Surfaces[0].ReviewDecisions.Count);
    }

    [Fact]
    public void Current_adjudication_export_excludes_obsolete_decisions_and_notes()
    {
        var (project, lineId, _, service, store) = Setup();
        var (old, _, _, _) = service.Record(project, lineId, "old-reviewer", ReviewOutcome.Dispute, note: "obsolete note");
        var changed = Edit(store, old, lineId, "νεα γραφη");
        var (first, _, _, _) = service.Record(changed, lineId, "current-a", ReviewOutcome.Accept);
        var (second, _, _, _) = service.Record(first, lineId, "current-b", ReviewOutcome.Dispute, note: "current note");

        var csv = service.ExportAdjudicationCsv(second);

        Assert.DoesNotContain("old-reviewer", csv);
        Assert.DoesNotContain("obsolete note", csv);
        Assert.Contains("current-a=Accept", csv);
        Assert.Contains("current-b=Dispute(current note)", csv);
        Assert.Equal(2, Assert.Single(service.Summarize(second).Lines).Decisions);
        Assert.Equal(3, second.Objects[0].Surfaces[0].ReviewDecisions.Count);
    }

    [Fact]
    public void Correction_provenance_does_not_count_as_review_of_its_new_text()
    {
        var (project, lineId, _, service, store) = Setup();
        var (first, _, _, _) = service.Record(project, lineId, "original-reviewer", ReviewOutcome.Accept);
        var (corrected, _, _, _) = service.Record(first, lineId, "corrector", ReviewOutcome.Correct,
            correctionText: "διορθωσις");
        var summary = service.Summarize(corrected);
        Assert.Equal(1, summary.CorrectedLines);
        Assert.Equal(0, summary.ReviewedLines);
        Assert.Equal(0, summary.AcceptedLines);
        Assert.Equal(0, summary.DoubleReviewedLines);
        Assert.Equal(0, summary.NeedsAdjudication);
        Assert.Equal(0, Assert.Single(summary.Lines).Decisions);

        var (accepted, _, _, _) = service.Record(corrected, lineId, "new-reviewer", ReviewOutcome.Accept);
        var acceptedSummary = service.Summarize(accepted);
        Assert.Equal(1, acceptedSummary.CorrectedLines);
        Assert.Equal(1, acceptedSummary.ReviewedLines);
        Assert.Equal(1, acceptedSummary.AcceptedLines);
        Assert.False(Assert.Single(acceptedSummary.Lines).DoubleReviewed);

        var edited = Edit(store, accepted, lineId, "μεταβολη");
        Assert.Equal(0, service.Summarize(edited).CorrectedLines);
        Assert.Equal(3, edited.Objects[0].Surfaces[0].ReviewDecisions.Count);
    }

    [Fact]
    public void Two_current_decisions_by_one_reviewer_count_as_one_reviewer()
    {
        var (project, lineId, _, service, _) = Setup();
        var (first, _, _, _) = service.Record(project, lineId, "same-reviewer", ReviewOutcome.Accept);
        var (second, _, _, _) = service.Record(first, lineId, "same-reviewer", ReviewOutcome.Dispute);

        var line = Assert.Single(service.Summarize(second).Lines);

        Assert.Equal(2, line.Decisions);
        Assert.Single(line.Reviewers);
        Assert.False(line.DoubleReviewed);
        Assert.False(line.NeedsAdjudication);
        Assert.Equal(ReviewOutcome.Dispute, line.LatestOutcome);
    }

    private static NativeProject Edit(InMemoryStore store, NativeProject project, EntityId lineId, string text)
    {
        var current = project.Objects[0].Surfaces[0].Lines.Single(line => line.Id == lineId).DiplomaticRevisionId;
        var (updated, _, _) = new TranscriptionService(store).AppendLineRevision(project, lineId,
            TextLayer.L1Diplomatic, text, new AgentRef { Id = "editor", IsModel = false }, current);
        return updated;
    }

    private static (NativeProject Project, EntityId LineId, NativeSurface Surface, ReviewService Service, InMemoryStore Store) Setup()
    {
        var store = new InMemoryStore();
        var project = SampleProjectBuilder.Build();
        store.SaveProject(project);
        return (project, project.Objects[0].Surfaces[0].Lines[0].Id,
            project.Objects[0].Surfaces[0], new ReviewService(store), store);
    }

    private static NativeProject Reload(InMemoryStore store, NativeProject project) =>
        store.LoadProject(project.Id)!;

    [Fact]
    public void Recording_a_decision_against_a_superseded_revision_is_refused()
    {
        var (project, lineId, surface, service, store) = Setup();
        var reviewed = surface.Lines[0].DiplomaticRevisionId!.Value;
        var transcription = new TranscriptionService(store);
        var (afterEdit, _, _) = transcription.AppendLineRevision(
            Reload(store, project), lineId, TextLayer.L1Diplomatic, "νεα γραφη",
            new AgentRef { Id = "editor", IsModel = false }, reviewed);

        var ex = Assert.Throws<LeviathanException>(() => service.Record(
            Reload(store, afterEdit), lineId, "reviewer-a", ReviewOutcome.Accept,
            reviewedRevisionId: reviewed));

        Assert.Equal(ErrorCode.RevisionConflict, ex.Error.Code);
        Assert.Equal(reviewed.ToString(), ex.Error.Details!["requestedRevision"]);
        Assert.Equal(RemedyHint.RebaseRevision, ex.Error.RemedyHint);
    }

    [Fact]
    public void Accept_is_anchored_to_the_reviewed_revision()
    {
        var (project, lineId, surface, service, store) = Setup();
        var revisionId = surface.Lines[0].DiplomaticRevisionId!.Value;

        var (updated, updatedSurface, decision, correction) =
            service.Record(project, lineId, "reviewer-a", ReviewOutcome.Accept);

        Assert.Null(correction);
        Assert.Equal(revisionId, decision.RevisionId);
        Assert.Equal(ReviewOutcome.Accept, decision.Outcome);
        Assert.Single(updatedSurface.ReviewDecisions);

        var summary = service.Summarize(Reload(store, updated));
        Assert.Equal(1, summary.ReviewedLines);
        Assert.Equal(1, summary.AcceptedLines);
        Assert.Equal(0, summary.CorrectedLines);
        Assert.Equal(0, summary.NeedsAdjudication);
        Assert.Equal(ReviewOutcome.Accept, summary.Lines.Single().LatestOutcome);
    }

    [Fact]
    public void Disagreement_between_two_reviewers_marks_the_line_for_adjudication()
    {
        var (project, lineId, _, service, store) = Setup();
        var (first, _, _, _) = service.Record(project, lineId, "reviewer-a", ReviewOutcome.Accept);
        var (second, _, _, _) = service.Record(first, lineId, "reviewer-b", ReviewOutcome.Dispute, note: "damaged");

        var summary = service.Summarize(Reload(store, second));
        var line = summary.Lines.Single();
        Assert.True(line.DoubleReviewed);
        Assert.True(line.NeedsAdjudication);
        Assert.Equal(2, line.Reviewers.Count);
        Assert.Equal(1, summary.NeedsAdjudication);
        Assert.Equal(1, summary.DisputedLines);

        var csv = service.ExportAdjudicationCsv(second);
        Assert.Contains("ln1", csv);
        Assert.Contains("reviewer-a=Accept", csv);
        Assert.Contains("reviewer-b=Dispute", csv);
    }

    [Fact]
    public void Correction_creates_a_new_l1_revision_and_links_both_ids()
    {
        var (project, lineId, surface, service, store) = Setup();
        var reviewedRevision = surface.Lines[0].DiplomaticRevisionId!.Value;

        var (updated, updatedSurface, decision, correction) = service.Record(
            project, lineId, "reviewer-a", ReviewOutcome.Correct, correctionText: "διορθωσις");

        Assert.NotNull(correction);
        Assert.Equal("διορθωσις", correction!.Text);
        Assert.Equal(reviewedRevision, correction.ParentRevisionId);
        Assert.Equal(reviewedRevision, decision.RevisionId);
        Assert.Equal(correction.Id, decision.CorrectedRevisionId);

        var reloaded = Reload(store, updated).Objects[0].Surfaces[0];
        var summary = service.Summarize(Reload(store, updated));
        Assert.Equal(1, summary.CorrectedLines);
        Assert.Equal(correction.Id, reloaded.Lines[0].DiplomaticRevisionId);
        Assert.Contains(reloaded.Revisions, r => r.Id == correction.Id && r.Text == "διορθωσις");
    }

    [Fact]
    public void Invalid_review_requests_are_rejected()
    {
        var (project, lineId, _, service, _) = Setup();

        Assert.Throws<LeviathanException>(() =>
            service.Record(project, lineId, " ", ReviewOutcome.Accept));
        Assert.Throws<LeviathanException>(() =>
            service.Record(project, lineId, "reviewer-a", ReviewOutcome.Correct));
        Assert.Throws<LeviathanException>(() =>
            service.Record(project, lineId, "reviewer-a", ReviewOutcome.Accept, correctionText: "x"));

        var lineWithoutText = project.Objects[0].Surfaces[0].Lines[0] with { DiplomaticRevisionId = null };
        var bare = project with
        {
            Objects = [project.Objects[0] with
            {
                Surfaces = [project.Objects[0].Surfaces[0] with { Lines = [lineWithoutText] }],
            }],
        };
        Assert.Throws<LeviathanException>(() =>
            service.Record(bare, lineId, "reviewer-a", ReviewOutcome.Accept));
    }
}
