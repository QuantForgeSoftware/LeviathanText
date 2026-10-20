using System.Text.Json;
using System.Text.Json.Serialization;
using LeviathanText.Contracts;
using LeviathanText.Imaging;
using SkiaSharp;
using TorchSharp;
using static TorchSharp.torch;

namespace LeviathanText.Layout;

/// <summary>One training page: image path plus PAGE line bands in original pixel coordinates.</summary>
public sealed record LayoutTrainingPage(string Name, string ImagePath, IReadOnlyList<(int Top, int Bottom)> Bands);

/// <summary>Configuration for the patch/row CNN layout model.</summary>
public sealed record PatchLayoutOptions
{
    public int InputHeight { get; init; } = 256;

    /// <summary>
    /// Aspect-preserving preprocessing: width is derived from the page aspect and clamped to
    /// these bounds. A fixed width distorted different materials (M02a vs P01) differently.
    /// </summary>
    public int MinInputWidth { get; init; } = 64;
    public int MaxInputWidth { get; init; } = 512;
    public int Epochs { get; init; } = 120;
    public int BatchSize { get; init; } = 4;
    public double LearningRate { get; init; } = 1e-3;
    public double PositiveWeight { get; init; } = 3.0;
    public int Seed { get; init; } = 1234;

    /// <summary>Bands narrower than this many output rows are discarded after decoding.</summary>
    public int MinBandBins { get; init; } = 2;

    /// <summary>
    /// Fraction of each PAGE line box marked as positive during training. PAGE boxes in real
    /// corpora are often adjacent/overlapping, so a full-box mask has no gaps and cannot
    /// separate lines; the central core gives the model actual separation to learn.
    /// </summary>
    public double CoreFraction { get; init; } = 0.5;

    /// <summary>
    /// Decode expansion: predicted cores are expanded to this multiple of their height
    /// (centered) to approximate full line boxes for matching/cropping. Tuned on dev.
    /// </summary>
    public double ExpansionFactor { get; init; } = 1.6;

    /// <summary>Row-probability threshold selected on a dev split.</summary>
    public float Threshold { get; init; } = 0.5f;
}

/// <summary>
/// Learned line-band detector: a small fully convolutional CNN over a height-normalized page
/// (single column). The model predicts a text-row probability per output row; bands are
/// decoded by thresholding and mapped back to the original frame. This replaces whole-row
/// ink statistics (which failed: docs/evaluation/layout-learned-comparison.md) with learned
/// local texture features.
///
/// Scope: horizontal, single-column pages. The width is collapsed deliberately, so column
/// structure is NOT modeled; multi-column/rotated layouts remain unsupported conditions.
/// </summary>
public sealed class PatchLayoutModel
{
    public const string Architecture = "patch-row-cnn-v2";
    public const string ModelVersion = "v2";
    private const string MetadataFileName = "layout-metadata.json";
    private const string WeightsFileName = "model.pt";
    private const string ModelCardFileName = "model-card.md";

    private readonly PatchCnn _model;
    private readonly PatchLayoutOptions _options;
    private readonly string _modelHash;
    private readonly TrainedMetadata _metadata;

    private PatchLayoutModel(PatchCnn model, PatchLayoutOptions options, TrainedMetadata metadata)
    {
        _model = model;
        _options = options;
        _metadata = metadata;
        _modelHash = HashOf($"{Architecture}|{options.InputHeight}|{metadata.TrainedPages}|{metadata.Seed}");
    }

    public PatchLayoutOptions Options => _options;
    public float Threshold => _options.Threshold;
    public IReadOnlyList<string> TrainedPageNames => _metadata.PageNames;
    public int EpochCount => _metadata.Epochs;
    public double FinalTrainingLoss => _metadata.FinalLoss;

    /// <summary>Train a model. Pages are preprocessed (content crop + resize + normalization) once.</summary>
    public static PatchLayoutModel Train(
        IReadOnlyList<LayoutTrainingPage> pages,
        PatchLayoutOptions? options = null,
        Action<string>? log = null)
    {
        if (pages.Count == 0) throw new ArgumentException("Training needs at least one page.", nameof(pages));
        options ??= new PatchLayoutOptions();

        manual_seed(options.Seed);
        var prepared = pages.Select(page => PreparedPage.Create(page, options)).ToList();
        var model = new PatchCnn();
        var optimizer = new TorchSharp.Modules.Adam(model.parameters(), lr: options.LearningRate);
        var posWeight = tensor((float)options.PositiveWeight);
        var rng = new Random(options.Seed);

        model.train();
        double lastLoss = 0;
        for (int epoch = 0; epoch < options.Epochs; epoch++)
        {
            var order = Enumerable.Range(0, prepared.Count).OrderBy(_ => rng.Next()).ToList();
            double total = 0;
            int batches = 0;

            for (int i = 0; i < order.Count;)
            {
                int width = prepared[order[i]].Width;
                int j = i;
                while (j < order.Count && j - i < options.BatchSize && prepared[order[j]].Width == width) j++;
                var batch = order.GetRange(i, j - i);
                i = j;
                using var input = BuildInputs(prepared, batch, options);
                using var target = BuildTargets(prepared, batch, options);

                optimizer.zero_grad();
                using var logits = model.forward(input);
                // Weighted BCE (pos_weight semantics); TorchSharp's helper lacks pos_weight.
                using var logP = torch.nn.functional.logsigmoid(logits);
                using var negLogits = -logits;
                using var logNotP = torch.nn.functional.logsigmoid(negLogits);
                using var one = torch.ones_like(target);
                using var weightedTarget = target * posWeight;
                using var positive = weightedTarget * logP;
                using var oneMinusTarget = one - target;
                using var negative = oneMinusTarget * logNotP;
                using var combined = positive + negative;
                using var loss = combined.mean().neg();
                loss.backward();
                optimizer.step();

                total += loss.item<float>();
                batches++;
            }

            lastLoss = batches == 0 ? 0 : total / batches;
            if (log is not null && ((epoch + 1) % 20 == 0 || epoch == 0))
                log($"epoch {epoch + 1}/{options.Epochs}: bce={lastLoss:0.###}");
        }

        model.eval();
        var metadata = new TrainedMetadata
        {
            Architecture = Architecture,
            InputHeight = options.InputHeight,
            MinInputWidth = options.MinInputWidth,
            MaxInputWidth = options.MaxInputWidth,
            Threshold = options.Threshold,
            MinBandBins = options.MinBandBins,
            CoreFraction = options.CoreFraction,
            ExpansionFactor = options.ExpansionFactor,
            Seed = options.Seed,
            Epochs = options.Epochs,
            FinalLoss = lastLoss,
            TrainedPages = prepared.Count,
            PageNames = [.. pages.Select(p => p.Name)],
            CreatedAt = DateTimeOffset.UtcNow,
            Note = "Pilot model trained from PAGE-derived line bands; labels are not specialist-adjudicated. " +
                   "Single-column pages only; width is collapsed and columns are not modeled.",
        };
        return new PatchLayoutModel(model, options, metadata);
    }

    /// <summary>A copy of this model with a different decode threshold (dev-selected).</summary>
    public PatchLayoutModel WithThreshold(float threshold) =>
        new(_model, _options with { Threshold = threshold }, _metadata with { Threshold = threshold });

    /// <summary>Per-output-row text probability for a page (diagnostics/threshold tuning).</summary>
    public float[] PredictRowProbabilities(SKBitmap page)
    {
        var prepared = PreparedPage.Create(page, _options, name: "(inference)");
        using var input = BuildInputs([prepared], [0], _options);
        using var _ = no_grad();
        using var logits = _model.forward(input);
        using var probabilities = sigmoid(logits);
        return probabilities[0].cpu().data<float>().ToArray();
    }

    /// <summary>Detect line bands on a page using the model's configured threshold.</summary>
    public LayoutResult Detect(SKBitmap page) => Detect(page, _options.Threshold);

    public LayoutResult Detect(SKBitmap page, float threshold)
    {
        var prepared = PreparedPage.Create(page, _options, name: "(inference)");
        var probabilities = Predict(prepared);
        return DecodePrepared(page, prepared, probabilities, threshold);
    }

    /// <summary>Decode bands from precomputed row probabilities (threshold tuning/analysis).</summary>
    public LayoutResult DetectFromProbabilities(SKBitmap page, float[] probabilities, float threshold)
    {
        var prepared = PreparedPage.Create(page, _options, name: "(inference)");
        return DecodePrepared(page, prepared, probabilities, threshold);
    }

    private float[] Predict(PreparedPage prepared)
    {
        using var input = BuildInputs([prepared], [0], _options);
        using var _ = no_grad();
        using var logits = _model.forward(input);
        using var probabilities = sigmoid(logits);
        return probabilities[0].cpu().data<float>().ToArray();
    }

    private LayoutResult DecodePrepared(
        SKBitmap page, PreparedPage prepared, float[] probabilities, float threshold)
    {
        // Ink threshold for horizontal extents/density: reuse the paper-relative calibration.
        var profile = RowFeatureExtractor.Profile(page);
        var bands = DecodeBands(probabilities, prepared, threshold, _options.MinBandBins);

        var lines = new List<DetectedLine>();
        foreach (var (top, bottom) in bands)
        {
            if (bottom - top < 2) continue;
            var (left, right, density) = RowFeatureExtractor.InkBounds(
                page, top, bottom, profile.InkThreshold, prepared.CropLeft, prepared.CropRight);
            lines.Add(new DetectedLine
            {
                Id = new EntityId(EntityKind.Region, $"patch{lines.Count:x4}"),
                Top = top,
                Bottom = bottom,
                Left = left,
                Right = right,
                OrderIndex = lines.Count,
                InkDensity = density,
            });
        }

        return new LayoutResult(lines, $"patch:{ModelVersion}", _modelHash);
    }

    public LayoutResult Detect(string imagePath)
    {
        using var decoded = ImageDecoder.Decode(imagePath);
        return Detect(decoded.Bitmap);
    }

    /// <summary>Save weights + metadata + model card into a package directory.</summary>
    public void Save(string directory)
    {
        Directory.CreateDirectory(directory);
        var weightsPath = Path.Combine(directory, WeightsFileName);
        _model.save(weightsPath);
        var digest = DigestOf(weightsPath);
        var metadata = _metadata with
        {
            Threshold = _options.Threshold,
            WeightsDigest = digest,
        };
        File.WriteAllText(Path.Combine(directory, MetadataFileName),
            JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(directory, ModelCardFileName), BuildModelCard(metadata));
    }

    public static PatchLayoutModel Load(string directory)
    {
        var metadataPath = Path.Combine(directory, MetadataFileName);
        if (!File.Exists(metadataPath))
            throw new InvalidDataException($"Layout model metadata not found at {metadataPath}.");
        var metadata = JsonSerializer.Deserialize<TrainedMetadata>(File.ReadAllText(metadataPath))
            ?? throw new InvalidDataException("Layout model metadata is empty.");
        if (metadata.Architecture != Architecture)
            throw new InvalidDataException(
                $"Layout model architecture '{metadata.Architecture}' is not compatible with '{Architecture}'.");

        var model = new PatchCnn();
        var weights = Path.Combine(directory, WeightsFileName);
        if (!File.Exists(weights))
            throw new InvalidDataException($"Layout model weights not found at {weights}.");
        if (!string.IsNullOrEmpty(metadata.WeightsDigest)
            && !string.Equals(DigestOf(weights), metadata.WeightsDigest, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Layout model weights do not match the digest recorded in the metadata.");
        model.load(weights);
        model.eval();

        var options = new PatchLayoutOptions
        {
            InputHeight = metadata.InputHeight,
            MinInputWidth = metadata.MinInputWidth,
            MaxInputWidth = metadata.MaxInputWidth,
            Threshold = metadata.Threshold,
            MinBandBins = metadata.MinBandBins,
            CoreFraction = metadata.CoreFraction,
            ExpansionFactor = metadata.ExpansionFactor,
            Seed = metadata.Seed,
            Epochs = metadata.Epochs,
        };
        return new PatchLayoutModel(model, options, metadata);
    }

    // ---- decoding ----

    private static List<(int Top, int Bottom)> DecodeBands(
        float[] probabilities, PreparedPage page, float threshold, int minBands)
    {
        var bands = new List<(int, int)>();
        int start = -1;
        for (int row = 0; row <= probabilities.Length; row++)
        {
            bool on = row < probabilities.Length && probabilities[row] >= threshold;
            if (on && start < 0) start = row;
            if (!on && start >= 0)
            {
                int end = row;
                if (end - start >= minBands)
                {
                    int top = page.OutputRowToPixel(start);
                    int bottom = page.OutputRowToPixel(end);
                    if (bottom > top)
                    {
                        // Expand the core to approximate a full line box (calibration, tuned on dev).
                        int coreHeight = bottom - top;
                        int extra = (int)Math.Round(coreHeight * (page.ExpansionFactor - 1) / 2);
                        bands.Add((Math.Max(page.CropTop, top - extra), Math.Min(page.CropBottom, bottom + extra)));
                    }
                }
                start = -1;
            }
        }
        return bands;
    }

    private static Tensor BuildInputs(
        IReadOnlyList<PreparedPage> prepared, IReadOnlyList<int> indices, PatchLayoutOptions options)
    {
        int count = indices.Count;
        int width = prepared[indices[0]].Width;
        if (indices.Any(index => prepared[index].Width != width))
            throw new InvalidOperationException("Batch samples must share the same preprocessed width.");
        int perSample = options.InputHeight * width;
        var pixels = new float[count * perSample];
        int cursor = 0;
        foreach (var index in indices)
            Array.Copy(prepared[index].Pixels, 0, pixels, (cursor++) * perSample, perSample);
        return tensor(pixels).reshape(count, 1, options.InputHeight, width);
    }

    private static Tensor BuildTargets(
        IReadOnlyList<PreparedPage> prepared, IReadOnlyList<int> indices, PatchLayoutOptions options)
    {
        int count = indices.Count;
        var mask = new float[count * options.InputHeight];
        int cursor = 0;
        foreach (var index in indices)
            Array.Copy(prepared[index].Mask, 0, mask, (cursor++) * options.InputHeight, options.InputHeight);
        return tensor(mask).reshape(count, options.InputHeight);
    }

    private static string DigestOf(string path) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)))
            .ToLowerInvariant();

    private string BuildModelCard(TrainedMetadata metadata)
    {
        var pages = metadata.PageNames.Count <= 8
            ? string.Join(", ", metadata.PageNames)
            : $"{string.Join(", ", metadata.PageNames.Take(4))} … ({metadata.PageNames.Count} pages)";
        return $"""
            # Layout model card — {metadata.Architecture}

            - Version: {ModelVersion}; created {metadata.CreatedAt:O}
            - Input: height {metadata.InputHeight}px, aspect-preserving width clamped to
              {metadata.MinInputWidth}–{metadata.MaxInputWidth}px; per-page ink normalization
            - Output: text-row probability per output row; threshold **{metadata.Threshold:0.00}**,
              minimum band {metadata.MinBandBins} rows, core fraction {metadata.CoreFraction:0.00},
              box expansion ×{metadata.ExpansionFactor:0.00}
            - Training: {metadata.TrainedPages} pages; seed {metadata.Seed}; {metadata.Epochs} epochs; final weighted BCE {metadata.FinalLoss:0.###}
            - Weights digest (SHA-256): `{metadata.WeightsDigest}`
            - Training pages: {pages}

            ## Scope and limitations

            Single-column, horizontal pages. The width axis is collapsed, so columns are not
            modeled. Metrics depend on the material and split; see the evaluation records under
            `docs/evaluation/layout-*.md`. This model is not a scholarly layout claim and does not
            replace manual correction. Threshold and expansion were selected on a development
            split; re-tune for a different material.
            """;
    }

    private static string HashOf(string value) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    // ---- preprocessing ----

    private sealed class PreparedPage
    {
        public required float[] Pixels { get; init; }
        public required float[] Mask { get; init; }
        public required int CropLeft { get; init; }
        public required int CropTop { get; init; }
        public required int CropRight { get; init; }
        public required int CropBottom { get; init; }
        public required int OutputHeight { get; init; }
        public required int Width { get; init; }
        public required int Height { get; init; }
        public required double ExpansionFactor { get; init; }

        public int OutputRowToPixel(int row)
        {
            int cropHeight = CropBottom - CropTop;
            double y = CropTop + (double)row * cropHeight / OutputHeight;
            return (int)Math.Round(Math.Clamp(y, CropTop, CropBottom));
        }

        public static PreparedPage Create(LayoutTrainingPage page, PatchLayoutOptions options)
        {
            using var decoded = ImageDecoder.Decode(page.ImagePath);
            return Create(decoded.Bitmap, options, page.Name, page.Bands);
        }

        public static PreparedPage Create(SKBitmap bitmap, PatchLayoutOptions options, string name,
            IReadOnlyList<(int Top, int Bottom)>? bands = null)
        {
            var (left, top, right, bottom) = RowFeatureExtractor.ContentBounds(bitmap);
            int cropWidth = Math.Max(1, right - left);
            int cropHeight = Math.Max(1, bottom - top);
            int inputWidth = Math.Clamp(
                (int)Math.Round(options.InputHeight * (double)cropWidth / cropHeight),
                options.MinInputWidth, options.MaxInputWidth);

            using var resized = new SKBitmap(inputWidth, options.InputHeight,
                SKColorType.Bgra8888, SKAlphaType.Opaque);
            using (var canvas = new SKCanvas(resized))
            {
                canvas.Clear(SKColors.White);
                canvas.DrawBitmap(bitmap,
                    new SKRect(left, top, right, bottom),
                    new SKRect(0, 0, inputWidth, options.InputHeight),
                    new SKSamplingOptions(SKFilterMode.Linear));
            }

            var info = resized.Info;
            var bytes = resized.Bytes;
            int count = inputWidth * options.InputHeight;
            var raw = new float[count];
            double sum = 0;
            for (int i = 0; i < count; i++)
            {
                int b = i * info.BytesPerPixel;
                float luminance = (0.299f * bytes[b + 2] + 0.587f * bytes[b + 1] + 0.114f * bytes[b]) / 255f;
                raw[i] = 1f - luminance; // ink high
                sum += raw[i];
            }
            double mean = sum / count;
            double variance = 0;
            for (int i = 0; i < count; i++) variance += (raw[i] - mean) * (raw[i] - mean);
            double std = Math.Max(1e-3, Math.Sqrt(variance / count));
            var pixels = new float[count];
            for (int i = 0; i < count; i++) pixels[i] = (float)((raw[i] - mean) / std);

            var mask = new float[options.InputHeight];
            if (bands is not null)
            {
                foreach (var (bandTop, bandBottom) in bands)
                {
                    double bandHeight = Math.Max(1, bandBottom - bandTop);
                    double inset = bandHeight * (1 - options.CoreFraction) / 2;
                    int from = (int)Math.Floor((bandTop + inset - top) * (double)options.InputHeight / cropHeight);
                    int to = (int)Math.Ceiling((bandBottom - inset - top) * (double)options.InputHeight / cropHeight);
                    if (to <= from) to = from + 1;
                    for (int row = Math.Max(0, from); row < Math.Min(options.InputHeight, to); row++)
                        mask[row] = 1f;
                }
            }

            return new PreparedPage
            {
                Pixels = pixels,
                Mask = mask,
                CropLeft = left,
                CropTop = top,
                CropRight = right,
                CropBottom = bottom,
                OutputHeight = options.InputHeight,
                ExpansionFactor = options.ExpansionFactor,
                Width = inputWidth,
                Height = options.InputHeight,
            };
        }
    }

    // ---- model ----

    private sealed class PatchCnn : nn.Module<Tensor, Tensor>
    {
        private readonly nn.Module<Tensor, Tensor> _conv1;
        private readonly nn.Module<Tensor, Tensor> _conv2;
        private readonly nn.Module<Tensor, Tensor> _conv3;
        private readonly nn.Module<Tensor, Tensor> _conv4;
        private readonly nn.Module<Tensor, Tensor> _head1;
        private readonly nn.Module<Tensor, Tensor> _head2;
        private readonly nn.Module<Tensor, Tensor> _headOut;

        public PatchCnn() : base(nameof(PatchCnn))
        {
            _conv1 = nn.Conv2d(1, 16, 3, padding: 1);
            _conv2 = nn.Conv2d(16, 32, 3, padding: 1);
            _conv3 = nn.Conv2d(32, 48, 3, padding: 1);
            _conv4 = nn.Conv2d(48, 64, 3, padding: 1);
            _head1 = nn.Conv1d(64, 64, 5, padding: 2);
            _head2 = nn.Conv1d(64, 64, 5, padding: 4, dilation: 2);
            _headOut = nn.Conv1d(64, 1, 1);
            RegisterComponents();
        }

        public override Tensor forward(Tensor input)
        {
            var x = torch.nn.functional.relu(_conv1.forward(input));
            x = torch.nn.functional.max_pool2d(x, new long[] { 1, 2 });
            x = torch.nn.functional.relu(_conv2.forward(x));
            x = torch.nn.functional.max_pool2d(x, new long[] { 1, 2 });
            x = torch.nn.functional.relu(_conv3.forward(x));
            x = torch.nn.functional.max_pool2d(x, new long[] { 1, 2 });
            x = torch.nn.functional.relu(_conv4.forward(x));
            x = torch.nn.functional.max_pool2d(x, new long[] { 1, 2 });

            // Collapse the (deliberately unmodeled) width axis, then a 1D head over rows.
            x = x.mean(new long[] { 3 });
            x = torch.nn.functional.relu(_head1.forward(x));
            x = torch.nn.functional.relu(_head2.forward(x));
            x = _headOut.forward(x); // B,1,H
            return x.squeeze(1);
        }
    }

    // ---- metadata ----

    private sealed record TrainedMetadata
    {
        public string Architecture { get; init; } = PatchLayoutModel.Architecture;
        public int InputHeight { get; init; }
        public int MinInputWidth { get; init; }
        public int MaxInputWidth { get; init; }
        public float Threshold { get; init; }
        public int MinBandBins { get; init; }
        public double CoreFraction { get; init; }
        public double ExpansionFactor { get; init; }
        public string WeightsDigest { get; init; } = "";
        public int Seed { get; init; }
        public int Epochs { get; init; }
        public double FinalLoss { get; init; }
        public int TrainedPages { get; init; }
        public IReadOnlyList<string> PageNames { get; init; } = [];
        public DateTimeOffset CreatedAt { get; init; }
        public string Note { get; init; } = "";
    }
}
