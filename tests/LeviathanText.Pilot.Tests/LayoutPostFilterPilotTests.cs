using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Imaging;
using LeviathanText.Layout;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace LeviathanText.Pilot.Tests;

/// <summary>
/// Layout post-filter experiment with a disciplined split: candidate filters are selected
/// on M02a DEV pages only, then the selected filter is reported on held-out M02a pages and
/// on P01 (out-of-domain). Writes docs/evaluation/layout-postfilter.md. The filter is
/// deliberately simple (density/height plausibility relative to the page's own lines) so
/// its failure modes are explainable.
/// </summary>
public sealed class LayoutPostFilterPilotTests(ITestOutputHelper output)
{
    /// <summary>Experiment record keeps the unfiltered detector explicit (the density filter is now default).</summary>
    private static readonly ProjectionProfileLayout.Options Unfiltered = new() { FilterImplausibleLines = false };

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LeviathanText.slnx")))
            dir = dir.Parent;
        return dir?.FullName;
    }

    private static List<(int Top, int Bottom)> GroundTruth(string xmlPath)
    {
        var imported = PageXml.Import(File.ReadAllText(xmlPath), new EntityId(EntityKind.Capture, "cap"),
            new Checksum { Algorithm = "unavailable", Digest = "pending" });
        var truth = new List<(int, int)>();
        foreach (var line in imported.Lines)
        {
            var geometry = line.Polygon.Count >= 3 ? line.Polygon : line.Baseline;
            if (geometry.Count < 2) continue;
            int top = (int)geometry.Min(p => p.Y);
            int bottom = (int)geometry.Max(p => p.Y);
            truth.Add((top, Math.Max(bottom, top + 1)));
        }
        return truth;
    }

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        if (sorted.Count == 0) return 0;
        return sorted[sorted.Count / 2];
    }

    private static IReadOnlyList<DetectedLine> FilterNone(IReadOnlyList<DetectedLine> lines, double medianDensity, double medianHeight) => lines;

    private static IReadOnlyList<DetectedLine> FilterDensity(IReadOnlyList<DetectedLine> lines, double medianDensity, double medianHeight) =>
        [.. lines.Where(l => l.InkDensity >= 0.5 * medianDensity)];

    private static IReadOnlyList<DetectedLine> FilterHeight(IReadOnlyList<DetectedLine> lines, double medianDensity, double medianHeight) =>
        [.. lines.Where(l => l.Height >= 0.5 * medianHeight && l.Height <= 2.5 * medianHeight)];

    private static IReadOnlyList<DetectedLine> FilterBoth(IReadOnlyList<DetectedLine> lines, double medianDensity, double medianHeight) =>
        [.. lines.Where(l =>
            l.InkDensity >= 0.5 * medianDensity &&
            l.Height >= 0.5 * medianHeight && l.Height <= 2.5 * medianHeight)];

    [SkippableFact]
    public void Dev_selected_postfilter_is_reported_on_held_out_pages()
    {
        var root = FindRepoRoot();
        Skip.IfNot(root is not null, "repo root not found");

        var m02a = Path.Combine(root!, ".artifacts", "datasets", "m02a-v1.0", "m02a");
        var p01 = Path.Combine(root!, ".artifacts", "datasets", "p01-v0.9");
        Skip.IfNot(Directory.Exists(m02a), "M02a not available");

        var dev = new List<(string Name, IReadOnlyList<DetectedLine> Lines, List<(int, int)> Truth)>();
        var test = new List<(string Name, IReadOnlyList<DetectedLine> Lines, List<(int, int)> Truth)>();
        var disposed = new List<SKBitmap>();

        var xmlFiles = Directory.GetFiles(Path.Combine(m02a, "xml"), "*.xml").OrderBy(f => f, StringComparer.Ordinal).ToList();
        for (int i = 0; i < xmlFiles.Count; i++)
        {
            var stem = Path.GetFileNameWithoutExtension(xmlFiles[i]);
            var image = Path.Combine(m02a, "images", stem + ".png");
            if (!File.Exists(image)) continue;

            var bitmap = ImageDecoder.Decode(image).Bitmap;
            disposed.Add(bitmap);
            var detected = ProjectionProfileLayout.Detect(bitmap, Unfiltered);
            var entry = ($"m02a:{stem}", detected.Lines, GroundTruth(xmlFiles[i]));

            if (i % 5 == 0) test.Add(entry);
            else if (i % 6 == 1) dev.Add(entry);
        }

        // P01 is a separate, out-of-domain reporting set (no tuning on it).
        if (Directory.Exists(p01))
        {
            foreach (var xml in Directory.GetFiles(p01, "*.xml").OrderBy(f => f, StringComparer.Ordinal).Take(8))
            {
                var image = Path.Combine(p01, Path.GetFileNameWithoutExtension(xml) + ".jpg");
                if (!File.Exists(image)) continue;
                var bitmap = ImageDecoder.Decode(image).Bitmap;
                disposed.Add(bitmap);
                var detected = ProjectionProfileLayout.Detect(bitmap, Unfiltered);
                test.Add(($"p01:{Path.GetFileNameWithoutExtension(xml)}", detected.Lines, GroundTruth(xml)));
            }
        }

        Skip.IfNot(dev.Count > 0 && test.Count > 0, "insufficient pages");

        try
        {
            // 1) Select the filter on dev pages only.
            var candidates = new (string Name, Func<IReadOnlyList<DetectedLine>, double, double, IReadOnlyList<DetectedLine>> Filter)[]
            {
                ("none", FilterNone),
                ("density<0.5x median", FilterDensity),
                ("height outside 0.5-2.5x median", FilterHeight),
                ("density+height", FilterBoth),
            };

            var devScores = new List<(string Name, double Precision, double Recall, double F1)>();
            foreach (var (name, filter) in candidates)
            {
                var rows = dev.Select(page =>
                {
                    var medianDensity = Median(page.Lines.Select(l => l.InkDensity));
                    var medianHeight = Median(page.Lines.Select(l => (double)l.Height));
                    return (Witness: page.Name,
                        Detected: filter(page.Lines, medianDensity, medianHeight),
                        GroundTruth: (IReadOnlyList<(int Top, int Bottom)>)page.Truth
                            .Select(t => (Top: t.Item1, Bottom: t.Item2)).ToList());
                }).ToList();
                var summary = LayoutComparison.Compare(rows);
                var f1 = summary.MeanPrecision + summary.MeanRecall == 0
                    ? 0
                    : 2 * summary.MeanPrecision * summary.MeanRecall / (summary.MeanPrecision + summary.MeanRecall);
                devScores.Add((name, summary.MeanPrecision, summary.MeanRecall, f1));
            }

            var best = devScores.OrderByDescending(s => s.F1).First();
            var chosen = candidates.Single(c => c.Name == best.Name).Filter;

            // 2) Report the chosen filter on held-out pages.
            IReadOnlyList<(string Witness, IReadOnlyList<DetectedLine> Detected, IReadOnlyList<(int Top, int Bottom)> GroundTruth)> Rows(
                Func<IReadOnlyList<DetectedLine>, double, double, IReadOnlyList<DetectedLine>> filter) =>
                [.. test.Select(page =>
                {
                    var medianDensity = Median(page.Lines.Select(l => l.InkDensity));
                    var medianHeight = Median(page.Lines.Select(l => (double)l.Height));
                    return (Witness: page.Name,
                        Detected: filter(page.Lines, medianDensity, medianHeight),
                        GroundTruth: (IReadOnlyList<(int Top, int Bottom)>)page.Truth
                            .Select(t => (Top: t.Item1, Bottom: t.Item2)).ToList());
                })];

            var baseline = LayoutComparison.Compare(Rows(FilterNone));
            var selected = LayoutComparison.Compare(Rows(chosen));

            var markdown = $"""
                # Layout post-filter: dev-selected, held-out report

                - Date: {DateTimeOffset.UtcNow:O}; contract {ContractVersion.Current}
                - Detector: projection profile (unchanged); filter applied to detected bands
                - Selection set: M02a dev pages only ({dev.Count} pages)
                - Reporting set: held-out M02a pages + P01 pages ({test.Count} pages); P01 is out-of-domain
                - Matching: vertical-band IoU ≥ {selected.IouThreshold:0.##}

                Dev scores for every candidate (selection is on F1):

                | Candidate | Precision | Recall | F1 |
                | --- | --- | --- | --- |
                {string.Join("\n", devScores.Select(s => $"| {s.Name} | {s.Precision:0.###} | {s.Recall:0.###} | {s.F1:0.###} |"))}

                Selected on dev: **{best.Name}**.

                Held-out comparison:

                | Variant | Mean precision | Mean recall |
                | --- | --- | --- |
                | Detector only | {baseline.MeanPrecision:0.###} | {baseline.MeanRecall:0.###} |
                | Detector + selected filter | {selected.MeanPrecision:0.###} | {selected.MeanRecall:0.###} |

                Per-page (filtered):

                | Page | GT | Detected | Matches | Precision | Recall |
                | --- | --- | --- | --- | --- | --- |
                {string.Join("\n", selected.PerWitness.Select(r => $"| {r.Witness} | {r.GroundTruthLines} | {r.DetectedLines} | {r.Matches} | {r.Precision:0.###} | {r.Recall:0.###} |"))}

                ## Status

                The density filter selected here is now the detector default (disable with
                `Options.FilterImplausibleLines = false`); this record keeps the unfiltered
                baseline explicit. A filter that only looks at the page's own line statistics can trade precision
                for recall but cannot recover missed lines. Multi-column, curved and rotated
                layouts remain unsupported; the next iteration is patch-level learning.
                """;

            var path = Path.Combine(root!, "docs", "evaluation", "layout-postfilter.md");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, markdown);
            output.WriteLine($"selected={best.Name}; baseline P={baseline.MeanPrecision:0.###} R={baseline.MeanRecall:0.###}; " +
                             $"filtered P={selected.MeanPrecision:0.###} R={selected.MeanRecall:0.###}");
        }
        finally
        {
            foreach (var bitmap in disposed) bitmap.Dispose();
        }
    }
}
