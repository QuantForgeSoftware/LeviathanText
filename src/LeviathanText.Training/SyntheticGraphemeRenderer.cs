using LeviathanText.Core.Unicode;
using LeviathanText.Recognition;

namespace LeviathanText.Training;

/// <summary>
/// Renders fully synthetic line images so the C# train/reload/infer path can be proven
/// before any real corpus exists (todo Phase 0 C# feasibility gate).
///
/// Each grapheme is drawn as a horizontal band whose vertical position is derived from
/// the grapheme's index in the supplied vocabulary, separated by blank gaps. This makes
/// the classes separable *by construction*, so a failure in the pipeline is a pipeline
/// failure and not an artefact of an impossible synthetic task. These images are NOT
/// real handwriting and are excluded from every corpus count and accuracy claim.
/// </summary>
public static class SyntheticGraphemeRenderer
{
    public const int SlotWidth = 4;
    public const int Gap = 4;
    private const int DefaultBandCount = 8;

    public static LineImage Render(string text, IReadOnlyList<string>? vocabulary = null, int height = CrnnModel.InputHeight)
    {
        var graphemes = Graphemes.Enumerate(text).Select(g => g.Text).ToList();
        int width = Math.Max(1, graphemes.Count * (SlotWidth + Gap));
        var pixels = new float[height * width];

        int bandCount = vocabulary is { Count: > 0 } ? vocabulary.Count : DefaultBandCount;
        int bandHeight = Math.Max(1, height / bandCount);

        for (int g = 0; g < graphemes.Count; g++)
        {
            int index = BandIndex(graphemes[g], vocabulary, bandCount);
            int bandStart = (index % bandCount) * bandHeight;
            int bandEnd = Math.Min(height, bandStart + bandHeight);
            int x0 = g * (SlotWidth + Gap);

            for (int row = 0; row < height; row++)
            {
                bool lit = row >= bandStart && row < bandEnd;
                if (!lit) continue;
                for (int col = 0; col < SlotWidth; col++)
                    pixels[row * width + x0 + col] = 1f;
            }
        }

        return new LineImage(height, width, pixels);
    }

    private static int BandIndex(string grapheme, IReadOnlyList<string>? vocabulary, int bandCount)
    {
        if (vocabulary is not null)
        {
            for (int i = 0; i < vocabulary.Count; i++)
                if (string.Equals(vocabulary[i], grapheme, StringComparison.Ordinal))
                    return i;
        }
        return StableCode(grapheme) % bandCount;
    }

    /// <summary>Stable, process-independent code for a grapheme (never <see cref="int.MinValue"/>).</summary>
    internal static int StableCode(string grapheme)
    {
        unchecked
        {
            int h = 17;
            foreach (char ch in grapheme) h = h * 31 + ch;
            return (h & 0x7FFFFFFF) | 1;
        }
    }
}
