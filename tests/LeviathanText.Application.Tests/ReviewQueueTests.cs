using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Recognition;

namespace LeviathanText.Application.Tests;

public sealed class ReviewQueueTests
{
    private static RecognitionPipeline.LineRecognition Line(
        string id, string text, params double[] confidences)
    {
        var spans = confidences.Select((value, index) => new GraphemeConfidence(
            index, index + 1, Confidence.Of(value, ConfidenceScale.RawModelProbability))).ToList();
        return new RecognitionPipeline.LineRecognition(
            new EntityId(EntityKind.Line, id), text, spans,
            new Checksum { Algorithm = "sha256", Digest = "x" }, "crop");
    }

    [Fact]
    public void Empty_outputs_come_first_then_lowest_confidence()
    {
        var queue = ReviewQueue.Build(
        [
            Line("ln-a", "abc", 0.9, 0.9),
            Line("ln-b", "", 0.5),
            Line("ln-c", "xy", 0.2, 0.3),
            new RecognitionPipeline.LineRecognition(
                new EntityId(EntityKind.Line, "ln-d"), "zz",
                [new GraphemeConfidence(0, 1, Confidence.None)],
                new Checksum { Algorithm = "sha256", Digest = "x" }, "crop"),
        ]);

        Assert.Equal(["ln-b", "ln-d", "ln-c", "ln-a"], queue.Select(e => e.LineId.Suffix));
        Assert.True(queue[0].EmptyOutput);
        Assert.Null(queue[1].MeanConfidence); // no confidence semantics → needs a human
    }

    [Fact]
    public void Csv_is_escaping_text_and_reports_missing_confidence_as_empty()
    {
        var queue = ReviewQueue.Build([Line("ln-a", "a,b", 0.25)]);
        var csv = ReviewQueue.ToCsv(queue);
        Assert.Contains("line_id,mean_confidence,graphemes,empty_output,text", csv);
        Assert.Contains("ln-a,0.25,1,false,\"a,b\"", csv);
    }
}
