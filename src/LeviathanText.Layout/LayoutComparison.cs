namespace LeviathanText.Layout;

/// <summary>
/// Automatic-vs-imported layout comparison (Phase 0 gate: compare automatic and
/// manually supplied layout; todo §7 layout metrics). Matching is **one-to-one greedy by
/// IoU** at the disclosed threshold: one detected band can account for at most one GT line
/// (a merged detection must not inflate both precision and recall). Reading-order agreement
/// is reported separately.
/// </summary>
public static class LayoutComparison
{
    public sealed record Record(
        string Witness,
        int GroundTruthLines,
        int DetectedLines,
        int Matches,
        double Precision,
        double Recall,
        double MeanVerticalIou);

    public sealed record Summary(IReadOnlyList<Record> PerWitness, double MeanPrecision, double MeanRecall, double IouThreshold);

    public static Summary Compare(
        IEnumerable<(string Witness, IReadOnlyList<DetectedLine> Detected, IReadOnlyList<(int Top, int Bottom)> GroundTruth)> witnesses,
        double iouThreshold = 0.5)
    {
        var perWitness = new List<Record>();
        foreach (var (witness, detected, groundTruth) in witnesses)
        {
            // Candidate pairs at/above the threshold, greedily consumed by descending IoU.
            var candidates = new List<(int Gt, int Det, double Iou)>();
            for (int g = 0; g < groundTruth.Count; g++)
                for (int d = 0; d < detected.Count; d++)
                {
                    double iou = VerticalIou(groundTruth[g], detected[d]);
                    if (iou >= iouThreshold) candidates.Add((g, d, iou));
                }

            var usedGt = new bool[groundTruth.Count];
            var usedDet = new bool[detected.Count];
            int matches = 0;
            double iouSum = 0;
            foreach (var (g, d, iou) in candidates.OrderByDescending(c => c.Iou).ThenBy(c => c.Gt).ThenBy(c => c.Det))
            {
                if (usedGt[g] || usedDet[d]) continue;
                usedGt[g] = true;
                usedDet[d] = true;
                matches++;
                iouSum += iou;
            }

            double precision = detected.Count == 0 ? 0 : (double)matches / detected.Count;
            double recall = groundTruth.Count == 0 ? 0 : (double)matches / groundTruth.Count;
            perWitness.Add(new Record(witness, groundTruth.Count, detected.Count, matches, precision, recall,
                matches == 0 ? 0 : iouSum / matches));
        }

        return new Summary(perWitness,
            perWitness.Count == 0 ? 0 : perWitness.Average(r => r.Precision),
            perWitness.Count == 0 ? 0 : perWitness.Average(r => r.Recall),
            iouThreshold);
    }

    private static double VerticalIou((int Top, int Bottom) gt, DetectedLine det) =>
        IoU(gt.Top, gt.Bottom, det.Top, det.Bottom);

    private static double IoU(int a0, int a1, int b0, int b1)
    {
        int inter = Math.Min(a1, b1) - Math.Max(a0, b0);
        if (inter <= 0) return 0;
        double union = (a1 - a0) + (b1 - b0) - inter;
        return inter / union;
    }
}
