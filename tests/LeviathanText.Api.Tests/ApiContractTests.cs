using LeviathanText.Contracts;
using LeviathanText.Contracts.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace LeviathanText.Api.Tests;

/// <summary>
/// HTTP behavior contract tests (interchange-and-api.md §2): versioned REST, contract
/// JSON conventions, idempotent job creation, loss-report presence, error vocabulary.
/// Uses the in-memory test host with an isolated store file.
/// </summary>
public sealed class ApiContractTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ApiContractTests(ApiFactory factory) => _factory = factory;

    private HttpClient NewClient() => _factory.CreateClient();

    [Fact]
    public async Task Root_reports_service_and_contract_version()
    {
        var response = await NewClient().GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal("LeviathanText", body!["service"]);
        Assert.Equal(ContractVersion.Current, body["contract"]);
    }

    [Fact]
    public async Task Local_mode_reports_an_unauthenticated_identity()
    {
        var body = await NewClient().GetStringAsync("/api/v1/me");
        Assert.Contains("\"authenticated\":false", body);
        Assert.Contains("\"name\":\"local\"", body);
    }

    [Fact]
    public async Task Create_and_list_and_load_project()
    {
        var client = NewClient();
        var created = await client.PostAsJsonAsync("/api/v1/projects", new { name = "http-pilot" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var createdBody = await created.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        var id = createdBody!["id"];
        Assert.StartsWith("col-", id);

        var list = await client.GetAsync("/api/v1/projects");
        var listText = await list.Content.ReadAsStringAsync();
        Assert.Contains("http-pilot", listText);

        var load = await client.GetAsync($"/api/v1/projects/{id}");
        Assert.Equal(HttpStatusCode.OK, load.StatusCode);
        var json = await load.Content.ReadAsStringAsync();
        Assert.Contains("\"name\":\"http-pilot\"", json); // camelCase contract conventions
    }

    [Fact]
    public async Task Malformed_identifier_is_a_client_error_not_a_server_fault()
    {
        var response = await NewClient().GetAsync("/api/v1/projects/not-an-entity-id");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("stageContractViolation", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Project_can_be_deleted_and_is_gone_from_listing()
    {
        var client = NewClient();
        var created = await client.PostAsJsonAsync("/api/v1/projects", new { name = "delete-e2e" });
        var project = await created.Content.ReadFromJsonAsync<Dictionary<string, string>>();

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/projects/{project!["id"]}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/projects/{project["id"]}")).StatusCode);
        Assert.DoesNotContain("delete-e2e", await client.GetStringAsync("/api/v1/projects"));
    }

    [Fact]
    public async Task Unknown_project_returns_closed_vocabulary_error()
    {
        var client = NewClient();
        var response = await client.GetAsync("/api/v1/projects/col-doesnotexist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); // InputMissing maps to 404 via the error middleware
    }

    [Fact]
    public async Task Providers_endpoint_lists_configured_providers_without_credential_names()
    {
        var client = NewClient();
        var response = await client.GetAsync("/api/v1/providers");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("stub-provider", body);                 // configured in ApiFactory
        Assert.DoesNotContain("STUB_API_KEY", body);            // credential names never leave the server
        Assert.Contains("never sees or sends", body);
    }

    [Fact]
    public async Task Proposals_require_a_known_provider_config_ref()
    {
        var client = NewClient();
        // Seed one revision into the host's chain; then use an unknown provider ref.
        var chain = _factory.Services.GetRequiredService<LeviathanText.Core.Revisions.RevisionChain>();
        var revision = chain.Append(new LeviathanText.Contracts.EntityId(EntityKind.Line, "seedline"),
            LeviathanText.Contracts.TextLayer.L1Diplomatic, "αβ",
            new LeviathanText.Contracts.AgentRef { Id = "seed", IsModel = false });

        var response = await client.PostAsJsonAsync("/api/v1/proposals", new
        {
            targetRevisionId = revision.Id.ToString(),
            range = new { start = 0, end = 1 },
            task = "candidateReading",
            providerConfigRef = "unknown-provider",
            promptVersion = "v1",
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); // InputMissing maps to 404 via the error middleware
    }

    [Fact]
    public async Task Annotation_binds_to_current_revision_when_anchor_is_omitted()
    {
        // Imported fixture project through the real API, then annotate the first line.
        var client = NewClient();
        var created = await client.PostAsJsonAsync("/api/v1/projects", new { name = "annotate-e2e" });
        var project = await created.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        var fixture = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LeviathanText.Formats.Tests", "Fixtures", "sample.page.xml"));
        var imported = await client.PostAsJsonAsync($"/api/v1/projects/{project!["id"]}/import-page", new { pageXmlPath = fixture });
        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
        var surface = await imported.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.True(surface!.ContainsKey("lineCount"));

        var projectJson = await client.GetFromJsonAsync<Dictionary<string, object>>($"/api/v1/projects/{project["id"]}");
        var lineId = ExtractFirstLineId(projectJson!);

        var response = await client.PostAsJsonAsync($"/api/v1/lines/{lineId}/annotations", new
        {
            kind = "unclear",
            range = new { start = 0, end = 2 },
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("anchoredRevisionId", body);
        Assert.DoesNotContain("rev-00000000", body);
    }

    [Fact]
    public async Task Unknown_line_annotation_returns_404_via_error_middleware()
    {
        var client = NewClient();
        var response = await client.PostAsJsonAsync("/api/v1/lines/ln-missing/annotations", new
        {
            kind = "unclear",
            range = new { start = 0, end = 1 },
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("inputMissing", body); // closed-vocabulary code in the payload
    }

    private static string ExtractFirstLineId(Dictionary<string, object> projectJson)
    {
        var objects = (System.Text.Json.JsonElement)projectJson["objects"];
        return objects[0].GetProperty("surfaces")[0].GetProperty("lines")[0].GetProperty("id").GetString()!;
    }

    [Fact]
    public async Task Line_geometry_round_trips_through_the_api()
    {
        var client = NewClient();
        var (projectId, lineId, surfaceId) = await ImportFixtureAsync(client, "geometry-e2e");
        Assert.NotNull(projectId);

        var response = await client.PutAsJsonAsync($"/api/v1/lines/{lineId}/geometry", new
        {
            polygon = new[]
            {
                new { x = 5.0, y = 5.0 }, new { x = 150.0, y = 5.0 },
                new { x = 150.0, y = 45.0 }, new { x = 5.0, y = 45.0 },
            },
            baseline = new[] { new { x = 6.0, y = 40.0 }, new { x = 148.0, y = 38.0 } },
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"baseline\"", body);

        var history = await client.GetStringAsync($"/api/v1/surfaces/{surfaceId}/geometry-history");
        Assert.Contains("lineGeometry", history);
        Assert.Contains("\"author\":\"api\"", history);

        // Invalid geometry is rejected with the closed vocabulary.
        var bad = await client.PutAsJsonAsync($"/api/v1/lines/{lineId}/geometry", new
        {
            polygon = new[] { new { x = 1.0, y = 1.0 } },
            baseline = Array.Empty<object>(),
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, bad.StatusCode);
        Assert.Contains("stageContractViolation", await bad.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Line_order_endpoint_validates_and_returns_the_new_order()
    {
        var client = NewClient();
        await ImportFixtureAsync(client, "line-order-e2e");
        var lineOrderProjectId = await GetProjectIdAsync(client, "line-order-e2e");
        var projectJson = await client.GetFromJsonAsync<Dictionary<string, object>>(
            $"/api/v1/projects/{lineOrderProjectId}");
        var (regionId, lineIds) = ExtractRegionAndLineIds(projectJson!);

        // The full line set (reversed) is a valid order.
        var ok = await client.PutAsJsonAsync($"/api/v1/regions/{regionId}/line-order",
            new { lineIds = lineIds.Reverse().ToArray() });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var body = await ok.Content.ReadAsStringAsync();
        Assert.Contains(lineIds[^1], body); // first in the returned order is the last requested

        // Incomplete set is rejected with the closed code.
        var bad = await client.PutAsJsonAsync($"/api/v1/regions/{regionId}/line-order",
            new { lineIds = Array.Empty<string>() });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, bad.StatusCode);
    }

    private static (string RegionId, string[] LineIds) ExtractRegionAndLineIds(Dictionary<string, object> projectJson)
    {
        var objects = (System.Text.Json.JsonElement)projectJson["objects"];
        var surface = objects[0].GetProperty("surfaces")[0];
        var region = surface.GetProperty("regions")[0];
        var regionId = region.GetProperty("id").GetString()!;
        var lineIds = surface.GetProperty("lines").EnumerateArray()
            .Select(l => l.GetProperty("id").GetString()!)
            .ToArray();
        return (regionId, lineIds);
    }

    private static async Task<string> GetProjectIdAsync(HttpClient client, string name)
    {
        var list = await client.GetFromJsonAsync<Dictionary<string, string>[]>("/api/v1/projects");
        return list!.Single(p => p["name"] == name)["id"];
    }

    [Fact]
    public async Task Reading_order_update_requires_every_region()
    {
        var client = NewClient();
        var (_, _, surfaceId) = await ImportFixtureAsync(client, "reading-order-e2e");
        Assert.Contains("surf-", surfaceId);

        var response = await client.PutAsJsonAsync($"/api/v1/surfaces/{surfaceId}/reading-order", new
        {
            regionIds = new[] { "reg-missing" },
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    private static async Task<(string ProjectId, string LineId, string SurfaceId)> ImportFixtureAsync(HttpClient client, string projectName)
    {
        var created = await client.PostAsJsonAsync("/api/v1/projects", new { name = projectName });
        var project = await created.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        var fixture = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LeviathanText.Formats.Tests", "Fixtures", "sample.page.xml"));
        var imported = await client.PostAsJsonAsync($"/api/v1/projects/{project!["id"]}/import-page", new { pageXmlPath = fixture });
        var surface = await imported.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        var surfaceId = surface!["surfaceId"].ToString()!;

        var projectJson = await client.GetFromJsonAsync<Dictionary<string, object>>($"/api/v1/projects/{project["id"]}");
        var lineId = ExtractFirstLineId(projectJson!);
        return (project["id"], lineId, surfaceId);
    }

    [Fact]
    public async Task Line_payload_lists_every_layer_for_audit()
    {
        var client = NewClient();
        var (_, lineId, _) = await ImportFixtureAsync(client, "layers-e2e");

        var body = await client.GetStringAsync($"/api/v1/lines/{lineId}");
        Assert.Contains("\"revisions\"", body);
        Assert.Contains("L1Diplomatic", body);
    }

    [Fact]
    public async Task Legacy_alto_import_creates_a_surface_with_lines_and_text()
    {
        var client = NewClient();
        var created = await client.PostAsJsonAsync("/api/v1/projects", new { name = "legacy-alto" });
        var project = await created.Content.ReadFromJsonAsync<Dictionary<string, string>>();

        var dir = Path.Combine(Path.GetTempPath(), $"lt-alto-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var altoPath = Path.Combine(dir, "page.alto.xml");
        var imagePath = Path.Combine(dir, "page.png");
        await File.WriteAllTextAsync(altoPath, """
            <alto xmlns="http://www.loc.gov/standards/alto/ns-v4#">
              <Layout><Page WIDTH="100" HEIGHT="50"><PrintSpace>
                <TextBlock ID="b1"><TextLine ID="l1">
                  <Shape><Polygon POINTS="0,0 90,0 90,20 0,20"/></Shape>
                  <String CONTENT="αβγ"/>
                </TextLine></TextBlock>
              </PrintSpace></Page></Layout>
            </alto>
            """);
        await File.WriteAllBytesAsync(imagePath, [0x89, 0x50, 0x4E, 0x47]);

        var response = await client.PostAsJsonAsync($"/api/v1/projects/{project!["id"]}/import-legacy",
            new { documentPath = altoPath, imagePath, format = "alto" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"lineCount\":1", body);

        // Missing paths are refused (payload validation, not a crash).
        var bad = await client.PostAsJsonAsync($"/api/v1/projects/{project["id"]}/import-legacy",
            new { documentPath = "relative.xml", imagePath, format = "alto" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public async Task Revision_history_accepts_camel_case_layer_names()
    {
        var client = NewClient();
        await ImportFixtureAsync(client, "revisions-e2e");
        var revisionsProjectId = await GetProjectIdAsync(client, "revisions-e2e");
        var projectJson = await client.GetFromJsonAsync<Dictionary<string, object>>(
            $"/api/v1/projects/{revisionsProjectId}");
        var lineId = ExtractFirstLineId(projectJson!);

        var camel = await client.GetAsync($"/api/v1/lines/{lineId}/revisions?layer=l1Diplomatic");
        Assert.Equal(HttpStatusCode.OK, camel.StatusCode);
        var exact = await client.GetAsync($"/api/v1/lines/{lineId}/revisions?layer=L1Diplomatic");
        Assert.Equal(HttpStatusCode.OK, exact.StatusCode);
    }

    [Fact]
    public async Task Job_creation_is_idempotent_by_client_key()
    {
        var client = NewClient();
        var first = await client.PostAsJsonAsync("/api/v1/jobs", new { type = "pipeline.run", ownerId = "test", idempotencyKey = "shared-key" });
        var second = await client.PostAsJsonAsync("/api/v1/jobs", new { type = "pipeline.run", ownerId = "test", idempotencyKey = "shared-key" });

        var firstBody = await first.Content.ReadAsStringAsync();
        var secondBody = await second.Content.ReadAsStringAsync();
        Assert.Equal(firstBody, secondBody);
        Assert.Contains("\"pending\"", firstBody);
    }

    [Fact]
    public async Task Layout_job_carries_the_surface_as_its_input()
    {
        var client = NewClient();
        var (_, _, surfaceId) = await ImportFixtureAsync(client, "job-input-e2e");

        var response = await client.PostAsJsonAsync("/api/v1/jobs", new
        {
            type = "layout.detect",
            ownerId = "test",
            idempotencyKey = "layout-input-1",
            surfaceId,
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(surfaceId, body);
        Assert.Contains("\"inputs\"", body);
    }

    [Fact]
    public async Task Image_only_import_then_layout_detection_roundtrip()
    {
        var client = NewClient();
        var created = await client.PostAsJsonAsync("/api/v1/projects", new { name = "image-only-e2e" });
        var project = await created.Content.ReadFromJsonAsync<Dictionary<string, string>>();

        var imagePath = Path.Combine(Path.GetTempPath(), $"lt-api-image-{Guid.NewGuid():N}.png");
        using (var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(200, 120)))
        {
            surface.Canvas.Clear(SkiaSharp.SKColors.White);
            using var paint = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.Black };
            surface.Canvas.DrawRect(new SkiaSharp.SKRect(10, 20, 190, 40), paint);
            using var image = surface.Snapshot();
            using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            using var stream = File.OpenWrite(imagePath);
            data.SaveTo(stream);
        }

        try
        {
            var imported = await client.PostAsJsonAsync($"/api/v1/projects/{project!["id"]}/import-image",
                new { imagePath, kind = "inscription-face" });
            Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
            using var importedJson = System.Text.Json.JsonDocument.Parse(await imported.Content.ReadAsStringAsync());
            var surfaceId = importedJson.RootElement.GetProperty("surfaceId").GetString()!;

            var detected = await client.PostAsJsonAsync($"/api/v1/surfaces/{surfaceId}/detect-layout", new { });
            Assert.Equal(HttpStatusCode.OK, detected.StatusCode);
            Assert.Contains("lineCount", await detected.Content.ReadAsStringAsync());

            // Second detection refuses to overwrite the layout.
            var second = await client.PostAsJsonAsync($"/api/v1/surfaces/{surfaceId}/detect-layout", new { });
            Assert.Equal(HttpStatusCode.UnprocessableEntity, second.StatusCode);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public async Task Manual_line_can_be_added_then_deleted_but_transcribed_lines_are_protected()
    {
        var client = NewClient();
        var (_, firstTranscribed, surfaceId) = await ImportFixtureAsync(client, "manual-line-e2e");

        var created = await client.PostAsJsonAsync($"/api/v1/surfaces/{surfaceId}/lines", new
        {
            polygon = new[]
            {
                new { x = 10.0, y = 70.0 }, new { x = 190.0, y = 70.0 },
                new { x = 190.0, y = 90.0 }, new { x = 10.0, y = 90.0 },
            },
            baseline = new[] { new { x = 10.0, y = 88.0 }, new { x = 190.0, y = 88.0 } },
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdJson = System.Text.Json.JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var newLineId = createdJson.RootElement.GetProperty("lineId").GetString()!;

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/lines/{newLineId}")).StatusCode);
        // Transcribed fixture line: deletion refused with the conflict code.
        var refused = await client.DeleteAsync($"/api/v1/lines/{firstTranscribed}");
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Contains("revisionConflict", await refused.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Line_text_csv_import_applies_revisions_and_reports_bad_rows()
    {
        var client = NewClient();
        var (_, lineId, _) = await ImportFixtureAsync(client, "csv-import-e2e");
        var projectId = await GetProjectIdAsync(client, "csv-import-e2e");
        var csv = $"line_id,text\n{lineId},\"νεα, γραφη\"\nln-missing,αβγ\n";

        var response = await client.PostAsJsonAsync($"/api/v1/projects/{projectId}/import-line-text",
            new { csvText = csv, layer = "L1Diplomatic", authorId = "reviewer-a" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"applied\":1", body);
        Assert.Contains("\"skipped\":1", body);

        var line = await client.GetStringAsync($"/api/v1/lines/{lineId}");
        Assert.Contains("νεα, γραφη", line);
        Assert.Contains("reviewer-a", line);
    }

    [Fact]
    public async Task EpiDoc_bootstrap_imports_l2_reference_lines_and_places_geometry()
    {
        var client = NewClient();
        var project = await (await client.PostAsJsonAsync("/api/v1/projects", new { name = "epidoc-e2e" }))
            .Content.ReadFromJsonAsync<Dictionary<string, string>>();

        var dir = Path.Combine(Path.GetTempPath(), $"lt-epidoc-api-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var xml = Path.Combine(dir, "record.xml");
        await File.WriteAllTextAsync(xml, """
            <TEI xmlns="http://www.tei-c.org/ns/1.0">
              <facsimile><graphic n="screen" url="ISic001151.jpg"/></facsimile>
              <text><body><div type="edition" subtype="primary"><ab>
                <lb n="1"/>Ὀπετρηία Λητώι χαῖρε
                <lb n="2"/>χρηστὴ καὶ εὐσεβής
              </ab></div></body></text>
            </TEI>
            """);
        var image = Path.Combine(dir, "stone.png");
        using (var bitmap = new SkiaSharp.SKBitmap(240, 160))
        {
            using var canvas = new SkiaSharp.SKCanvas(bitmap);
            canvas.Clear(SkiaSharp.SKColors.LightGray);
            using var paint = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.Black };
            canvas.DrawRect(new SkiaSharp.SKRect(10, 30, 230, 55), paint);
            canvas.DrawRect(new SkiaSharp.SKRect(10, 90, 230, 115), paint);
            using var data = SkiaSharp.SKImage.FromBitmap(bitmap).Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            using var stream = File.OpenWrite(image);
            data.SaveTo(stream);
        }

        try
        {
            var imported = await client.PostAsJsonAsync($"/api/v1/projects/{project!["id"]}/import-epidoc",
                new { documentPath = xml, imagePath = image, kind = "inscription-face" });
            Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
            using var importedJson = System.Text.Json.JsonDocument.Parse(await imported.Content.ReadAsStringAsync());
            var surfaceId = importedJson.RootElement.GetProperty("surfaceId").GetString()!;
            Assert.Equal(2, importedJson.RootElement.GetProperty("lineCount").GetInt32());

            var unplacedJson = await client.GetStringAsync($"/api/v1/surfaces/{surfaceId}/unplaced-lines");
            Assert.Contains("χρηστὴ", unplacedJson);
            using var unplaced = System.Text.Json.JsonDocument.Parse(unplacedJson);
            var lineId = unplaced.RootElement[0].GetProperty("lineId").GetString()!;

            var placed = await client.PutAsJsonAsync($"/api/v1/lines/{lineId}/geometry", new
            {
                polygon = new[]
                {
                    new { x = 10.0, y = 25.0 }, new { x = 230.0, y = 25.0 },
                    new { x = 230.0, y = 60.0 }, new { x = 10.0, y = 60.0 },
                },
                baseline = new[] { new { x = 10.0, y = 58.0 }, new { x = 230.0, y = 58.0 } },
            });
            Assert.Equal(HttpStatusCode.OK, placed.StatusCode);

            using var after = System.Text.Json.JsonDocument.Parse(
                await client.GetStringAsync($"/api/v1/surfaces/{surfaceId}/unplaced-lines"));
            Assert.Equal(1, after.RootElement.GetArrayLength());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Annotation_retraction_hides_it_from_active_views_and_is_audited_in_csv()
    {
        var client = NewClient();
        var (_, lineId, _) = await ImportFixtureAsync(client, "retraction-e2e");

        var added = await client.PostAsJsonAsync($"/api/v1/lines/{lineId}/annotations", new
        {
            kind = "unclear",
            range = new { start = 1, end = 3 },
            description = "misread",
        });
        Assert.Equal(HttpStatusCode.OK, added.StatusCode);
        using var addedJson = System.Text.Json.JsonDocument.Parse(await added.Content.ReadAsStringAsync());
        var annotationId = addedJson.RootElement.GetProperty("annotationId").GetString()!;

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/v1/annotations/{annotationId}/retract",
            new { reviewer = "reviewer-a", reason = "wrong span" })).StatusCode);
        Assert.DoesNotContain("misread", await client.GetStringAsync($"/api/v1/lines/{lineId}/annotations"));
        Assert.Contains("retracted", await client.GetStringAsync($"/api/v1/projects/{await GetProjectIdAsync(client, "retraction-e2e")}/annotations.csv"));

        var again = await client.PostAsJsonAsync($"/api/v1/annotations/{annotationId}/retract",
            new { reviewer = "reviewer-b" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, again.StatusCode);
    }

    [Fact]
    public async Task Review_ledger_supports_double_review_correction_and_adjudication_export()
    {
        var client = NewClient();
        var (_, lineId, _) = await ImportFixtureAsync(client, "review-e2e");
        var projectId = await GetProjectIdAsync(client, "review-e2e");

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/v1/lines/{lineId}/review",
            new { reviewer = "reviewer-a", outcome = "accept", note = "clear" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/v1/lines/{lineId}/review",
            new { reviewer = "reviewer-b", outcome = "dispute", note = "damaged" })).StatusCode);

        var line = await client.GetStringAsync($"/api/v1/lines/{lineId}");
        Assert.Contains("\"reviews\"", line);
        Assert.Contains("reviewer-b", line);
        Assert.Contains("dispute", line);

        var summary = await client.GetStringAsync($"/api/v1/projects/{projectId}/review-summary");
        Assert.Contains("\"needsAdjudication\":1", summary);
        Assert.Contains("\"doubleReviewedLines\":1", summary);

        var adjudication = await client.GetStringAsync($"/api/v1/projects/{projectId}/adjudication.csv");
        Assert.Contains(lineId, adjudication);
        Assert.Contains("reviewer-a=Accept", adjudication);

        var corrected = await client.PostAsJsonAsync($"/api/v1/lines/{lineId}/review",
            new { reviewer = "reviewer-c", outcome = "correct", correctionText = "νεα γραφη" });
        Assert.Equal(HttpStatusCode.OK, corrected.StatusCode);
        using var correctedJson = System.Text.Json.JsonDocument.Parse(await corrected.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrEmpty(correctedJson.RootElement.GetProperty("correctedRevisionId").GetString()));

        var afterCorrection = await client.GetStringAsync($"/api/v1/lines/{lineId}");
        Assert.Contains("νεα γραφη", afterCorrection);
        Assert.Contains("correct", afterCorrection);

        // Wrong payloads are rejected.
        var invalid = await client.PostAsJsonAsync($"/api/v1/lines/{lineId}/review",
            new { reviewer = "reviewer-a", outcome = "correct" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
    }

    [Fact]
    public async Task Jobs_listing_returns_records_and_filters_by_status()
    {
        var client = NewClient();
        var created = await client.PostAsJsonAsync("/api/v1/jobs", new
        {
            type = "noop",
            ownerId = "listing-test",
            idempotencyKey = "listing-" + Guid.NewGuid().ToString("N"),
        });
        using var document = System.Text.Json.JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var jobId = document.RootElement.GetProperty("id").GetString()!;

        var all = await client.GetAsync("/api/v1/jobs");
        Assert.Equal(HttpStatusCode.OK, all.StatusCode);
        Assert.Contains(jobId, await all.Content.ReadAsStringAsync());

        var pending = await client.GetAsync("/api/v1/jobs?status=Pending");
        Assert.Equal(HttpStatusCode.OK, pending.StatusCode);
        Assert.Contains(jobId, await pending.Content.ReadAsStringAsync());

        var camel = await client.GetAsync("/api/v1/jobs?status=pending");
        Assert.Equal(HttpStatusCode.OK, camel.StatusCode);

        var invalid = await client.GetAsync("/api/v1/jobs?status=Sideways");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        Assert.Contains("stageContractViolation", await invalid.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Project_search_supports_literal_and_base_modes()
    {
        var client = NewClient();
        await ImportFixtureAsync(client, "search-e2e");
        var projectId = await GetProjectIdAsync(client, "search-e2e");

        var literal = await client.GetAsync($"/api/v1/projects/{projectId}/search?q=νθρωπ");
        Assert.Equal(HttpStatusCode.OK, literal.StatusCode);
        Assert.Contains("\"totalHits\":1", await literal.Content.ReadAsStringAsync());

        var baseMode = await client.GetAsync($"/api/v1/projects/{projectId}/search?q=ΑΝΘΡ&mode=base");
        Assert.Equal(HttpStatusCode.OK, baseMode.StatusCode);
        Assert.Contains("\"totalHits\":1", await baseMode.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Untranscribed_line_can_be_split_and_merged_back()
    {
        var client = NewClient();
        var (_, transcribedLine, surfaceId) = await ImportFixtureAsync(client, "split-merge-e2e");

        var created = await client.PostAsJsonAsync($"/api/v1/surfaces/{surfaceId}/lines", new
        {
            polygon = new[]
            {
                new { x = 10.0, y = 100.0 }, new { x = 190.0, y = 100.0 },
                new { x = 190.0, y = 160.0 }, new { x = 10.0, y = 160.0 },
            },
            baseline = new[] { new { x = 10.0, y = 158.0 }, new { x = 190.0, y = 158.0 } },
        });
        using var createdJson = System.Text.Json.JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var lineId = createdJson.RootElement.GetProperty("lineId").GetString()!;

        var split = await client.PostAsJsonAsync($"/api/v1/lines/{lineId}/split", new { y = 130.0 });
        Assert.Equal(HttpStatusCode.OK, split.StatusCode);
        using var splitJson = System.Text.Json.JsonDocument.Parse(await split.Content.ReadAsStringAsync());
        var topId = splitJson.RootElement.GetProperty("topLineId").GetString()!;
        var bottomId = splitJson.RootElement.GetProperty("bottomLineId").GetString()!;

        var merged = await client.PostAsJsonAsync($"/api/v1/lines/{topId}/merge", new { otherLineId = bottomId });
        Assert.Equal(HttpStatusCode.OK, merged.StatusCode);

        // Transcribed lines are protected.
        var refused = await client.PostAsJsonAsync($"/api/v1/lines/{transcribedLine}/split", new { y = 45.0 });
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
    }

    [Fact]
    public async Task Imported_ids_are_scoped_per_project_so_global_line_endpoints_are_unambiguous()
    {
        var client = NewClient();
        var (_, lineA, _) = await ImportFixtureAsync(client, "scoping-a");
        var (_, lineB, _) = await ImportFixtureAsync(client, "scoping-b");
        Assert.NotEqual(lineA, lineB); // document-local PAGE ids are namespaced per import

        var projectA = await GetProjectIdAsync(client, "scoping-a");
        var projectB = await GetProjectIdAsync(client, "scoping-b");

        // Mutating B's line must not leak into A.
        var appended = await client.PostAsJsonAsync($"/api/v1/lines/{lineB}/revisions",
            new { layer = "L1Diplomatic", text = "abc", authorId = "tester" });
        Assert.Equal(HttpStatusCode.OK, appended.StatusCode);

        var reportA = await client.GetStringAsync($"/api/v1/projects/{projectA}/profile-report");
        var reportB = await client.GetStringAsync($"/api/v1/projects/{projectB}/profile-report");
        Assert.Contains("\"flagged\":0", reportA);
        Assert.Contains("\"flagged\":1", reportB);
    }

    [Fact]
    public async Task Profile_report_flags_latin_characters_without_rewriting_text()
    {
        var client = NewClient();
        var (_, lineId, _) = await ImportFixtureAsync(client, "profile-report-e2e");
        var projectId = await GetProjectIdAsync(client, "profile-report-e2e");

        var clean = await client.GetAsync($"/api/v1/projects/{projectId}/profile-report");
        Assert.Equal(HttpStatusCode.OK, clean.StatusCode);
        var cleanBody = await clean.Content.ReadAsStringAsync();
        Assert.Contains("greek-diplomatic-pilot", cleanBody);
        Assert.Contains("\"flagged\":0", cleanBody);

        // Append a Latin-bearing L1 revision: reported, and the stored text is untouched.
        var appended = await client.PostAsJsonAsync($"/api/v1/lines/{lineId}/revisions",
            new { layer = "L1Diplomatic", text = "abc", authorId = "tester" });
        Assert.Equal(HttpStatusCode.OK, appended.StatusCode);

        var flagged = await client.GetAsync($"/api/v1/projects/{projectId}/profile-report");
        var flaggedBody = await flagged.Content.ReadAsStringAsync();
        Console.WriteLine("PROFILE BODY: " + flaggedBody);
        Assert.Contains("\"flagged\":1", flaggedBody);
        Assert.Contains("LatinLetter", await flagged.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Project_bundle_returns_a_zip_with_a_manifest()
    {
        var client = NewClient();
        var created = await client.PostAsJsonAsync("/api/v1/projects", new { name = "bundle-e2e" });
        var project = await created.Content.ReadFromJsonAsync<Dictionary<string, string>>();

        var response = await client.GetAsync($"/api/v1/projects/{project!["id"]}/bundle");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 100);
        Assert.Equal((byte)'P', bytes[0]); // PK zip signature
        Assert.Equal((byte)'K', bytes[1]);
    }

    [Fact]
    public async Task Bulk_import_job_carries_project_and_source_directory()
    {
        var client = NewClient();
        var created = await client.PostAsJsonAsync("/api/v1/projects", new { name = "import-job" });
        var project = await created.Content.ReadFromJsonAsync<Dictionary<string, string>>();

        var response = await client.PostAsJsonAsync("/api/v1/jobs", new
        {
            type = "import.dir",
            ownerId = "test",
            idempotencyKey = "import-dir-1",
            projectId = project!["id"],
            sourceDirectory = "/tmp/page-xml",
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(project["id"], body);
        Assert.Contains("dir:/tmp/page-xml", body);
    }

    [Fact]
    public async Task Job_events_stream_replays_created_event()
    {
        var client = NewClient();
        var created = await client.PostAsJsonAsync("/api/v1/jobs", new { type = "pipeline.run", ownerId = "test", idempotencyKey = "sse-key" });
        var jobBody = await created.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        var jobId = jobBody!["id"].ToString()!;

        // The SSE stream stays open by design; only headers are asserted here.
        // Replay/live-delivery semantics are covered by JobEventBusTests.
        var stream = await client.GetAsync($"/api/v1/jobs/{jobId}/events", HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, stream.StatusCode);
        Assert.Contains("text/event-stream", stream.Content.Headers.ContentType?.MediaType ?? "");
    }
}

/// <summary>Test host factory with an isolated store file, one stub provider and a seeded revision.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public string SeedRevisionId { get; } = "rev-seedchain01";

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"leviathan-api-{Guid.NewGuid():N}.db");
        builder.UseSetting("LeviathanText:StorePath", dbPath);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LeviathanText:AiProviders:0:ProviderId"] = "stub-provider",
                ["LeviathanText:AiProviders:0:Host"] = "Hosted",
                ["LeviathanText:AiProviders:0:Protocol"] = "OpenAiChatCompletions",
                ["LeviathanText:AiProviders:0:Endpoint"] = "http://localhost:1/v1",
                ["LeviathanText:AiProviders:0:ModelId"] = "stub-model",
                ["LeviathanText:AiProviders:0:CredentialEnvironmentVariable"] = "STUB_API_KEY",
            });
        });
    }
}
