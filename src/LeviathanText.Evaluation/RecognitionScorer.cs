namespace LeviathanText.Evaluation;

/// <summary>Errors attributed to base (non-mark) characters.</summary>
public sealed record BaseErrorBreakdown(int Insertions, int Deletions, int Substitutions)
{
    public int Total => Insertions + Deletions + Substitutions;
}

/// <summary>
/// Errors attributed to combining marks, reported separately (examples.md §1).
/// An accent wrongly supplied on unmarked text is a mark insertion; a missing
/// accent is a mark deletion.
/// </summary>
public sealed record MarkErrorBreakdown(int Insertions, int Deletions, int Substitutions)
{
    public int Total => Insertions + Deletions + Substitutions;
}

/// <summary>Primary recognition score for one span/line (examples.md §1).</summary>
public sealed record RecognitionScore(
    int ReferenceScalarLength,
    int HypothesisScalarLength,
    int ScalarDistance,
    double ScalarCer,
    int ReferenceGraphemeLength,
    int GraphemeDistance,
    double GraphemeCer,
    BaseErrorBreakdown BaseErrors,
    MarkErrorBreakdown MarkErrors);

/// <summary>
/// Character and grapheme scoring with separate mark-error reporting.
///
/// Primary metrics are computed on NFC canonical-equivalent scalars/graphemes
/// (case-sensitive). Mark attribution is computed on the NFD decomposition so a
/// precomposed polytonic vowel is correctly seen as a base plus its marks.
/// </summary>
public static class RecognitionScorer
{
    public static RecognitionScore Score(string reference, string hypothesis)
    {
        var refNfc = CanonicalText.Nfc(reference);
        var hypNfc = CanonicalText.Nfc(hypothesis);

        // Primary: NFC scalar and grapheme CER.
        var refScalars = CanonicalText.ToScalars(refNfc);
        var hypScalars = CanonicalText.ToScalars(hypNfc);
        var scalarScript = EditScript.Compute(refScalars, hypScalars);
        double scalarCer = Cer(scalarScript.Distance, refScalars.Count, hypScalars.Count);

        var refGraphemes = LeviathanText.Core.Unicode.Graphemes.Enumerate(refNfc).Select(g => g.Text).ToList();
        var hypGraphemes = LeviathanText.Core.Unicode.Graphemes.Enumerate(hypNfc).Select(g => g.Text).ToList();
        var graphemeScript = EditScript.Compute(refGraphemes, hypGraphemes);
        double graphemeCer = Cer(graphemeScript.Distance, refGraphemes.Count, hypGraphemes.Count);

        // Mark attribution: align the NFD decompositions and classify each op.
        var (baseErrors, markErrors) = AttributeMarks(refNfc, hypNfc);

        return new RecognitionScore(
            refScalars.Count,
            hypScalars.Count,
            scalarScript.Distance,
            scalarCer,
            refGraphemes.Count,
            graphemeScript.Distance,
            graphemeCer,
            baseErrors,
            markErrors);
    }

    private static (BaseErrorBreakdown, MarkErrorBreakdown) AttributeMarks(string refNfc, string hypNfc)
    {
        var refNfd = CanonicalText.ToScalars(CanonicalText.Nfd(refNfc));
        var hypNfd = CanonicalText.ToScalars(CanonicalText.Nfd(hypNfc));
        var script = EditScript.Compute(refNfd, hypNfd);

        int baseIns = 0, baseDel = 0, baseSub = 0;
        int markIns = 0, markDel = 0, markSub = 0;

        foreach (var op in script.Ops)
        {
            switch (op.Kind)
            {
                case EditKind.Match:
                    break;
                case EditKind.Insert:
                {
                    if (CanonicalText.IsCombiningMark(hypNfd[op.HypothesisIndex])) markIns++;
                    else baseIns++;
                    break;
                }
                case EditKind.Delete:
                {
                    if (CanonicalText.IsCombiningMark(refNfd[op.ReferenceIndex])) markDel++;
                    else baseDel++;
                    break;
                }
                case EditKind.Substitute:
                {
                    bool refMark = CanonicalText.IsCombiningMark(refNfd[op.ReferenceIndex]);
                    bool hypMark = CanonicalText.IsCombiningMark(hypNfd[op.HypothesisIndex]);
                    if (!refMark && !hypMark) baseSub++;
                    else if (refMark && hypMark) markSub++;
                    else if (!refMark && hypMark) { baseDel++; markIns++; } // a base replaced by a mark
                    else { baseIns++; markDel++; }                          // a mark replaced by a base
                    break;
                }
            }
        }

        return (new BaseErrorBreakdown(baseIns, baseDel, baseSub), new MarkErrorBreakdown(markIns, markDel, markSub));
    }

    /// <summary>
    /// CER with an explicit convention for an empty reference: 0 when both are empty,
    /// otherwise 1.0 (never NaN/Infinity, which would poison aggregates).
    /// </summary>
    internal static double Cer(int distance, int referenceLength, int hypothesisLength)
    {
        if (referenceLength == 0) return hypothesisLength == 0 ? 0.0 : 1.0;
        return (double)distance / referenceLength;
    }
}
