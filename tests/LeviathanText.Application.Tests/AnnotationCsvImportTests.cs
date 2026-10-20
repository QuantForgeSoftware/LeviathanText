using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application.Tests;

public sealed class AnnotationCsvImportTests
{
    [Fact]
    public void Valid_rows_are_applied_and_invalid_rows_reported()
    {
        var store = new InMemoryStore();
        var project = SampleProjectBuilder.Build();
        store.SaveProject(project);
        var lineId = project.Objects[0].Surfaces[0].Lines[0].Id.ToString();
        var csv = $"""
            line_id,kind,start,end,gap_extent,alternative_text,description
            {lineId},Unclear,1,3,,,faded
            {lineId},Lost,4,4,,,gap at position
            {lineId},AlternativeReading,2,4,,αβγ,competing reading
            {lineId},NotARealKind,0,1,,,
            {lineId},Unclear,0,999,,,out of range
            ln-missing,Unclear,0,1,,,
            """;

        var result = AnnotationCsvImport.Import(project, csv, new TranscriptionService(store), store);

        Assert.Equal(3, result.Applied);
        Assert.Equal(3, result.Skipped);
        var surface = store.LoadProject(project.Id)!.Objects[0].Surfaces[0];
        Assert.Equal(project.Objects[0].Surfaces[0].Annotations.Count + 3, surface.Annotations.Count);
        Assert.Contains(surface.Annotations, a => a.Kind == AnnotationKind.Lost && a.GapExtent == GapExtent.Unknown);
        Assert.Contains(surface.Annotations, a => a.Kind == AnnotationKind.AlternativeReading && a.AlternativeText == "αβγ");
    }

    [Fact]
    public void Missing_required_headers_is_rejected()
    {
        var store = new InMemoryStore();
        var project = SampleProjectBuilder.Build();
        var ex = Assert.Throws<LeviathanException>(() => AnnotationCsvImport.Import(
            project, "line_id,kind\nx,y\n", new TranscriptionService(store), store));
        Assert.Equal(ErrorCode.StageContractViolation, ex.Error.Code);
    }
}
