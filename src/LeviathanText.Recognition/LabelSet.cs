using LeviathanText.Contracts;
using LeviathanText.Core.Unicode;

namespace LeviathanText.Recognition;

/// <summary>
/// A label set maps grapheme clusters to CTC class indices. Index 0 is always the CTC
/// blank; labels occupy indices 1..N. A label may be a multi-scalar grapheme cluster
/// (e.g. a base plus combining marks), so decoding never splits a cluster.
/// </summary>
public sealed class LabelSet
{
    public const int BlankIndex = 0;

    private readonly List<string> _labels;
    private readonly Dictionary<string, int> _index;

    public LabelSet(IEnumerable<string> labels)
    {
        _labels = [.. labels];
        if (_labels.Count == 0) throw new ArgumentException("A label set needs at least one label.", nameof(labels));

        _index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < _labels.Count; i++)
        {
            var label = _labels[i];
            if (string.IsNullOrEmpty(label)) throw new ArgumentException("Labels must be non-empty.", nameof(labels));
            if (!_index.TryAdd(label, i + 1))
                throw new ArgumentException($"Duplicate label '{label}'.", nameof(labels));
        }
    }

    /// <summary>Number of CTC classes, including the blank.</summary>
    public int NumClasses => _labels.Count + 1;

    /// <summary>Number of non-blank labels.</summary>
    public int LabelCount => _labels.Count;

    public IReadOnlyList<string> Labels => _labels;

    public string LabelAt(int index) =>
        index == BlankIndex ? string.Empty : _labels[index - 1];

    /// <summary>Encode grapheme clusters to CTC indices; an unknown label fails loudly.</summary>
    public int[] Encode(IEnumerable<string> graphemes)
    {
        var indices = new List<int>();
        foreach (var grapheme in graphemes)
        {
            if (!_index.TryGetValue(grapheme, out var index))
                throw new LeviathanException(new LeviathanError
                {
                    Code = ErrorCode.UnknownLabel,
                    Scope = nameof(LabelSet),
                    Details = new Dictionary<string, string> { ["grapheme"] = grapheme },
                    RemedyHint = RemedyHint.MapUnknownLabel,
                    Message = $"Grapheme '{grapheme}' is not in the label set.",
                });
            indices.Add(index);
        }
        return [.. indices];
    }

    /// <summary>Encode a string by splitting it into extended grapheme clusters first.</summary>
    public int[] EncodeText(string text) => Encode(Graphemes.Enumerate(text).Select(g => g.Text));

    /// <summary>Decode CTC indices to text; blanks are skipped.</summary>
    public string Decode(IEnumerable<int> indices)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var index in indices)
        {
            if (index == BlankIndex) continue;
            if (index < 0 || index >= NumClasses) throw new ArgumentOutOfRangeException(nameof(indices), $"Index {index} is outside the class range.");
            sb.Append(_labels[index - 1]);
        }
        return sb.ToString();
    }

    /// <summary>
    /// A minimal default Greek alphabet (lowercase base letters, space, middle dot).
    /// Real profiles add diacritic composites; this is a starting vocabulary, not a
    /// claim of completeness.
    /// </summary>
    public static LabelSet DefaultGreek() => new(
    [
        "α", "β", "γ", "δ", "ε", "ζ", "η", "θ", "ι", "κ", "λ", "μ", "ν", "ξ", "ο",
        "π", "ρ", "σ", "ς", "τ", "υ", "φ", "χ", "ψ", "ω",
        " ", "·",
    ]);
}
