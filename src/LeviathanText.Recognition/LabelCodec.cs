using LeviathanText.Contracts;
using System.Collections.ObjectModel;

namespace LeviathanText.Recognition;

/// <summary>
/// General code-point/sequence codec. Unlike <see cref="LabelSet"/> (one grapheme per CTC
/// class), this supports multi-codepoint encodings (one-to-many, many-to-one, many-to-many)
/// with prefix-free label mappings. It is deliberately not used by the current CRC/CTC trainer
/// unless a model profile explicitly selects it.
/// </summary>
public sealed class LabelCodec
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<int>> _c2l;
    private readonly Dictionary<string, int[]> _l2cByText;
    private readonly Dictionary<int, string> _singleLabels;
    private readonly string[] _codesByLength;
    private readonly int[][] _labelCodesByLength;

    public bool Strict { get; }

    public LabelCodec(string charset, bool strict = false)
        : this(CodePoints(charset).Distinct(StringComparer.Ordinal)
            .OrderBy(c => c, StringComparer.Ordinal).ToList(), strict)
    {
    }

    public LabelCodec(IEnumerable<string> charset, bool strict = false)
        : this(charset.Distinct(StringComparer.Ordinal).OrderBy(c => c, StringComparer.Ordinal)
            .Select((c, i) => new KeyValuePair<string, IReadOnlyList<int>>(c, [i + 1]))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal), strict)
    {
    }

    public LabelCodec(IReadOnlyDictionary<string, IReadOnlyList<int>> mapping, bool strict = false)
    {
        Strict = strict;
        ArgumentNullException.ThrowIfNull(mapping);
        if (mapping.Count == 0) throw new ArgumentException("A codec needs at least one mapping.", nameof(mapping));
        var snapshot = new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal);
        foreach (var (text, units) in mapping)
        {
            if (string.IsNullOrEmpty(text) || units is null || units.Count == 0)
                throw new ArgumentException("Codec text and label sequences must be nonempty.", nameof(mapping));
            snapshot.Add(text, Array.AsReadOnly(units.ToArray()));
        }
        _c2l = new ReadOnlyDictionary<string, IReadOnlyList<int>>(snapshot);
        _l2cByText = _c2l.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray(), StringComparer.Ordinal);
        _singleLabels = _c2l.Where(kv => kv.Value.Count == 1).ToDictionary(kv => kv.Value[0], kv => kv.Key);
        _codesByLength = [.. _c2l.Keys.OrderByDescending(k => k.Length).ThenBy(k => k, StringComparer.Ordinal)];
        _labelCodesByLength = [.. _c2l.Values.Select(v => v.ToArray()).OrderByDescending(v => v.Length).ThenBy(v => v[0])];
        if (!IsValid) throw new ArgumentException("Codec mapping is non-singular or not prefix-free.", nameof(mapping));
    }

    public int Count => _c2l.Count;
    public int MaxLabel => _c2l.Values.SelectMany(v => v).DefaultIfEmpty(0).Max();
    public IReadOnlyDictionary<string, IReadOnlyList<int>> CharacterToLabels => _c2l;

    public bool IsValid
    {
        get
        {
            if (_l2cByText.Values.Select(v => string.Join(',', v)).Distinct().Count() != _c2l.Count) return false;
            var sequences = _c2l.Values.Select(v => v.ToArray()).ToList();
            for (int i = 0; i < sequences.Count; i++)
                for (int j = 0; j < sequences.Count; j++)
                {
                    if (i == j) continue;
                    if (sequences[j].Length < sequences[i].Length
                        && sequences[i].Take(sequences[j].Length).SequenceEqual(sequences[j])) return false;
                }
            return true;
        }
    }

    public int[] Encode(string text)
    {
        var labels = new List<int>();
        int index = 0;
        while (index < text.Length)
        {
            bool encoded = false;
            foreach (var code in _codesByLength)
            {
                int end = index + code.Length;
                if (text.AsSpan(index).StartsWith(code, StringComparison.Ordinal)
                    && !(end < text.Length && char.IsHighSurrogate(text[end - 1]) && char.IsLowSurrogate(text[end])))
                {
                    labels.AddRange(_c2l[code]);
                    index += code.Length;
                    encoded = true;
                    break;
                }
            }
            if (encoded) continue;
            if (Strict)
                throw new LeviathanException(new LeviathanError
                {
                    Code = ErrorCode.UnknownLabel,
                    Scope = nameof(LabelCodec),
                    Details = new Dictionary<string, string> { ["suffix"] = text[index..Math.Min(text.Length, index + 5)] },
                    RemedyHint = RemedyHint.MapUnknownLabel,
                    Message = "Non-encodable character sequence.",
                });
            index += CodePointWidth(text, index); // Preserve scalar boundaries, including astral Greek symbols.
        }
        return [.. labels];
    }

    public string Decode(IReadOnlyList<int> labels)
    {
        var text = new System.Text.StringBuilder();
        int index = 0;
        while (index < labels.Count)
        {
            bool decoded = false;
            foreach (var code in _labelCodesByLength)
            {
                if (index + code.Length > labels.Count) continue;
                if (!labels.Skip(index).Take(code.Length).SequenceEqual(code)) continue;
                text.Append(_l2cByText.First(kv => kv.Value.SequenceEqual(code)).Key);
                index += code.Length;
                decoded = true;
                break;
            }
            if (!decoded)
            {
                if (Strict)
                    throw new LeviathanException(new LeviathanError
                    {
                        Code = ErrorCode.UnknownLabel,
                        Scope = nameof(LabelCodec),
                        Details = new Dictionary<string, string> { ["label"] = labels[index].ToString() },
                        RemedyHint = RemedyHint.MapUnknownLabel,
                        Message = "Non-decodable label sequence.",
                    });
                index += 1;
            }
        }
        return text.ToString();
    }

    public LabelCodec AddLabels(string labels) => AddLabels(CodePoints(labels));

    public LabelCodec AddLabels(IEnumerable<string> labels)
    {
        var mapping = new Dictionary<string, IReadOnlyList<int>>(_c2l, StringComparer.Ordinal);
        int next = MaxLabel + 1;
        foreach (var label in labels.Distinct(StringComparer.Ordinal))
            if (!mapping.ContainsKey(label)) mapping[label] = [next++];
        return new LabelCodec(mapping, Strict);
    }

    public LabelCodec AddLabels(IReadOnlyDictionary<string, IReadOnlyList<int>> labels)
    {
        var mapping = new Dictionary<string, IReadOnlyList<int>>(_c2l, StringComparer.Ordinal);
        foreach (var (key, value) in labels) mapping[key] = value;
        return new LabelCodec(mapping, Strict);
    }

    /// <summary>
    /// Merge compatible unit identities, removing old units exclusive to removed text
    /// and compacting retained old units. Common sequences must establish a consistent
    /// one-to-one incoming/old unit correspondence; incompatible identities are rejected.
    /// This returns a codec mapping, not a checkpoint or weight migration.
    /// </summary>
    public (LabelCodec Codec, IReadOnlySet<int> RemovedLabels) Merge(LabelCodec other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var retained = _c2l.Where(kv => other._c2l.ContainsKey(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value.ToArray(), StringComparer.Ordinal);
        var used = retained.Values.SelectMany(v => v).ToHashSet();
        var removed = _c2l.Values.SelectMany(v => v).Where(label => !used.Contains(label)).ToHashSet();
        var incomingToOld = new Dictionary<int, int>();
        var oldToIncoming = new Dictionary<int, int>();
        foreach (var (text, oldUnits) in retained)
        {
            var incoming = other._c2l[text];
            if (oldUnits.Length != incoming.Count)
                throw new ArgumentException("Common text sequences have incompatible label lengths.", nameof(other));
            for (int i = 0; i < oldUnits.Length; i++)
            {
                if ((incomingToOld.TryGetValue(incoming[i], out int old) && old != oldUnits[i])
                    || (oldToIncoming.TryGetValue(oldUnits[i], out int source) && source != incoming[i]))
                    throw new ArgumentException("Common text sequences have incompatible label identities.", nameof(other));
                incomingToOld[incoming[i]] = oldUnits[i];
                oldToIncoming[oldUnits[i]] = incoming[i];
            }
        }

        var oldToMerged = new Dictionary<int, int>();
        int next = 1;
        foreach (var label in used.Order()) oldToMerged[label] = next++;
        var incomingToMerged = incomingToOld.ToDictionary(kv => kv.Key, kv => oldToMerged[kv.Value]);
        foreach (var label in other._c2l.Values.SelectMany(v => v).Distinct().Order())
            if (!incomingToMerged.ContainsKey(label)) incomingToMerged[label] = next++;

        var merged = retained.ToDictionary(kv => kv.Key,
            kv => (IReadOnlyList<int>)kv.Value.Select(label => oldToMerged[label]).ToArray(), StringComparer.Ordinal);
        foreach (var (key, value) in other._c2l.Where(kv => !merged.ContainsKey(kv.Key)))
            merged[key] = [.. value.Select(label => incomingToMerged[label])];
        return (new LabelCodec(merged, Strict), removed);
    }

    // Match Core ScalarText's isolated-surrogate policy without silently replacing
    // malformed halves with U+FFFD while splitting valid pairs into one code point.
    private static IEnumerable<string> CodePoints(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        for (int index = 0; index < text.Length;)
        {
            int width = CodePointWidth(text, index);
            yield return text.Substring(index, width);
            index += width;
        }
    }

    private static int CodePointWidth(string text, int index) =>
        char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]) ? 2 : 1;
}
