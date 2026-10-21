namespace LeviathanText.Layout;

/// <summary>
/// One-dimensional line-detection metrics with one-to-one matching by center distance.
/// Kept separate from LayoutComparison (which is IoU/vertical-band based) so callers can
/// choose the disclosed metric. Empty/empty is perfect by convention; extra detections
/// always penalize precision.
/// </summary>
public static class DetectionMetrics
{
    public readonly record struct Interval(double Top, double Bottom)
    {
        public double Center => (Top + Bottom) / 2;
    }

    public sealed record Result(double Precision, double Recall, double F1, int NumPred, int NumGt);

    public static Result Compute(IReadOnlyList<Interval> predictions, IReadOnlyList<Interval> truth, double tolerance)
    {
        if (predictions.Count == 0 && truth.Count == 0) return new Result(1, 1, 1, 0, 0);
        if (predictions.Count == 0 || truth.Count == 0) return new Result(0, 0, 0, predictions.Count, truth.Count);

        var pairs = new List<(int Pred, int Gt, double Distance)>();
        for (int p = 0; p < predictions.Count; p++)
            for (int g = 0; g < truth.Count; g++)
            {
                double distance = Math.Abs(predictions[p].Center - truth[g].Center);
                if (distance <= tolerance) pairs.Add((p, g, distance));
            }

        var usedPred = new bool[predictions.Count];
        var usedGt = new bool[truth.Count];
        int matches = 0;
        foreach (var (p, g, _) in pairs.OrderBy(x => x.Distance).ThenBy(x => x.Pred).ThenBy(x => x.Gt))
        {
            if (usedPred[p] || usedGt[g]) continue;
            usedPred[p] = usedGt[g] = true;
            matches++;
        }

        double precision = (double)matches / predictions.Count;
        double recall = (double)matches / truth.Count;
        double f1 = precision + recall == 0 ? 0 : 2 * precision * recall / (precision + recall);
        return new Result(precision, recall, f1, predictions.Count, truth.Count);
    }

    /// <summary>Macro-average across pages, matching Kraken's aggregation semantics.</summary>
    public static Result Aggregate(IReadOnlyList<Result> pages)
    {
        if (pages.Count == 0) return new Result(0, 0, 0, 0, 0);
        double precision = pages.Average(p => p.Precision);
        double recall = pages.Average(p => p.Recall);
        double f1 = pages.Average(p => p.F1);
        return new Result(precision, recall, f1, pages.Sum(p => p.NumPred), pages.Sum(p => p.NumGt));
    }
}
