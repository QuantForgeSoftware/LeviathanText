using System.Text.Json.Serialization;

namespace LeviathanText.Recognition;

/// <summary>
/// A single-channel grayscale line image. Pixels are row-major, normalized to [0,1],
/// height 16 by default (the CRNN's input height).
/// </summary>
public sealed class LineImage
{
    public LineImage(int height, int width, float[] pixels) : this(height, width, pixels, backgroundValue: 0)
    {
    }

    [JsonConstructor]
    public LineImage(int height, int width, float[] pixels, float backgroundValue = 0)
    {
        if (height <= 0 || width <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Line image dimensions must be positive.");
        if (pixels.Length != height * width)
            throw new ArgumentException($"Expected {height * width} pixels, got {pixels.Length}.", nameof(pixels));
        if (!float.IsFinite(backgroundValue) || backgroundValue is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(backgroundValue), "Background must be finite and normalized to [0,1].");

        Height = height;
        Width = width;
        Pixels = pixels;
        BackgroundValue = backgroundValue;
    }

    public int Height { get; }
    public int Width { get; }
    public float[] Pixels { get; }

    /// <summary>
    /// Fill outside this image when batching or augmenting. Defaults to 0 for legacy
    /// ink-positive tensors; ordinary luminance tensors use white background (1).
    /// This value declares pixel meaning and is never inferred from edge ink.
    /// </summary>
    public float BackgroundValue { get; }

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
