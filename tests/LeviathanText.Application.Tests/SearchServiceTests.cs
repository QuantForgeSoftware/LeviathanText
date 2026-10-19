using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application.Tests;

public sealed class SearchServiceTests
{
    private static NativeProject ProjectWith(params (string LineId, string Text)[] lines)
    {
        var baseProject = SampleProjectBuilder.Build();
        var surface = baseProject.Objects[0].Surfaces[0];
        var revisions = new List<TextRevision>();
        var nativeLines = new List<NativeLine>();
        foreach (var (lineId, text) in lines)
        {
            var id = new EntityId(EntityKind.Line, lineId);
            var revision = new TextRevision
            {
                Id = new EntityId(EntityKind.TranscriptionRevision, "rev-" + lineId),
                ScopeId = id,
                Layer = TextLayer.L1Diplomatic,
                Author = SampleProjectBuilder.Agent,
                CreatedAt = DateTimeOffset.UtcNow,
                Text = text,
            };
            revisions.Add(revision);
            nativeLines.Add(new NativeLine
            {
                Id = id,
                RegionId = surface.Regions[0].Id,
                FrameRef = surface.Captures[0].Id,
                DiplomaticRevisionId = revision.Id,
            });
        }
        return baseProject with
        {
            Objects = [baseProject.Objects[0] with
            {
                Surfaces = [surface with { Lines = nativeLines, Revisions = revisions }],
            }],
        };
    }

    [Fact]
    public void Literal_search_reports_scalar_ranges()
    {
        var project = ProjectWith(("ln1", "α\u0305λ\u0305α\u0305νθρωπ"), ("ln2", "καὶ ἐγένετο φῶς"));
        var result = SearchService.Search(project, "νθρωπ");
        var hit = Assert.Single(result.Hits);
        Assert.Equal(new ScalarRange(6, 11), hit.Match); // overbars count as scalars
        Assert.Equal(TextLayer.L1Diplomatic, hit.Layer);
    }

    [Fact]
    public void Base_search_ignores_marks_and_case_and_maps_back_to_stored_offsets()
    {
        var project = ProjectWith(("ln1", "ἄνθρωπος"));
        var result = SearchService.Search(project, "ΑΝΘΡ", "base");
        var hit = Assert.Single(result.Hits);
        Assert.Equal(0, hit.Match.Start);
        Assert.True(hit.Match.End >= 3);
        // The stored text is untouched (search never rewrites).
        Assert.Equal("ἄνθρωπος", project.Objects[0].Surfaces[0].Revisions[0].Text);
    }

    [Fact]
    public void Literal_search_does_not_match_across_marks()
    {
        var project = ProjectWith(("ln1", "ἄνθρωπος"));
        Assert.Empty(SearchService.Search(project, "ανθρωπ").Hits);
    }

    [Fact]
    public void Invalid_or_empty_queries_are_rejected()
    {
        var project = ProjectWith(("ln1", "αβγ"));
        Assert.Throws<LeviathanException>(() => SearchService.Search(project, "  "));
        var ex = Assert.Throws<LeviathanException>(() => SearchService.Search(project, "a", "regex"));
        Assert.Equal(ErrorCode.StageContractViolation, ex.Error.Code);
    }
}
