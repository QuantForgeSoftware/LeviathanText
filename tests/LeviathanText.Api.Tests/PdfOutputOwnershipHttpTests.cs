using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SkiaSharp;

namespace LeviathanText.Api.Tests;

/// <summary>Generated PDF-only capture ownership: a refused import never rewrites prior bytes or native state.</summary>
public sealed class PdfOutputOwnershipHttpTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static void WritePdf(string path, int pages, SKColor color)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var document = SKDocument.CreatePdf(path);
        for (int page = 0; page < pages; page++)
        {
            using var canvas = document.BeginPage(100, 80);
            canvas.Clear(color);
            document.EndPage();
        }
        document.Close();
    }
    private static async Task<string> Create(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/projects", new { name = "Generated PDF ownership" });
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetString()!;
    }
    private static async Task Refusal(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("pdf-output-exists", json.RootElement.GetProperty("details").GetProperty("reason").GetString());
    }
    [Fact]
    public async Task Same_stem_import_cannot_replace_an_existing_capture_or_append_to_its_project()
    {
        var root = Path.Combine(Path.GetTempPath(), "leviathan-pdf-owner-http-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var original = Path.Combine(root, "original", "generated.pdf");
            var incoming = Path.Combine(root, "incoming", "generated.pdf");
            var output = Path.Combine(root, "captures");
            WritePdf(original, 1, SKColors.White); WritePdf(incoming, 1, SKColors.Black);
            using var client = factory.CreateClient(); var id = await Create(client);
            using var initial = await client.PostAsJsonAsync($"/api/v1/projects/{id}/import-pdf",
                new { pdfPath = original, outputDirectory = output, dpi = 72 });
            initial.EnsureSuccessStatusCode();
            var capture = Assert.Single(Directory.GetFiles(output, "*.png"));
            var pixels = File.ReadAllBytes(capture);
            var native = await client.GetStringAsync($"/api/v1/projects/{id}");
            using var refused = await client.PostAsJsonAsync($"/api/v1/projects/{id}/import-pdf",
                new { pdfPath = incoming, outputDirectory = output, dpi = 72 });
            Assert.Equal(pixels, File.ReadAllBytes(capture));
            await Refusal(refused);
            Assert.Equal(native, await client.GetStringAsync($"/api/v1/projects/{id}"));
            Assert.Single(Directory.GetFiles(output, "*.png"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
    [Fact]
    public async Task Later_page_collision_refuses_before_any_new_capture_or_project_write()
    {
        var root = Path.Combine(Path.GetTempPath(), "leviathan-pdf-page-http-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var pdf = Path.Combine(root, "generated.pdf"); var output = Path.Combine(root, "captures");
            WritePdf(pdf, 2, SKColors.White); Directory.CreateDirectory(output);
            var existing = Path.Combine(output, "generated-page-0002.png");
            byte[] sentinel = [0, 255, 17, 34, 0, 99]; File.WriteAllBytes(existing, sentinel);
            using var client = factory.CreateClient(); var id = await Create(client);
            var native = await client.GetStringAsync($"/api/v1/projects/{id}");
            using var refused = await client.PostAsJsonAsync($"/api/v1/projects/{id}/import-pdf",
                new { pdfPath = pdf, outputDirectory = output, dpi = 72 });
            Assert.Equal(sentinel, File.ReadAllBytes(existing));
            await Refusal(refused);
            Assert.Equal(existing, Assert.Single(Directory.GetFiles(output)));
            Assert.Equal(native, await client.GetStringAsync($"/api/v1/projects/{id}"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
