using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LeviathanText.Api.Tests;

/// <summary>
/// Optional per-project membership: when enabled, non-admin accounts only see and touch projects
/// they belong to; creators are added automatically and admins manage membership. Local mode and
/// the default (membership disabled) are unchanged.
/// </summary>
public sealed class ApiMembershipTests
{
    private const string AdminKey = "membership-admin-key-0123456789";

    private sealed class MembershipFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("LeviathanText:StorePath",
                Path.Combine(Path.GetTempPath(), $"leviathan-members-{Guid.NewGuid():N}.db"));
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["LeviathanText:Auth:ApiKey"] = AdminKey,
                    ["LeviathanText:Auth:ProjectMembership"] = "true",
                });
            });
        }
    }

    private static HttpClient WithKey(HttpClient client, string key)
    {
        client.DefaultRequestHeaders.Add("X-Api-Key", key);
        return client;
    }

    private static async Task<(string Id, string Key)> CreateUser(HttpClient admin, string name, string role)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/users", new { name, role });
        using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (json.RootElement.GetProperty("account").GetProperty("id").GetString()!,
            json.RootElement.GetProperty("apiKey").GetString()!);
    }

    [Fact]
    public async Task Members_only_see_their_projects_and_admin_can_grant_or_revoke()
    {
        using var factory = new MembershipFactory();
        var admin = WithKey(factory.CreateClient(), AdminKey);
        var (userAId, keyA) = await CreateUser(admin, "anna", "annotator");
        var (userBId, keyB) = await CreateUser(admin, "ben", "annotator");
        Assert.NotEqual(userAId, userBId);

        var userA = WithKey(factory.CreateClient(), keyA);
        var userB = WithKey(factory.CreateClient(), keyB);

        var created = await userA.PostAsJsonAsync("/api/v1/projects", new { name = "shared-project" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var project = await created.Content.ReadFromJsonAsync<Dictionary<string, string>>();

        Assert.Contains("shared-project", await userA.GetStringAsync("/api/v1/projects"));
        Assert.DoesNotContain("shared-project", await userB.GetStringAsync("/api/v1/projects"));
        Assert.Equal(HttpStatusCode.Forbidden, (await userB.GetAsync($"/api/v1/projects/{project!["id"]}")).StatusCode);

        var fixture = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "LeviathanText.Formats.Tests", "Fixtures", "sample.page.xml"));
        Assert.Equal(HttpStatusCode.OK, (await userA.PostAsJsonAsync($"/api/v1/projects/{project["id"]}/import-page",
            new { pageXmlPath = fixture })).StatusCode);
        using var payload = System.Text.Json.JsonDocument.Parse(await userA.GetStringAsync($"/api/v1/projects/{project["id"]}"));
        var lineId = payload.RootElement.GetProperty("objects")[0].GetProperty("surfaces")[0]
            .GetProperty("lines")[0].GetProperty("id").GetString()!;

        // Indirect entity route is also membership-scoped.
        Assert.Equal(HttpStatusCode.Forbidden, (await userB.GetAsync($"/api/v1/lines/{lineId}")).StatusCode);

        // Admin grants access; B can now read and edit.
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/projects/{project["id"]}/members",
            new { userId = userBId })).StatusCode);
        Assert.Contains("shared-project", await userB.GetStringAsync("/api/v1/projects"));
        Assert.Equal(HttpStatusCode.OK, (await userB.GetAsync($"/api/v1/lines/{lineId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await userB.PostAsJsonAsync($"/api/v1/lines/{lineId}/revisions",
            new { layer = "L1Diplomatic", text = "ben text", authorId = "ben" })).StatusCode);

        // Revocation takes effect immediately.
        Assert.Equal(HttpStatusCode.NoContent,
            (await admin.DeleteAsync($"/api/v1/projects/{project["id"]}/members/{userBId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await userB.GetAsync($"/api/v1/projects/{project["id"]}")).StatusCode);
        Assert.DoesNotContain("shared-project", await userB.GetStringAsync("/api/v1/projects"));

        // Admin always sees the project.
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/v1/projects/{project["id"]}")).StatusCode);
    }
}
