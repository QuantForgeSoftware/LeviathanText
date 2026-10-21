using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Infrastructure;

namespace LeviathanText.Application.Tests;

public class ReviewDecisionImportTests : IDisposable
{
    private readonly string _db = Path.Combine(Path.GetTempPath(), $"lt-review-decisions-{Guid.NewGuid():N}.db");

    [Fact]
    public void Applies_agree_correct_and_unclear_decisions_through_the_review_ledger()
    {
        using var store = new LocalStore(_db);
        var lineId = new EntityId(EntityKind.Line, "ln-1");
        var revision = new TextRevision
        {
            Id = new EntityId(EntityKind.TranscriptionRevision, "rev-1"),
            ScopeId = lineId, Layer = TextLayer.L1Diplomatic,
            Author = new AgentRef { Id = "test", IsModel = false },
            CreatedAt = DateTimeOffset.UtcNow, Text = "αβγ",
        };
        var project = new NativeProject
        {
            Id = new EntityId(EntityKind.Collection, "col-1"), Name = "review",
            Objects = [new NativeObject
            {
                Id = new EntityId(EntityKind.PhysicalObject, "obj-1"),
                Surfaces = [new NativeSurface
                {
                    Id = new EntityId(EntityKind.Surface, "surf-1"), Kind = "page",
                    Captures = [], Regions = [],
                    Lines = [new NativeLine
                    {
                        Id = lineId, RegionId = new EntityId(EntityKind.Region, "reg-1"),
                        FrameRef = new EntityId(EntityKind.Capture, "cap-1"),
                        DiplomaticRevisionId = revision.Id,
                    }],
                    Revisions = [revision],
                }],
            }],
        };
        store.SaveProject(project);

        var id = lineId.ToString();
        var csv = "line_id,surface_id,decision,corrected_text,notes\n"
            + $"{id},surf-1,agree,αβγ,\n"
            + $"{id},surf-1,correct,αβδ,visible delta\n"
            + $"{id},surf-1,unclear,,faint\n";
        var preview = ReviewDecisionImport.Preview(project, csv);
        Assert.Equal(3, preview.Total);
        Assert.Equal(1, preview.Agree);
        Assert.Equal(1, preview.Correct);
        Assert.Equal(1, preview.Unclear);
        Assert.Equal(0, preview.Invalid);
        Assert.Empty(store.LoadProject(project.Id)!.Objects[0].Surfaces[0].ReviewDecisions);

        var result = ReviewDecisionImport.Apply(project, csv, "reviewer",
            new ReviewService(store), store);

        Assert.Equal(3, result.Applied);
        Assert.Equal(0, result.Skipped);
        var saved = store.LoadProject(project.Id)!;
        Assert.Contains(saved.Objects[0].Surfaces[0].Revisions,
            r => r.Layer == TextLayer.L1Diplomatic && r.Text == "αβδ");
        Assert.Equal(3, saved.Objects[0].Surfaces[0].ReviewDecisions.Count);
    }

    public void Dispose()
    {
        if (File.Exists(_db)) File.Delete(_db);
    }
}
