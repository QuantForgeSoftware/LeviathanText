using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LeviathanText.Api.Tests;

/// <summary>
/// Opt-in shared-deployment guard: API key required under /api/v1 when configured,
/// optional read-only mode blocks mutations. This is a transport guard, not a user
/// system (no accounts/roles/audit) — documented as such.
/// </summary>
public sealed class ApiAuthTests
{
    private const string Key = "pilot-key-0123456789";

    private sealed class AuthFactory(bool readOnly) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("LeviathanText:StorePath",
                Path.Combine(Path.GetTempPath(), $"leviathan-auth-{Guid.NewGuid():N}.db"));
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["LeviathanText:Auth:ApiKey"] = Key,
                    ["LeviathanText:Auth:ReadOnly"] = readOnly ? "true" : "false",
                });
            });
        }
    }

    [Fact]
    public async Task Missing_key_is_rejected_under_api_surface()
    {
        using var factory = new AuthFactory(readOnly: false);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/projects");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("unauthorized", await response.Content.ReadAsStringAsync());

        // The root endpoint stays reachable for liveness.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);
    }

    [Fact]
    public async Task Bearer_and_header_keys_are_accepted_and_wrong_keys_rejected()
    {
        using var factory = new AuthFactory(readOnly: false);
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/projects");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Key);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(request)).StatusCode);

        var headerRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/projects");
        headerRequest.Headers.Add("X-Api-Key", Key);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(headerRequest)).StatusCode);

        var wrong = new HttpRequestMessage(HttpMethod.Get, "/api/v1/projects");
        wrong.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Key + "x");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(wrong)).StatusCode);
    }

    [Fact]
    public async Task Query_token_is_accepted_for_event_source_compatibility()
    {
        using var factory = new AuthFactory(readOnly: false);
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync($"/api/v1/projects?api_key={Key}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/v1/projects?api_key=wrong")).StatusCode);
    }

    [Fact]
    public async Task Read_only_mode_blocks_mutations_but_allows_reads()
    {
        using var factory = new AuthFactory(readOnly: true);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", Key);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/projects")).StatusCode);

        var write = await client.PostAsJsonAsync("/api/v1/projects", new { name = "should-fail" });
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
        Assert.Contains("read_only", await write.Content.ReadAsStringAsync());
    }
}
