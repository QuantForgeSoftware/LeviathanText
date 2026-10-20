using System.IO.Compression;
using System.Text.Json;
using LeviathanText.Application;
using LeviathanText.Formats;

namespace LeviathanText.Application.Tests;

public sealed class ProjectBundleTests
{
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
            Assert.Contains("review-summary.json", names);
            Assert.Contains("adjudication.csv", names);
            var surface = project.Objects[0].Surfaces[0];
            Assert.Contains($"surfaces/{surface.Id}/diplomatic.txt", names);
            Assert.Contains($"surfaces/{surface.Id}/tei.xml", names);
            Assert.Contains($"surfaces/{surface.Id}/alto.xml", names);
            Assert.Contains($"surfaces/{surface.Id}/hocr.html", names);

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
