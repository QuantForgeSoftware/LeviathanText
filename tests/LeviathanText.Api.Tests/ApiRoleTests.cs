using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LeviathanText.Api.Tests;

/// <summary>
/// Role-based authorization with user accounts: reader (GET), annotator (mutations),
/// reviewer (+ review decisions), admin (+ user management). The configured key is an admin
/// bootstrap credential; keys are returned once and stored hashed.
/// </summary>
public sealed class ApiRoleTests
{
    private const string AdminKey = "bootstrap-admin-key-0123456789";

    private sealed class RoleFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("LeviathanText:StorePath",
                Path.Combine(Path.GetTempPath(), $"leviathan-roles-{Guid.NewGuid():N}.db"));
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["LeviathanText:Auth:ApiKey"] = AdminKey,
                });
            });
        }
    }

    private static HttpClient WithKey(HttpClient client, string key)
    {
        client.DefaultRequestHeaders.Add("X-Api-Key", key);
        return client;
    }

    private static async Task<string> CreateUser(HttpClient admin, string name, string role)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/users", new { name, role });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("apiKey").GetString()!;
    }

    [Fact]
    public async Task Roles_are_enforced_for_reads_mutations_reviews_and_admin_routes()
    {
        using var factory = new RoleFactory();
        var admin = WithKey(factory.CreateClient(), AdminKey);

        var readerKey = await CreateUser(admin, "reader", "reader");
        var annotatorKey = await CreateUser(admin, "annotator", "annotator");
        var reviewerKey = await CreateUser(admin, "reviewer", "reviewer");

        var reader = WithKey(factory.CreateClient(), readerKey);
        var annotator = WithKey(factory.CreateClient(), annotatorKey);
        var reviewer = WithKey(factory.CreateClient(), reviewerKey);

        // Reads: reader allowed; mutations: reader forbidden; annotator allowed.
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync("/api/v1/projects")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync("/api/v1/projects", new { name = "nope" })).StatusCode);
        var created = await annotator.PostAsJsonAsync("/api/v1/projects", new { name = "roles-e2e" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var project = await created.Content.ReadFromJsonAsync<Dictionary<string, string>>();

        var fixture = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "LeviathanText.Formats.Tests", "Fixtures", "sample.page.xml"));
        var imported = await annotator.PostAsJsonAsync($"/api/v1/projects/{project!["id"]}/import-page",
            new { pageXmlPath = fixture });
        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
        using var importedJson = System.Text.Json.JsonDocument.Parse(await imported.Content.ReadAsStringAsync());
        var surfaceId = importedJson.RootElement.GetProperty("surfaceId").GetString()!;
        var projectJson = await annotator.GetStringAsync($"/api/v1/projects/{project["id"]}");
        using var payload = System.Text.Json.JsonDocument.Parse(projectJson);
        var lineId = payload.RootElement.GetProperty("objects")[0].GetProperty("surfaces")[0]
            .GetProperty("lines")[0].GetProperty("id").GetString()!;

        // Review decisions: annotator forbidden, reviewer allowed.
        Assert.Equal(HttpStatusCode.Forbidden, (await annotator.PostAsJsonAsync($"/api/v1/lines/{lineId}/review",
            new { reviewer = "annotator", outcome = "accept" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await reviewer.PostAsJsonAsync($"/api/v1/lines/{lineId}/review",
            new { reviewer = "reviewer", outcome = "accept" })).StatusCode);

        // User management is admin-only; disabling a user blocks their key.
        Assert.Equal(HttpStatusCode.Forbidden, (await reviewer.GetAsync("/api/v1/users")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/users")).StatusCode);
        var users = await admin.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/v1/users");
        var readerId = users.EnumerateArray().First(u => u.GetProperty("name").GetString() == "reader")
            .GetProperty("id").GetString()!;
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/v1/users/{readerId}/disable", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await reader.GetAsync("/api/v1/projects")).StatusCode);

        // Rotating a key invalidates the old one.
        var usersAfter = await admin.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/v1/users");
        var annotatorId = usersAfter.EnumerateArray().First(u => u.GetProperty("name").GetString() == "annotator")
            .GetProperty("id").GetString()!;
        var rotated = await admin.PostAsync($"/api/v1/users/{annotatorId}/rotate", null);
        using var rotatedJson = System.Text.Json.JsonDocument.Parse(await rotated.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await annotator.GetAsync("/api/v1/projects")).StatusCode);
        var newAnnotator = WithKey(factory.CreateClient(), rotatedJson.RootElement.GetProperty("apiKey").GetString()!);
        Assert.Equal(HttpStatusCode.OK, (await newAnnotator.GetAsync("/api/v1/projects")).StatusCode);

        var me = await newAnnotator.GetStringAsync("/api/v1/me");
        Assert.Contains("\"authenticated\":true", me);
        Assert.Contains("\"name\":\"annotator\"", me);
        Assert.Contains("Annotator", me);

        // Auth audit: admin can read it; annotators cannot.
        Assert.Equal(HttpStatusCode.Forbidden, (await newAnnotator.GetAsync("/api/v1/audit")).StatusCode);
        var audit = await admin.GetStringAsync("/api/v1/audit?limit=100");
        Assert.Contains("unauthorized", audit);
        Assert.Contains("authenticated", audit);

        // Project deletion is admin-only.
        Assert.Equal(HttpStatusCode.Forbidden, (await newAnnotator.DeleteAsync($"/api/v1/projects/{project["id"]}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/projects/{project["id"]}")).StatusCode);
        Assert.NotNull(surfaceId);
    }
}
