using System.Text;

namespace LeviathanText.Evaluation;

/// <summary>Completed means the caller verified a complete, protocol-valid model response.</summary>
public enum VlmCompletionStatus
{
    Completed,
    InvalidOutput,
    Truncated,
    Timeout,
    TransportError,
    Cancelled,
    InputRejected,
    NotAttempted,
}

public enum VlmControlKind
{
    NaturalLine,
    NoCharacterTraces,
    UnreadableTraces,
    MaskedCounterfactual,
}

public enum VlmControlOutcome
{
    NotAControl,
    Pass,
    Fail,
    NeedsManualReview,
}

/// <summary>
/// Offline reference only; never part of a model request. Unreadable-trace references
/// must contain only repetitions of their frozen non-letter uncertainty marker.
/// </summary>
public sealed record VlmBenchmarkReference(
    string SampleId,
    string PhysicalUnit,
    string Material,
    string ReferenceText,
    VlmControlKind ControlKind = VlmControlKind.NaturalLine,
    string? UncertaintyMarker = null);

/// <summary>
/// CapturedModelText is the entire model-generated text, including nonempty failed or
/// truncated output. HTTP error bodies are not model text. Null means no captured output;
/// an explicit empty string can be a successful blank response.
/// </summary>
public sealed record VlmBenchmarkOutput(
    string SampleId,
    VlmCompletionStatus Status,
    string? CapturedModelText);

public sealed record VlmBenchmarkLineScore(
    string SampleId,
    string PhysicalUnit,
    string Material,
    VlmControlKind ControlKind,
    VlmCompletionStatus Status,
    int ReferenceScalars,
    int HypothesisScalars,
    int Substitutions,
    int Deletions,
    int Insertions,
    double? ScalarCer,
    bool ExactTextMatch,
    bool ValidCompleted,
    VlmControlOutcome ControlOutcome)
{
    public long ScalarDistance => (long)Substitutions + Deletions + Insertions;
}

/// <summary>Natural-line totals only. Undefined ratios are null, never fabricated zero.</summary>
public sealed record VlmBenchmarkAggregate(
    int NaturalLineCount,
    int PhysicalUnitCount,
    long ReferenceScalars,
    long HypothesisScalars,
    long Substitutions,
    long Deletions,
    long Insertions,
    int ValidCompletedCount,
    int NonemptyOutputCount,
    int ExactLineCount,
    double? MicroCer,
    double? MacroPhysicalUnitCer,
    double? ValidCompletionCoverage,
    double? NonemptyOutputCoverage,
    double? ExactLineAccuracy,
    double? OutputReferenceLengthRatio)
{
    public long ScalarDistance => Substitutions + Deletions + Insertions;
}

public sealed record VlmBenchmarkGroupScore(string Key, VlmBenchmarkAggregate Score);

public sealed record VlmBenchmarkControlSummary(
    VlmControlKind Kind, int Count, int Passed, int Failed, int NeedsManualReview);

/// <summary>
/// One model/condition/repetition. No confidence interval, scholarly readiness or adoption
/// decision is inferred. Repeated runs remain separate repeated measurements.
/// </summary>
public sealed record VlmBenchmarkRunScore(
    string PolicyVersion,
    IReadOnlyList<VlmBenchmarkLineScore> Rows,
    VlmBenchmarkAggregate Overall,
    IReadOnlyList<VlmBenchmarkGroupScore> ByMaterial,
    IReadOnlyList<VlmBenchmarkGroupScore> ByPhysicalUnit,
    IReadOnlyList<VlmBenchmarkControlSummary> Controls);

/// <summary>
/// VLM-BENCH-1 offline scoring: canonical NFC scalar edit counts, full expected-sample
/// denominators, and grounding controls separated from natural transcription accuracy.
/// No trimming, case/sigma folding, accent stripping, prose extraction, or CER cap.
/// </summary>
public static class VlmBenchmarkScorer
{
    public const string PolicyVersion = "VLM-NFC-SCALAR-1";
    public const int MaxSamples = 1000;
    public const int MaxReferenceScalars = 8192;
    public const int MaxHypothesisScalars = 1_048_576;
    public const long MaxAlignmentCellsPerRun = 16_000_000;

    public static VlmBenchmarkRunScore ScoreRun(
        IEnumerable<VlmBenchmarkReference> references,
        IEnumerable<VlmBenchmarkOutput> outputs)
    {
        ArgumentNullException.ThrowIfNull(references);
        ArgumentNullException.ThrowIfNull(outputs);
        var expected = references.Take(MaxSamples + 1).ToArray();
        if (expected.Length > MaxSamples)
            throw new ArgumentException($"A scoring run supports at most {MaxSamples} samples; no partial score was produced.", nameof(references));
        var expectedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var reference in expected)
        {
            ValidateReference(reference);
            if (!expectedIds.Add(reference.SampleId))
                throw new ArgumentException($"Duplicate reference sample '{reference.SampleId}'.", nameof(references));
        }

        var observed = new Dictionary<string, VlmBenchmarkOutput>(StringComparer.Ordinal);
        foreach (var output in outputs)
        {
            ArgumentNullException.ThrowIfNull(output);
            if (!expectedIds.Contains(output.SampleId))
                throw new ArgumentException($"Output sample '{output.SampleId}' is not in the frozen references.", nameof(outputs));
            if (!Enum.IsDefined(output.Status))
                throw new ArgumentException("Unknown completion status.", nameof(outputs));
            if (output.Status == VlmCompletionStatus.NotAttempted && output.CapturedModelText is not null)
                throw new ArgumentException("A not-attempted request cannot have captured model text.", nameof(outputs));
            if (!observed.TryAdd(output.SampleId, output))
                throw new ArgumentException($"Duplicate output sample '{output.SampleId}'; score repetitions separately.", nameof(outputs));
        }

        // Validate the complete workload before any alignment. Rolling storage alone
        // cannot bound quadratic CPU work, including many individually small rows.
        var prepared = Preflight(expected, observed);
        var rows = prepared.Select(Score).ToArray();
        var natural = rows.Where(row => row.ControlKind == VlmControlKind.NaturalLine).ToArray();
        return new(
            PolicyVersion,
            Array.AsReadOnly(rows),
            Aggregate(natural),
            Group(natural, row => row.Material),
            Group(natural, row => row.PhysicalUnit),
            rows.Where(row => row.ControlKind != VlmControlKind.NaturalLine)
                .GroupBy(row => row.ControlKind).OrderBy(group => group.Key)
                .Select(group => new VlmBenchmarkControlSummary(group.Key, group.Count(),
                    group.Count(row => row.ControlOutcome == VlmControlOutcome.Pass),
                    group.Count(row => row.ControlOutcome == VlmControlOutcome.Fail),
                    group.Count(row => row.ControlOutcome == VlmControlOutcome.NeedsManualReview))).ToArray());
    }

    private sealed record PreparedScore(VlmBenchmarkReference Reference, VlmCompletionStatus Status,
        string Target, string Hypothesis, int ReferenceScalars, int HypothesisScalars);

    private static PreparedScore[] Preflight(VlmBenchmarkReference[] references,
        IReadOnlyDictionary<string, VlmBenchmarkOutput> outputs)
    {
        var prepared = new List<PreparedScore>(references.Length);
        long cells = 0;
        foreach (var reference in references)
        {
            outputs.TryGetValue(reference.SampleId, out var output);
            var status = output?.Status ?? VlmCompletionStatus.NotAttempted;
            if (status == VlmCompletionStatus.Completed && output?.CapturedModelText is null)
                status = VlmCompletionStatus.InvalidOutput;
            string target = CanonicalText.Nfc(reference.ReferenceText);
            string hypothesis = CanonicalText.Nfc(output?.CapturedModelText ?? string.Empty);
            int targetLength = CanonicalText.ScalarLength(target);
            int hypothesisLength = CanonicalText.ScalarLength(hypothesis);
            if (targetLength > MaxReferenceScalars || hypothesisLength > MaxHypothesisScalars)
                throw new ArgumentException($"Sample '{reference.SampleId}' exceeds the NFC scalar limits " +
                    $"({MaxReferenceScalars} reference, {MaxHypothesisScalars} hypothesis); no partial score was produced.");
            // Include initialized axes so empty-reference controls and absent outputs
            // still consume work/storage budget. Multiplication uses checked bounds/long.
            cells += ((long)targetLength + 1) * ((long)hypothesisLength + 1);
            if (cells > MaxAlignmentCellsPerRun)
                throw new ArgumentException($"The complete scoring run exceeds {MaxAlignmentCellsPerRun} alignment cells; " +
                    "retain the raw artifacts and revise the declared scoring workload before retrying. No partial score was produced.");
            prepared.Add(new(reference, status, target, hypothesis, targetLength, hypothesisLength));
        }
        return prepared.ToArray();
    }

    private static VlmBenchmarkLineScore Score(PreparedScore prepared)
    {
        var reference = prepared.Reference;
        var status = prepared.Status;
        var targetScalars = Scalars(prepared.Target);
        var hypothesisScalars = Scalars(prepared.Hypothesis);
        var errors = CountEdits(targetScalars, hypothesisScalars);
        bool completed = status == VlmCompletionStatus.Completed;
        bool exact = string.Equals(prepared.Target, prepared.Hypothesis, StringComparison.Ordinal);
        var control = reference.ControlKind switch
        {
            VlmControlKind.NaturalLine => VlmControlOutcome.NotAControl,
            VlmControlKind.MaskedCounterfactual when completed => VlmControlOutcome.NeedsManualReview,
            _ when completed && exact => VlmControlOutcome.Pass,
            _ => VlmControlOutcome.Fail,
        };
        return new(reference.SampleId, reference.PhysicalUnit, reference.Material, reference.ControlKind,
            status, targetScalars.Count, hypothesisScalars.Count, errors.Substitutions, errors.Deletions,
            errors.Insertions, reference.ControlKind == VlmControlKind.NaturalLine
                ? (double)errors.Distance / targetScalars.Count : null,
            exact, completed, control);
    }

    private static VlmBenchmarkAggregate Aggregate(VlmBenchmarkLineScore[] rows)
    {
        long reference = rows.Sum(row => (long)row.ReferenceScalars);
        long hypothesis = rows.Sum(row => (long)row.HypothesisScalars);
        long substitutions = rows.Sum(row => (long)row.Substitutions);
        long deletions = rows.Sum(row => (long)row.Deletions);
        long insertions = rows.Sum(row => (long)row.Insertions);
        int completed = rows.Count(row => row.ValidCompleted);
        int nonempty = rows.Count(row => row.HypothesisScalars > 0);
        int exact = rows.Count(row => row.ExactTextMatch);
        var units = rows.GroupBy(row => row.PhysicalUnit, StringComparer.Ordinal).ToArray();
        return new(rows.Length, units.Length, reference, hypothesis, substitutions, deletions, insertions,
            completed, nonempty, exact,
            Ratio(substitutions + deletions + insertions, reference),
            units.Length == 0 ? null : units.Average(unit =>
                (double)unit.Sum(row => row.ScalarDistance) / unit.Sum(row => (long)row.ReferenceScalars)),
            Ratio(completed, rows.Length), Ratio(nonempty, rows.Length), Ratio(exact, rows.Length),
            Ratio(hypothesis, reference));
    }

    private static VlmBenchmarkGroupScore[] Group(VlmBenchmarkLineScore[] rows, Func<VlmBenchmarkLineScore, string> key)
        => rows.GroupBy(key, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new VlmBenchmarkGroupScore(group.Key, Aggregate(group.ToArray()))).ToArray();

    private static double? Ratio(long numerator, long denominator)
        => denominator == 0 ? null : (double)numerator / denominator;

    private static void ValidateReference(VlmBenchmarkReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(reference.SampleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reference.PhysicalUnit);
        ArgumentException.ThrowIfNullOrWhiteSpace(reference.Material);
        ArgumentNullException.ThrowIfNull(reference.ReferenceText);
        if (!Enum.IsDefined(reference.ControlKind))
            throw new ArgumentException("Unknown control kind.", nameof(reference));
        if (reference.ControlKind == VlmControlKind.NaturalLine && reference.ReferenceText.Length == 0)
            throw new ArgumentException("An empty reference must be explicitly classified as a control.", nameof(reference));
        if (reference.ControlKind == VlmControlKind.NoCharacterTraces && reference.ReferenceText.Length != 0)
            throw new ArgumentException("No-character-trace controls require an empty reference.", nameof(reference));
        if (reference.ControlKind == VlmControlKind.UnreadableTraces)
        {
            string? marker = reference.UncertaintyMarker;
            if (string.IsNullOrEmpty(marker) || marker.EnumerateRunes().Any(rune => Rune.IsLetterOrDigit(rune) || Rune.IsWhiteSpace(rune)))
                throw new ArgumentException("Unreadable-trace controls require a non-letter, non-whitespace uncertainty marker.", nameof(reference));
            string target = CanonicalText.Nfc(reference.ReferenceText);
            marker = CanonicalText.Nfc(marker);
            if (target.Length == 0 || target.Replace(marker, string.Empty, StringComparison.Ordinal).Length != 0)
                throw new ArgumentException("Unreadable-trace references must contain only the frozen uncertainty marker.", nameof(reference));
        }
    }

    private static List<int> Scalars(string text)
    {
        // NFC rejects malformed UTF-16 rather than silently replacing an invalid code unit.
        // Enumerating valid runes is linear, unlike repeatedly slicing from the start.
        var scalars = new List<int>(text.Length);
        foreach (var rune in text.EnumerateRunes()) scalars.Add(rune.Value);
        return scalars;
    }

    private readonly record struct Edits(int Substitutions, int Deletions, int Insertions)
    {
        public int Distance => Substitutions + Deletions + Insertions;
    }

    private static Edits CountEdits(IReadOnlyList<int> reference, IReadOnlyList<int> hypothesis)
    {
        // The same match/substitute/delete/insert tie order as EditScript.Compute,
        // retaining only two rows. Long generated prose cannot allocate a quadratic
        // traceback matrix; every emitted scalar still contributes to the score.
        var previous = new Edits[hypothesis.Count + 1];
        var current = new Edits[hypothesis.Count + 1];
        for (int j = 1; j <= hypothesis.Count; j++) previous[j] = new(0, 0, j);
        for (int i = 1; i <= reference.Count; i++)
        {
            current[0] = new(0, i, 0);
            for (int j = 1; j <= hypothesis.Count; j++)
            {
                var diagonal = previous[j - 1];
                if (reference[i - 1] != hypothesis[j - 1])
                    diagonal = diagonal with { Substitutions = diagonal.Substitutions + 1 };
                var deleted = previous[j] with { Deletions = previous[j].Deletions + 1 };
                var inserted = current[j - 1] with { Insertions = current[j - 1].Insertions + 1 };
                var best = diagonal;
                if (deleted.Distance < best.Distance) best = deleted;
                if (inserted.Distance < best.Distance) best = inserted;
                current[j] = best;
            }
            (previous, current) = (current, previous);
        }
        return previous[hypothesis.Count];
    }
}
