using System.Net;
using System.Net.Http.Json;
using SkiaSharp;
using Xunit;

namespace LeviathanText.Api.Tests;

public sealed class PdfImportApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public PdfImportApiTests(ApiFactory factory) => _factory = factory;

    private static string MakePdf()
    {
        var path = Path.Combine(Path.GetTempPath(), $"leviathan-api-pdf-{Guid.NewGuid():N}.pdf");
        using var document = SKDocument.CreatePdf(path);
        for (int i = 0; i < 2; i++)
        {
            using var canvas = document.BeginPage(180, 120);
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.Black, StrokeWidth = 2 };
            canvas.DrawLine(10, 20, 170, 20, paint);
            canvas.DrawText($"page {i + 1}", 20, 70, SKTextAlign.Left,
                new SKFont(SKTypeface.FromFamilyName("serif"), 18), paint);
            document.EndPage();
        }
        document.Close();
        return path;
    }

    [Fact]
    public async Task Pdf_import_creates_one_capture_surface_per_page()
    {
        var client = _factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/v1/projects", new { name = $"pdf-api-{Guid.NewGuid():N}" });
        var project = await created.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        var pdf = MakePdf();
        var output = Path.Combine(Path.GetTempPath(), $"leviathan-api-pdf-out-{Guid.NewGuid():N}");

        var response = await client.PostAsJsonAsync($"/api/v1/projects/{project!["id"]}/import-pdf",
            new { pdfPath = pdf, outputDirectory = output, dpi = 72 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, json.RootElement.GetProperty("pageCount").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("surfaceIds").GetArrayLength());
        Assert.True(Directory.GetFiles(output, "*.png").Length == 2);

        var projectJson = await client.GetStringAsync($"/api/v1/projects/{project["id"]}");
        Assert.Contains("\"pdf:pages\":\"2\"", projectJson);
    }

    [Fact]
    public async Task Missing_pdf_path_is_a_client_error()
    {
        var client = _factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/v1/projects", new { name = $"pdf-missing-{Guid.NewGuid():N}" });
        var project = await created.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        var response = await client.PostAsJsonAsync($"/api/v1/projects/{project!["id"]}/import-pdf",
            new { pdfPath = "/definitely/missing.pdf" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
