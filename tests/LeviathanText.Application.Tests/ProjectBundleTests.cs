using System.IO.Compression;
using System.Text.Json;
using LeviathanText.Application;
using LeviathanText.Formats;
using LeviathanText.Imaging;
using SkiaSharp;
using UglyToad.PdfPig;

namespace LeviathanText.Application.Tests;

public sealed class ProjectBundleTests
{
    [Fact]
    public void Opt_in_bundle_images_are_copied_and_omissions_are_reported()
    {
        var image = Path.Combine(Path.GetTempPath(), $"lt-image-{Guid.NewGuid():N}.png");
        var originalBytes = ImageBytes();
        File.WriteAllBytes(image, originalBytes);
        var project = SampleProjectBuilder.Build();
        var surface = project.Objects[0].Surfaces[0];
        var originalCapture = surface.Captures[0];
        var capture = originalCapture with
        {
            ImageLocation = image,
            Frame = originalCapture.Frame with { ImageChecksum = ImageDecoder.ChecksumOf(originalBytes) },
        };
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
            using var editionReader = new StreamReader(archive.GetEntry("edition.html")!.Open());
            Assert.Contains($"images/{capture.Id}.png", editionReader.ReadToEnd());
            using var surfaceEditionReader = new StreamReader(
                archive.GetEntry($"surfaces/{surface.Id}/edition.html")!.Open());
            Assert.Contains($"../../images/{capture.Id}.png", surfaceEditionReader.ReadToEnd());
            using var stream = archive.GetEntry($"images/{capture.Id}.png")!.Open();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            Assert.Equal(originalBytes, memory.ToArray());
            Assert.Equal(1, EmbeddedImages(archive, $"surfaces/{surface.Id}/searchable.pdf"));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(image)) File.Delete(image);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Default_and_checksum_rejected_bundles_have_no_embedded_capture(bool includeImages)
    {
        var directory = Directory.CreateTempSubdirectory("lt-bundle-pdf-");
        try
        {
            var image = Path.Combine(directory.FullName, "capture.png");
            File.WriteAllBytes(image, ImageBytes());
            var project = SampleProjectBuilder.Build();
            var obj = project.Objects[0];
            var surface = obj.Surfaces[0];
            var capture = surface.Captures[0] with { ImageLocation = image };
            // The fixture's pinned checksum differs from the valid bytes now at the path.
            var modified = project with
            {
                Objects = [obj with { Surfaces = [surface with { Captures = [capture] }] }],
            };
            var path = Path.Combine(directory.FullName, "bundle.zip");

            ProjectBundle.Export(modified, path, includeImages);

            using var archive = ZipFile.OpenRead(path);
            Assert.DoesNotContain(archive.Entries, entry => entry.FullName.StartsWith("images/", StringComparison.Ordinal));
            Assert.Equal(0, EmbeddedImages(archive, $"surfaces/{surface.Id}/searchable.pdf"));
            if (includeImages)
            {
                using var omissions = new StreamReader(archive.GetEntry("images-omitted.txt")!.Open());
                Assert.Contains("checksum mismatch", omissions.ReadToEnd());
            }
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public void Failed_export_preserves_existing_destination()
    {
        var directory = Directory.CreateTempSubdirectory("lt-bundle-atomic-");
        try
        {
            var project = SampleProjectBuilder.Build();
            var obj = project.Objects[0];
            var surface = obj.Surfaces[0];
            var invalid = project with
            {
                Objects = [obj with { Surfaces = [surface with { Revisions = [surface.Revisions[0], surface.Revisions[0]] }] }],
            };
            var path = Path.Combine(directory.FullName, "existing.zip");
            byte[] original = "existing export"u8.ToArray();
            File.WriteAllBytes(path, original);

            Assert.Throws<ArgumentException>(() => ProjectBundle.Export(invalid, path));

            Assert.Equal(original, File.ReadAllBytes(path));
            Assert.Single(directory.GetFiles());
        }
        finally { directory.Delete(recursive: true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Export_refuses_to_replace_a_source_capture(bool throughDirectoryLink)
    {
        if (throughDirectoryLink && OperatingSystem.IsWindows()) return;
        var directory = Directory.CreateTempSubdirectory("lt-bundle-source-");
        try
        {
            var image = Path.Combine(directory.FullName, "capture.png");
            var original = ImageBytes();
            File.WriteAllBytes(image, original);
            var project = SampleProjectBuilder.Build();
            var obj = project.Objects[0];
            var surface = obj.Surfaces[0];
            var capture = surface.Captures[0] with { ImageLocation = image };
            var modified = project with { Objects = [obj with { Surfaces = [surface with { Captures = [capture] }] }] };

            var destination = image;
            if (throughDirectoryLink)
            {
                var link = Path.Combine(directory.FullName, "alias");
                Directory.CreateSymbolicLink(link, directory.FullName);
                destination = Path.Combine(link, "capture.png");
            }
            Assert.Throws<IOException>(() => ProjectBundle.Export(modified, destination, includeImages: true));

            Assert.Equal(original, File.ReadAllBytes(image));
        }
        finally { directory.Delete(recursive: true); }
    }

    private static int EmbeddedImages(ZipArchive archive, string name)
    {
        using var stream = archive.GetEntry(name)!.Open();
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        using var pdf = PdfDocument.Open(bytes.ToArray());
        return pdf.GetPage(1).GetImages().Count();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Non_image_and_truncated_capture_files_are_omitted(bool truncated)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"lt-bundle-validation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var image = Path.Combine(directory, "pretend-image.png");
        File.WriteAllBytes(image, truncated ? ImageBytes()[..24] : "synthetic non-image content"u8.ToArray());
        var project = SampleProjectBuilder.Build();
        var surface = project.Objects[0].Surfaces[0];
        var capture = surface.Captures[0] with { ImageLocation = image };
        var modified = project with
        {
            Objects = [project.Objects[0] with { Surfaces = [surface with { Captures = [capture] }] }],
        };
        try
        {
            var path = Path.Combine(directory, "bundle.zip");
            ProjectBundle.Export(modified, path, includeImages: true);
            using var archive = ZipFile.OpenRead(path);
            Assert.DoesNotContain(archive.Entries, e => e.FullName.StartsWith("images/", StringComparison.Ordinal));
            using var omissions = new StreamReader(archive.GetEntry("images-omitted.txt")!.Open());
            Assert.Contains(capture.Id.ToString(), omissions.ReadToEnd());
            Assert.NotNull(archive.GetEntry("project.json"));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static byte[] ImageBytes()
    {
        using var bitmap = new SKBitmap(12, 8);
        bitmap.Erase(SKColors.LightGray);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
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
            File.WriteAllText(path, "previous export");
            var result = ProjectBundle.Export(project, path);

            Assert.True(File.Exists(path));
            Assert.True(result.FileCount >= 7); // manifest, project, training + 4 per surface
            Assert.Equal(new FileInfo(path).Length, result.SizeBytes);

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
