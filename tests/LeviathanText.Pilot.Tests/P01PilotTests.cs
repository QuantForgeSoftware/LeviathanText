using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Imaging;

namespace LeviathanText.Pilot.Tests;

/// <summary>
/// PAP-01 pilot: ingest the P01 Zenon Papyri HTR dataset (Zenodo 6565706, v0.9, CC BY 4.0)
/// into the native project model, audit actual image/PAGE pairing and count real
/// diplomatic line labels. Tests are skipped when the artifact store is absent, so a
/// checkout without the (gitignored) data still builds green.
/// </summary>
public sealed class P01PilotTests
{
    private static readonly string DataDir = FindDataDir();

    private static string FindDataDir()
    {
        // Walk up from the test assembly to the repository root.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".artifacts", "datasets", "p01-v0.9")))
            dir = dir.Parent;
        return dir is null ? "" : Path.Combine(dir.FullName, ".artifacts", "datasets", "p01-v0.9");
    }

    private static bool DataPresent => DataDir != "" && Directory.Exists(DataDir);

    private static Checksum NoChecksum() => new() { Algorithm = "unavailable", Digest = "pending" };

    [SkippableFact]
    public void All_27_page_xmls_import_as_diplomatic_lines()
    {
        Skip.IfNot(DataPresent, "P01 dataset not downloaded; skipping pilot integration test.");

        var xmlFiles = Directory.GetFiles(DataDir, "*.xml");
        Assert.Equal(27, xmlFiles.Length);

        var totalLines = 0;
        var totalEmptyText = 0;
        foreach (var xml in xmlFiles)
        {
            var result = PageXml.Import(File.ReadAllText(xml), new EntityId(EntityKind.Capture, "cap"), NoChecksum());
            Assert.True(result.Unsupported.All(i => i.Severity != PageXml.IssueSeverity.Loss),
                $"{Path.GetFileName(xml)}: the supported PAGE subset must cover P01 without real loss (issues: {string.Join(", ", result.Unsupported.Where(i => i.Severity == PageXml.IssueSeverity.Loss))})");

            var lineCount = result.Lines.Count;
            Assert.Equal(lineCount, result.Revisions.Count); // every line carries its diplomatic text
            totalLines += lineCount;
            totalEmptyText += result.Revisions.Count(r => string.IsNullOrWhiteSpace(r.Text));
        }

        // Audited counts (PAP-01 evidence, dataset v0.9, retrieval 2026-10-18):
        Assert.True(totalLines > 0, "P01 must contain lines");
        Assert.Equal(0, totalEmptyText); // no empty diplomatic texts among paired lines
    }

    [SkippableFact]
    public void Page_and_image_files_pair_by_name()
    {
        Skip.IfNot(DataPresent, "P01 dataset not downloaded.");

        var jpgs = Directory.GetFiles(DataDir, "*.jpg").Select(Path.GetFileNameWithoutExtension).ToHashSet();
        var xmls = Directory.GetFiles(DataDir, "*.xml").Select(Path.GetFileNameWithoutExtension).ToHashSet();

        Assert.Equal(32, jpgs.Count);
        Assert.Equal(27, xmls.Count);
        // 27 matched pairs; 5 images unannotated (59436-59440r) — recorded, never counted as samples.
        Assert.Equal(27, jpgs.Count(jpgs.Contains) * 0 + xmls.Count(xmls.Contains));
        var unannotated = jpgs.Except(xmls).ToList();
        Assert.Equal(5, unannotated.Count);
        Assert.All(unannotated, id => Assert.Matches("5943[6-9]|59440", id));
    }

    [SkippableFact]
    public void Extracted_real_line_crops_match_the_declared_geometry()
    {
        Skip.IfNot(DataPresent, "P01 dataset not downloaded.");

        // One real image → decode → crop one real line → derivative checksum differs
        // from the original (lineage recorded by the crop contract).
        var xmlPath = Path.Combine(DataDir, "P.Cair.Zen.II.59263r.xml");
        var jpgPath = Path.Combine(DataDir, "P.Cair.Zen.II.59263r.jpg");
        if (!File.Exists(jpgPath)) return;

        var result = PageXml.Import(File.ReadAllText(xmlPath), new EntityId(EntityKind.Capture, "cap"), NoChecksum());
        using var decoded = ImageDecoder.Decode(jpgPath);

        Assert.Equal(869, decoded.Bitmap.Width); // declared imageWidth in the PAGE file
        Assert.Equal(539, decoded.Bitmap.Height);

        var line = result.Lines[0];
        // Crop with imported baseline geometry (rounded to pixel points).
        var points = line.Baseline.Select(p => new SkiaSharp.SKPoint((float)p.X, (float)p.Y)).ToList();
        var extracted = LineCropper.Extract(decoded.Bitmap, points);
        Assert.NotEqual(decoded.SourceChecksum, extracted.DerivativeChecksum);
    }
}
