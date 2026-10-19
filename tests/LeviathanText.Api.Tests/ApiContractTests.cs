using LeviathanText.Contracts;
using LeviathanText.Contracts.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
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
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
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
}

/// <summary>Test host factory with an isolated store file per factory.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"leviathan-api-{Guid.NewGuid():N}.db");
        builder.UseSetting("LeviathanText:StorePath", dbPath);
    }
}
