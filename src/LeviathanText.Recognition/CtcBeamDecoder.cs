namespace LeviathanText.Recognition;

/// <summary>
/// CTC decoding strategies. Greedy collapses per-frame argmax; prefix beam search sums
/// the probability of every alignment that maps to a label sequence, which can recover
/// sequences greedy misses when label mass is spread across repeated frames
/// (todo §5: decoding strategy is chosen by measurement, not assumption).
/// </summary>
public enum CtcDecodeMode
{
    Greedy,
    PrefixBeam,
}

/// <summary>
/// Probability-space decoders. Inputs are per-time-step class distributions (not log
/// probabilities); a caller with log softmax output can pass <c>exp()</c> values.
/// </summary>
public static class CtcBeamDecoder
{
    private const double TinyProbability = 1e-12;
    private const double NegativeInfinity = double.NegativeInfinity;

    /// <summary>Per-frame argmax followed by greedy collapse.</summary>
    public static int[] Greedy(IReadOnlyList<IReadOnlyList<double>> probabilities, int blank = LabelSet.BlankIndex)
    {
        Validate(probabilities, blank);
        var argmax = new int[probabilities.Count];
        for (var t = 0; t < probabilities.Count; t++)
        {
            var frame = probabilities[t];
            var best = 0;
            for (var c = 1; c < frame.Count; c++)
                if (frame[c] > frame[best]) best = c;
            argmax[t] = best;
        }
        return CtcDecoder.Greedy(argmax, blank);
    }

    /// <summary>
    /// Standard CTC prefix beam search in log space. Prefixes are pruned to
    /// <paramref name="beamWidth"/> after each frame; ties are broken deterministically by
    /// sequence length then lexical order.
    /// </summary>
    public static int[] PrefixBeamSearch(
        IReadOnlyList<IReadOnlyList<double>> probabilities,
        int beamWidth = 8,
        int blank = LabelSet.BlankIndex)
    {
        Validate(probabilities, blank);
        if (beamWidth < 1) throw new ArgumentOutOfRangeException(nameof(beamWidth), "Beam width must be at least 1.");

        var beams = new Dictionary<Prefix, Beam>(PrefixComparer.Instance)
        {
            [Prefix.Empty] = new Beam(0.0, NegativeInfinity),
        };

        foreach (var frame in probabilities)
        {
            var next = new Dictionary<Prefix, Beam>(PrefixComparer.Instance);
            foreach (var (prefix, beam) in beams)
            {
                var total = LogSumExp(beam.BlankLogProbability, beam.NonBlankLogProbability);

                for (var c = 0; c < frame.Count; c++)
                {
                    var logP = Math.Log(Math.Max(frame[c], TinyProbability));

                    if (c == blank)
                    {
                        Accumulate(next, prefix, blankContribution: total + logP, nonBlankContribution: NegativeInfinity);
                        continue;
                    }

                    var repeatsLast = prefix.Length > 0 && prefix.Last == c;
                    if (repeatsLast)
                    {
                        // Same label without a blank stays in this prefix (non-blank);
                        // through a blank it emits a new, separated label.
                        Accumulate(next, prefix, blankContribution: NegativeInfinity, nonBlankContribution: beam.NonBlankLogProbability + logP);
                        Accumulate(next, prefix.Append(c), blankContribution: NegativeInfinity, nonBlankContribution: beam.BlankLogProbability + logP);
                    }
                    else
                    {
                        Accumulate(next, prefix.Append(c), blankContribution: NegativeInfinity, nonBlankContribution: total + logP);
                    }
                }
            }

            beams = Prune(next, beamWidth);
        }

        var best = beams
            .Select(kv => (kv.Key, Score: LogSumExp(kv.Value.BlankLogProbability, kv.Value.NonBlankLogProbability)))
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Key.Length)
            .ThenBy(item => item.Key, PrefixComparer.Instance)
            .First();

        return best.Key.ToArray();
    }

    /// <summary>
    /// Best single alignment of a fixed label sequence, used to attach a real per-label
    /// probability to beam-search output. Repeating labels require a blank between them,
    /// matching CTC collapse semantics. Returns (time step, probability) per label.
    /// </summary>
    public static IReadOnlyList<(int TimeStep, double Probability)> BestAlignment(
        IReadOnlyList<IReadOnlyList<double>> probabilities,
        IReadOnlyList<int> labels,
        int blank = LabelSet.BlankIndex)
    {
        Validate(probabilities, blank);
        ArgumentNullException.ThrowIfNull(labels);

        var frames = probabilities.Count;
        var count = labels.Count;
        if (count == 0) return [];

        // B[t,u]: best score where frame t is blank and u labels are emitted.
        // L[t,u]: best score where frame t emitted label u-1.
        var blankEnd = new double[frames, count + 1];
        var labelEnd = new double[frames, count + 1];
        for (var t = 0; t < frames; t++)
            for (var u = 0; u <= count; u++)
            {
                blankEnd[t, u] = NegativeInfinity;
                labelEnd[t, u] = NegativeInfinity;
            }

        blankEnd[0, 0] = Log(probabilities[0], blank);
        if (count > 0) labelEnd[0, 1] = Log(probabilities[0], labels[0]);

        for (var t = 1; t < frames; t++)
        {
            for (var u = 0; u <= count; u++)
            {
                blankEnd[t, u] = Max(blankEnd[t - 1, u], labelEnd[t - 1, u]) + Log(probabilities[t], blank);

                if (u == 0) continue;
                var previous = labels[u - 1];
                var predecessor = blankEnd[t - 1, u - 1];
                var repeats = u >= 2 && labels[u - 2] == previous;
                if (!repeats) predecessor = Math.Max(predecessor, labelEnd[t - 1, u - 1]);

                labelEnd[t, u] = predecessor + Log(probabilities[t], previous);
            }
        }

        if (double.IsNegativeInfinity(Math.Max(blankEnd[frames - 1, count], labelEnd[frames - 1, count])))
            throw new InvalidOperationException("The label sequence cannot be aligned to the frame count.");

        var result = new (int TimeStep, double Probability)[count];
        var emitted = count;
        var time = frames - 1;
        var atLabelEnd = labelEnd[frames - 1, count] >= blankEnd[frames - 1, count];

        while (emitted > 0 && time >= 0)
        {
            if (!atLabelEnd)
            {
                if (time == 0) break;
                atLabelEnd = labelEnd[time - 1, emitted] >= blankEnd[time - 1, emitted];
                time--;
                continue;
            }

            emitted--;
            result[emitted] = (time, Math.Clamp(probabilities[time][labels[emitted]], 0.0, 1.0));

            if (emitted == 0) break;
            var repeats = labels[emitted - 1] == labels[emitted];
            atLabelEnd = !repeats && labelEnd[time - 1, emitted] >= blankEnd[time - 1, emitted];
            time--;
        }

        return result;
    }

    private static double Max(double a, double b) => Math.Max(a, b);

    private static Dictionary<Prefix, Beam> Prune(Dictionary<Prefix, Beam> beams, int beamWidth)
    {
        var kept = beams
            .Select(kv => (kv.Key, kv.Value, Score: LogSumExp(kv.Value.BlankLogProbability, kv.Value.NonBlankLogProbability)))
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Key.Length)
            .ThenBy(item => item.Key, PrefixComparer.Instance)
            .Take(beamWidth)
            .ToDictionary(item => item.Key, item => item.Value, PrefixComparer.Instance);
        return kept;
    }

    private static void Accumulate(
        Dictionary<Prefix, Beam> beams,
        Prefix prefix,
        double blankContribution,
        double nonBlankContribution)
    {
        var current = beams.TryGetValue(prefix, out var existing) ? existing : Beam.Empty;
        beams[prefix] = new Beam(
            LogSumExp(current.BlankLogProbability, blankContribution),
            LogSumExp(current.NonBlankLogProbability, nonBlankContribution));
    }

    private static double Log(IReadOnlyList<double> frame, int index) => Math.Log(Math.Max(frame[index], TinyProbability));

    private static double LogSumExp(double a, double b)
    {
        if (double.IsNegativeInfinity(a)) return b;
        if (double.IsNegativeInfinity(b)) return a;
        var max = Math.Max(a, b);
        return max + Math.Log(Math.Exp(a - max) + Math.Exp(b - max));
    }

    private static void Validate(IReadOnlyList<IReadOnlyList<double>> probabilities, int blank)
    {
        ArgumentNullException.ThrowIfNull(probabilities);
        if (probabilities.Count == 0) throw new ArgumentException("At least one time step is required.", nameof(probabilities));

        for (var t = 0; t < probabilities.Count; t++)
        {
            var frame = probabilities[t];
            if (frame is null || frame.Count < 2)
                throw new ArgumentException($"Frame {t} must contain at least blank plus one label.", nameof(probabilities));
            if (blank < 0 || blank >= frame.Count)
                throw new ArgumentOutOfRangeException(nameof(blank), "Blank index is outside the class range.");
            for (var c = 0; c < frame.Count; c++)
                if (double.IsNaN(frame[c]) || frame[c] < 0)
                    throw new ArgumentException($"Frame {t} class {c} is not a probability.", nameof(probabilities));
        }
    }

    private readonly record struct Beam(double BlankLogProbability, double NonBlankLogProbability)
    {
        public static readonly Beam Empty = new(NegativeInfinity, NegativeInfinity);
    }

    /// <summary>An immutable prefix of CTC indices with value equality.</summary>
    private sealed class Prefix
    {
        private readonly int[] _indices;

        private Prefix(int[] indices) => _indices = indices;

        public static readonly Prefix Empty = new([]);

        public int Length => _indices.Length;

        public int Last => _indices[^1];

        public Prefix Append(int index)
        {
            var copy = new int[Length + 1];
            Array.Copy(_indices, copy, Length);
            copy[^1] = index;
            return new Prefix(copy);
        }

        public int[] ToArray() => (int[])_indices.Clone();

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var index in _indices) hash.Add(index);
            return hash.ToHashCode();
        }

        public override bool Equals(object? obj) =>
            obj is Prefix other && _indices.AsSpan().SequenceEqual(other._indices);

        public override string ToString() => string.Join(',', _indices);
    }

    private sealed class PrefixComparer : IEqualityComparer<Prefix>, IComparer<Prefix>
    {
        public static readonly PrefixComparer Instance = new();

        public bool Equals(Prefix? x, Prefix? y) => ReferenceEquals(x, y) || (x is not null && x.Equals(y));

        public int GetHashCode(Prefix obj) => obj.GetHashCode();

        public int Compare(Prefix? x, Prefix? y)
        {
            var a = x!.ToString();
            var b = y!.ToString();
            return string.CompareOrdinal(a, b);
        }
    }
}
