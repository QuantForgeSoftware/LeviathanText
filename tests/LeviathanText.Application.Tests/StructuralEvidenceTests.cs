using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application.Tests;

public sealed class StructuralEvidenceTests
{
    public static IEnumerable<object[]> EvidenceCases =>
        from operation in new[] { "delete", "split", "merge-first", "merge-second" }
        from layer in Enum.GetValues<TextLayer>()
        select new object[] { operation, layer };

    private static (InMemoryStore Store, NativeProject Project) Seed(TextLayer? layer = null, int evidenceLine = 0,
        bool pointerOnly = false, bool reviewOnly = false, int? unplacedLine = null)
    {
        var project = SampleProjectBuilder.Build();
        var obj = project.Objects[0];
        var surface = obj.Surfaces[0];
        var original = surface.Lines[0] with
        {
            DiplomaticRevisionId = null,
            Polygon = [new(0, 0), new(40, 0), new(40, 40), new(0, 40)],
            Baseline = [new(0, 38), new(40, 38)],
        };
        NativeLine[] lines = [original, original with
        {
            Id = new(EntityKind.Line, "second"),
            Polygon = original.Polygon.Select(p => new PointD(p.X, p.Y + 50)).ToArray(),
            Baseline = original.Baseline.Select(p => new PointD(p.X, p.Y + 50)).ToArray(),
        }];
        var revisionId = new EntityId(EntityKind.TranscriptionRevision, "evidence");
        if (pointerOnly) lines[evidenceLine] = lines[evidenceLine] with { DiplomaticRevisionId = revisionId };
        if (unplacedLine is { } unplaced) lines[unplaced] = lines[unplaced] with { Polygon = [], Baseline = [] };
        var revisions = layer is { } value ? new[] { new TextRevision
        {
            Id = revisionId, ScopeId = lines[evidenceLine].Id, Layer = value, Text = "αβ",
            Author = SampleProjectBuilder.Agent, CreatedAt = DateTimeOffset.UnixEpoch,
        } } : [];
        var annotations = layer is not null ? new[] { new Annotation
        {
            Id = new(EntityKind.Annotation, "evidence"), Kind = AnnotationKind.Unclear,
            AnchoredRevisionId = revisionId, Range = new(0, 1),
        } } : [];
        project = project with { Objects = [obj with { Surfaces = [surface with
        {
            Lines = lines, Revisions = revisions, Annotations = annotations, GeometryEdits = [],
            ReviewDecisions = reviewOnly ? [new ReviewDecision
            {
                Id = "review", LineId = lines[evidenceLine].Id, RevisionId = revisionId,
                Reviewer = "fixture-reviewer", Outcome = ReviewOutcome.Accept, At = DateTimeOffset.UnixEpoch,
            }] : [],
        }] }] };
        var store = new InMemoryStore();
        store.SaveProject(project);
        return (store, store.LoadProject(project.Id)!);
    }

    private static void Apply(GeometryService service, NativeProject project, string operation)
    {
        var lines = project.Objects[0].Surfaces[0].Lines;
        if (operation == "delete") service.DeleteLine(project, lines[0].Id);
        else if (operation == "split")
            service.SplitLine(project, lines[0].Id, (lines[0].Polygon.Min(p => p.Y) + lines[0].Polygon.Max(p => p.Y)) / 2);
        else service.MergeLines(project, lines[0].Id, lines[1].Id);
    }

    [Theory]
    [MemberData(nameof(EvidenceCases))]
    public void Every_scoped_revision_layer_and_its_annotations_prevent_structural_repartition(string operation, TextLayer layer)
    {
        var (store, project) = Seed(layer, operation == "merge-second" ? 1 : 0);
        Assert.Empty(ProjectIntegrity.Audit(project));

        var error = Assert.Throws<LeviathanException>(() => Apply(new GeometryService(store), project, operation));

        Assert.Equal(ErrorCode.RevisionConflict, error.Error.Code);
        var saved = store.LoadProject(project.Id)!;
        Assert.Equal(NativeJson.Save(project), NativeJson.Save(saved));
        Assert.Empty(ProjectIntegrity.Audit(saved));
    }

    [Theory]
    [InlineData("delete", false)]
    [InlineData("split", false)]
    [InlineData("merge-first", false)]
    [InlineData("merge-second", false)]
    [InlineData("delete", true)]
    [InlineData("split", true)]
    [InlineData("merge-first", true)]
    [InlineData("merge-second", true)]
    public void Unresolved_pointer_or_review_evidence_is_retained_without_new_integrity_damage(string operation, bool review)
    {
        var (store, project) = Seed(evidenceLine: operation == "merge-second" ? 1 : 0, pointerOnly: !review, reviewOnly: review);
        var beforeIssues = ProjectIntegrity.ToJson(ProjectIntegrity.Audit(project));
        var error = Assert.Throws<LeviathanException>(() => Apply(new GeometryService(store), project, operation));
        Assert.Equal(ErrorCode.RevisionConflict, error.Error.Code);
        var saved = store.LoadProject(project.Id)!;
        Assert.Equal(NativeJson.Save(project), NativeJson.Save(saved));
        Assert.Equal(beforeIssues, ProjectIntegrity.ToJson(ProjectIntegrity.Audit(saved)));
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    public void Unplaced_geometry_returns_a_contract_error_without_mutation(bool merge, int unplaced)
    {
        var (store, project) = Seed(unplacedLine: unplaced);
        var lines = project.Objects[0].Surfaces[0].Lines;
        var error = Assert.Throws<LeviathanException>(() =>
        {
            if (merge) new GeometryService(store).MergeLines(project, lines[0].Id, lines[1].Id);
            else new GeometryService(store).SplitLine(project, lines[0].Id, 25);
        });
        Assert.Equal(ErrorCode.StageContractViolation, error.Error.Code);
        Assert.Equal(NativeJson.Save(project), NativeJson.Save(store.LoadProject(project.Id)!));
        Assert.Empty(ProjectIntegrity.Audit(store.LoadProject(project.Id)!));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Nonfinite_split_position_is_a_contract_error_without_mutation(double y)
    {
        var (store, project) = Seed();
        var line = project.Objects[0].Surfaces[0].Lines[0];
        var error = Assert.Throws<LeviathanException>(() => new GeometryService(store).SplitLine(project, line.Id, y));
        Assert.Equal(ErrorCode.StageContractViolation, error.Error.Code);
        Assert.Equal(NativeJson.Save(project), NativeJson.Save(store.LoadProject(project.Id)!));
    }

    [Theory]
    [InlineData("delete")]
    [InlineData("split")]
    [InlineData("merge-first")]
    public void Evidence_free_structural_edits_remain_supported_with_no_dangling_records(string operation)
    {
        var (store, project) = Seed();
        Apply(new GeometryService(store), project, operation);
        var saved = store.LoadProject(project.Id)!;
        Assert.Equal(project.Version + 1, saved.Version);
        Assert.Single(saved.Objects[0].Surfaces[0].GeometryEdits);
        Assert.Empty(ProjectIntegrity.Audit(saved));
    }

    [Fact]
    public void Deliberate_deletion_of_an_unplaced_evidence_free_line_remains_supported()
    {
        var (store, project) = Seed(unplacedLine: 0);
        var lineId = project.Objects[0].Surfaces[0].Lines[0].Id;
        new GeometryService(store).DeleteLine(project, lineId);
        var saved = store.LoadProject(project.Id)!;
        Assert.DoesNotContain(saved.Objects[0].Surfaces[0].Lines, line => line.Id == lineId);
        Assert.Equal(project.Version + 1, saved.Version);
        Assert.Empty(ProjectIntegrity.Audit(saved));
    }
}
