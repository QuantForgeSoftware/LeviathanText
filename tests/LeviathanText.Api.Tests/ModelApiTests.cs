using System.Net;
using System.Net.Http.Json;
using LeviathanText.Contracts;
using LeviathanText.Recognition;
using LeviathanText.Training;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using SkiaSharp;
using Xunit;

namespace LeviathanText.Api.Tests;

/// <summary>
/// Model discovery + inline recognition from the API: clients name a package under the
/// configured directory (never a path); L0 revisions are persisted with provenance.
/// </summary>
public sealed class ModelApiTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"lt-models-{Guid.NewGuid():N}");
    private readonly string _package;

    public ModelApiTests()
    {
        _package = Path.Combine(_root, "tiny");
        Directory.CreateDirectory(_package);
        var labels = new LabelSet(["α", "β"]);
        var model = new CrnnModel(labels.NumClasses, hiddenSize: 8);
        model.save(CheckpointStore.ModelPath(_package, "tiny", ".best"));
        CheckpointStore.WriteMetadata(_package, "tiny", new CheckpointMetadata
        {
            Epoch = 1,
            GlobalStep = 1,
            BestValidationCer = 1.0,
            LabelSetHash = CheckpointStore.LabelSetHash(labels),
            Labels = labels.Labels,
            ConfigHash = "test",
            HiddenSize = 8,
            InputHeightPixels = CrnnModel.InputHeight,
            WidthDownsampleFactor = CrnnModel.DefaultWidthDownsampleFactor,
            TorchVersion = "test",
            CreatedAt = DateTimeOffset.UtcNow,
        }, ".best");
    }

    private sealed class ModelFactory(string modelsDirectory) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("LeviathanText:StorePath",
                Path.Combine(Path.GetTempPath(), $"leviathan-models-{Guid.NewGuid():N}.db"));
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["LeviathanText:Models:Directory"] = modelsDirectory,
                });
            });
        }
    }

    [Fact]
    public async Task Lists_packages_and_recognizes_an_image_only_surface()
    {
        using var factory = new ModelFactory(_root);
        var client = factory.CreateClient();

        var models = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/v1/models");
        Assert.Contains("tiny", models.GetRawText());

        var project = await (await client.PostAsJsonAsync("/api/v1/projects", new { name = "model-api" }))
            .Content.ReadFromJsonAsync<Dictionary<string, string>>();

        var imagePath = Path.Combine(_root, "page.png");
        using (var surface = SKSurface.Create(new SKImageInfo(200, 120)))
        {
            surface.Canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.Black };
            surface.Canvas.DrawRect(new SKRect(10, 30, 190, 60), paint);
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.OpenWrite(imagePath);
            data.SaveTo(stream);
        }

        var imported = await client.PostAsJsonAsync($"/api/v1/projects/{project!["id"]}/import-image",
            new { imagePath, kind = "page" });
        using var importedJson = System.Text.Json.JsonDocument.Parse(await imported.Content.ReadAsStringAsync());
        var surfaceId = importedJson.RootElement.GetProperty("surfaceId").GetString()!;

        // Draw a line manually so recognition has a target.
        var added = await client.PostAsJsonAsync($"/api/v1/surfaces/{surfaceId}/lines", new
        {
            polygon = new[]
            {
                new { x = 5.0, y = 20.0 }, new { x = 195.0, y = 20.0 },
                new { x = 195.0, y = 70.0 }, new { x = 5.0, y = 70.0 },
            },
            baseline = new[] { new { x = 5.0, y = 68.0 }, new { x = 195.0, y = 68.0 } },
        });
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);

        var recognized = await client.PostAsJsonAsync($"/api/v1/surfaces/{surfaceId}/recognize", new { model = "tiny" });
        Assert.Equal(HttpStatusCode.OK, recognized.StatusCode);
        var body = await recognized.Content.ReadAsStringAsync();
        Assert.Contains("\"lines\":1", body);

        // L0 exists and is attributed to a model.
        using var projectJson = System.Text.Json.JsonDocument.Parse(
            await client.GetStringAsync($"/api/v1/projects/{project["id"]}"));
        var lineId = projectJson.RootElement.GetProperty("objects")[0].GetProperty("surfaces")[0]
            .GetProperty("lines")[0].GetProperty("id").GetString()!;
        var revisions = await client.GetStringAsync($"/api/v1/lines/{lineId}/revisions?layer=L0RawRecognition");
        Assert.Contains("L0RawRecognition", revisions);
        Assert.Contains("model:tiny", revisions);
        Assert.Contains("\"graphemeConfidences\"", await client.GetStringAsync($"/api/v1/lines/{lineId}"));

        var queue = await client.GetStringAsync($"/api/v1/projects/{project["id"]}/review-queue");
        Assert.Contains(lineId, queue);

        // Path traversal and unknown packages are refused.
        var traversal = await client.PostAsJsonAsync($"/api/v1/surfaces/{surfaceId}/recognize", new { model = "../tiny" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, traversal.StatusCode);
        var unknown = await client.PostAsJsonAsync($"/api/v1/surfaces/{surfaceId}/recognize", new { model = "missing" });
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
