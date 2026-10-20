using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application.Tests;

public sealed class LineTextImportTests
{
    [Fact]
    public void Applies_rows_as_l1_revisions_and_reports_unknown_lines()
    {
        var store = new InMemoryStore();
        var project = SampleProjectBuilder.Build();
        store.SaveProject(project);
        var lineId = project.Objects[0].Surfaces[0].Lines[0].Id.ToString();
        var original = project.Objects[0].Surfaces[0].Lines[0].DiplomaticRevisionId;
        var csv = $"""
            line_id,text,note
            {lineId},"διορθωμένο, κείμενο","reviewed"
            ln-does-not-exist,αβγ,
            {lineId},,
            """;

        var result = LineTextImport.Import(project, csv, TextLayer.L1Diplomatic, "reviewer-a",
            new TranscriptionService(store), store);

        Assert.Equal(1, result.Applied);
        Assert.Equal(2, result.Skipped);
        var reloaded = store.LoadProject(project.Id)!.Objects[0].Surfaces[0];
        var line = reloaded.Lines[0];
        Assert.NotEqual(original, line.DiplomaticRevisionId);
        var revision = reloaded.Revisions.Single(r => r.Id == line.DiplomaticRevisionId);
        Assert.Equal("διορθωμένο, κείμενο", revision.Text);
        Assert.Equal(original, revision.ParentRevisionId);
        Assert.Equal("reviewer-a", revision.Author.Id);
    }

    [Fact]
    public void Csv_parser_handles_quotes_commas_and_newlines()
    {
        var rows = LineTextImport.ParseCsv("line_id,text\nln-1,\"a,\"\"b\"\"\nc\"\n");
        Assert.Equal(2, rows.Count);
        Assert.Equal("a,\"b\"\nc", rows[1][1]);
    }

    [Fact]
    public void Missing_required_columns_is_rejected()
    {
        var store = new InMemoryStore();
        var project = SampleProjectBuilder.Build();
        var ex = Assert.Throws<LeviathanException>(() => LineTextImport.Import(
            project, "id,value\nln1,x\n", TextLayer.L1Diplomatic, "a",
            new TranscriptionService(store), store));
        Assert.Equal(ErrorCode.StageContractViolation, ex.Error.Code);
    }
}
