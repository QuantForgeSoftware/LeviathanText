using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application.Tests;

public sealed class ActiveLearningQueueTests
{
    [Fact]
    public void Missing_and_disputed_lines_rank_above_clean_reviewed_lines()
    {
        var project = SampleProjectBuilder.Build();
        var surface = project.Objects[0].Surfaces[0];
        var line = surface.Lines[0];
        var l0 = new TextRevision
        {
            Id = new EntityId(EntityKind.TranscriptionRevision, "l0-1"),
            ScopeId = line.Id,
            Layer = TextLayer.L0RawRecognition,
            Author = new AgentRef { Id = "model", IsModel = true },
            CreatedAt = DateTimeOffset.UtcNow,
            Text = "α",
            Confidence = Confidence.Of(0.2, ConfidenceScale.RawModelProbability),
            GraphemeConfidences =
            [
                new GraphemeConfidenceSpan
                {
                    Range = new ScalarRange(0, 1),
                    Confidence = Confidence.Of(0.2, ConfidenceScale.RawModelProbability),
                },
            ],
        };
        var review = new ReviewDecision
        {
            Id = "rd-1",
            LineId = line.Id,
            RevisionId = l0.Id,
            Reviewer = "reviewer-a",
            Outcome = ReviewOutcome.Dispute,
            At = DateTimeOffset.UtcNow,
        };
        var withL0 = project with
        {
            Objects = [project.Objects[0] with
            {
                Surfaces = [surface with
                {
                    Revisions = [.. surface.Revisions, l0],
                    ReviewDecisions = [review],
                }],
            }],
        };

        var before = Assert.Single(ActiveLearningQueue.Build(project));
        var after = Assert.Single(ActiveLearningQueue.Build(withL0));

        Assert.True(after.Priority > before.Priority);
        Assert.Contains("low confidence", after.Reasons);
        Assert.Contains("review disagreement", after.Reasons);
        Assert.True(after.Disputed);
        Assert.True(after.Reviewed);
    }

    [Fact]
    public void Csv_contains_reasons_and_priority()
    {
        var csv = ActiveLearningQueue.ToCsv(ActiveLearningQueue.Build(SampleProjectBuilder.Build()));
        Assert.StartsWith("line_id,surface_id,priority,mean_confidence", csv);
        Assert.Contains("no L0", csv);
    }
}
