using LeviathanText.Core.Unicode;

namespace LeviathanText.Evaluation;

/// <summary>
/// One decoded grapheme's confidence, in scalar offsets of the hypothesis text. This is
/// the Evaluation-side shape of a recognizer's per-grapheme confidence; keeping it local
/// avoids an Evaluation→Recognition dependency.
/// </summary>
public sealed record GraphemeSpan(int StartScalar, int EndScalar, double Confidence);

/// <summary>
/// Pairs decoded grapheme confidences with correctness so
/// <see cref="SelectivePredictionScorer.Calibration"/> can be run on real output.
/// Alignment is grapheme-level (not NFD scalar-level) because a confidence value belongs
/// to a whole cluster: splitting a precomposed vowel would fabricate samples.
/// A reference grapheme that was never produced (a deletion) has no confidence and is
/// skipped rather than assigned a made-up value.
/// </summary>
public static class GraphemeCalibration
{
    public static IReadOnlyList<GraphemeSample> Samples(
        string reference,
        string hypothesis,
        IReadOnlyList<GraphemeSpan> hypothesisGraphemes)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(hypothesis);
        ArgumentNullException.ThrowIfNull(hypothesisGraphemes);

        // Text is used exactly as produced: NFC would compose decomposed sequences and
        // shift the scalar offsets the confidence spans refer to.
        var referenceGraphemes = Graphemes.Enumerate(reference).Select(g => g.Text).ToList();
        var hypothesisClusters = Graphemes.Enumerate(hypothesis).ToList();
        var scalarLength = CanonicalText.ScalarLength(hypothesis);

        foreach (var span in hypothesisGraphemes)
        {
            if (double.IsNaN(span.Confidence) || span.Confidence is < 0 or > 1)
                throw new ArgumentException("Grapheme confidence must be within [0,1].", nameof(hypothesisGraphemes));
            if (span.StartScalar < 0 || span.EndScalar > scalarLength || span.EndScalar <= span.StartScalar)
                throw new ArgumentException($"Grapheme span {span.StartScalar}..{span.EndScalar} is outside the hypothesis.", nameof(hypothesisGraphemes));
        }

        var hypothesisTexts = hypothesisClusters.Select(g => g.Text).ToList();
        var script = EditScript.Compute(referenceGraphemes, hypothesisTexts);

        var samples = new List<GraphemeSample>(hypothesisTexts.Count);
        foreach (var op in script.Ops)
        {
            if (op.HypothesisIndex < 0) continue; // deletion: no produced grapheme to calibrate

            var confidence = ConfidenceFor(hypothesisGraphemes, hypothesisClusters[op.HypothesisIndex].StartScalar);
            samples.Add(new GraphemeSample(confidence, op.Kind == EditKind.Match));
        }

        return samples;
    }

    private static double ConfidenceFor(IReadOnlyList<GraphemeSpan> spans, int startScalar)
    {
        foreach (var span in spans)
            if (span.StartScalar == startScalar)
                return span.Confidence;

        throw new ArgumentException(
            $"No grapheme confidence starts at scalar {startScalar}; every produced grapheme needs one.",
            nameof(spans));
    }
}
