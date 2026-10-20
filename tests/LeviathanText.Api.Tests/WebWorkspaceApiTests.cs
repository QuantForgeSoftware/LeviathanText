using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LeviathanText.Api.Tests;

public sealed class WebWorkspaceApiTests
{
    private sealed class WebFactory : WebApplicationFactory<Program>
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), $"leviathan-web-{Guid.NewGuid():N}");

        public WebFactory()
        {
            Directory.CreateDirectory(Root);
            File.WriteAllText(Path.Combine(Root, "index.html"), "<html><body>workspace-marker</body></html>");
            File.WriteAllText(Path.Combine(Root, "app.js"), "console.log('ok')");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("LeviathanText:StorePath",
                Path.Combine(Path.GetTempPath(), $"leviathan-web-store-{Guid.NewGuid():N}.db"));
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["LeviathanText:WebRoot"] = Root,
                    ["LeviathanText:WebPath"] = "/ui",
                }));
        }
    }

    [Fact]
    public async Task Packaged_workspace_is_served_under_ui_with_spa_fallback()
    {
        using var factory = new WebFactory();
        var client = factory.CreateClient();

        Assert.Contains("workspace-marker", await client.GetStringAsync("/ui/"));
        Assert.Contains("workspace-marker", await client.GetStringAsync("/ui/deep/client/route"));
        Assert.Contains("console.log", await client.GetStringAsync("/ui/app.js"));

        // The service/version endpoint and API remain separate from the SPA fallback.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/projects")).StatusCode);
    }
}
