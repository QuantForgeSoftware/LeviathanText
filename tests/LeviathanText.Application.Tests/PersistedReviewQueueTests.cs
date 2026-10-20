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
        var withSpans = L0("l0-spans", "αβγ", Confidence.Of(0.6, ConfidenceScale.RawModelProbability), DateTimeOffset.UtcNow.AddMinutes(1)) with
        {
            GraphemeConfidences =
            [
                new GraphemeConfidenceSpan { Range = new ScalarRange(0, 1), Confidence = Confidence.Of(0.9, ConfidenceScale.RawModelProbability) },
                new GraphemeConfidenceSpan { Range = new ScalarRange(1, 2), Confidence = Confidence.Of(0.2, ConfidenceScale.RawModelProbability) },
                new GraphemeConfidenceSpan { Range = new ScalarRange(2, 3), Confidence = Confidence.Of(0.7, ConfidenceScale.RawModelProbability) },
            ],
        };
        var mutated = project with
        {
            Objects = [project.Objects[0] with
            {
                Surfaces = [surface with { Revisions = [.. surface.Revisions, older, latest, withSpans] }],
            }],
        };

        var queue = PersistedReviewQueue.Build(mutated);
        var entry = Assert.Single(queue);
        Assert.Equal(withSpans.Id, entry.RevisionId); // latest revision wins
        Assert.False(entry.EmptyOutput);
        Assert.Equal(0.6, entry.MeanConfidence!.Value, 3);
        Assert.Equal(new ScalarRange(1, 2), entry.WorstSpan); // lowest-confidence grapheme
    }
}
