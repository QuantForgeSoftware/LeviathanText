using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Imaging;
using LeviathanText.Layout;
using Xunit;
using Xunit.Abstractions;

namespace LeviathanText.Pilot.Tests;

/// <summary>
/// Phase 0 gate: automatic vs manually-supplied layout comparison on REAL pages.
/// Ground truth = imported PAGE line boxes; automatic = ProjectionProfileLayout.
/// Writes docs/evaluation/layout-comparison.md. Results are disclosed with the
/// matching tolerance; fully automatic processing is never conflated with manual layout.
/// </summary>
public sealed class LayoutComparisonPilotTests(ITestOutputHelper output)
{
    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LeviathanText.slnx")))
            dir = dir.Parent;
        return dir?.FullName;
    }

    [SkippableFact]
    public void CompareAutomaticVsImportedLayout_OnBothTracks()
    {
        var root = FindRepoRoot();
        Skip.IfNot(root is not null, "repo root not found");

        var witnesses = new List<(string, IReadOnlyList<DetectedLine>, IReadOnlyList<(int, int)>)>();

        // Papyri: P01 files (jpg next to xml).
        var p01Dir = Path.Combine(root, ".artifacts", "datasets", "p01-v0.9");
        if (Directory.Exists(p01Dir))
            witnesses.AddRange(CompareTrack(p01Dir, ".jpg", "p01", pagesToSample: 6));

        // Manuscripts: M02a pages (png under images/).
        var m02aXml = Path.Combine(root, ".artifacts", "datasets", "m02a-v1.0", "m02a", "xml");
        if (Directory.Exists(m02aXml))
            witnesses.AddRange(CompareTrack(m02aXml, ".png", "m02a", pagesToSample: 6));

        Skip.IfNot(witnesses.Count > 0, "no datasets downloaded");

        var summary = LayoutComparison.Compare(witnesses);
        var markdown = $"""
            # Automatic vs imported layout comparison (pilot)

            - Date: {DateTimeOffset.UtcNow:O}; contract {ContractVersion.Current}
            - Automatic: {nameof(ProjectionProfileLayout)} (single-column projection profile; multi-column/curved unsupported and recorded, not silently mis-segmented)
            - Matching: vertical-band IoU ≥ {summary.IouThreshold:0.##} (disclosed tolerance)
            - Confidence semantics: None (heuristic module has no probability claim)

            | Witness | GT lines | Detected | Matches | Precision | Recall | Mean IoU |
            | --- | --- | --- | --- | --- | --- | --- |
            {string.Join("\n", summary.PerWitness.Select(r => $"| {r.Witness} | {r.GroundTruthLines} | {r.DetectedLines} | {r.Matches} | {r.Precision:0.###} | {r.Recall:0.###} | {r.MeanVerticalIou:0.###} |"))}

            **Mean precision {summary.MeanPrecision:0.###}, mean recall {summary.MeanRecall:0.###}** (pilot sample).

            Status: measurement baseline only; per-hand/capture strata and full acceptance thresholds await Phase 0 completion.
            """;
        var path = Path.Combine(root, "docs", "evaluation", "layout-comparison.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, markdown);
        output.WriteLine($"wrote {path}: precision {summary.MeanPrecision:0.###}, recall {summary.MeanRecall:0.###}");

        Assert.True(summary.MeanRecall > 0.3, $"automatic line recall {summary.MeanRecall:0.###} unexpectedly low for single-column pages");
    }

    private static IEnumerable<(string, IReadOnlyList<DetectedLine>, IReadOnlyList<(int, int)>)> CompareTrack(
        string directory, string imageExtension, string trackPrefix, int pagesToSample)
    {
        var xmlFiles = Directory.GetFiles(directory, "*.xml").OrderBy(f => f, StringComparer.Ordinal).Take(pagesToSample);
        foreach (var xmlPath in xmlFiles)
        {
            var baseName = Path.GetFileNameWithoutExtension(xmlPath);
            var imagePath = ResolveImage(directory, baseName, imageExtension);
            if (imagePath is null) continue;

            var imported = PageXml.Import(File.ReadAllText(xmlPath), new EntityId(EntityKind.Capture, "cap"),
                new Checksum { Algorithm = "unavailable", Digest = "pending" });
            using var decoded = ImageDecoder.Decode(imagePath);

            // Imported ground truth: the page's real line bands (from line Coords/baseline y-extent).
            var groundTruth = new List<(int, int)>();
            foreach (var line in imported.Lines)
            {
                var geometry = line.Polygon.Count >= 3 ? line.Polygon : line.Baseline;
                if (geometry.Count < 2) continue;
                int top = (int)geometry.Min(p => p.Y);
                int bottom = (int)geometry.Max(p => p.Y);
                groundTruth.Add((top, Math.Max(bottom, top + 1)));
            }

            var detected = ProjectionProfileLayout.Detect(decoded.Bitmap);
            yield return ($"{trackPrefix}:{baseName}", detected.Lines, groundTruth);
        }
    }

    private static string? ResolveImage(string directory, string baseName, string imageExtension)
    {
        string[] candidates =
        {
            Path.Combine(directory, baseName + imageExtension),
            Path.Combine(directory, "images", baseName + imageExtension),
            Path.Combine(directory, "image", baseName + imageExtension),
            Path.Combine(Path.GetDirectoryName(directory)!, "images", baseName + imageExtension),
            Path.Combine(Path.GetDirectoryName(directory)!, "image", baseName + imageExtension),
        };
        return candidates.FirstOrDefault(File.Exists);
    }
}
