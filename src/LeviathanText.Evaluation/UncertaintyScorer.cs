using LeviathanText.Contracts;

namespace LeviathanText.Evaluation;

/// <summary>
/// Result of stratifying a line score by excluded (uncertain/illegible/lost) spans.
/// Coverage is the fraction of reference scalars actually scored, so an empty or
/// over-masked output cannot masquerade as a good score (examples.md §1).
/// </summary>
public sealed record UncertaintyScore(
    int TotalReferenceScalars,
    int ExcludedScalars,
    int ScoredReferenceScalars,
    int ScalarDistance,
    double ScalarCer,
    double Coverage);

/// <summary>
/// Scores only the agreed (non-excluded) reference spans. Alignment is computed on
/// the full NFC scalar sequences; operations whose reference scalar falls inside an
/// excluded range are dropped from the score, while insertions (no reference scalar)
/// are always kept as errors. Never pools uncertainty into the main CER.
/// </summary>
public static class UncertaintyScorer
{
    public static UncertaintyScore Score(string reference, string hypothesis, IReadOnlyList<ScalarRange> uncertainRanges)
    {
        var refScalars = CanonicalText.ToScalars(CanonicalText.Nfc(reference));
        var hypScalars = CanonicalText.ToScalars(CanonicalText.Nfc(hypothesis));

        var excluded = new bool[refScalars.Count];
        foreach (var range in uncertainRanges)
        {
            if (range.Start < 0 || range.End > refScalars.Count || range.End < range.Start)
                throw new ArgumentOutOfRangeException(nameof(uncertainRanges), $"Range {range} is outside the reference ({refScalars.Count} scalars).");
            for (int i = range.Start; i < range.End; i++) excluded[i] = true;
        }

        var script = EditScript.Compute(refScalars, hypScalars);

        int distance = 0;
        foreach (var op in script.Ops)
        {
            switch (op.Kind)
            {
                case EditKind.Match:
                    break;
                case EditKind.Insert:
                    distance++; // no reference anchor; always counted
                    break;
                default:
                    if (!excluded[op.ReferenceIndex]) distance++;
                    break;
            }
        }

        int excludedCount = excluded.Count(e => e);
        int scored = refScalars.Count - excludedCount;
        double cer = scored == 0 ? (distance == 0 ? 0.0 : 1.0) : (double)distance / scored;
        double coverage = refScalars.Count == 0 ? 1.0 : (double)scored / refScalars.Count;

        return new UncertaintyScore(refScalars.Count, excludedCount, scored, distance, cer, coverage);
    }
}
