using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application.Tests;

public sealed class TrainingDataExportTests
{
    private static NativeProject MutateSurface(NativeProject project, Func<NativeSurface, NativeSurface> mutate) =>
        project with
        {
            Objects =
            [
                project.Objects[0] with
                {
                    Surfaces = [mutate(project.Objects[0].Surfaces[0])],
                },
            ],
        };

    private static NativeSurface Surface(NativeProject project) => project.Objects[0].Surfaces[0];
    private static NativeLine Line(NativeProject project) => Surface(project).Lines[0];
    private static EntityId RevisionId(NativeProject project) => Line(project).DiplomaticRevisionId!.Value;

    [Fact]
    public void Clean_line_is_exported_with_full_exact_span()
    {
        var project = SampleProjectBuilder.Build();
        var doc = TrainingDataExport.Export(project);

        var line = doc.Lines.Single(l => l.LineId == Line(project).Id);
        Assert.Equal("clean", line.Status);
        Assert.Equal("α\u0305λ\u0305α\u0305νθρωπ", line.Text);
        var exact = Assert.Single(line.ExactSpans);
        Assert.Equal(new ScalarRange(0, 11), exact); // α+overbar ×3 (6 scalars) + ν θ ρ ω π (5)
        Assert.Empty(line.UncertainSpans);
    }

    [Fact]
    public void Uncertainty_spans_split_exact_regions()
    {
        var project = SampleProjectBuilder.Build();
        var mutation = MutateSurface(project, surface => surface with
        {
            Annotations =
            [
                .. surface.Annotations,
                new Annotation
                {
                    Id = new EntityId(EntityKind.Annotation, "u1"),
                    Kind = AnnotationKind.Unclear,
                    AnchoredRevisionId = RevisionId(project),
                    Range = new ScalarRange(2, 5),
                },
            ],
        });

        var line = TrainingDataExport.Export(mutation).Lines.Single(l => l.LineId == Line(project).Id);
        Assert.Equal("uncertain", line.Status);
        Assert.Equal([new ScalarRange(2, 5)], line.UncertainSpans);
        Assert.Equal([new ScalarRange(0, 2), new ScalarRange(5, 11)], line.ExactSpans);
        Assert.Contains("Unclear", line.AnnotationKinds);
    }

    [Fact]
    public void Editorial_supply_excludes_the_line_entirely()
    {
        var project = SampleProjectBuilder.Build();
        var mutation = MutateSurface(project, surface => surface with
        {
            Annotations =
            [
                .. surface.Annotations,
                new Annotation
                {
                    Id = new EntityId(EntityKind.Annotation, "s1"),
                    Kind = AnnotationKind.SuppliedEditorial,
                    AnchoredRevisionId = RevisionId(project),
                    Range = new ScalarRange(0, 1),
                },
            ],
        });

        var line = TrainingDataExport.Export(mutation).Lines.Single(l => l.LineId == Line(project).Id);
        Assert.Equal("excluded", line.Status);
        Assert.Equal("editorial-supply-present", line.ExclusionReason);
        Assert.Empty(line.ExactSpans);
    }

    [Fact]
    public void L2_editorial_text_is_never_used_as_the_label()
    {
        var project = SampleProjectBuilder.Build();
        var mutation = MutateSurface(project, surface => surface with
        {
            Revisions =
            [
                .. surface.Revisions,
                new TextRevision
                {
                    Id = new EntityId(EntityKind.TranscriptionRevision, "r2editorial"),
                    ScopeId = Line(project).Id,
                    Layer = TextLayer.L2Editorial,
                    ParentRevisionId = RevisionId(project),
                    Author = SampleProjectBuilder.Agent,
                    CreatedAt = DateTimeOffset.UtcNow,
                    Text = "ἀλλά", // editorial expansion must not become the training label
                },
            ],
        });

        var line = TrainingDataExport.Export(mutation).Lines.Single(l => l.LineId == Line(project).Id);
        Assert.Equal("α\u0305λ\u0305α\u0305νθρωπ", line.Text); // L1 stays the label
        Assert.DoesNotContain("ἀλλά", line.Text);
    }

    [Fact]
    public void Retracted_uncertainty_no_longer_affects_the_training_label()
    {
        var project = SampleProjectBuilder.Build();
        var revisionId = RevisionId(project);
        var annotation = new Annotation
        {
            Id = new EntityId(EntityKind.Annotation, "u-retracted"),
            Kind = AnnotationKind.Unclear,
            AnchoredRevisionId = revisionId,
            Range = new ScalarRange(2, 5),
        };
        var mutation = MutateSurface(project, surface => surface with
        {
            Annotations = [.. surface.Annotations, annotation],
            AnnotationRetractions =
            [
                new AnnotationRetraction
                {
                    Id = "ar-1",
                    AnnotationId = annotation.Id,
                    Reviewer = "reviewer-a",
                    At = DateTimeOffset.UtcNow,
                },
            ],
        });

        var line = TrainingDataExport.Export(mutation).Lines.Single(l => l.LineId == Line(project).Id);
        Assert.Equal("clean", line.Status);
        Assert.Empty(line.UncertainSpans);
        Assert.Empty(line.AnnotationKinds);
    }

    [Fact]
    public void Untranscribed_line_is_excluded_with_reason()
    {
        var project = SampleProjectBuilder.Build();
        var mutation = MutateSurface(project, surface => surface with
        {
            Lines = [surface.Lines[0] with { DiplomaticRevisionId = null }],
        });

        var line = TrainingDataExport.Export(mutation).Lines.Single(l => l.LineId == Line(project).Id);
        Assert.Equal("excluded", line.Status);
        Assert.Equal("not-yet-transcribed", line.ExclusionReason);
    }

    [Fact]
    public void Export_document_serializes_with_contract_conventions()
    {
        var json = TrainingDataExport.ToJson(TrainingDataExport.Export(SampleProjectBuilder.Build()));
        Assert.Contains("\"contractVersion\":\"C0-0.4-draft\"", json);
        Assert.Contains("\"leviathantext-training-lines\"", json);
        Assert.Contains("\"L1-visible-text-only", json);
    }

    [Fact]
    public void MergeRanges_combines_overlaps_and_adjacency()
    {
        var merged = TrainingDataExport.MergeRanges(
        [
            new ScalarRange(0, 3), new ScalarRange(2, 5), new ScalarRange(5, 6), new ScalarRange(9, 10),
        ]);
        Assert.Equal([new ScalarRange(0, 6), new ScalarRange(9, 10)], merged);
    }

    [Fact]
    public void Complement_returns_gaps_and_edges()
    {
        Assert.Equal([new ScalarRange(0, 2), new ScalarRange(4, 8)],
            TrainingDataExport.Complement([new ScalarRange(2, 4)], 8));
        Assert.Equal([new ScalarRange(0, 5)], TrainingDataExport.Complement([], 5));
        Assert.Empty(TrainingDataExport.Complement([new ScalarRange(0, 5)], 5));
    }
}
