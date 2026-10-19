using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application.Tests;

public sealed class AnnotationCsvTests
{
    [Fact]
    public void Csv_lists_annotations_with_scalar_ranges_and_escaped_fields()
    {
        var project = SampleProjectBuilder.Build();
        var surface = project.Objects[0].Surfaces[0];
        var revisionId = surface.Lines[0].DiplomaticRevisionId!.Value;
        var mutated = project with
        {
            Objects = [project.Objects[0] with
            {
                Surfaces = [surface with
                {
                    Annotations =
                    [
                        new Annotation
                        {
                            Id = new EntityId(EntityKind.Annotation, "ann-a"),
                            Kind = AnnotationKind.Unclear,
                            AnchoredRevisionId = revisionId,
                            Range = new ScalarRange(1, 3),
                            Description = "faded, \"maybe\"",
                        },
                    ],
                }],
            }],
        };

        var csv = AnnotationCsv.Export(mutated);
        var rows = csv.Split('\n');
        Assert.StartsWith("annotation_id,surface_id,line_id,kind,start,end,gap_extent", rows[0]);
        var row = rows.First(r => r.Contains("ann-a"));
        Assert.Contains("Unclear", row);
        Assert.Contains(",1,3,", row);
        Assert.Contains("\"faded, \"\"maybe\"\"\"", row);
        Assert.Contains(surface.Lines[0].Id.ToString(), row);
    }
}
