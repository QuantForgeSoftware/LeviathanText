using LeviathanText.Contracts;

namespace LeviathanText.Recognition;

/// <summary>
/// General code-point/sequence codec. Unlike <see cref="LabelSet"/> (one grapheme per CTC
/// class), this supports multi-codepoint encodings (one-to-many, many-to-one, many-to-many)
/// with prefix-free label mappings. It is deliberately not used by the current CRC/CTC trainer
/// unless a model profile explicitly selects it.
/// </summary>
public sealed class LabelCodec
{
    private readonly Dictionary<string, IReadOnlyList<int>> _c2l;
    private readonly Dictionary<string, int[]> _l2cByText;
    private readonly Dictionary<int, string> _singleLabels;
    private readonly string[] _codesByLength;
    private readonly int[][] _labelCodesByLength;

    public bool Strict { get; }

    public LabelCodec(string charset, bool strict = false)
        : this(charset.Select(c => c.ToString()).Distinct(StringComparer.Ordinal)
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
        _c2l = new Dictionary<string, IReadOnlyList<int>>(mapping, StringComparer.Ordinal);
        if (_c2l.Count == 0) throw new ArgumentException("A codec needs at least one mapping.", nameof(mapping));
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
                if (text.AsSpan(index).StartsWith(code, StringComparison.Ordinal))
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
            index += 1; // non-strict: advance one UTF-16 code unit, matching Kraken's code-point fallback
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

    public LabelCodec AddLabels(string labels) => AddLabels(labels.Select(c => c.ToString()));

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

    /// <summary>Merge mappings, dropping labels only used by removed sequences and closing label gaps.</summary>
    public (LabelCodec Codec, IReadOnlySet<int> RemovedLabels) Merge(LabelCodec other)
    {
        var retained = _c2l.Where(kv => other._c2l.ContainsKey(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value.ToArray(), StringComparer.Ordinal);
        var removed = new HashSet<int>();
        var used = retained.Values.SelectMany(v => v).ToHashSet();
        foreach (var value in _c2l.Where(kv => !other._c2l.ContainsKey(kv.Key)).Select(kv => kv.Value))
            foreach (var label in value)
                if (!used.Contains(label)) removed.Add(label);

        var remap = new Dictionary<int, int>();
        int next = 1;
        foreach (var label in _c2l.Values.SelectMany(v => v).Concat(retained.Values.SelectMany(v => v)).Distinct().OrderBy(v => v))
            if (!removed.Contains(label)) remap[label] = next++;

        var merged = retained.ToDictionary(kv => kv.Key,
            kv => (IReadOnlyList<int>)kv.Value.Select(label => remap[label]).ToArray(), StringComparer.Ordinal);
        foreach (var (key, value) in other._c2l.Where(kv => !merged.ContainsKey(kv.Key)))
        {
            var targets = value.Select(label => other._c2l.Values.SelectMany(v => v).Contains(label) ? label : label).ToArray();
            merged[key] = [.. targets.Select(label => remap.GetValueOrDefault(label, next++))];
        }
        return (new LabelCodec(merged, Strict), removed);
    }
}
