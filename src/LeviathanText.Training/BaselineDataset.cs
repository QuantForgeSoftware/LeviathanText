using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Imaging;
using SkiaSharp;

namespace LeviathanText.Training;

public sealed record BaselineDatasetIssue(string Page, string Message);

public sealed record BaselineSample(
    string Page,
    string ImagePath,
    float[] Pixels,
    int Channels,
    int Width,
    int Height,
    float[,,] Target);

public sealed record BaselineDatasetResult(
    IReadOnlyList<BaselineSample> Samples,
    IReadOnlyList<BaselineDatasetIssue> Issues);

/// <summary>
/// PAGE/image baseline-dataset loader. One sample is one page image with a multi-class
/// target at the requested dimensions. Page XML remains the label source; images are
/// decoded and normalized with <see cref="LineInputTransform"/>. Missing images, absent
/// geometry and unknown labels become explicit issues/skips, not silent background.
/// </summary>
public static class BaselineDataset
{
    public sealed record Options
    {
        public string ImageExtension { get; init; } = ".png";
        public int TargetWidth { get; init; } = 256;
        public int TargetHeight { get; init; } = 256;
        public int Channels { get; init; } = 1;
    }

    public static BaselineDatasetResult Load(
        string directory,
        SegmentationClassMap classes,
        Options? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(classes);
        options ??= new Options();
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException(directory);

        var samples = new List<BaselineSample>();
        var issues = new List<BaselineDatasetIssue>();
        foreach (var xmlPath in Directory.GetFiles(directory, "*.xml").OrderBy(path => path, StringComparer.Ordinal))
        {
            var page = Path.GetFileNameWithoutExtension(xmlPath);
            var imagePath = ResolveImage(directory, page, options.ImageExtension);
            if (imagePath is null)
            {
                issues.Add(new BaselineDatasetIssue(page, "No image matched the PAGE file."));
                continue;
            }

            PageXml.PageImportResult parsed;
            try
            {
                parsed = PageXml.Import(File.ReadAllText(xmlPath),
                    new EntityId(EntityKind.Capture, "baseline"),
                    new Checksum { Algorithm = "unavailable", Digest = "pending" });
            }
            catch (Exception ex) when (ex is InvalidDataException or System.Xml.XmlException)
            {
                issues.Add(new BaselineDatasetIssue(page, ex.Message));
                continue;
            }

            using var decoded = ImageDecoder.Decode(imagePath);
            var image = LineInputTransform.Apply(decoded.Bitmap, new LineInputTransform.Options
            {
                Height = options.TargetHeight,
                Width = options.TargetWidth,
                Channels = options.Channels,
            });

            var regions = parsed.Regions
                .Where(region => region.Polygon.Count >= 3)
                .Select(region => new SegmentationRegion(TypeOf(region.Tags), region.Polygon))
                .ToList();
            var lines = parsed.Lines
                .Where(line => line.Polygon.Count >= 3 || line.Baseline.Count >= 2)
                .Select(line => new SegmentationBaseline(
                    TypeOf(line.Tags),
                    line.Polygon.Count >= 3 ? line.Polygon : line.Baseline))
                .ToList();
            if (regions.Count == 0 && lines.Count == 0)
            {
                issues.Add(new BaselineDatasetIssue(page, "No usable region or line geometry."));
                continue;
            }

            var target = SegmentationTargetBuilder.Build(options.TargetWidth, options.TargetHeight, classes, regions, lines);
            samples.Add(new BaselineSample(page, imagePath, image.Pixels, image.Channels,
                image.Width, image.Height, target));
        }
        return new BaselineDatasetResult(samples, issues);
    }

    private static string TypeOf(IReadOnlyDictionary<string, IReadOnlyList<string>> tags) =>
        tags.TryGetValue("type", out var values) && values.Count > 0 ? values[0] : "$par";

    private static string? ResolveImage(string directory, string page, string extension)
    {
        string[] candidates =
        [
            Path.Combine(directory, page + extension),
            Path.Combine(directory, "images", page + extension),
            Path.Combine(directory, "image", page + extension),
            Path.Combine(Path.GetDirectoryName(directory)!, "images", page + extension),
            Path.Combine(Path.GetDirectoryName(directory)!, "image", page + extension),
        ];
        return candidates.FirstOrDefault(File.Exists);
    }
}
