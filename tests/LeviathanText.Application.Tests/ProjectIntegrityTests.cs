using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application.Tests;

public sealed class ProjectIntegrityTests
{
    [Fact]
    public void Healthy_sample_project_has_no_issues()
    {
        var issues = ProjectIntegrity.Audit(SampleProjectBuilder.Build());
        Assert.Empty(issues);
    }

    [Fact]
    public void Unplaced_lines_are_not_reported_as_dangling_corruption()
    {
        var project = SampleProjectBuilder.Build();
        var surface = project.Objects[0].Surfaces[0];
        var unplaced = surface.Lines[0] with { RegionId = default, FrameRef = default };
        var modified = project with
        {
            Objects =
            [
                project.Objects[0] with
                {
                    Surfaces = [surface with { Lines = [unplaced] }],
                },
            ],
        };

        var issues = ProjectIntegrity.Audit(modified);

        Assert.DoesNotContain(issues, issue => issue.Code is "dangling-region" or "dangling-frame");
    }

    [Fact]
    public void Duplicate_dangling_and_out_of_range_references_are_reported_read_only()
    {
        var project = SampleProjectBuilder.Build();
        var surface = project.Objects[0].Surfaces[0];
        var revision = surface.Revisions.First();
        var line = surface.Lines[0];
        var duplicateLine = line with { Id = line.Id };
        var danglingLine = line with
        {
            Id = new EntityId(EntityKind.Line, "ln-dangling"),
            RegionId = new EntityId(EntityKind.Region, "reg-missing"),
        };
        var annotation = new Annotation
        {
            Id = new EntityId(EntityKind.Annotation, "ann-outside"),
            Kind = AnnotationKind.Unclear,
            AnchoredRevisionId = revision.Id,
            Range = new ScalarRange(0, 9999),
        };
        var cycleId = new EntityId(EntityKind.TranscriptionRevision, "rev-cycle");
        var cycle = revision with { Id = cycleId, ParentRevisionId = cycleId };
        NativeLine SourceTagged(string id) => line with
        {
            Id = new EntityId(EntityKind.Line, id),
            Tags = new Dictionary<string, IReadOnlyList<string>>
            {
                ["source-id"] = ["same-source"],
            },
        };
        var bad = project with
        {
            Objects =
            [
                project.Objects[0] with
                {
                    Fragments =
                    [
                        new NativeFragment
                        {
                            Id = new EntityId(EntityKind.Fragment, "frag-bad"),
                            SurfaceIds = [new EntityId(EntityKind.Surface, "surf-missing")],
                        },
                    ],
                    Surfaces =
                    [
                        surface with
                        {
                            Lines = [.. surface.Lines, duplicateLine, danglingLine,
                                SourceTagged("ln-source-a"), SourceTagged("ln-source-b")],
                            Revisions = [.. surface.Revisions, cycle],
                            Annotations = [.. surface.Annotations, annotation],
                            ReviewDecisions =
                            [
                                new ReviewDecision
                                {
                                    Id = "review-bad",
                                    LineId = danglingLine.Id,
                                    RevisionId = revision.Id,
                                    Reviewer = "tester",
                                    Outcome = ReviewOutcome.Accept,
                                    At = DateTimeOffset.UtcNow,
                                },
                                new ReviewDecision
                                {
                                    Id = "review-bad",
                                    LineId = line.Id,
                                    RevisionId = revision.Id,
                                    Reviewer = "tester",
                                    Outcome = ReviewOutcome.Accept,
                                    At = DateTimeOffset.UtcNow,
                                },
                            ],
                            AnnotationRetractions =
                            [
                                .. surface.AnnotationRetractions,
                                new AnnotationRetraction
                                {
                                    Id = "ar-missing",
                                    AnnotationId = new EntityId(EntityKind.Annotation, "ann-missing"),
                                    Reviewer = "tester",
                                    At = DateTimeOffset.UtcNow,
                                },
                                new AnnotationRetraction
                                {
                                    Id = "ar-missing",
                                    AnnotationId = new EntityId(EntityKind.Annotation, "ann-missing-2"),
                                    Reviewer = "tester",
                                    At = DateTimeOffset.UtcNow,
                                },
                            ],
                        },
                    ],
                },
            ],
        };

        var issues = ProjectIntegrity.Audit(bad);

        Assert.Contains(issues, i => i.Code == "duplicate-id");
        Assert.Contains(issues, i => i.Code == "dangling-region");
        Assert.Contains(issues, i => i.Code == "annotation-out-of-range");
        Assert.Contains(issues, i => i.Code == "dangling-retraction");
        Assert.Contains(issues, i => i.Code == "review-scope-mismatch");
        Assert.Contains(issues, i => i.Code == "duplicate-review-id");
        Assert.Contains(issues, i => i.Code == "duplicate-retraction-id");
        Assert.Contains(issues, i => i.Code == "duplicate-source-id");
        Assert.Contains(issues, i => i.Code == "revision-cycle");
        Assert.Contains(issues, i => i.Code == "dangling-fragment-surface");
        Assert.Contains("duplicate-id", ProjectIntegrity.ToCsv(issues));
        Assert.Contains("\"code\"", ProjectIntegrity.ToJson(issues));
        Assert.NotEmpty(bad.Objects[0].Surfaces[0].Lines); // audit does not rewrite
    }
}
