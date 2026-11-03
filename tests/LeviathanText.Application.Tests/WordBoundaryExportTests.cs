using LeviathanText.Application;
using LeviathanText.Contracts;

namespace LeviathanText.Application.Tests;

public sealed class WordBoundaryExportTests
{
    [Fact]
    public void Proposed_word_boundaries_export_with_source_context()
    {
        var project = SampleProjectBuilder.Build();
        var surface = project.Objects[0].Surfaces[0];
        var revision = surface.Revisions[0];
        var annotation = new Annotation
        {
            Id = new EntityId(EntityKind.Annotation, "ann-boundary"),
            Kind = AnnotationKind.WordBoundary,
            AnchoredRevisionId = revision.Id,
            Range = new ScalarRange(3, 3),
            Description = "between words",
        };
        var tagged = project with
        {
            Objects =
            [
                project.Objects[0] with
                {
                    Surfaces =
                    [
                        surface with { Annotations = [.. surface.Annotations, annotation] },
                    ],
                },
            ],
        };

        var csv = WordBoundaryCsv.Export(tagged);

        Assert.Contains("lineId,revisionId,layer,scalarPosition,before,after,description", csv);
        Assert.Contains(revision.ScopeId.ToString(), csv);
        Assert.Contains(",3,", csv);
        Assert.Contains("between words", csv);
    }
}
