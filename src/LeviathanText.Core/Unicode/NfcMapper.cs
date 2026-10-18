using LeviathanText.Contracts;

namespace LeviathanText.Core.Unicode;

/// <summary>
/// Result of NFC normalization with a source-scalar → normalized-scalar map, so
/// annotations survive normalization without detaching (greek-transcription.md §4).
/// </summary>
public sealed record NfcMapping
{
    public required string Normalized { get; init; }

    /// <summary>
    /// For each source scalar index: the normalized scalar index it maps to. Under
    /// canonical composition (e.g. α + U+0301 → ά) several source scalars can map to one
    /// normalized scalar; counts are otherwise preserved. The array has one extra entry
    /// at <see cref="SourceScalarLength"/> holding the final target offset, so
    /// end-anchored ranges (End == source length) are addressable. Never null within range.
    /// </summary>
    public required int[] SourceToTarget { get; init; }

    public required int SourceScalarLength { get; init; }
    public required int TargetScalarLength { get; init; }

    /// <summary>
    /// Map an annotation range from source to normalized text. Canonical composition
    /// (e.g. α + U+0301 → ά) merges contiguous source scalars into one target scalar;
    /// a range that intersects such a merged run without containing it cannot be
    /// represented and fails with <see cref="ErrorCode.StaleAnnotationAnchor"/> — the
    /// caller rebases deliberately (domain-and-pipeline.md §6).
    /// </summary>
    public ScalarRange MapRange(ScalarRange range)
    {
        int rangeStart = range.Start;
        int rangeEnd = range.End;
        int n = SourceToTarget.Length;

        // Merged runs: maximal spans of contiguous source scalars sharing one target scalar.
        int i = 0;
        while (i < n)
        {
            int j = i;
            while (j + 1 < n && SourceToTarget[j + 1] == SourceToTarget[i]) j++;

            bool intersects = i < rangeEnd && j >= rangeStart;
            bool contained = i >= rangeStart && j < rangeEnd;
            if (intersects && !contained)
                throw new LeviathanException(new LeviathanError
                {
                    Code = ErrorCode.StaleAnnotationAnchor,
                    Scope = $"range {range}",
                    Details = new Dictionary<string, string> { ["reason"] = "range boundary intersects a canonically composed cluster" },
                    RemedyHint = RemedyHint.RebaseRevision,
                    Message = "Annotation range splits a composed cluster under NFC; rebase the annotation on grapheme-cluster boundaries.",
                });

            i = j + 1;
        }

        return new ScalarRange(SourceToTarget[rangeStart], SourceToTarget[rangeEnd]);
    }
}

/// <summary>
/// NFC storage normalization with offset mapping. Normalizes per extended grapheme
/// cluster and concatenates; a guard verifies the result equals whole-string NFC, so
/// per-cluster normalization is provably equivalent for the inputs it accepts.
/// </summary>
public static class NfcMapper
{
    public static NfcMapping Map(string source)
    {
        var clusters = Graphemes.Enumerate(source).ToArray();
        var wholeNfc = source.Normalize(System.Text.NormalizationForm.FormC);

        var pieces = new string[clusters.Length];
        int sourceScalars = ScalarText.ScalarLength(source);
        var sourceToTarget = new int[sourceScalars + 1];

        int targetScalar = 0;
        for (int c = 0; c < clusters.Length; c++)
        {
            var (startScalar, _, clusterText) = clusters[c];
            var normalized = clusterText.Normalize(System.Text.NormalizationForm.FormC);
            pieces[c] = normalized;

            int clusterSourceScalars = ScalarText.ScalarLength(clusterText);
            int clusterTargetScalars = ScalarText.ScalarLength(normalized);

            if (clusterTargetScalars == clusterSourceScalars)
            {
                for (int i = 0; i < clusterSourceScalars; i++)
                    sourceToTarget[startScalar + i] = targetScalar + i;
            }
            else
            {
                // Composition merged some scalars. Map each source scalar to the last
                // normalized scalar of its prefix: scalar k → (scalar count of
                // NFC(prefix through k)) − 1. Monotone and deterministic; only the
                // actually composed scalars merge into one target.
                for (int i = 0; i < clusterSourceScalars; i++)
                {
                    int utf16End = ScalarText.ScalarToUtf16Index(clusterText, i + 1);
                    int prefixTargets = ScalarText.ScalarLength(clusterText[..utf16End].Normalize(System.Text.NormalizationForm.FormC));
                    sourceToTarget[startScalar + i] = targetScalar + prefixTargets - 1;
                }
            }
            targetScalar += clusterTargetScalars;
        }
        sourceToTarget[sourceScalars] = targetScalar; // end-of-text boundary

        var normalizedText = string.Concat(pieces);
        if (!string.Equals(normalizedText, wholeNfc, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Per-cluster NFC diverged from whole-string NFC; refusing to produce a mapping (source: {source}).");

        return new NfcMapping
        {
            Normalized = normalizedText,
            SourceToTarget = sourceToTarget,
            SourceScalarLength = sourceScalars,
            TargetScalarLength = ScalarText.ScalarLength(normalizedText),
        };
    }
}
