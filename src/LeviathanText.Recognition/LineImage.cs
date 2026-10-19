namespace LeviathanText.Recognition;

/// <summary>
/// A single-channel grayscale line image. Pixels are row-major, normalized to [0,1],
/// height 16 by default (the CRNN's input height).
/// </summary>
public sealed class LineImage
{
    public LineImage(int height, int width, float[] pixels)
    {
        if (height <= 0 || width <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Line image dimensions must be positive.");
        if (pixels.Length != height * width)
            throw new ArgumentException($"Expected {height * width} pixels, got {pixels.Length}.", nameof(pixels));

        Height = height;
        Width = width;
        Pixels = pixels;
    }

    public int Height { get; }
    public int Width { get; }
    public float[] Pixels { get; }

    public float this[int row, int column] => Pixels[row * Width + column];
}

/// <summary>Per-grapheme confidence attached to a recognised result (scale is explicit).</summary>
public sealed record GraphemeConfidence(int StartScalar, int EndScalar, LeviathanText.Contracts.Confidence Confidence);

/// <summary>
/// L0 raw recognition output (greek-transcription.md §1): untouched decoded text plus
/// the raw CTC indices and per-grapheme confidence.
/// </summary>
public sealed record RecognitionResult(
    string Text,
    IReadOnlyList<int> RawIndices,
    IReadOnlyList<GraphemeConfidence> Graphemes);

/// <summary>A recogniser that turns line images into L0 results.</summary>
public interface ICtcRecognizer
{
    IReadOnlyList<RecognitionResult> Recognize(IReadOnlyList<LineImage> lines);
}
