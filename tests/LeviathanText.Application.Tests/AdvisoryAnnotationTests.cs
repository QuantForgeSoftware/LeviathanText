using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application.Tests;

public sealed class AdvisoryAnnotationTests
{
    [Fact]
    public void Advisory_results_can_be_stored_as_annotations_without_rewriting_text()
    {
        var store = new InMemoryStore();
        var project = SampleProjectBuilder.Build();
        store.SaveProject(project);
        var services = new TranscriptionService(store);
        var lineId = project.Objects[0].Surfaces[0].Lines[0].Id;

        var (updated, _, annotation) = services.AddAnnotation(project, lineId, new Annotation
        {
            Id = new EntityId(EntityKind.Annotation, "ann-translation"),
            Kind = AnnotationKind.Translation,
            AnchoredRevisionId = default,
            Range = new ScalarRange(0, 1),
            AlternativeText = "Lord",
            Description = "advisory translation",
        });

        Assert.Equal(AnnotationKind.Translation, annotation.Kind);
        Assert.Equal("Lord", annotation.AlternativeText);
        Assert.Contains(services.AnnotationsFor(updated, lineId), a => a.Id == annotation.Id);
        var reloaded = store.LoadProject(project.Id)!;
        Assert.Contains(reloaded.Objects[0].Surfaces[0].Annotations,
            a => a.Id == annotation.Id && a.AlternativeText == "Lord");
        Assert.Empty(ProjectIntegrity.Audit(reloaded));
    }
}
