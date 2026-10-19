using System.Text;

namespace LeviathanText.Training;

/// <summary>
/// Generates a deterministic synthetic dataset of labeled line images from a small
/// vocabulary of single-grapheme labels. Same seed + vocabulary = same dataset, so
/// tests and demonstrations are reproducible.
/// </summary>
public static class SyntheticLineDataset
{
    public static IReadOnlyList<LabeledLine> Generate(
        int count,
        IReadOnlyList<string> vocabulary,
        int seed,
        int minLength = 2,
        int maxLength = 3)
    {
        if (vocabulary.Count == 0) throw new ArgumentException("Vocabulary must be non-empty.", nameof(vocabulary));
        if (minLength < 1 || maxLength < minLength) throw new ArgumentOutOfRangeException(nameof(minLength));

        var rng = new Random(seed);
        var lines = new List<LabeledLine>(count);
        for (int i = 0; i < count; i++)
        {
            int length = rng.Next(minLength, maxLength + 1);
            var sb = new StringBuilder(length);
            for (int j = 0; j < length; j++) sb.Append(vocabulary[rng.Next(vocabulary.Count)]);
            var text = sb.ToString();
            lines.Add(new LabeledLine(text, SyntheticGraphemeRenderer.Render(text, vocabulary)));
        }
        return lines;
    }
}
