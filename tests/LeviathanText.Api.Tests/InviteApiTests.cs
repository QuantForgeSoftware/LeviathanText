using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LeviathanText.Api.Tests;

public sealed class InviteApiTests
{
    private const string AdminKey = "invite-admin-key-0123456789";

    private sealed class InviteFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("LeviathanText:StorePath",
                Path.Combine(Path.GetTempPath(), $"leviathan-invite-{Guid.NewGuid():N}.db"));
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["LeviathanText:Auth:ApiKey"] = AdminKey,
                    ["LeviathanText:Auth:ProjectMembership"] = "true",
                }));
        }
    }

    private static HttpClient WithKey(HttpClient client, string key)
    {
        client.DefaultRequestHeaders.Add("X-Api-Key", key);
        return client;
    }

    [Fact]
    public async Task Admin_invite_is_one_time_and_creates_a_project_member()
    {
        using var factory = new InviteFactory();
        var admin = WithKey(factory.CreateClient(), AdminKey);
        var created = await admin.PostAsJsonAsync("/api/v1/projects", new { name = "invite-e2e" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var project = await created.Content.ReadFromJsonAsync<Dictionary<string, string>>();

        var inviteResponse = await admin.PostAsJsonAsync($"/api/v1/projects/{project!["id"]}/invites",
            new { role = "annotator", expiresInHours = 24 });
        Assert.Equal(HttpStatusCode.OK, inviteResponse.StatusCode);
        using var inviteJson = System.Text.Json.JsonDocument.Parse(await inviteResponse.Content.ReadAsStringAsync());
        var token = inviteJson.RootElement.GetProperty("token").GetString()!;

        // Acceptance is intentionally unauthenticated (the token is the credential).
        var anonymous = factory.CreateClient();
        var accepted = await anonymous.PostAsJsonAsync("/api/v1/invites/accept", new { token, name = "invited-anna" });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using var acceptedJson = System.Text.Json.JsonDocument.Parse(await accepted.Content.ReadAsStringAsync());
        var key = acceptedJson.RootElement.GetProperty("apiKey").GetString()!;
        var invited = WithKey(factory.CreateClient(), key);

        Assert.Equal(HttpStatusCode.OK, (await invited.GetAsync($"/api/v1/projects/{project["id"]}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await invited.PostAsJsonAsync($"/api/v1/projects/{project["id"]}/invites", new { role = "annotator" })).StatusCode);

        var reused = await factory.CreateClient().PostAsJsonAsync("/api/v1/invites/accept", new { token, name = "second" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reused.StatusCode);
    }
}
