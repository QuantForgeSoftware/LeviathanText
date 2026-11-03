using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application.Tests;

public sealed class EditionHtmlTests
{
    [Fact]
    public void Edition_html_renders_l1_l2_and_escaped_apparatus()
    {
        var project = SampleProjectBuilder.Build();
        var surface = project.Objects[0].Surfaces[0];
        var line = surface.Lines[0];
        var l1 = surface.Revisions.Single(r => r.Id == line.DiplomaticRevisionId);
        var l2 = new TextRevision
        {
            Id = new EntityId(EntityKind.TranscriptionRevision, "rev-edition-l2"),
            ScopeId = line.Id,
            Layer = TextLayer.L2Editorial,
            Author = SampleProjectBuilder.Agent,
            CreatedAt = DateTimeOffset.UtcNow,
            Text = "αβγ editorial",
        };
        var annotation = new Annotation
        {
            Id = new EntityId(EntityKind.Annotation, "ann-edition"),
            Kind = AnnotationKind.Unclear,
            AnchoredRevisionId = l1.Id,
            Range = new ScalarRange(0, 1),
            Description = "<damage>",
        };
        var modified = project with
        {
            Objects =
            [
                project.Objects[0] with
                {
                    Surfaces =
                    [
                        surface with
                        {
                            Lines = [line with { Direction = TextDirection.Rtl }],
                            Revisions = [.. surface.Revisions, l2],
                            Annotations = [annotation],
                        },
                    ],
                },
            ],
        };

        var result = EditionHtml.Export(modified, surface.Id, "Test <edition>");

        Assert.Contains("<title>Test &lt;edition&gt;</title>", result.Html);
        Assert.Contains(l1.Text, result.Html);
        Assert.Contains(l2.Text, result.Html);
        Assert.Contains("Unclear", result.Html);
        Assert.Contains("&lt;damage&gt;", result.Html);
        Assert.Contains("dir=\"rtl\"", result.Html);
        Assert.Contains("edition-html", result.Report.Format);
        Assert.Contains(result.Report.Entries, entry => entry.Category == "presentation-only");

        var projectEdition = EditionHtml.ExportProject(modified, "Whole project");
        Assert.Contains("Whole project", projectEdition.Html);
        Assert.Contains("surface-section", projectEdition.Html);
        Assert.Contains(l1.Text, projectEdition.Html);
        Assert.Contains(projectEdition.Report.Entries, entry => entry.Category == "presentation-only");
    }
}
