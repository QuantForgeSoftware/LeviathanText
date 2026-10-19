using System.Net;
using System.Net.Http.Json;
using LeviathanText.Contracts;
using LeviathanText.Layout;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using SkiaSharp;
using Xunit;

namespace LeviathanText.Api.Tests;

/// <summary>
/// Per-material layout model selection from configuration: a package registered for the
/// surface kind is used by the detect endpoint; unknown kinds fall back to projection.
/// </summary>
public sealed class LayoutModelApiTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"lt-layout-api-{Guid.NewGuid():N}");
    private readonly string _package;

    public LayoutModelApiTests()
    {
        Directory.CreateDirectory(_root);
        _package = Path.Combine(_root, "codex");
        var pages = new List<LayoutTrainingPage>();
        for (int variant = 0; variant < 3; variant++)
        {
            var bands = new (int, int)[] { (30 + variant, 60 + variant), (140, 175) };
            pages.Add(new LayoutTrainingPage($"p{variant}", WritePage($"p{variant}", bands), bands));
        }
        PatchLayoutModel.Train(pages, new PatchLayoutOptions
        {
            Epochs = 30, BatchSize = 2, InputHeight = 128, MaxInputWidth = 160, Seed = 9,
        }).Save(_package);
    }

    private string WritePage(string name, params (int Top, int Bottom)[] bands)
    {
        var path = Path.Combine(_root, name + ".png");
        using var bitmap = new SKBitmap(240, 300);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.Black };
            foreach (var (top, bottom) in bands)
                canvas.DrawRect(new SKRect(20, top, 220, bottom), paint);
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);
        return path;
    }

    private sealed class LayoutFactory(string modelsRoot) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("LeviathanText:StorePath",
                Path.Combine(Path.GetTempPath(), $"leviathan-layout-{Guid.NewGuid():N}.db"));
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["LeviathanText:Layout:Models:codex-page"] = modelsRoot,
                });
            });
        }
    }

    [Fact]
    public async Task Configured_model_runs_for_its_surface_kind()
    {
        using var factory = new LayoutFactory(_package);
        var client = factory.CreateClient();

        var project = await (await client.PostAsJsonAsync("/api/v1/projects", new { name = "learned-layout-api" }))
            .Content.ReadFromJsonAsync<Dictionary<string, string>>();
        var image = WritePage("target", (50, 85), (180, 215));
        var imported = await client.PostAsJsonAsync($"/api/v1/projects/{project!["id"]}/import-image",
            new { imagePath = image, kind = "codex-page" });
        using var importedJson = System.Text.Json.JsonDocument.Parse(await imported.Content.ReadAsStringAsync());
        var surfaceId = importedJson.RootElement.GetProperty("surfaceId").GetString()!;

        var detected = await client.PostAsJsonAsync($"/api/v1/surfaces/{surfaceId}/detect-layout", new { });
        Assert.Equal(HttpStatusCode.OK, detected.StatusCode);
        var body = await detected.Content.ReadAsStringAsync();
        Assert.Contains("patch:", body);

        // A kind without a configured model keeps the projection detector.
        var other = WritePage("other", (50, 85));
        var otherImport = await client.PostAsJsonAsync($"/api/v1/projects/{project["id"]}/import-image",
            new { imagePath = other, kind = "papyrus-side" });
        using var otherJson = System.Text.Json.JsonDocument.Parse(await otherImport.Content.ReadAsStringAsync());
        var otherSurface = otherJson.RootElement.GetProperty("surfaceId").GetString()!;
        var otherDetected = await client.PostAsJsonAsync($"/api/v1/surfaces/{otherSurface}/detect-layout", new { });
        Assert.Contains("ProjectionProfileLayout", await otherDetected.Content.ReadAsStringAsync());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
