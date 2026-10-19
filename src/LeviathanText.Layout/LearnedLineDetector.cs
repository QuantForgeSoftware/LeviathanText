using System.Text.Json;
using SkiaSharp;

namespace LeviathanText.Layout;

/// <summary>
/// Learned line detector, first model: per-row logistic regression over ink-profile
/// features (see <see cref="RowFeatureExtractor"/>), trained from PAGE boxes.
///
/// Scope and honesty:
/// - Linear model on one feature family; a measured baseline, not general layout
///   understanding (multi-column, curved and rotated layouts remain unsupported).
/// - Training is deterministic (fixed order/initialization); reported numbers are
///   reproducible from the same corpus snapshot.
/// - Weights AND feature normalization (mean/std) are persisted, so inference cannot
///   silently drift from training.
/// </summary>
public sealed class LearnedLineDetector
{
    public const string ModelVersion = "row-logit-v2";

    private readonly float[] _weights;
    private readonly float[] _mean;
    private readonly float[] _std;
    private readonly float _bias;
    private readonly DetectorOptions _options;

    public sealed record DetectorOptions
    {
        public float Threshold { get; init; } = 0.5f;
        public int MinLineHeight { get; init; } = 4;
        public int MaxGapRows { get; init; } = 1;
        public double LearningRate { get; init; } = 3.0;
        public int Epochs { get; init; } = 400;
        public double L2 { get; init; } = 1e-3;
        public int Seed { get; init; } = 1234;
    }

    public sealed record TrainingExample(RowFeatureExtractor.PageProfile Profile, bool[] IsTextRow);

    private LearnedLineDetector(float[] weights, float[] mean, float[] std, float bias, DetectorOptions options)
    {
        _weights = weights;
        _mean = mean;
        _std = std;
        _bias = bias;
        _options = options;
    }

    public DetectorOptions Options => _options;
    public IReadOnlyList<float> Weights => _weights;
    public float Bias => _bias;

    public static LearnedLineDetector Train(IReadOnlyList<TrainingExample> examples, DetectorOptions? options = null)
    {
        options ??= new DetectorOptions();
        if (examples.Count == 0) throw new ArgumentException("Training needs at least one example.", nameof(examples));

        // 1) Feature normalization over all training rows.
        int featureCount = RowFeatureExtractor.FeatureCount;
        var mean = new double[featureCount];
        var sq = new double[featureCount];
        long rows = 0;
        foreach (var example in examples)
        {
            for (int row = 0; row < example.Profile.Height; row++)
            {
                var features = RowFeatureExtractor.Features(example.Profile, row);
                for (int f = 0; f < featureCount; f++)
                {
                    mean[f] += features[f];
                    sq[f] += (double)features[f] * features[f];
                }
                rows++;
            }
        }
        var std = new double[featureCount];
        for (int f = 0; f < featureCount; f++)
        {
            mean[f] /= Math.Max(1, rows);
            var variance = sq[f] / Math.Max(1, rows) - mean[f] * mean[f];
            std[f] = Math.Sqrt(Math.Max(1e-6, variance));
        }

        var rng = new Random(options.Seed);
        var weights = new float[featureCount];
        for (int i = 0; i < weights.Length; i++)
            weights[i] = (float)((rng.NextDouble() - 0.5) * 0.01);
        float bias = 0f;

        long positives = examples.Sum(e => e.IsTextRow.Count(v => v));
        long total = examples.Sum(e => e.IsTextRow.Length);
        long negatives = total - positives;
        float posWeight = positives == 0 ? 1f : Math.Clamp((float)negatives / positives, 1f, 5f);

        // 2) Deterministic FULL-BATCH gradient descent on standardized features:
        //    per-epoch mean gradient (fast, stable on the tiny pilot corpora).
        var gradW = new double[featureCount];
        for (int epoch = 0; epoch < options.Epochs; epoch++)
        {
            Array.Clear(gradW);
            double gradB = 0;
            long rowCount = 0;

            foreach (var example in examples)
            {
                var profile = example.Profile;
                for (int row = 0; row < profile.Height; row++)
                {
                    var features = RowFeatureExtractor.Features(profile, row);
                    float label = example.IsTextRow[row] ? 1f : 0f;
                    double z = bias;
                    for (int f = 0; f < featureCount; f++)
                        z += weights[f] * ((features[f] - mean[f]) / std[f]);

                    float prediction = Sigmoid((float)z);
                    float weight = label > 0.5f ? posWeight : 1f;
                    float gradient = (prediction - label) * weight;

                    for (int f = 0; f < featureCount; f++)
                        gradW[f] += gradient * ((features[f] - mean[f]) / std[f]);
                    gradB += gradient;
                    rowCount++;
                }
            }

            if (rowCount == 0) break;
            for (int f = 0; f < featureCount; f++)
                weights[f] -= (float)(options.LearningRate * (gradW[f] / rowCount + options.L2 * weights[f]));
            bias -= (float)(options.LearningRate * (gradB / rowCount));
        }

        return new LearnedLineDetector(
            weights,
            mean.Select(m => (float)m).ToArray(),
            std.Select(s => (float)s).ToArray(),
            bias, options);
    }

    public DetectionStats Diagnostics(RowFeatureExtractor.PageProfile profile, bool[] labels)
    {
        float max = 0, min = 1;
        double sumPos = 0, sumNeg = 0;
        int pos = 0, neg = 0;
        for (int row = 0; row < profile.Height; row++)
        {
            var p = Probability(profile, row);
            max = Math.Max(max, p);
            min = Math.Min(min, p);
            if (labels[row]) { sumPos += p; pos++; } else { sumNeg += p; neg++; }
        }
        return new DetectionStats(max, min, pos == 0 ? 0 : sumPos / pos, neg == 0 ? 0 : sumNeg / neg);
    }

    public sealed record DetectionStats(float MaxProbability, float MinProbability, double MeanPositive, double MeanNegative);

    /// <summary>Pick the row-level F1-maximizing threshold on a validation set; returns a detector with that threshold.</summary>
    public LearnedLineDetector WithTunedThreshold(IReadOnlyList<TrainingExample> validation,
        float from = 0.02f, float to = 0.98f, float step = 0.02f)
    {
        var probabilities = new List<(float P, bool IsText)>();
        foreach (var example in validation)
            for (int row = 0; row < example.Profile.Height; row++)
                probabilities.Add((Probability(example.Profile, row), example.IsTextRow[row]));

        long positives = probabilities.Count(p => p.IsText);
        if (positives == 0) return this;

        float best = _options.Threshold;
        double bestF1 = -1;
        for (float t = from; t <= to; t += step)
        {
            long tp = 0, fp = 0, fn = 0;
            foreach (var (p, isText) in probabilities)
            {
                if (p >= t && isText) tp++;
                else if (p >= t) fp++;
                else if (isText) fn++;
            }
            double precision = tp + fp == 0 ? 0 : (double)tp / (tp + fp);
            double recall = (double)tp / positives;
            double f1 = precision + recall == 0 ? 0 : 2 * precision * recall / (precision + recall);
            if (f1 > bestF1) { bestF1 = f1; best = t; }
        }

        return new LearnedLineDetector(_weights, _mean, _std, _bias, _options with { Threshold = best });
    }

    private float Probability(RowFeatureExtractor.PageProfile profile, int row)
    {
        var features = RowFeatureExtractor.Features(profile, row);
        double z = _bias;
        for (int f = 0; f < _weights.Length; f++)
            z += _weights[f] * ((features[f] - _mean[f]) / _std[f]);
        return Sigmoid((float)z);
    }

    public LayoutResult Detect(SKBitmap page)
    {
        var profile = RowFeatureExtractor.Profile(page);
        return Detect(profile);
    }

    public LayoutResult Detect(RowFeatureExtractor.PageProfile profile)
    {
        var predicted = new bool[profile.Height];
        for (int row = 0; row < profile.Height; row++)
        {
            var features = RowFeatureExtractor.Features(profile, row);
            double z = _bias;
            for (int f = 0; f < _weights.Length; f++)
                z += _weights[f] * ((features[f] - _mean[f]) / _std[f]);
            predicted[row] = Sigmoid((float)z) >= _options.Threshold;
        }

        var closed = CloseGaps(predicted, _options.MaxGapRows);
        var lines = new List<DetectedLine>();
        int start = -1;
        for (int row = 0; row <= profile.Height; row++)
        {
            bool inBand = row < profile.Height && closed[row];
            if (inBand && start < 0) start = row;
            if (!inBand && start >= 0)
            {
                int end = row;
                if (end - start >= _options.MinLineHeight)
                    lines.Add(new DetectedLine
                    {
                        Id = new LeviathanText.Contracts.EntityId(LeviathanText.Contracts.EntityKind.Region, $"learned{lines.Count:x4}"),
                        Top = start,
                        Bottom = end,
                        Left = 0,
                        Right = profile.Width,
                        OrderIndex = lines.Count,
                        InkDensity = MeanDensity(profile, start, end),
                    });
                start = -1;
            }
        }

        return new LayoutResult(lines, $"learned:{ModelVersion}", HashOf(_weights, _bias));
    }

    public string ToJson() => JsonSerializer.Serialize(new Persisted
    {
        ModelVersion = ModelVersion,
        Weights = _weights,
        Mean = _mean,
        Std = _std,
        Bias = _bias,
        Threshold = _options.Threshold,
        MinLineHeight = _options.MinLineHeight,
        MaxGapRows = _options.MaxGapRows,
        FeatureVersion = RowFeatureExtractor.FeatureCount,
    });

    public static LearnedLineDetector FromJson(string json)
    {
        var persisted = JsonSerializer.Deserialize<Persisted>(json)
            ?? throw new InvalidDataException("model JSON empty");
        if (persisted.ModelVersion != ModelVersion || persisted.FeatureVersion != RowFeatureExtractor.FeatureCount)
            throw new InvalidDataException($"model {persisted.ModelVersion} (features {persisted.FeatureVersion}) is incompatible with {ModelVersion}");
        if (persisted.Weights.Length != RowFeatureExtractor.FeatureCount
            || persisted.Mean.Length != RowFeatureExtractor.FeatureCount
            || persisted.Std.Length != RowFeatureExtractor.FeatureCount)
            throw new InvalidDataException("persisted model arrays do not match the feature count");

        return new LearnedLineDetector(persisted.Weights, persisted.Mean, persisted.Std, persisted.Bias,
            new DetectorOptions
            {
                Threshold = persisted.Threshold,
                MinLineHeight = persisted.MinLineHeight,
                MaxGapRows = persisted.MaxGapRows,
            });
    }

    private sealed record Persisted
    {
        public string ModelVersion { get; init; } = LearnedLineDetector.ModelVersion;
        public float[] Weights { get; init; } = [];
        public float[] Mean { get; init; } = [];
        public float[] Std { get; init; } = [];
        public float Bias { get; init; }
        public float Threshold { get; init; } = 0.5f;
        public int MinLineHeight { get; init; } = 4;
        public int MaxGapRows { get; init; } = 1;
        public int FeatureVersion { get; init; }
    }

    private static bool[] CloseGaps(bool[] flags, int maxGap)
    {
        var closed = (bool[])flags.Clone();
        int run = 0;
        for (int i = 0; i < closed.Length; i++)
        {
            if (closed[i]) { run = 0; continue; }
            run++;
            if (run <= maxGap && i - run >= 0 && closed[i - run] &&
                i + 1 < closed.Length && closed[i + 1])
            {
                for (int k = i - run + 1; k <= i; k++) closed[k] = true;
                run = 0;
            }
        }
        return closed;
    }

    private static float MeanDensity(RowFeatureExtractor.PageProfile profile, int top, int bottom)
    {
        double sum = 0;
        for (int row = top; row < bottom; row++) sum += profile.RowDensity[row];
        return (float)(sum / Math.Max(1, bottom - top));
    }

    private static float Sigmoid(float x) => 1f / (1f + MathF.Exp(-x));

    private static string HashOf(float[] weights, float bias) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{ModelVersion}|{bias:F6}|{string.Join(',', weights.Select(w => w.ToString("F6")))}")))
            .ToLowerInvariant();
}
