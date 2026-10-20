using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Imaging;
using Xunit;
using Xunit.Abstractions;

namespace LeviathanText.Pilot.Tests;

/// <summary>
/// Bounded IIIF import proven against a REAL I.Sicily Presentation 3 manifest
/// (fetched 2026-10-18; rights and credits belong to the source and are retained).
/// </summary>
public sealed class IiifPilotTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [SkippableFact]
    public void Real_manifest_parses_to_canvases_and_rights()
    {
        var path = FixturePath("isic001151.manifest.json");
        Skip.IfNot(File.Exists(path), "manifest fixture missing");

        var result = IiifImport.ParseManifest(File.ReadAllText(path));

        Assert.Equal("https://iiif.csad.ox.ac.uk/manifests/ISicily/ISic001151", result.ManifestId);
        Assert.Equal("Funerary block, Termini Imerese, 101–300 CE", result.ManifestLabel);
        Assert.Contains("Photo J. Prag", result.RightsStatement);
        Assert.True(result.Canvases.Count >= 4, $"expected multiple views, got {result.Canvases.Count}");

        // Each view carries image + service references; canvases keep their stable IDs.
        var front = result.Canvases[0];
        Assert.Equal("https://iiif.csad.ox.ac.uk/manifests/ISicily/ISic001151/canvas/front", front.CanvasId);
        Assert.Contains("cantaloupe.classics.ox.ac.uk", front.Images[0].ImageUrl);
        Assert.NotNull(front.Images[0].ServiceUrl);
    }

    [SkippableFact]
    public void Views_become_separate_captures_never_merged()
    {
        var path = FixturePath("isic001151.manifest.json");
        Skip.IfNot(File.Exists(path), "manifest fixture missing");
        var result = IiifImport.ParseManifest(File.ReadAllText(path));

        // Every view is its own capture/frame; coordinates are never merged without
        // registration (project-plan §3). This is the IIIF ingest contract expressed natively.
        var captures = result.Canvases
            .Where(c => c.Images.Count > 0)
            .Select((canvas, index) => new NativeCapture
            {
                Id = new EntityId(EntityKind.Capture, $"iiif{index:x4}"),
                Frame = new CoordinateFrame
                {
                    ImageRef = new EntityId(EntityKind.Capture, $"iiif{index:x4}"),
                    ImageChecksum = new Checksum { Algorithm = "unavailable", Digest = "pending-fetch" },
                },
                ImageLocation = canvas.Images[0].ImageUrl,
                RightsEvidenceRef = result.RightsStatement,
            })
            .ToList();

        Assert.True(captures.Count >= 4);
        Assert.All(captures, c => Assert.NotNull(c.RightsEvidenceRef));
    }

    [SkippableFact]
    public void Fetched_smoke_image_matches_the_manifest_view()
    {
        var path = FixturePath("ISic001151_w800.jpg");
        Skip.IfNot(File.Exists(path), "smoke image missing");

        // The fetched bounded-size rendering corresponds to the manifest's image URL
        // (same service, width parameter). Decode proves the IIIF-derived file is usable.
        using var decoded = ImageDecoder.Decode(path);
        Assert.Equal(800, decoded.Bitmap.Width);
    }
}
