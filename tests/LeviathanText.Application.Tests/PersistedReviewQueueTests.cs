using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application.Tests;

public sealed class PersistedReviewQueueTests
{
    [Fact]
    public void Queue_uses_latest_l0_per_line_and_orders_empty_then_low_confidence()
    {
        var project = SampleProjectBuilder.Build();
        var surface = project.Objects[0].Surfaces[0];
        var line = surface.Lines[0];

        TextRevision L0(string suffix, string text, Confidence confidence, DateTimeOffset at) => new()
        {
            Id = new EntityId(EntityKind.TranscriptionRevision, suffix),
            ScopeId = line.Id,
            Layer = TextLayer.L0RawRecognition,
            Author = new AgentRef { Id = "model:tiny", IsModel = true },
            CreatedAt = at,
            Text = text,
            Confidence = confidence,
        };

        var older = L0("l0-old", "old", Confidence.Of(0.1, ConfidenceScale.RawModelProbability), DateTimeOffset.UtcNow.AddMinutes(-5));
        var latest = L0("l0-new", "", Confidence.None, DateTimeOffset.UtcNow);
        var mutated = project with
        {
            Objects = [project.Objects[0] with
            {
                Surfaces = [surface with { Revisions = [.. surface.Revisions, older, latest] }],
            }],
        };

        var queue = PersistedReviewQueue.Build(mutated);
        var entry = Assert.Single(queue);
        Assert.Equal(latest.Id, entry.RevisionId); // latest wins, not the confident older one
        Assert.True(entry.EmptyOutput);
        Assert.Null(entry.MeanConfidence);
    }
}
