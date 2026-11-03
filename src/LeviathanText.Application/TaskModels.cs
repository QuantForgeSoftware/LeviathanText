using LeviathanText.Formats;
using LeviathanText.Imaging;
using LeviathanText.Recognition;
using SkiaSharp;

namespace LeviathanText.Application;

/// <summary>
/// Recognition task wrapper over the existing C# line recognizer. It accepts a page
/// bitmap plus source lines and returns geometry-bearing recognition records; it does not
/// persist revisions or decide L1 truth.
/// </summary>
public sealed class RecognitionTaskModel
{
    private readonly CrnnModel _model;
    private readonly LabelSet _labels;
    private readonly LinePreprocessingProfile _preprocessing;

    public RecognitionTaskModel(CrnnModel model, LabelSet labels) : this(model, labels, null)
    {
    }

    public RecognitionTaskModel(CrnnModel model, LabelSet labels, LinePreprocessingProfile? preprocessing)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _labels = labels ?? throw new ArgumentNullException(nameof(labels));
        if (model.NumClasses != labels.NumClasses)
            throw new ArgumentException("Model and label set class counts differ.");
        _preprocessing = preprocessing ?? LinePreprocessingProfile.LegacyInkPositive(model.InputHeightPixels);
        _preprocessing.Validate(model.InputHeightPixels);
    }

    public IReadOnlyList<RecognitionRecord> Predict(SKBitmap page, IReadOnlyList<NativeLine> lines)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(lines);
        _preprocessing.RequireStandardGeometry();
        if (lines.Count == 0) return [];

        var images = new List<LineImage>(lines.Count);
        var geometries = new List<(NativeLine Line, IReadOnlyList<PointD> Geometry)>(lines.Count);
        foreach (var line in lines)
        {
            var geometry = line.Polygon.Count >= 3 ? line.Polygon : line.Baseline;
            geometries.Add((line, geometry));
            if (geometry.Count < 2)
            {
                using var blank = new SKBitmap(8, _model.InputHeightPixels);
                blank.Erase(SKColors.White);
                images.Add(LinePreprocessor.Apply(blank, _preprocessing));
                continue;
            }
            var points = geometry.Select(p => new SKPoint((float)p.X, (float)p.Y)).ToList();
            var crop = LineCropper.Extract(page, points);
            try { images.Add(RecognitionPipeline.ToLineImage(crop.Bitmap, _preprocessing)); }
            finally { crop.Bitmap.Dispose(); }
        }

        var results = new TorchLineRecognizer(_model, _labels).Recognize(images);
        var records = new List<RecognitionRecord>(lines.Count);
        for (int i = 0; i < lines.Count; i++)
        {
            var (line, geometry) = geometries[i];
            (double Left, double Top, double Right, double Bottom)? box = null;
            if (geometry.Count >= 2)
            {
                double left = geometry.Min(p => p.X), right = geometry.Max(p => p.X);
                double top = geometry.Min(p => p.Y), bottom = Math.Max(geometry.Max(p => p.Y), top + 1);
                box = (left, top, right, bottom);
            }
            records.Add(new RecognitionRecord(line.Id.Suffix, results[i].Text, results[i].Graphemes,
                line.Direction == TextDirection.Rtl)
            {
                RegionId = line.RegionId,
                Bbox = box,
                Baseline = geometry.Select(p => (p.X, p.Y)).ToArray(),
            });
        }
        return records;
    }
}

/// <summary>Forced-alignment task wrapper over the existing line cropper and CTC aligner.</summary>
public sealed class ForcedAlignmentTaskModel
{
    private readonly CrnnModel _model;
    private readonly LabelSet _labels;
    private readonly LinePreprocessingProfile _preprocessing;

    public ForcedAlignmentTaskModel(CrnnModel model, LabelSet labels) : this(model, labels, null)
    {
    }

    public ForcedAlignmentTaskModel(CrnnModel model, LabelSet labels, LinePreprocessingProfile? preprocessing)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _labels = labels ?? throw new ArgumentNullException(nameof(labels));
        _preprocessing = preprocessing ?? LinePreprocessingProfile.LegacyInkPositive(model.InputHeightPixels);
        _preprocessing.Validate(model.InputHeightPixels);
    }

    public IReadOnlyList<ForcedAlignmentRecord> Predict(
        SKBitmap page,
        IEnumerable<(NativeLine Line, string Text)> lines)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(lines);
        _preprocessing.RequireStandardGeometry();
        var records = new List<ForcedAlignmentRecord>();
        foreach (var (line, text) in lines)
        {
            var geometry = line.Polygon.Count >= 3 ? line.Polygon : line.Baseline;
            if (geometry.Count < 2) continue;
            var points = geometry.Select(p => new SKPoint((float)p.X, (float)p.Y)).ToList();
            var crop = LineCropper.Extract(page, points);
            try
            {
                records.Add(ForcedAlignmentTask.Align(
                    _model, _labels, line, RecognitionPipeline.ToLineImage(crop.Bitmap, _preprocessing), text));
            }
            finally { crop.Bitmap.Dispose(); }
        }
        return records;
    }
}
