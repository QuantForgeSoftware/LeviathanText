using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LeviathanText.Api.Tests;

public sealed class ReviewRolePathTests
{
    [Theory]
    [InlineData("annotator", false, HttpStatusCode.Forbidden)]
    [InlineData("annotator", true, HttpStatusCode.Forbidden)]
    [InlineData("reader", false, HttpStatusCode.Forbidden)]
    [InlineData("reader", true, HttpStatusCode.Forbidden)]
    [InlineData("reviewer", false, HttpStatusCode.OK)]
    [InlineData("reviewer", true, HttpStatusCode.OK)]
    public async Task Equivalent_review_paths_require_the_same_role(string role, bool trailingSlash, HttpStatusCode expected)
    {
        using var factory = new Factory(); using var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Add("X-Api-Key", "role-path-bootstrap");
        using var account = await admin.PostAsJsonAsync("/api/v1/users", new { name = "role-path-user", role });
        Assert.Equal(HttpStatusCode.OK, account.StatusCode);
        var json = await account.Content.ReadFromJsonAsync<JsonElement>();
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Add("X-Api-Key", json.GetProperty("apiKey").GetString());
        using (var scope = factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<LocalStore>();
            var line = new EntityId(EntityKind.Line, "role-path");
            var revision = new TextRevision
            {
                Id = new(EntityKind.TranscriptionRevision, "role-path"), ScopeId = line, Layer = TextLayer.L1Diplomatic,
                Text = "αβ", Author = new AgentRef { Id = "source", IsModel = false }, CreatedAt = DateTimeOffset.UnixEpoch,
            };
            store.SaveProject(new NativeProject
            {
                Id = new(EntityKind.Collection, "role-path"), Name = "generated role fixture", Objects = [new NativeObject
                {
                    Id = new(EntityKind.PhysicalObject, "role-path"), Surfaces = [new NativeSurface
                    {
                        Id = new(EntityKind.Surface, "role-path"), Kind = "codex-page", Captures = [new NativeCapture
                        {
                            Id = new(EntityKind.Capture, "role-path"), ImageLocation = "generated-role-fixture.png",
                            Frame = new CoordinateFrame { ImageRef = new(EntityKind.Capture, "role-path"),
                                ImageChecksum = new Checksum { Algorithm = "unavailable", Digest = "pending" } },
                        }],
                        Regions = [new NativeRegion { Id = new(EntityKind.Region, "role-path"), FrameRef = new(EntityKind.Capture, "role-path") }],
                        Lines = [new NativeLine { Id = line, RegionId = new(EntityKind.Region, "role-path"), FrameRef = new(EntityKind.Capture, "role-path"), DiplomaticRevisionId = revision.Id }],
                        Revisions = [revision],
                    }],
                }],
            });
        }
        using var response = await client.PostAsJsonAsync("/api/v1/lines/ln-role-path/review" + (trailingSlash ? "/" : ""),
            new { reviewer = "claimed-reviewer", outcome = "accept", revisionId = "rev-role-path" });
        Assert.True(expected == response.StatusCode, $"Expected {expected}, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        using var verification = factory.Services.CreateScope();
        var saved = verification.ServiceProvider.GetRequiredService<LocalStore>().LoadProject(new(EntityKind.Collection, "role-path"))!;
        var decisions = saved.Objects[0].Surfaces[0].ReviewDecisions;
        Assert.Equal(expected == HttpStatusCode.OK ? 1 : 0, decisions.Count);
        Assert.Equal(expected == HttpStatusCode.OK ? 2 : 1, saved.Version);
    }
    private sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"lt-role-path-{Guid.NewGuid():N}");
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("LeviathanText:StorePath", Path.Combine(_root, "store.db"));
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
                { ["LeviathanText:Auth:ApiKey"] = "role-path-bootstrap" }));
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
    }
}
