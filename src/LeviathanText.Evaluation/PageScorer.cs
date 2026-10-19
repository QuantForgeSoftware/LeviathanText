namespace LeviathanText.Evaluation;

/// <summary>Ground truth for one line/region. A blank (uninscribed) region is explicit, never inferred.</summary>
public sealed record LineTruth(string LineId, string Reference, bool IsBlank = false);

/// <summary>A model output for one line. Null hypothesis = the line was not proposed at all.</summary>
public sealed record LinePrediction(string LineId, string? Hypothesis);

/// <summary>Per-line outcome, retained so a report can drill into failures.</summary>
public sealed record LineOutcome(
    string LineId,
    int ReferenceScalars,
    int ScalarDistance,
    bool Missed,
    int BlankRegionInsertions);

/// <summary>
/// Page-level score. Missed lines and text recognized on blank regions are counted
/// in the denominator, never dropped (examples.md §1, EX-9).
/// </summary>
public sealed record PageScore(
    int LineCount,
    int ReferenceScalarLength,
    int ScalarDistance,
    double ScalarCer,
    int MissedLines,
    int BlankRegionInsertions,
    IReadOnlyList<LineOutcome> Lines);

/// <summary>Aggregates line scores with explicit blank/missed-line handling.</summary>
public static class PageScorer
{
    public static PageScore Evaluate(IEnumerable<(LineTruth Truth, LinePrediction? Prediction)> lines)
    {
        int lineCount = 0, referenceScalars = 0, distance = 0, missed = 0, blankInsertions = 0;
        var outcomes = new List<LineOutcome>();

        foreach (var (truth, prediction) in lines)
        {
            lineCount++;
            string? hypothesis = prediction?.Hypothesis;

            if (truth.IsBlank)
            {
                int inserted = string.IsNullOrEmpty(hypothesis) ? 0 : CanonicalText.ScalarLength(CanonicalText.Nfc(hypothesis));
                blankInsertions += inserted;
                distance += inserted;
                outcomes.Add(new LineOutcome(truth.LineId, 0, inserted, Missed: false, inserted));
                continue;
            }

            int refLen = CanonicalText.ScalarLength(CanonicalText.Nfc(truth.Reference));
            referenceScalars += refLen;

            if (string.IsNullOrEmpty(hypothesis))
            {
                // Whole line missed: scored as a full deletion, not omitted.
                missed++;
                distance += refLen;
                outcomes.Add(new LineOutcome(truth.LineId, refLen, refLen, Missed: true, 0));
                continue;
            }

            var score = RecognitionScorer.Score(truth.Reference, hypothesis);
            distance += score.ScalarDistance;
            outcomes.Add(new LineOutcome(truth.LineId, refLen, score.ScalarDistance, Missed: false, 0));
        }

        double denominator = referenceScalars + blankInsertions;
        double cer = denominator == 0 ? 0.0 : distance / denominator;

        return new PageScore(lineCount, referenceScalars, distance, cer, missed, blankInsertions, outcomes);
    }
}
