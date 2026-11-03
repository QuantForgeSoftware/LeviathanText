using System.IO.Compression;
using System.Text.Json;
using LeviathanText.Application;
using LeviathanText.Formats;

namespace LeviathanText.Application.Tests;

public sealed class ProjectBundleTests
{
    [Fact]
    public void Opt_in_bundle_images_are_copied_and_omissions_are_reported()
    {
        var image = Path.Combine(Path.GetTempPath(), $"lt-image-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(image, [1, 2, 3, 4, 5]);
        var project = SampleProjectBuilder.Build();
        var surface = project.Objects[0].Surfaces[0];
        var capture = surface.Captures[0] with { ImageLocation = image };
        var modified = project with
        {
            Objects =
            [
                project.Objects[0] with
                {
                    Surfaces =
                    [
                        surface with { Captures = [capture] },
                    ],
                },
            ],
        };
        var path = Path.Combine(Path.GetTempPath(), $"lt-bundle-{Guid.NewGuid():N}.zip");
        try
        {
            ProjectBundle.Export(modified, path, includeImages: true);

            using var archive = ZipFile.OpenRead(path);
            var names = archive.Entries.Select(e => e.FullName).ToList();
            Assert.Contains($"images/{capture.Id}.png", names);
            Assert.DoesNotContain("images-omitted.txt", names);
            using var stream = archive.GetEntry($"images/{capture.Id}.png")!.Open();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            Assert.Equal([1, 2, 3, 4, 5], memory.ToArray());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(image)) File.Delete(image);
        }
    }

    [Fact]
    public void Missing_opt_in_images_are_reported_instead_of_failing_the_bundle()
    {
        var project = SampleProjectBuilder.Build();
        var surface = project.Objects[0].Surfaces[0];
        var capture = surface.Captures[0] with
        {
            ImageLocation = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.png"),
        };
        var modified = project with
        {
            Objects =
            [
                project.Objects[0] with
                {
                    Surfaces = [surface with { Captures = [capture] }],
                },
            ],
        };
        var path = Path.Combine(Path.GetTempPath(), $"lt-bundle-{Guid.NewGuid():N}.zip");
        try
        {
            ProjectBundle.Export(modified, path, includeImages: true);
            using var archive = ZipFile.OpenRead(path);
            Assert.Contains("images-omitted.txt", archive.Entries.Select(e => e.FullName));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Bundle_contains_manifest_project_and_per_surface_exports()
    {
        var project = SampleProjectBuilder.Build();
        var path = Path.Combine(Path.GetTempPath(), $"lt-bundle-{Guid.NewGuid():N}.zip");
        try
        {
            var result = ProjectBundle.Export(project, path);

            Assert.True(File.Exists(path));
            Assert.True(result.FileCount >= 7); // manifest, project, training + 4 per surface

            using var archive = ZipFile.OpenRead(path);
            var names = archive.Entries.Select(e => e.FullName).ToList();
            Assert.Contains("manifest.json", names);
            Assert.Contains("project.json", names);
            Assert.Contains("training-export.json", names);
            Assert.Contains("profile-report.json", names);
            Assert.Contains("lines.csv", names);
            Assert.Contains("annotations.csv", names);
            Assert.Contains("word-boundaries.csv", names);
            Assert.Contains("crosswalk.csv", names);
            Assert.Contains("line-crosswalk.csv", names);
            Assert.Contains("fragment-crosswalk.csv", names);
            Assert.Contains("integrity.json", names);
            Assert.Contains("overlap.json", names);
            Assert.Contains("edition.html", names);
            Assert.Contains("edition-loss.txt", names);
            Assert.Contains("review-summary.json", names);
            Assert.Contains("adjudication.csv", names);
            var surface = project.Objects[0].Surfaces[0];
            Assert.Contains($"surfaces/{surface.Id}/diplomatic.txt", names);
            Assert.Contains($"surfaces/{surface.Id}/tei.xml", names);
            Assert.Contains($"surfaces/{surface.Id}/alto.xml", names);
            Assert.Contains($"surfaces/{surface.Id}/hocr.html", names);
            Assert.Contains($"surfaces/{surface.Id}/edition.html", names);
            Assert.Contains($"surfaces/{surface.Id}/edition-loss.txt", names);
            Assert.Contains($"surfaces/{surface.Id}/searchable.pdf", names);
            Assert.Contains($"surfaces/{surface.Id}/searchable-pdf-loss.txt", names);

            using var reader = new StreamReader(archive.GetEntry("manifest.json")!.Open());
            using var manifest = JsonDocument.Parse(reader.ReadToEnd());
            Assert.Equal("leviathantext-bundle", manifest.RootElement.GetProperty("format").GetString());
            Assert.Equal(project.Name, manifest.RootElement.GetProperty("project").GetProperty("name").GetString());
            Assert.Equal(1, manifest.RootElement.GetProperty("surfaces").GetArrayLength());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
