using static TorchSharp.torch;

namespace LeviathanText.Recognition;

/// <summary>
/// Pads a set of variable-width line images into one batch tensor, zero-filling the
/// right margin. Recorded input lengths let the CTC loss ignore padding, which is what
/// makes variable-width batching correct rather than merely convenient.
/// </summary>
public static class LineBatcher
{
    public static (Tensor Images, int[] InputLengths) ToTensor(
        IReadOnlyList<LineImage> lines,
        int widthDownsampleFactor = CrnnModel.DefaultWidthDownsampleFactor,
        int inputHeight = CrnnModel.InputHeight)
    {
        if (lines.Count == 0) throw new ArgumentException("Cannot build a batch from zero lines.", nameof(lines));

        int height = inputHeight;
        foreach (var line in lines)
            if (line.Height != height)
                throw new ArgumentException($"Line height {line.Height} does not match the model input height {height}.", nameof(lines));

        int maxWidth = 0;
        foreach (var line in lines) maxWidth = Math.Max(maxWidth, line.Width);

        var data = new float[lines.Count * height * maxWidth];
        var lengths = new int[lines.Count];
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            int batchOffset = i * height * maxWidth;
            for (int row = 0; row < height; row++)
                Array.Copy(line.Pixels, row * line.Width, data, batchOffset + row * maxWidth, line.Width);
            lengths[i] = CrnnModel.TimeStepsFor(line.Width, widthDownsampleFactor);
        }

        var images = tensor(data, new long[] { lines.Count, 1, height, maxWidth });
        return (images, lengths);
    }
}
