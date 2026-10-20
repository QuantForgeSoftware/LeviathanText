using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application.Tests;

public sealed class ReviewServiceTests
{
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
