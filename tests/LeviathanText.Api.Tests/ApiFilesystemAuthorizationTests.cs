using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using SkiaSharp;

namespace LeviathanText.Api.Tests;

public sealed class ApiFilesystemAuthorizationTests
{
    [Theory]
    [InlineData("import-image")]
    [InlineData("import-page")]
    [InlineData("import-pdf")]
    [InlineData("import-tei")]
    [InlineData("import-epidoc")]
    [InlineData("import-legacy")]
    [InlineData("import-iiif")]
    [InlineData("IMPORT-IMAGE/")]
    public async Task Server_path_imports_require_an_administrator(string route)
    {
        using var factory = new FilesystemFactory();
        using var admin = factory.Admin();
        var created = await admin.PostAsJsonAsync("/api/v1/projects", new { name = "path-boundary" });
        var project = await created.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        foreach (var role in new[] { "annotator", "reviewer" })
        {
            using var user = await factory.User(admin, role);
            var response = await user.PostAsJsonAsync($"/api/v1/projects/{project!["id"]}/{route}", new
            {
                imagePath = factory.ImagePath, pageXmlPath = factory.ImagePath,
                pdfPath = factory.ImagePath, documentPath = factory.ImagePath,
                manifestPath = factory.ImagePath, format = "alto",
            });
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        using var stored = JsonDocument.Parse(await admin.GetStringAsync($"/api/v1/projects/{project!["id"]}"));
        Assert.Empty(stored.RootElement.GetProperty("objects").EnumerateArray());
    }

    [Theory]
    [InlineData("annotator")]
    [InlineData("reviewer")]
    public async Task Directory_import_jobs_require_an_administrator(string role)
    {
        using var factory = new FilesystemFactory();
        using var admin = factory.Admin();
        using var user = await factory.User(admin, role);
        var created = await user.PostAsJsonAsync("/api/v1/projects", new { name = "job-boundary" });
        var project = await created.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        var request = new { type = "import.dir", ownerId = "fixture", projectId = project!["id"], sourceDirectory = factory.Root };

        Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsJsonAsync("/api/v1/jobs", request)).StatusCode);
        using var jobs = JsonDocument.Parse(await admin.GetStringAsync("/api/v1/jobs"));
        Assert.Empty(jobs.RootElement.EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/v1/jobs", request)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await user.PostAsJsonAsync("/api/v1/jobs", new { type = "noop", ownerId = "fixture" })).StatusCode);
    }

    [Fact]
    public async Task Administrator_image_import_remains_exportable_by_project_users()
    {
        using var factory = new FilesystemFactory();
        using var admin = factory.Admin();
        using var user = await factory.User(admin, "annotator");
        var created = await user.PostAsJsonAsync("/api/v1/projects", new { name = "valid-image" });
        var project = await created.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        var imported = await admin.PostAsJsonAsync($"/api/v1/projects/{project!["id"]}/import-image", new { imagePath = factory.ImagePath });
        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);

        var response = await user.GetAsync($"/api/v1/projects/{project["id"]}/bundle?images=true");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var bytes = new MemoryStream(await response.Content.ReadAsByteArrayAsync());
        using var archive = new ZipArchive(bytes);
        var entry = Assert.Single(archive.Entries, e => e.FullName.StartsWith("images/", StringComparison.Ordinal));
        using var content = entry.Open();
        using var copied = new MemoryStream();
        await content.CopyToAsync(copied);
        Assert.Equal(File.ReadAllBytes(factory.ImagePath), copied.ToArray());

        var html = await user.GetStringAsync($"/api/v1/projects/{project["id"]}/edition.html");
        Assert.DoesNotContain(factory.ImagePath, html);
        var encoded = System.Text.RegularExpressions.Regex.Match(html, "src=\"data:image/png;base64,([^\"]+)\"");
        Assert.True(encoded.Success, "Standalone editions must carry a portable image.");
        using var preview = SKBitmap.Decode(Convert.FromBase64String(encoded.Groups[1].Value));
        Assert.NotNull(preview);
        Assert.Equal(12, preview.Width);

        await Task.WhenAll(Enumerable.Range(0, 16).Select(async slot =>
        {
            bool withImages = slot % 2 == 0;
            string suffix = withImages ? "?images=true" : "";
            using var bundle = await user.GetAsync($"/api/v1/projects/{project["id"]}/bundle{suffix}");
            Assert.Equal(HttpStatusCode.OK, bundle.StatusCode);
            using var buffer = new MemoryStream(await bundle.Content.ReadAsByteArrayAsync());
            using var returned = new ZipArchive(buffer);
            Assert.Equal(withImages, returned.Entries.Any(e => e.FullName.StartsWith("images/", StringComparison.Ordinal)));
            using var manifestStream = returned.GetEntry("manifest.json")!.Open();
            using var manifest = JsonDocument.Parse(manifestStream);
            Assert.Equal(withImages, manifest.RootElement.GetProperty("imageFilesRequested").GetBoolean());
        }));
        Assert.Equal(8, preview.Height);
    }

    private sealed class FilesystemFactory : WebApplicationFactory<Program>
    {
        private const string AdminKey = "filesystem-fixture-admin-key";
        public string Root { get; } = Path.Combine(Path.GetTempPath(), $"lt-api-filesystem-{Guid.NewGuid():N}");
        public string ImagePath => Path.Combine(Root, "valid.png");

        public FilesystemFactory()
        {
            Directory.CreateDirectory(Root);
            using var bitmap = new SKBitmap(12, 8);
            bitmap.Erase(SKColors.White);
            using var image = SKImage.FromBitmap(bitmap);
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(ImagePath, encoded.ToArray());
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("LeviathanText:StorePath", Path.Combine(Root, "store.db"));
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["LeviathanText:Auth:ApiKey"] = AdminKey }));
        }

        public HttpClient Admin() => Client(AdminKey);
        private HttpClient Client(string key)
        {
            var client = CreateClient();
            client.DefaultRequestHeaders.Add("X-Api-Key", key);
            return client;
        }

        public async Task<HttpClient> User(HttpClient admin, string role)
        {
            var response = await admin.PostAsJsonAsync("/api/v1/users", new { name = role, role });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return Client(json.RootElement.GetProperty("apiKey").GetString()!);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
