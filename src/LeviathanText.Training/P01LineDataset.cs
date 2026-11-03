using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Imaging;
using LeviathanText.Recognition;
using SkiaSharp;

namespace LeviathanText.Training;

/// <summary>
/// Real-corpus pilot dataset: loads the md5-verified P01 Zenon Papyri HTR dataset
/// (Zenodo 6565706, v0.9, CC BY 4.0) from the artifact store, crops each PAGE TextLine
/// by its own Coords box (baseline as fallback), resizes to the CRNN input height and
/// returns labeled lines, with the page file as the split unit (witness-level
/// splitting; no random line split, per project-plan §5).
///
/// These are REAL diplomatic labels, but they are Transkribus-prepared and NOT yet
/// expert-adjudicated (DATA-03): any number produced here is a pilot-scale measurement,
/// not a release claim.
/// </summary>
public static class P01LineDataset
{
    public sealed record Snapshot(
        IReadOnlyList<LabeledLine> Train,
        IReadOnlyList<LabeledLine> Dev,
        IReadOnlyList<LabeledLine> TestLocked,
        IReadOnlyList<string> UnannotatedImages,
        int TotalLines,
        IReadOnlyDictionary<string, string> WitnessSplitMap);

    /// <summary>
    /// Load a PAGE/image corpus directory (works for any track; image extension differs —
    /// P01 uses .jpg, manuscript scans use .png). The directory/file remains the split unit.
    /// </summary>
    // Retain the original compiled call signature; the overload below adds source-compatible profile selection.
    public static Snapshot Load(string datasetDirectory, string imageExtension, int testEveryNth, int devEveryNth, int targetHeight) =>
        Load(datasetDirectory, imageExtension, testEveryNth, devEveryNth, targetHeight, preprocessingProfile: null);

    public static Snapshot Load(string datasetDirectory, string imageExtension = ".jpg", int testEveryNth = 5, int devEveryNth = 6,
        int? targetHeight = null, LinePreprocessingProfile? preprocessingProfile = null)
    {
        var profile = preprocessingProfile ?? LinePreprocessingProfile.P01Otsu(targetHeight ?? CrnnModel.InputHeight);
        profile.Validate(targetHeight);
        profile.RequireStandardGeometry();
        if (!Directory.Exists(datasetDirectory))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = datasetDirectory,
                Message = "P01 artifact directory not found.",
            });

        var xmlFiles = Directory.GetFiles(datasetDirectory, "*.xml")
            .OrderBy(f => f, StringComparer.Ordinal).ToList();
        if (xmlFiles.Count == 0)
            throw new InvalidDataException($"No PAGE XML files under {datasetDirectory}.");

        var train = new List<LabeledLine>();
        var dev = new List<LabeledLine>();
        var test = new List<LabeledLine>();
        var witnessSplit = new Dictionary<string, string>();
        var noChecksum = new Checksum { Algorithm = "unavailable", Digest = "pending" };

        for (int i = 0; i < xmlFiles.Count; i++)
        {
            var xmlPath = xmlFiles[i];
            var baseName = Path.GetFileNameWithoutExtension(xmlPath);
            var imagePath = ResolveImagePath(datasetDirectory, baseName, imageExtension);
            if (imagePath is null)
                continue; // XML without image: recorded in the manifest, not used here

            string split = i % testEveryNth == 0 ? "test" : i % devEveryNth == 1 ? "dev" : "train";
            witnessSplit[baseName] = split;

            var imported = PageXml.Import(File.ReadAllText(xmlPath), new EntityId(EntityKind.Capture, "cap"), noChecksum);
            using var decoded = ImageDecoder.Decode(imagePath);

            var revisionById = imported.Revisions.ToDictionary(r => r.Id);
            foreach (var line in imported.Lines)
            {
                if (line.DiplomaticRevisionId is not { } rid || !revisionById.TryGetValue(rid, out var revision)) continue;
                if (string.IsNullOrWhiteSpace(revision.Text)) continue;

                // Prefer the full ink band (the TextLine's own Coords) over the bare
                // baseline: the bbox of a nearly horizontal baseline otherwise leaves
                // ~zero vertical margin for ascenders, descenders and marks.
                var geometry = line.Polygon.Count >= 3 ? line.Polygon : line.Baseline;
                var points = geometry.Select(p => new SKPoint((float)p.X, (float)p.Y)).ToList();
                if (points.Count < 2) continue;

                var extracted = LineCropper.Extract(decoded.Bitmap, points);
                LineImage lineImage;
                try
                {
                    if (preprocessingProfile is null)
                    {
                        // Old training used a subset of the original page rather than
                        // the drawn derivative. Keep that exact raster path for old calls.
                        var crop = extracted.AppliedCrop;
                        using var subset = new SKBitmap();
                        if (!decoded.Bitmap.ExtractSubset(subset, new SKRectI(crop.Left, crop.Top, crop.Left + crop.Width, crop.Top + crop.Height)))
                            throw new InvalidDataException("Cannot extract the legacy PAGE line crop.");
                        lineImage = LinePreprocessor.Apply(subset, profile);
                    }
                    else
                    {
                        lineImage = LinePreprocessor.Apply(extracted.Bitmap, profile);
                    }
                }
                finally { extracted.Bitmap.Dispose(); }

                var labeled = new LabeledLine(revision.Text, lineImage)
                {
                    SourceId = Path.GetFileName(xmlPath) + "#" + line.Id.Suffix,
                };
                switch (split)
                {
                    case "test": test.Add(labeled); break;
                    case "dev": dev.Add(labeled); break;
                    default: train.Add(labeled); break;
                }
            }
        }

        var unannotated = Directory.GetFiles(datasetDirectory, "*" + imageExtension)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name is not null && !File.Exists(Path.Combine(datasetDirectory, name + ".xml")))
            .Select(name => name!)
            .ToList();

        int total = train.Count + dev.Count + test.Count;
        return new Snapshot(train, dev, test, unannotated, total, witnessSplit);
    }

    /// <summary>Find the image for a PAGE file: next to the XML, or in sibling/child images|image directories (Transkribus conventions vary).</summary>
    private static string? ResolveImagePath(string datasetDirectory, string baseName, string imageExtension)
    {
        string[] candidates =
        {
            Path.Combine(datasetDirectory, baseName + imageExtension),
            Path.Combine(datasetDirectory, "images", baseName + imageExtension),
            Path.Combine(datasetDirectory, "image", baseName + imageExtension),
            Path.Combine(Path.GetDirectoryName(datasetDirectory)!, "images", baseName + imageExtension),
            Path.Combine(Path.GetDirectoryName(datasetDirectory)!, "image", baseName + imageExtension),
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    /// <summary>Legacy helper retained for callers; the pixel implementation uses this same shared algorithm.</summary>
    internal static int Otsu(int[] histogram, int total) => Binarization.Otsu(histogram, total);
}
