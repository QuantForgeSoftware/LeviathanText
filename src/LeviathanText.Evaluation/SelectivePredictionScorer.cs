namespace LeviathanText.Evaluation;

/// <summary>One scored grapheme or decision with its model confidence.</summary>
public sealed record GraphemeSample(double Confidence, bool Correct);

/// <summary>One bin of a reliability diagram.</summary>
public sealed record CalibrationBin(double LowerBound, double UpperBound, int Count, double MeanConfidence, double Accuracy);

/// <summary>
/// Reliability of raw model confidence: expected and maximum calibration error over
/// equal-width bins. A model that is confidently wrong shows up as a high ECE; this is
/// why model confidence is never reported as scholarly certainty.
/// </summary>
public sealed record CalibrationReport(double ExpectedCalibrationError, double MaximumCalibrationError, IReadOnlyList<CalibrationBin> Bins);

/// <summary>
/// One line's confidence and its scored output size. <paramref name="ReferenceScalars"/>
/// lets coverage be text-weighted rather than line-counted.
/// </summary>
public sealed record SelectiveSample(double Confidence, int ReferenceScalars, int ScalarDistance);

/// <summary>One point on the risk–coverage curve.</summary>
public sealed record RiskCoveragePoint(double Coverage, double RiskCer, int RetainedSamples);

/// <summary>
/// Selective-prediction summary (acceptance-plan.md §3): how CER behaves as the system
/// abstains on its least confident lines. Lower AURC is better; the curve exposes a
/// broad-mask or empty-output trick instead of hiding it in one number.
/// </summary>
public sealed record SelectivePredictionReport(
    int TotalSamples,
    int TotalReferenceScalars,
    double FullCoverageCer,
    double AreaUnderRiskCoverageCurve,
    IReadOnlyList<RiskCoveragePoint> Curve)
{
    /// <summary>
    /// The largest text-weighted coverage whose CER stays at or below
    /// <paramref name="targetCer"/>. Zero when even the most confident point misses it.
    /// </summary>
    public double CoverageForCerBelow(double targetCer) =>
        Curve.Where(p => p.RiskCer <= targetCer).Select(p => p.Coverage).DefaultIfEmpty(0.0).Max();
}

/// <summary>Calibration and risk–coverage scoring; no numeric threshold is invented here.</summary>
public static class SelectivePredictionScorer
{
    public static CalibrationReport Calibration(IReadOnlyList<GraphemeSample> samples, int bins = 10)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (bins < 1) throw new ArgumentOutOfRangeException(nameof(bins), "At least one bin is required.");
        if (samples.Count == 0) throw new ArgumentException("At least one sample is required.", nameof(samples));

        foreach (var sample in samples)
            if (double.IsNaN(sample.Confidence) || sample.Confidence is < 0 or > 1)
                throw new ArgumentException("Confidence must be within [0,1].", nameof(samples));

        var counts = new int[bins];
        var confidenceSums = new double[bins];
        var correct = new int[bins];
        foreach (var sample in samples)
        {
            var index = Math.Min((int)(sample.Confidence * bins), bins - 1);
            counts[index]++;
            confidenceSums[index] += sample.Confidence;
            if (sample.Correct) correct[index]++;
        }

        var result = new List<CalibrationBin>(bins);
        double ece = 0, mce = 0;
        for (var i = 0; i < bins; i++)
        {
            if (counts[i] == 0)
            {
                result.Add(new CalibrationBin((double)i / bins, (double)(i + 1) / bins, 0, 0, 0));
                continue;
            }

            var mean = confidenceSums[i] / counts[i];
            var accuracy = (double)correct[i] / counts[i];
            var gap = Math.Abs(accuracy - mean);
            ece += counts[i] / (double)samples.Count * gap;
            mce = Math.Max(mce, gap);
            result.Add(new CalibrationBin((double)i / bins, (double)(i + 1) / bins, counts[i], mean, accuracy));
        }

        return new CalibrationReport(ece, mce, result);
    }

    public static SelectivePredictionReport Selective(IReadOnlyList<SelectiveSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count == 0) throw new ArgumentException("At least one sample is required.", nameof(samples));
        if (samples.Any(s => s.ReferenceScalars < 0 || s.ScalarDistance < 0))
            throw new ArgumentException("Reference length and distance must be non-negative.", nameof(samples));

        var totalReference = samples.Sum(s => s.ReferenceScalars);
        if (totalReference == 0) throw new ArgumentException("At least one reference scalar is required.", nameof(samples));

        var ordered = samples.OrderByDescending(s => s.Confidence).ToArray();
        var curve = new List<RiskCoveragePoint>(ordered.Length);
        int retainedReference = 0, retainedDistance = 0;
        for (var i = 0; i < ordered.Length; i++)
        {
            retainedReference += ordered[i].ReferenceScalars;
            retainedDistance += ordered[i].ScalarDistance;
            var coverage = (double)retainedReference / totalReference;
            var risk = retainedReference == 0 ? 0 : (double)retainedDistance / retainedReference;
            curve.Add(new RiskCoveragePoint(coverage, risk, i + 1));
        }

        // Rectangle rule over the retained prefixes: AURC = risk at each point times the
        // coverage gained since the previous point (starting from coverage 0).
        double area = 0;
        double previousCoverage = 0;
        foreach (var point in curve)
        {
            area += point.RiskCer * (point.Coverage - previousCoverage);
            previousCoverage = point.Coverage;
        }

        return new SelectivePredictionReport(
            samples.Count,
            totalReference,
            (double)samples.Sum(s => s.ScalarDistance) / totalReference,
            area,
            curve);
    }
}
