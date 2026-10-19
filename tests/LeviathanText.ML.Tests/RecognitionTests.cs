using LeviathanText.Contracts;
using LeviathanText.Recognition;
using LeviathanText.Training;
using Xunit;

namespace LeviathanText.ML.Tests;

public class RecognitionTests
{
    [Fact]
    public void LabelSet_EncodeDecode_RoundTripsIncludingMultiScalarGraphemes()
    {
        var labels = new LabelSet(new[] { "α", "β", "ά" });
        var indices = labels.EncodeText("αβά");
        Assert.Equal("αβά", labels.Decode(indices));
        Assert.Equal(4, labels.NumClasses); // blank + 3 labels
    }

    [Fact]
    public void LabelSet_UnknownGrapheme_FailsLoudly()
    {
        var labels = new LabelSet(new[] { "α", "β" });
        var ex = Assert.Throws<LeviathanException>(() => labels.EncodeText("ω"));
        Assert.Equal(ErrorCode.UnknownLabel, ex.Error.Code);
    }

    [Fact]
    public void CtcDecoder_CollapsesRepeatsAndDropsBlanks()
    {
        // blank=0; sequence keeps the second '1' because a blank separates the repeats.
        var decoded = CtcDecoder.Greedy(new[] { 0, 1, 1, 0, 1, 2, 2 });
        Assert.Equal(new[] { 1, 1, 2 }, decoded);
    }

    [Fact]
    public void Renderer_ProducesExpectedShape()
    {
        var image = SyntheticGraphemeRenderer.Render("αβ");
        Assert.Equal(CrnnModel.InputHeight, image.Height);
        Assert.Equal(2 * (SyntheticGraphemeRenderer.SlotWidth + SyntheticGraphemeRenderer.Gap), image.Width);
        Assert.Equal(image.Height * image.Width, image.Pixels.Length);
    }

    [Fact]
    public void Collator_PadsVariableWidthBatchAndEncodesTargets()
    {
        var labels = new LabelSet(new[] { "α", "β" });
        var lines = new List<LabeledLine>
        {
            new("αα", SyntheticGraphemeRenderer.Render("αα")),
            new("β", SyntheticGraphemeRenderer.Render("β")),
        };

        var batch = VariableWidthCollator.Collate(lines, labels);

        Assert.Equal(2, (int)batch.Images.shape[0]);
        Assert.Equal(1, (int)batch.Images.shape[1]);
        Assert.Equal(CrnnModel.InputHeight, (int)batch.Images.shape[2]);
        Assert.Equal(2 * (SyntheticGraphemeRenderer.SlotWidth + SyntheticGraphemeRenderer.Gap), (int)batch.Images.shape[3]);
        Assert.Equal(3, (int)batch.TargetLengths.sum().item<long>()); // 2 + 1 target indices
    }

    [Fact]
    public void CrnnModel_ForwardProducesTimeByBatchByClassLogProbs()
    {
        var labels = new LabelSet(new[] { "α", "β", "γ", "δ" });
        var model = new CrnnModel(labels.NumClasses);
        var lines = new[] { SyntheticGraphemeRenderer.Render("αβ"), SyntheticGraphemeRenderer.Render("γδα") };

        var (images, _) = LineBatcher.ToTensor(lines);
        var logProbs = model.forward(images);

        Assert.Equal(3, logProbs.dim());
        Assert.Equal(2, (int)logProbs.shape[1]);
        Assert.Equal(labels.NumClasses, (int)logProbs.shape[2]);
        Assert.Equal(CrnnModel.TimeStepsFor(images.shape[3] == 0 ? 0 : (int)images.shape[3]), (int)logProbs.shape[0]);
    }

    [Fact]
    public void Recognizer_ClassCountMismatch_ReportsModelIncompatible()
    {
        var model = new CrnnModel(5);
        var labels = new LabelSet(new[] { "α", "β", "γ" }); // 4 classes, mismatched
        var recognizer = new TorchLineRecognizer(model, labels);

        var ex = Assert.Throws<LeviathanException>(() => recognizer.Recognize(new[] { SyntheticGraphemeRenderer.Render("αβ") }));
        Assert.Equal(ErrorCode.ModelIncompatible, ex.Error.Code);
    }
}
