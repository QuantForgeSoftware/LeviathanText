using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using UglyToad.PdfPig;

namespace LeviathanText.Application.Tests;

public sealed class SearchablePdfProjectTests
{
    [Fact]
    public void Project_pdf_pages_each_surface()
    {
        var store = new InMemoryStore();
        var project = SampleProjectBuilder.Build();
        var surface = project.Objects[0].Surfaces[0];
        var second = surface with { Id = new EntityId(EntityKind.Surface, "surf-second") };
        var modified = project with
        {
            Objects = [project.Objects[0] with { Surfaces = [surface, second] }],
        };
        var interchange = new InterchangeService(new ProjectService(store));

        var (pdf, report) = interchange.ExportSearchablePdfProject(modified, "project");

        using var document = PdfDocument.Open(pdf);
        Assert.Equal(2, document.NumberOfPages);
        Assert.Equal("searchable-pdf", report.Format);
        Assert.False(string.IsNullOrWhiteSpace(document.GetPage(1).Text));
    }
}
