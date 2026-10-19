namespace LeviathanText.Evaluation;

/// <summary>
/// A recurring edit. <see cref="Expected"/> is empty for an insertion,
/// <see cref="Produced"/> is empty for a deletion. Values are NFD scalars rendered as
/// text, so a combining mark appears as the mark itself.
/// </summary>
public sealed record ConfusionPair(string Expected, string Produced, int Count)
{
    public bool IsInsertion => Expected.Length == 0;
    public bool IsDeletion => Produced.Length == 0;
}

/// <summary>
/// Ranked error analysis for one line or an aggregate (acceptance-plan.md §3: error
/// analysis is a deliverable, not a debugging side effect). Mark errors are counted
/// separately so a pooled substitution count cannot hide accent damage.
/// </summary>
public sealed record ErrorAnalysisReport(
    int TotalSubstitutions,
    int TotalInsertions,
    int TotalDeletions,
    int MarkSubstitutions,
    int MarkInsertions,
    int MarkDeletions,
    IReadOnlyList<ConfusionPair> TopSubstitutions,
    IReadOnlyList<ConfusionPair> TopInsertions,
    IReadOnlyList<ConfusionPair> TopDeletions)
{
    public int TotalDistance => TotalSubstitutions + TotalInsertions + TotalDeletions;
}

/// <summary>Levenshtein-based confusion analysis over NFD scalars.</summary>
public static class ErrorAnalysis
{
    public static ErrorAnalysisReport Analyze(string reference, string hypothesis, int top = 10)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(hypothesis);
        if (top < 0) throw new ArgumentOutOfRangeException(nameof(top));

        var refScalars = CanonicalText.ToScalars(CanonicalText.Nfd(reference));
        var hypScalars = CanonicalText.ToScalars(CanonicalText.Nfd(hypothesis));
        var script = EditScript.Compute(refScalars, hypScalars);

        int substitutions = 0, insertions = 0, deletions = 0;
        int markSubstitutions = 0, markInsertions = 0, markDeletions = 0;
        var substitutionCounts = new Dictionary<(string, string), int>();
        var insertionCounts = new Dictionary<string, int>();
        var deletionCounts = new Dictionary<(string, string), int>();

        foreach (var op in script.Ops)
        {
            switch (op.Kind)
            {
                case EditKind.Substitute:
                {
                    substitutions++;
                    var expected = Scalar(refScalars, op.ReferenceIndex);
                    var produced = Scalar(hypScalars, op.HypothesisIndex);
                    if (CanonicalText.IsCombiningMark(refScalars[op.ReferenceIndex]) || CanonicalText.IsCombiningMark(hypScalars[op.HypothesisIndex]))
                        markSubstitutions++;
                    Bump(substitutionCounts, (expected, produced));
                    break;
                }
                case EditKind.Insert:
                {
                    insertions++;
                    var produced = Scalar(hypScalars, op.HypothesisIndex);
                    if (CanonicalText.IsCombiningMark(hypScalars[op.HypothesisIndex])) markInsertions++;
                    Bump(insertionCounts, produced);
                    break;
                }
                case EditKind.Delete:
                {
                    deletions++;
                    var expected = Scalar(refScalars, op.ReferenceIndex);
                    if (CanonicalText.IsCombiningMark(refScalars[op.ReferenceIndex])) markDeletions++;
                    Bump(deletionCounts, (expected, string.Empty));
                    break;
                }
            }
        }

        return new ErrorAnalysisReport(
            substitutions,
            insertions,
            deletions,
            markSubstitutions,
            markInsertions,
            markDeletions,
            Rank(substitutionCounts, top),
            insertionCounts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
                .Take(top).Select(kv => new ConfusionPair(string.Empty, kv.Key, kv.Value)).ToArray(),
            Rank(deletionCounts, top));
    }

    /// <summary>Merge per-line reports; the top lists are recomputed from summed counts.</summary>
    public static ErrorAnalysisReport Aggregate(IEnumerable<ErrorAnalysisReport> reports, int top = 10)
    {
        ArgumentNullException.ThrowIfNull(reports);
        if (top < 0) throw new ArgumentOutOfRangeException(nameof(top));

        var list = reports.ToArray();
        var substitutions = new Dictionary<(string, string), int>();
        var insertions = new Dictionary<string, int>();
        var deletions = new Dictionary<(string, string), int>();

        foreach (var report in list)
        {
            foreach (var pair in report.TopSubstitutions) Bump(substitutions, (pair.Expected, pair.Produced), pair.Count);
            foreach (var pair in report.TopInsertions) Bump(insertions, pair.Produced, pair.Count);
            foreach (var pair in report.TopDeletions) Bump(deletions, (pair.Expected, pair.Produced), pair.Count);
        }

        return new ErrorAnalysisReport(
            list.Sum(r => r.TotalSubstitutions),
            list.Sum(r => r.TotalInsertions),
            list.Sum(r => r.TotalDeletions),
            list.Sum(r => r.MarkSubstitutions),
            list.Sum(r => r.MarkInsertions),
            list.Sum(r => r.MarkDeletions),
            Rank(substitutions, top),
            insertions.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
                .Take(top).Select(kv => new ConfusionPair(string.Empty, kv.Key, kv.Value)).ToArray(),
            Rank(deletions, top));
    }

    private static string Scalar(IReadOnlyList<int> scalars, int index) =>
        CanonicalText.FromScalars([scalars[index]]);

    private static void Bump<TKey>(Dictionary<TKey, int> counts, TKey key, int by = 1) where TKey : notnull =>
        counts[key] = counts.GetValueOrDefault(key) + by;

    private static ConfusionPair[] Rank(Dictionary<(string Expected, string Produced), int> counts, int top) =>
        counts.OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key.Expected, StringComparer.Ordinal)
            .ThenBy(kv => kv.Key.Produced, StringComparer.Ordinal)
            .Take(top)
            .Select(kv => new ConfusionPair(kv.Key.Expected, kv.Key.Produced, kv.Value))
            .ToArray();
}
