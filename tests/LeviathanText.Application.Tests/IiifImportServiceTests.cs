using LeviathanText.Application;
using LeviathanText.Formats;

namespace LeviathanText.Application.Tests;

/// <summary>
/// Bounded IIIF import through the Application layer, using the real I.Sicily manifest
/// fixture. Views stay separate captures; attribution is preserved; nothing is fetched.
/// </summary>
public sealed class IiifImportServiceTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "isic001151.manifest.json");

    [SkippableFact]
    public void Manifest_imports_as_one_object_with_separate_capture_per_view()
    {
        Skip.IfNot(File.Exists(FixturePath), "manifest fixture missing");

        var store = new InMemoryStore();
        var projects = new ProjectService(store);
        var project = projects.Create("iiif-application-test");
        var interchange = new InterchangeService(projects);

        var (imported, manifest, warnings) = interchange.ImportIiif(project, FixturePath);

        var surface = Assert.Single(imported.Surfaces);
        Assert.True(surface.Captures.Count >= 4, $"expected multiple views, got {surface.Captures.Count}");
        Assert.All(surface.Captures, c => Assert.Contains("cantaloupe.classics.ox.ac.uk", c.ImageLocation));
        Assert.All(surface.Captures, c => Assert.Equal(manifest.RightsStatement, c.RightsEvidenceRef));
        Assert.Contains("Photo J. Prag", manifest.RightsStatement);
        Assert.Contains("iiif-manifest", imported.ExternalRefs.Keys);

        // Views are NOT coordinate-merged: each capture is its own frame.
        Assert.Equal(surface.Captures.Count, surface.Captures.Select(c => c.Frame.ImageRef).Distinct().Count());
        Assert.Empty(warnings);

        // Persisted: reload finds the object.
        var reloaded = projects.Load(project.Id);
        Assert.Single(reloaded.Objects);
    }

    [SkippableFact]
    public void Manifest_without_canvases_imports_nothing_and_warns()
    {
        Skip.IfNot(File.Exists(FixturePath), "manifest fixture missing");
        var emptyManifest = Path.Combine(Path.GetTempPath(), $"empty-{Guid.NewGuid():N}.json");
        File.WriteAllText(emptyManifest, """{"@context":"http://iiif.io/api/presentation/3/context.json","id":"x","type":"Manifest","items":[]}""");

        try
        {
            var store = new InMemoryStore();
            var projects = new ProjectService(store);
            var project = projects.Create("iiif-empty-test");
            var (imported, _, warnings) = new InterchangeService(projects).ImportIiif(project, emptyManifest);

            Assert.Empty(imported.Surfaces[0].Captures);
            Assert.NotEmpty(warnings);
            Assert.Empty(projects.Load(project.Id).Objects); // nothing persisted
        }
        finally
        {
            File.Delete(emptyManifest);
        }
    }
}
