using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LeviathanText.Recognition;
using LeviathanText.Training;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using SkiaSharp;

namespace LeviathanText.Api.Tests;

public sealed class PreprocessingApiTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lt-preprocessing-api", Guid.NewGuid().ToString("N"));

    public PreprocessingApiTests() => Directory.CreateDirectory(_root);

    private sealed class Factory(string root) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("LeviathanText:StorePath", Path.Combine(root, "store.db"));
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LeviathanText:Models:Directory"] = root,
            }));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Surface_recognition_uses_declared_height_and_reports_external_geometry_as_422(bool external)
    {
        string package = Path.Combine(_root, "fixture");
        Directory.CreateDirectory(package);
        var profile = LinePreprocessingProfile.Luminance(32) with { Geometry = external ? "externally-prepared-line" : "line-crop" };
        var labels = new LabelSet(["α", "β"]);
        using (var model = new CrnnModel(labels.NumClasses, hiddenSize: 8, inputHeight: 32))
            model.save(CheckpointStore.ModelPath(package, "fixture", ".best"));
        CheckpointStore.WriteMetadata(package, "fixture", new CheckpointMetadata
        {
            Epoch = 1, GlobalStep = 1, BestValidationCer = 1, LabelSetHash = CheckpointStore.LabelSetHash(labels),
            Labels = labels.Labels, ConfigHash = "fixture", HiddenSize = 8, InputHeightPixels = 32,
            WidthDownsampleFactor = 4, TorchVersion = "fixture", CreatedAt = DateTimeOffset.UtcNow, PreprocessingProfile = profile,
        }, ".best");
        string image = Path.Combine(_root, "page.png");
        using (var bitmap = new SKBitmap(80, 48))
        {
            bitmap.Erase(SKColors.White);
            using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.Create(image);
            data.SaveTo(stream);
        }
        using var factory = new Factory(_root);
        using var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/v1/projects", new { name = "preprocessing" });
        created.EnsureSuccessStatusCode();
        var project = (await created.Content.ReadFromJsonAsync<Dictionary<string, string>>())!;
        var imported = await client.PostAsJsonAsync($"/api/v1/projects/{project["id"]}/import-image", new { imagePath = image, kind = "page" });
        imported.EnsureSuccessStatusCode();
        using var importedJson = JsonDocument.Parse(await imported.Content.ReadAsStringAsync());
        string surfaceId = importedJson.RootElement.GetProperty("surfaceId").GetString()!;
        var added = await client.PostAsJsonAsync($"/api/v1/surfaces/{surfaceId}/lines", new
        {
            polygon = new[] { new { x = 8.0, y = 8.0 }, new { x = 72.0, y = 8.0 }, new { x = 72.0, y = 40.0 }, new { x = 8.0, y = 40.0 } },
            baseline = new[] { new { x = 8.0, y = 38.0 }, new { x = 72.0, y = 38.0 } },
        });
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var response = await client.PostAsJsonAsync($"/api/v1/surfaces/{surfaceId}/recognize", new { model = "fixture" });
        Assert.Equal(external ? HttpStatusCode.UnprocessableEntity : HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains(external ? "modelIncompatible" : "\"lines\":1", body);
        if (external)
        {
            using var persisted = JsonDocument.Parse(await client.GetStringAsync($"/api/v1/projects/{project["id"]}"));
            Assert.Empty(persisted.RootElement.GetProperty("objects")[0].GetProperty("surfaces")[0].GetProperty("revisions").EnumerateArray());
        }
    }

    public void Dispose() => Directory.Delete(_root, true);
}
