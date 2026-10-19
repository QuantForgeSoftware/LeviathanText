using LeviathanText.Contracts;

namespace LeviathanText.Evaluation;

/// <summary>Proposal task kind, mirrored here so scoring stays independent of any provider package.</summary>
public enum SuggestionKind
{
    CandidateReading,
    WordDivision,
    Expansion,
    Accentuation,
    Restoration,
}

/// <summary>Acceptance lifecycle of a suggestion at scoring time.</summary>
public enum SuggestionDisposition
{
    Proposed,
    Accepted,
    Rejected,
    Expired,
}

/// <summary>
/// One reviewed suggestion. <paramref name="SourceText"/> is the diplomatic span the
/// suggestion targeted; <see cref="Target"/> is the scalar range in the raw line when
/// known (used to detect overwriting an already-correct witness reading).
/// </summary>
public sealed record SuggestionAudit(
    SuggestionKind Kind,
    SuggestionDisposition Disposition,
    string ProposedText,
    string SourceText,
    ScalarRange? Target = null);

/// <summary>Per-suggestion addition/substitution accounting.</summary>
public sealed record SuggestionFinding(
    SuggestionKind Kind,
    SuggestionDisposition Disposition,
    int AddedScalars,
    int UnsupportedAddedScalars,
    int SuppliedAddedScalars,
    int SubstitutedScalars,
    bool OverwritesCorrectWitnessReading);

/// <summary>
/// Expert/manual correction time measured outside the scorer. Reported separately from
/// automatic metrics; no time is invented when the harness did not record one.
/// </summary>
public sealed record CorrectionTime(TimeSpan Baseline, TimeSpan Assisted)
{
    public double Reduction => Baseline <= TimeSpan.Zero ? 0 : 1 - Assisted.TotalMilliseconds / Baseline.TotalMilliseconds;
}

/// <summary>
/// Correction-effort and AI-assistance score for one line (acceptance-plan.md §5).
/// Raw and final outputs are scored separately against the reference, and the effort
/// to turn raw into final is compared with the effort raw→reference would have taken.
/// </summary>
public sealed record CorrectionEffortScore(
    int ReferenceScalars,
    int RawScalars,
    int FinalScalars,
    int BaselineDistance,
    int AssistedDistance,
    double RawCer,
    double FinalCer,
    double BaselineCorrectionCer,
    double AssistedCorrectionCer,
    double EffortReduction,
    int AcceptedSuggestions,
    int RejectedSuggestions,
    int PendingSuggestions,
    int UnsupportedAdditions,
    int SuppliedRestorationScalars,
    int WitnessVariantOverwrites,
    IReadOnlyList<SuggestionFinding> Findings,
    CorrectionTime? Time);

/// <summary>
/// Scores a raw model line and the human final line, then classifies each reviewed
/// suggestion. Unsupported additions are accepted scalars with no support in the
/// diplomatic source span; restorations and expansions are counted as supplied text
/// instead, exactly as the contract requires them to stay marked.
/// </summary>
public static class CorrectionEffortScorer
{
    public static CorrectionEffortScore Score(
        string reference,
        string raw,
        string final,
        IReadOnlyList<SuggestionAudit>? suggestions = null,
        CorrectionTime? time = null)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentNullException.ThrowIfNull(final);
        var audits = suggestions ?? [];

        var referenceScalars = CanonicalText.ToScalars(CanonicalText.Nfc(reference));
        var rawScalars = CanonicalText.ToScalars(CanonicalText.Nfc(raw));
        var finalScalars = CanonicalText.ToScalars(CanonicalText.Nfc(final));

        var baselineDistance = EditScript.Compute(referenceScalars, rawScalars).Distance;
        var assistedDistance = EditScript.Compute(rawScalars, finalScalars).Distance;

        var rawScore = RecognitionScorer.Score(reference, raw);
        var finalScore = RecognitionScorer.Score(reference, final);

        var findings = audits.Select(audit => Classify(audit, referenceScalars, rawScalars)).ToArray();

        var accepted = findings.Where(f => f.Disposition == SuggestionDisposition.Accepted).ToArray();
        var reduction = baselineDistance == 0 ? 0 : 1 - (double)assistedDistance / baselineDistance;

        return new CorrectionEffortScore(
            referenceScalars.Count,
            rawScalars.Count,
            finalScalars.Count,
            baselineDistance,
            assistedDistance,
            rawScore.ScalarCer,
            finalScore.ScalarCer,
            Cer(baselineDistance, referenceScalars.Count),
            Cer(assistedDistance, referenceScalars.Count),
            reduction,
            findings.Count(f => f.Disposition == SuggestionDisposition.Accepted),
            findings.Count(f => f.Disposition == SuggestionDisposition.Rejected),
            findings.Count(f => f.Disposition is SuggestionDisposition.Proposed or SuggestionDisposition.Expired),
            accepted.Sum(f => f.UnsupportedAddedScalars),
            accepted.Sum(f => f.SuppliedAddedScalars),
            accepted.Count(f => f.OverwritesCorrectWitnessReading),
            findings,
            time);
    }

    /// <summary>
    /// Micro aggregate over lines: distance sums, not an average of per-line ratios, so
    /// long lines weigh proportionally as in the per-track acceptance report.
    /// </summary>
    public static CorrectionEffortScore Aggregate(IEnumerable<CorrectionEffortScore> scores)
    {
        var list = scores.ToArray();
        if (list.Length == 0)
            throw new ArgumentException("At least one score is required.", nameof(scores));

        var referenceScalars = list.Sum(s => s.ReferenceScalars);
        var baseline = list.Sum(s => s.BaselineDistance);
        var assisted = list.Sum(s => s.AssistedDistance);

        var time = list.Any(s => s.Time is not null)
            ? new CorrectionTime(
                TimeSpan.FromTicks(list.Sum(s => s.Time?.Baseline.Ticks ?? 0)),
                TimeSpan.FromTicks(list.Sum(s => s.Time?.Assisted.Ticks ?? 0)))
            : null;

        return new CorrectionEffortScore(
            referenceScalars,
            list.Sum(s => s.RawScalars),
            list.Sum(s => s.FinalScalars),
            baseline,
            assisted,
            Cer(list.Sum(s => (int)Math.Round(s.RawCer * s.ReferenceScalars)), referenceScalars),
            Cer(list.Sum(s => (int)Math.Round(s.FinalCer * s.ReferenceScalars)), referenceScalars),
            Cer(baseline, referenceScalars),
            Cer(assisted, referenceScalars),
            baseline == 0 ? 0 : 1 - (double)assisted / baseline,
            list.Sum(s => s.AcceptedSuggestions),
            list.Sum(s => s.RejectedSuggestions),
            list.Sum(s => s.PendingSuggestions),
            list.Sum(s => s.UnsupportedAdditions),
            list.Sum(s => s.SuppliedRestorationScalars),
            list.Sum(s => s.WitnessVariantOverwrites),
            list.SelectMany(s => s.Findings).ToArray(),
            time);
    }

    private static SuggestionFinding Classify(
        SuggestionAudit audit,
        IReadOnlyList<int> referenceScalars,
        IReadOnlyList<int> rawScalars)
    {
        var sourceScalars = CanonicalText.ToScalars(CanonicalText.Nfc(audit.SourceText));
        var proposedScalars = CanonicalText.ToScalars(CanonicalText.Nfc(audit.ProposedText));
        var script = EditScript.Compute(sourceScalars, proposedScalars);

        var added = 0;
        var unsupported = 0;
        var supplied = 0;

        foreach (var op in script.Ops)
        {
            if (op.Kind != EditKind.Insert) continue;
            added++;
            var scalar = proposedScalars[op.HypothesisIndex];
            if (IsExpectedAddition(audit.Kind, scalar))
                supplied++;
            else
                unsupported++;
        }

        var substitutions = script.Substitutions;
        var overwrites = audit.Disposition == SuggestionDisposition.Accepted
            && audit.Kind == SuggestionKind.CandidateReading
            && OverwritesCorrectWitnessReading(audit, referenceScalars, rawScalars);

        return new SuggestionFinding(
            audit.Kind,
            audit.Disposition,
            added,
            unsupported,
            supplied,
            substitutions,
            overwrites);
    }

    private static bool IsExpectedAddition(SuggestionKind kind, int scalar) => kind switch
    {
        SuggestionKind.Expansion or SuggestionKind.Restoration => true,
        SuggestionKind.WordDivision => IsWordSeparator(scalar),
        SuggestionKind.Accentuation => CanonicalText.IsCombiningMark(scalar),
        _ => false,
    };

    private static bool IsWordSeparator(int scalar) => scalar is 0x0020 or 0x00A0 or 0x00B7 or 0x0387;

    private static bool OverwritesCorrectWitnessReading(
        SuggestionAudit audit,
        IReadOnlyList<int> referenceScalars,
        IReadOnlyList<int> rawScalars)
    {
        if (audit.Target is not { } range) return false;
        if (range.Start < 0 || range.End > rawScalars.Count || range.End > referenceScalars.Count || range.End < range.Start)
            return false;

        var raw = CanonicalText.FromScalars(rawScalars.Skip(range.Start).Take(range.End - range.Start));
        var reference = CanonicalText.FromScalars(referenceScalars.Skip(range.Start).Take(range.End - range.Start));
        if (!string.Equals(raw, reference, StringComparison.Ordinal)) return false; // the raw reading was not already correct

        var proposed = CanonicalText.Nfc(audit.ProposedText);
        return !string.Equals(proposed, raw, StringComparison.Ordinal);
    }

    /// <summary>
    /// CER with the same empty-reference convention as <see cref="RecognitionScorer"/>:
    /// 0 when both are empty, otherwise a ratio that is never NaN/Infinity.
    /// </summary>
    private static double Cer(int distance, int referenceLength) =>
        referenceLength == 0 ? (distance == 0 ? 0.0 : 1.0) : (double)distance / referenceLength;
}
