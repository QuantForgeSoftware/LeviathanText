using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Imaging;
using LeviathanText.Layout;

namespace LeviathanText.Cli;

/// <summary>Offline, paired PAGE line-band evaluation in one declared image frame.</summary>
internal static class LayoutCompareCommand
{
    private const int MaxMarkupBytes = 16 * 1024 * 1024;
    private const int MaxPairComparisons = 1_000_000;

    public static int Run(string[] args)
    {
        if (args is not [var truthPath, var predictionPath, "--metric", var metric, "--threshold", var thresholdText]
            || metric is not ("vertical-iou" or "center-distance")
            || !double.TryParse(thresholdText, NumberStyles.Float, CultureInfo.InvariantCulture, out double threshold)
            || !double.IsFinite(threshold) || threshold < 0 || (metric == "vertical-iou" && (threshold == 0 || threshold > 1)))
            throw new ArgumentException("Use layout compare <truth.xml> <prediction.xml> --metric vertical-iou|center-distance --threshold <value>. IoU uses (0,1]; center distance uses nonnegative pixels.");

        var (truth, truthHash) = Load(truthPath);
        var (predicted, predictedHash) = Load(predictionPath);
        if (string.IsNullOrWhiteSpace(truth.ImageFilename) || truth.ImageFilename != predicted.ImageFilename
            || truth.WidthPx is not (> 0 and <= ImageDecoder.MaxDimension)
            || truth.HeightPx is not (> 0 and <= ImageDecoder.MaxDimension)
            || truth.WidthPx != predicted.WidthPx || truth.HeightPx != predicted.HeightPx)
            throw new InvalidDataException("Both PAGE documents must declare the same image filename and supported positive dimensions.");
        if ((long)truth.Lines.Count * predicted.Lines.Count > MaxPairComparisons)
            throw new InvalidDataException("PAGE line counts exceed the comparison pair limit.");

        var truthBands = Bands(truth, metric);
        var predictedBands = Bands(predicted, metric);
        object result;
        if (metric == "center-distance")
        {
            result = DetectionMetrics.Compute(predictedBands.Select(b => new DetectionMetrics.Interval(b.Top, b.Bottom)).ToList(),
                truthBands.Select(b => new DetectionMetrics.Interval(b.Top, b.Bottom)).ToList(), threshold);
        }
        else
        {
            var detections = predictedBands.Select((band, index) => new DetectedLine
            {
                Id = predicted.Lines[index].Id,
                Top = band.Top,
                Bottom = band.Bottom,
                Left = band.Left,
                Right = band.Right,
            }).ToList();
            result = LayoutComparison.Compare(
                [(truth.ImageFilename, detections, truthBands.Select(b => (b.Top, b.Bottom)).ToList())], threshold)
                .PerWitness.Single();
        }

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            metric,
            threshold,
            units = metric == "center-distance" ? "pixels" : "ratio",
            geometry = metric == "center-distance" ? "polygon bounds, falling back to baseline bounds" : "polygon bounds",
            frame = new { truth.ImageFilename, truth.WidthPx, truth.HeightPx },
            truthSha256 = truthHash,
            predictionSha256 = predictedHash,
            truthImportIssues = truth.Unsupported,
            predictionImportIssues = predicted.Unsupported,
            result,
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }));
        return 0;
    }

    private static (PageXml.PageImportResult Page, string Hash) Load(string path)
    {
        using var file = File.OpenRead(path);
        if (file.Length > MaxMarkupBytes)
            throw new InvalidDataException("PAGE document exceeds the comparison size limit.");
        var bytes = new byte[checked((int)file.Length)];
        file.ReadExactly(bytes);
        if (file.ReadByte() != -1) throw new IOException("PAGE document changed during comparison.");
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        using var reader = new StreamReader(new MemoryStream(bytes));
        var capture = new EntityId(EntityKind.Capture, "layout-compare");
        var xml = reader.ReadToEnd();
        var page = PageXml.Import(xml, capture, new Checksum { Algorithm = "unavailable", Digest = "pending" });
        var root = XDocument.Parse(xml).Root!;
        var ns = root.Name.Namespace;
        var lines = root.Descendants(ns + "TextLine").ToList();
        if (lines.Count != page.Lines.Count
            || lines.Sum(line => PointTokens(line.Element(ns + "Coords"))) != page.Lines.Sum(line => line.Polygon.Count)
            || lines.Sum(line => PointTokens(line.Element(ns + "Baseline"))) != page.Lines.Sum(line => line.Baseline.Count))
            throw new InvalidDataException("PAGE contains unsupported lines or malformed point coordinates.");
        return (page, hash);
    }

    private static int PointTokens(XElement? coordinates) =>
        ((string?)coordinates?.Attribute("points"))?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length ?? 0;

    private static IReadOnlyList<(int Top, int Bottom, int Left, int Right)> Bands(PageXml.PageImportResult page, string metric)
    {
        var bands = new List<(int, int, int, int)>(page.Lines.Count);
        foreach (var line in page.Lines)
        {
            var points = line.Polygon.Count >= 3 ? line.Polygon
                : metric == "center-distance" && line.Baseline.Count >= 2 ? line.Baseline : [];
            if (points.Count == 0 || points.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y)
                    || p.X < 0 || p.X > page.WidthPx!.Value || p.Y < 0 || p.Y > page.HeightPx!.Value))
                throw new InvalidDataException($"Line {line.Id} lacks usable in-frame geometry for {metric}.");
            int top = (int)Math.Floor(points.Min(p => p.Y));
            int bottom = (int)Math.Ceiling(points.Max(p => p.Y));
            int left = (int)Math.Floor(points.Min(p => p.X));
            int right = (int)Math.Ceiling(points.Max(p => p.X));
            if (metric == "vertical-iou" && (bottom <= top || right <= left))
                throw new InvalidDataException($"Line {line.Id} has a degenerate polygon.");
            bands.Add((top, bottom, left, right));
        }
        return bands;
    }
}
