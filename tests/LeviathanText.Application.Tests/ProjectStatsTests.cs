using LeviathanText.Application;
using LeviathanText.Contracts;

namespace LeviathanText.Application.Tests;

public sealed class ProjectStatsTests
{
    [Fact]
    public void Counts_lines_revisions_and_annotations()
    {
        var project = SampleProjectBuilder.Build();
        var counts = ProjectStats.Build(project);

        var surface = project.Objects[0].Surfaces[0];
        Assert.Equal(1, counts.Objects);
        Assert.Equal(1, counts.Surfaces);
        Assert.Equal(surface.Lines.Count, counts.Lines);
        Assert.Equal(surface.Lines.Count, counts.TranscribedLines);
        Assert.Equal(surface.Lines.Count, counts.LinesWithL0);
        Assert.Equal(0, counts.L0Revisions);
        Assert.Equal(project.Objects[0].Surfaces[0].Annotations.Count, counts.Annotations);
        Assert.Equal(counts.Annotations, counts.AnnotationsByKind.Values.Sum());
    }

    [Fact]
    public void L0_and_flags_are_counted_when_present()
    {
        var project = SampleProjectBuilder.Build();
        var surface = project.Objects[0].Surfaces[0];
        var line = surface.Lines[0];
        var l0 = new TextRevision
        {
            Id = new EntityId(EntityKind.TranscriptionRevision, "rev-l0"),
            ScopeId = line.Id,
            Layer = TextLayer.L0RawRecognition,
            Author = new AgentRef { Id = "model:tiny", IsModel = true, ModelIdentity = "tiny@1" },
            CreatedAt = DateTimeOffset.UtcNow,
            Text = "abc",
        };
        var latin = new TextRevision
        {
            Id = new EntityId(EntityKind.TranscriptionRevision, "rev-l1-latin"),
            ScopeId = line.Id,
            Layer = TextLayer.L1Diplomatic,
            Author = SampleProjectBuilder.Agent,
            CreatedAt = DateTimeOffset.UtcNow,
            Text = "abc",
        };
        var updated = project with
        {
            Objects = [project.Objects[0] with
            {
                Surfaces = [surface with
                {
                    Revisions = [.. surface.Revisions, l0, latin],
                    Lines = [line with { DiplomaticRevisionId = latin.Id }],
                }],
            }],
        };

        var counts = ProjectStats.Build(updated);
        Assert.Equal(1, counts.L0Revisions);
        Assert.True(counts.FlaggedLines >= 1); // Latin "abc" is outside the Greek profile
    }
}
