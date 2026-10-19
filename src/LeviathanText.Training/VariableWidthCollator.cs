using LeviathanText.Recognition;
using static TorchSharp.torch;

namespace LeviathanText.Training;

/// <summary>A line image with its ground-truth text (an L1 diplomatic string).</summary>
public sealed record LabeledLine(string Text, LineImage Image);

/// <summary>
/// One collated variable-width batch: padded images, per-sample input lengths, the
/// concatenated target indices and per-sample target lengths. Shapes are exactly what
/// <c>ctc_loss</c> expects.
/// </summary>
public sealed class CtcBatch
{
    public required Tensor Images { get; init; }
    public required Tensor InputLengths { get; init; }
    public required Tensor Targets { get; init; }
    public required Tensor TargetLengths { get; init; }
    public required IReadOnlyList<LabeledLine> Lines { get; init; }
    public required IReadOnlyList<int[]> TargetIndices { get; init; }
}

/// <summary>Pads a variable-width batch and encodes each ground-truth text with a label set.</summary>
public static class VariableWidthCollator
{
    public static CtcBatch Collate(
        IReadOnlyList<LabeledLine> lines,
        LabelSet labels,
        int widthDownsampleFactor = CrnnModel.DefaultWidthDownsampleFactor,
        int inputHeight = CrnnModel.InputHeight)
    {
        if (lines.Count == 0) throw new ArgumentException("Cannot collate zero lines.", nameof(lines));

        var (images, inputLengths) = LineBatcher.ToTensor([.. lines.Select(l => l.Image)], widthDownsampleFactor, inputHeight);
        var targetIndices = lines.Select(l => labels.EncodeText(l.Text)).ToList();

        var flat = targetIndices.SelectMany(s => s).Select(i => (long)i).ToArray();
        var targets = tensor(flat, new long[] { flat.Length });
        var targetLengths = tensor(targetIndices.Select(s => (long)s.Length).ToArray(), new long[] { targetIndices.Count });
        var inputLengthTensor = tensor(inputLengths.Select(i => (long)i).ToArray(), new long[] { inputLengths.Length });

        return new CtcBatch
        {
            Images = images,
            InputLengths = inputLengthTensor,
            Targets = targets,
            TargetLengths = targetLengths,
            Lines = lines,
            TargetIndices = targetIndices,
        };
    }
}
