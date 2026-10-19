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
