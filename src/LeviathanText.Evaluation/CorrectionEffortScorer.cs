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

/// <summary>
/// Per-suggestion accounting on NFD scalars: added scalars are those beyond the source
/// multiset; substitutions pair a removed source scalar with an added one.
/// </summary>
public sealed record SuggestionFinding(
    SuggestionKind Kind,
    SuggestionDisposition Disposition,
    int AddedScalars,
    int SubstitutedScalars,
    int UnsupportedAddedScalars,
    int UnsupportedSubstitutedScalars,
    int SuppliedAddedScalars,
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
/// Raw and reviewed output are scored separately against the reference. Either scalar
/// edit distance is symmetric when the review corrects the line completely, so the
/// quality gain is reported as CER improvement and the labor gain as recorded
/// correction time; the scalars carried by accepted suggestions are reported as the
/// text volume the model supplied.
/// </summary>
public sealed record CorrectionEffortScore(
    int ReferenceScalars,
    int RawScalars,
    int FinalScalars,
    int ScalarEditsWithoutAssistance,
    int ScalarEditsWithAssistance,
    double RawCer,
    double FinalCer,
    double CerImprovement,
    double CorrectionCerWithoutAssistance,
    double CorrectionCerWithAssistance,
    int AcceptedSuggestions,
    int RejectedSuggestions,
    int PendingSuggestions,
    int AcceptedSuggestionScalars,
    int UnsupportedAddedScalars,
    int UnsupportedSubstitutedScalars,
    int SuppliedRestorationScalars,
    int WitnessVariantOverwrites,
    IReadOnlyList<SuggestionFinding> Findings,
    CorrectionTime? Time);

/// <summary>
/// Scores a raw model line and the human-reviewed final line, then classifies each
/// reviewed suggestion. Unsupported changes are scalars a task was not allowed to make:
/// a candidate reading may not add marks the source does not show, word division may add
/// only separators, and accentuation may add only combining marks. Expansions and
/// restorations add supplied text and are counted as such, never as unsupported.
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

        var editsWithoutAssistance = EditScript.Compute(referenceScalars, rawScalars).Distance;
        var editsWithAssistance = EditScript.Compute(rawScalars, finalScalars).Distance;

        var rawCer = RecognitionScorer.Score(reference, raw).ScalarCer;
        var finalCer = RecognitionScorer.Score(reference, final).ScalarCer;

        var findings = audits.Select(audit => Classify(audit, referenceScalars, rawScalars)).ToArray();
        var accepted = findings.Where(f => f.Disposition == SuggestionDisposition.Accepted).ToArray();

        return new CorrectionEffortScore(
            referenceScalars.Count,
            rawScalars.Count,
            finalScalars.Count,
            editsWithoutAssistance,
            editsWithAssistance,
            rawCer,
            finalCer,
            rawCer - finalCer,
            Cer(editsWithoutAssistance, referenceScalars.Count),
            Cer(editsWithAssistance, referenceScalars.Count),
            accepted.Length,
            findings.Count(f => f.Disposition == SuggestionDisposition.Rejected),
            findings.Count(f => f.Disposition is SuggestionDisposition.Proposed or SuggestionDisposition.Expired),
            audits.Where((_, index) => findings[index].Disposition == SuggestionDisposition.Accepted)
                .Sum(a => CanonicalText.ToScalars(CanonicalText.Nfc(a.ProposedText)).Count),
            accepted.Sum(f => f.UnsupportedAddedScalars),
            accepted.Sum(f => f.UnsupportedSubstitutedScalars),
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
        var editsWithout = list.Sum(s => s.ScalarEditsWithoutAssistance);
        var editsWith = list.Sum(s => s.ScalarEditsWithAssistance);
        var rawCer = Cer(list.Sum(s => (int)Math.Round(s.RawCer * s.ReferenceScalars)), referenceScalars);
        var finalCer = Cer(list.Sum(s => (int)Math.Round(s.FinalCer * s.ReferenceScalars)), referenceScalars);

        var time = list.Any(s => s.Time is not null)
            ? new CorrectionTime(
                TimeSpan.FromTicks(list.Sum(s => s.Time?.Baseline.Ticks ?? 0)),
                TimeSpan.FromTicks(list.Sum(s => s.Time?.Assisted.Ticks ?? 0)))
            : null;

        return new CorrectionEffortScore(
            referenceScalars,
            list.Sum(s => s.RawScalars),
            list.Sum(s => s.FinalScalars),
            editsWithout,
            editsWith,
            rawCer,
            finalCer,
            rawCer - finalCer,
            Cer(editsWithout, referenceScalars),
            Cer(editsWith, referenceScalars),
            list.Sum(s => s.AcceptedSuggestions),
            list.Sum(s => s.RejectedSuggestions),
            list.Sum(s => s.PendingSuggestions),
            list.Sum(s => s.AcceptedSuggestionScalars),
            list.Sum(s => s.UnsupportedAddedScalars),
            list.Sum(s => s.UnsupportedSubstitutedScalars),
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
        // Classification uses NFD so a precomposed accented vowel counts as base + mark.
        var sourceScalars = CanonicalText.ToScalars(CanonicalText.Nfd(audit.SourceText));
        var proposedScalars = CanonicalText.ToScalars(CanonicalText.Nfd(audit.ProposedText));

        var available = new Dictionary<int, int>();
        foreach (var scalar in sourceScalars)
            available[scalar] = available.GetValueOrDefault(scalar) + 1;

        var added = new List<int>();
        foreach (var scalar in proposedScalars)
        {
            if (available.TryGetValue(scalar, out var count) && count > 0)
                available[scalar] = count - 1;
            else
                added.Add(scalar);
        }

        var matched = proposedScalars.Count - added.Count;
        var removed = sourceScalars.Count - matched;
        var substituted = Math.Min(added.Count, removed);

        var unsupportedAdded = added.Count(scalar => !IsExpectedAddition(audit.Kind, scalar));
        var suppliedAdded = added.Count - unsupportedAdded;
        var unsupportedSubstituted = audit.Kind is SuggestionKind.WordDivision or SuggestionKind.Accentuation
            ? substituted
            : 0;

        var overwrites = audit.Disposition == SuggestionDisposition.Accepted
            && audit.Kind == SuggestionKind.CandidateReading
            && OverwritesCorrectWitnessReading(audit, referenceScalars, rawScalars);

        return new SuggestionFinding(
            audit.Kind,
            audit.Disposition,
            added.Count,
            substituted,
            unsupportedAdded,
            unsupportedSubstituted,
            suppliedAdded,
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
