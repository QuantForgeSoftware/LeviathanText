using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application.Tests;

public sealed class LineCsvTests
{
    [Fact]
    public void Csv_contains_l1_rows_and_escapes_special_characters()
    {
        var project = SampleProjectBuilder.Build();
        var surface = project.Objects[0].Surfaces[0];
        var line = surface.Lines[0];
        var revision = surface.Revisions.Single(r => r.Id == line.DiplomaticRevisionId);
        var mutated = project with
        {
            Objects = [project.Objects[0] with
            {
                Surfaces = [surface with
                {
                    Revisions = [revision with { Text = "a,\"b\"\nγ" }],
                }],
            }],
        };

        var csv = LineCsv.Export(mutated);
        var lines = csv.Split('\n');
        Assert.StartsWith("line_id,surface_id,region_id,layer,revision_id,annotation_count,scalar_length,text", lines[0]);
        Assert.Contains("L1Diplomatic", csv);
        Assert.Contains("\"a,\"\"b\"\"", csv); // quotes doubled, field quoted
        Assert.Contains(line.Id.ToString(), csv);
    }

    [Fact]
    public void Untranscribed_lines_are_present_with_empty_text()
    {
        var project = SampleProjectBuilder.Build();
        var surface = project.Objects[0].Surfaces[0];
        var extra = new NativeLine
        {
            Id = new EntityId(EntityKind.Line, "ln-empty"),
            RegionId = surface.Regions[0].Id,
            FrameRef = surface.Captures[0].Id,
        };
        var mutated = project with
        {
            Objects = [project.Objects[0] with
            {
                Surfaces = [surface with { Lines = [.. surface.Lines, extra] }],
            }],
        };

        var csv = LineCsv.Export(mutated);
        Assert.Contains("ln-empty", csv);
        var row = csv.Split('\n').Single(l => l.Contains("ln-empty"));
        Assert.EndsWith(",", row); // empty text after the last comma
    }
}
