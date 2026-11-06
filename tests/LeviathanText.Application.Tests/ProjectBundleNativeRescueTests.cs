using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using SkiaSharp;

namespace LeviathanText.Application.Tests;

public sealed class ProjectBundleNativeRescueTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("lt-bundle-rescue-").FullName;
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private (NativeProject Project, string[] Paths, byte[][] Bytes) Project(bool duplicateSurfaces, bool duplicateCaptures)
    {
        var paths = new[] { Path.Combine(root, "first.png"), Path.Combine(root, "second.png") };
        var bytes = new byte[2][];
        for (int index = 0; index < 2; index++)
        {
            using var bitmap = new SKBitmap(32, 24); bitmap.Erase(index == 0 ? SKColors.White : SKColors.Black);
            using var png = bitmap.Encode(SKEncodedImageFormat.Png, 100); bytes[index] = png.ToArray();
            File.WriteAllBytes(paths[index], bytes[index]);
        }
        var surfaces = Enumerable.Range(0, 2).Select(index =>
        {
            var capture = new EntityId(EntityKind.Capture, duplicateCaptures ? "shared" : "capture-" + index);
            return new NativeSurface
            {
                Id = new(EntityKind.Surface, duplicateSurfaces ? "shared" : "surface-" + index), Kind = "page", Regions = [], Lines = [],
                Captures = [new NativeCapture
                {
                    Id = capture, ImageLocation = paths[index], WidthPx = 32, HeightPx = 24,
                    Frame = new CoordinateFrame { ImageRef = capture, ImageChecksum = new Checksum { Algorithm = "sha256", Digest = Hash(bytes[index]) } },
                }],
            };
        }).ToArray();
        return (new NativeProject
        {
            Id = new(EntityKind.Collection, "generated-rescue"), Name = "Generated ambiguous bundle", Version = 7,
            Objects = [new NativeObject { Id = new(EntityKind.PhysicalObject, "generated"), Surfaces = surfaces }],
        }, paths, bytes);
    }
    private static string Read(ZipArchive archive, string name)
    { using var reader = new StreamReader(archive.GetEntry(name)!.Open()); return reader.ReadToEnd(); }
    private static byte[] Bytes(ZipArchiveEntry entry)
    { using var source = entry.Open(); using var bytes = new MemoryStream(); source.CopyTo(bytes); return bytes.ToArray(); }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Ambiguous_surface_or_capture_identity_retains_native_without_derived_first_match_or_duplicate_paths(bool duplicateSurfaces, bool includeImages)
    {
        var source = Project(duplicateSurfaces, duplicateCaptures: !duplicateSurfaces);
        var expectedNative = NativeJson.Save(source.Project); var target = Path.Combine(root, "rescue.zip");
        var result = ProjectBundle.Export(source.Project, target, includeImages);
        using var archive = ZipFile.OpenRead(target); using var manifest = JsonDocument.Parse(Read(archive, "manifest.json"));
        // On the preserved baseline this records actual first-image reuse or duplicate ZIP names,
        // not merely absence of the new mode field. Generated source/embedded hashes are retained in the failure.
        var images = archive.Entries.Where(entry => entry.FullName.StartsWith("images/", StringComparison.Ordinal))
            .Select(entry => new { name = entry.FullName, sha256 = Hash(Bytes(entry)) }).ToArray();
        var duplicateNames = archive.Entries.GroupBy(entry => entry.FullName, StringComparer.Ordinal)
            .Where(group => group.Count() > 1).Select(group => new { name = group.Key, count = group.Count() }).ToArray();
        var imageReuse = archive.Entries.Where(entry => entry.FullName.StartsWith("surfaces/", StringComparison.Ordinal) && entry.FullName.EndsWith("/edition.html", StringComparison.Ordinal))
            .Select(entry => new { name = entry.FullName, sharedImageReference = System.Text.Encoding.UTF8.GetString(Bytes(entry)).Contains("images/cap-shared.png", StringComparison.Ordinal) }).ToArray();
        var observed = JsonSerializer.Serialize(new { sourceSha256 = source.Bytes.Select(Hash).ToArray(), images, duplicateNames, imageReuse });
        Assert.True(manifest.RootElement.TryGetProperty("exportMode", out var mode) && mode.GetString() == "native-rescue", observed);
        Assert.Equal(expectedNative, Read(archive, "project.json"));
        Assert.Equal(4, result.FileCount); Assert.Equal(4, archive.Entries.Count);
        Assert.Equal(new[] { "exports-omitted.json", "integrity.json", "manifest.json", "project.json" }, archive.Entries.Select(entry => entry.FullName).Order(StringComparer.Ordinal));
        Assert.Empty(duplicateNames); Assert.Empty(images);
        Assert.Equal("leviathantext-bundle", manifest.RootElement.GetProperty("format").GetString());
        Assert.Equal(includeImages, manifest.RootElement.GetProperty("imageFilesRequested").GetBoolean());
        Assert.Equal(0, manifest.RootElement.GetProperty("imageFilesIncluded").GetInt32());
        Assert.Equal(2, manifest.RootElement.GetProperty("surfaces").GetArrayLength());
        var reason = Assert.Single(manifest.RootElement.GetProperty("exportReasons").EnumerateArray());
        Assert.Equal(duplicateSurfaces ? "surface-id" : "capture-id", reason.GetProperty("kind").GetString());
        Assert.Equal(2, reason.GetProperty("count").GetInt32());
        Assert.Contains("duplicate-id", Read(archive, "integrity.json"), StringComparison.Ordinal);
        Assert.Contains("All convenience exports", Read(archive, "exports-omitted.json"), StringComparison.Ordinal);
        Assert.Equal(expectedNative, NativeJson.Save(source.Project));
        for (int index = 0; index < 2; index++) Assert.Equal(source.Bytes[index], File.ReadAllBytes(source.Paths[index]));
    }

    [Fact]
    public void Unique_identity_keeps_the_existing_full_bundle_contract()
    {
        var source = Project(duplicateSurfaces: false, duplicateCaptures: false); var target = Path.Combine(root, "normal.zip");
        ProjectBundle.Export(source.Project, target, includeImages: true);
        using var archive = ZipFile.OpenRead(target); using var manifest = JsonDocument.Parse(Read(archive, "manifest.json"));
        Assert.False(manifest.RootElement.TryGetProperty("exportMode", out _));
        Assert.Equal(2, manifest.RootElement.GetProperty("imageFilesIncluded").GetInt32());
        Assert.Equal(NativeJson.Save(source.Project), Read(archive, "project.json"));
        Assert.Contains(archive.Entries, entry => entry.FullName == "training-export.json");
        Assert.Contains(archive.Entries, entry => entry.FullName == "edition.html");
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName == "exports-omitted.json");
    }
    public void Dispose() => Directory.Delete(root, recursive: true);
}
