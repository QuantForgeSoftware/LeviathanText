using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Imaging;
using Xunit;
using Xunit.Abstractions;

namespace LeviathanText.Pilot.Tests;

/// <summary>
/// INS-01 pilot: the inscription track's first real-data increment against I01
/// (I.Sicily / Crossreads, CC BY 4.0). Deliverables proven here:
///  1. Comment-aware EpiDoc audit — the Greek + active-graphic intersection
///     (graphics hidden inside XML comments are NOT image availability).
///  2. A fetched smoke image (bounded IIIF size) decodes with recorded checksum.
///  3. Real EpiDoc imports through the bounded TEI profile without throwing;
///     outside-profile elements are reported, never flattened silently.
/// </summary>
public sealed class InscriptionPilotTests(ITestOutputHelper output)
{
    private static string RepoDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".artifacts", "datasets", "i01", "repo")))
            dir = dir.Parent;
        return dir is null ? "" : Path.Combine(dir.FullName, ".artifacts", "datasets", "i01", "repo");
    }

    [SkippableFact]
    public void Audit_GreekRecords_WithCommentAwareImageAvailability()
    {
        var repo = RepoDir();
        Skip.IfNot(repo != "", "I01 repo not downloaded.");

        var files = Directory.GetFiles(Path.Combine(repo, "inscriptions"), "*.xml");
        Assert.True(files.Length > 5000, $"expected the full ISicily repo, got {files.Length}");

        int greekTotal = 0, greekActiveImage = 0, nonGreek = 0;
        foreach (var file in files)
        {
            var xml = File.ReadAllText(file);
            var langs = new HashSet<string>();
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(xml, "xml:lang=\"([^\"]+)\""))
                langs.Add(m.Groups[1].Value);

            bool isGreek = langs.Any(l => l.StartsWith("grc") || l.StartsWith("greek"));
            if (!isGreek) { nonGreek++; continue; }
            greekTotal++;

            var active = System.Text.RegularExpressions.Regex.Replace(xml, "<!--.*?-->", "", System.Text.RegularExpressions.RegexOptions.Singleline);
            if (active.Contains("<graphic")) greekActiveImage++;
        }

        output.WriteLine($"INS-01 audit: records={files.Length}; greek={greekTotal}; greek-with-active-image={greekActiveImage}; non-greek={nonGreek}");
        Assert.Equal(3135, greekTotal);
        Assert.Equal(448, greekActiveImage);
        Assert.Equal(2001, nonGreek);
    }

    [SkippableFact]
    public void SmokeImage_Decodes_WithChecksum()
    {
        var repo = RepoDir();
        Skip.IfNot(repo != "", "I01 repo not downloaded.");
        var imagePath = Path.Combine(Path.GetDirectoryName(repo)!, "smoke", "ISic001151_w800.jpg");
        Skip.IfNot(File.Exists(imagePath), "smoke image not fetched");

        using var decoded = ImageDecoder.Decode(imagePath);
        Assert.Equal(800, decoded.Bitmap.Width);
        Assert.Equal("sha256", decoded.SourceChecksum.Algorithm);
    }

    [SkippableTheory]
    [InlineData("ISic001151")]
    [InlineData("ISic003785")]
    public void Real_EpiDoc_Imports_ThroughBoundedProfile(string id)
    {
        var repo = RepoDir();
        Skip.IfNot(repo != "", "I01 repo not downloaded.");
        var path = Path.Combine(repo, "inscriptions", id + ".xml");
        Skip.IfNot(File.Exists(path), "record missing");

        var result = TeiXml.Import(File.ReadAllText(path));
        Assert.True(result.Revisions.Count > 0 || result.Unsupported.Count > 0,
            "a real record must yield either transcribed lines or an explicit unsupported report");

        output.WriteLine($"{id}: revisions={result.Revisions.Count}, unsupported={result.Unsupported.Count} " +
            $"({string.Join(", ", result.Unsupported.Take(6).Select(i => i.Element))})");

        // Every produced revision must contain no literal bracket conventions.
        foreach (var revision in result.Revisions)
            Assert.DoesNotContain('[', revision.Text);
    }
}
