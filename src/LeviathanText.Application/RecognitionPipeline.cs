using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Imaging;
using LeviathanText.Recognition;
using SkiaSharp;

namespace LeviathanText.Application;

/// <summary>
/// Recognition pipeline use case (todo Phase 1: preparation, extraction, recognition
/// and alignment with bounded resource use). Takes a surface with captured/imported
/// line geometry and a trained model package, crops lines from the original capture,
/// recognizes L0 text with per-grapheme confidence, and records stage provenance.
/// L0 results are stored as raw-recognition revisions — never merged into L1
/// (greek-transcription.md §1).
/// </summary>
public sealed class RecognitionPipeline
{
    public sealed record Options
    {
        public string? ModelDirectory { get; init; }
        public string ModelName { get; init; } = "p01-pilot-crnn";
        public string ModelVersion { get; init; } = "pilot";
        public required Func<CrnnModel> ModelFactory { get; init; }
        public required Func<LabelSet> LabelSetFactory { get; init; }
    }

    public sealed record SurfaceOutcome(
        IReadOnlyList<LineRecognition> Lines,
        IReadOnlyList<StageRun> Stages,
        IReadOnlyList<EntityId> SkippedLines);

    public sealed record LineRecognition(
        EntityId LineId,
        string L0Text,
        IReadOnlyList<GraphemeConfidence> Confidence,
        Checksum LineCropChecksum,
        string TransformChain);

    private readonly IProjectStore _store;
    private readonly ImageResolver? _imageResolver;

    public RecognitionPipeline(IProjectStore store, ImageResolver? imageResolver = null)
    {
        _store = store;
        _imageResolver = imageResolver;
    }

    public SurfaceOutcome Run(NativeProject project, EntityId surfaceId, Options options, CancellationToken cancellation = default)
    {
        var surface = project.Objects.SelectMany(o => o.Surfaces).FirstOrDefault(s => s.Id == surfaceId)
            ?? throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = surfaceId.ToString(),
                Message = "No such surface in the project.",
            });

        var capture = surface.Captures.FirstOrDefault()
            ?? throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = surfaceId.ToString(),
                Message = "Surface has no capture to recognize against.",
            });

        // Remote captures (IIIF URLs) are fetched once into the checksummed cache.
        if (!File.Exists(capture.ImageLocation) && _imageResolver is not null)
        {
            var resolved = _imageResolver.EnsureLocal(project, surfaceId);
            project = resolved.Project;
            surface = resolved.Surface;
            capture = resolved.Capture;
        }

        var labels = options.LabelSetFactory();
        using var model = options.ModelFactory();
        var recognizer = new TorchLineRecognizer(model, labels);

        using var decoded = ImageDecoder.Decode(capture.ImageLocation);
        var stages = new List<StageRun>();
        var modelRef = new ModelRef
        {
            PackageId = options.ModelName,
            Version = options.ModelVersion,
            IoContractHash = LabelSetHash(labels),
        };

        var results = new List<LineRecognition>();
        var linesById = surface.Lines.ToDictionary(l => l.Id);
        var lineImages = new List<(EntityId Id, LineImage Image, Checksum CropChecksum)>();
        var skippedLines = new List<EntityId>();

        foreach (var line in surface.Lines)
        {
            cancellation.ThrowIfCancellationRequested();
            var geometry = line.Polygon.Count >= 3 ? line.Polygon : line.Baseline;
            if (geometry.Count < 2)
            {
                skippedLines.Add(line.Id); // absent geometry: explicit skip, never silent
                continue;
            }

            var points = geometry.Select(p => new SKPoint((float)p.X, (float)p.Y)).ToList();
            var extracted = LineCropper.Extract(decoded.Bitmap, points);
            lineImages.Add((line.Id, ToLineImage(extracted.AppliedCrop, decoded.Bitmap), extracted.DerivativeChecksum));
        }

        var recognition = recognizer.Recognize([.. lineImages.Select(li => li.Image)]);
        for (int i = 0; i < lineImages.Count; i++)
        {
            var (lineId, _, cropChecksum) = lineImages[i];
            results.Add(new LineRecognition(lineId, recognition[i].Text, recognition[i].Graphemes, cropChecksum, "crop>resize>otsu"));
        }

        stages.Add(new StageRun
        {
            Id = new EntityId(EntityKind.Job, Guid.NewGuid().ToString("N")[..12]),
            Stage = StageName.Recognition,
            ContractVersion = ContractVersion.Current,
            Inputs = [new StageArtifact(capture.Id, decoded.SourceChecksum)],
            Outputs = [.. lineImages.Select(li => new StageArtifact(li.Id, li.CropChecksum))],
            Model = modelRef,
            ConfigHash = options.ModelName,
            Status = StageStatus.Succeeded,
            LayoutSourceUsed = LayoutSource.Imported,
        });

        return new SurfaceOutcome(results, stages, skippedLines);
    }

    /// <summary>Stable label-set identity for model provenance (same convention as CheckpointStore).</summary>
    private static string LabelSetHash(LabelSet labels)
    {
        var joined = string.Join('\u001f', labels.Labels);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(joined))).ToLowerInvariant();
    }

    private static LineImage ToLineImage(ImageTransform.Crop crop, SKBitmap page)
    {
        var region = new SKRectI(crop.Left, crop.Top, crop.Left + crop.Width, crop.Top + crop.Height);
        using var subset = new SKBitmap();
        page.ExtractSubset(subset, region);

        int targetHeight = CrnnModel.InputHeight;
        int targetWidth = Math.Max(8, (int)Math.Round((float)subset.Width * targetHeight / subset.Height));
        using var resized = subset.Resize(new SKImageInfo(targetWidth, targetHeight), new SKSamplingOptions(SKFilterMode.Linear));

        var pixels = new float[targetHeight * targetWidth];
        var info = resized.Info;
        var bytes = resized.Bytes;
        for (int p = 0, b = 0; p < pixels.Length; p++, b += info.BytesPerPixel)
        {
            byte blue = bytes[b], green = bytes[b + 1], red = bytes[b + 2];
            float luminance = (0.299f * red + 0.587f * green + 0.114f * blue) / 255f;
            pixels[p] = 1f - luminance;
        }
        return new LineImage(targetHeight, targetWidth, pixels);
    }
}
