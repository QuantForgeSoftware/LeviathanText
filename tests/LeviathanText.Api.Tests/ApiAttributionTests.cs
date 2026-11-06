using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.AI;
using LeviathanText.Contracts.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LeviathanText.Api.Tests;

public sealed class ApiAttributionTests
{
    private const string AdminKey = "attribution-fixture-bootstrap";
    private sealed class AuthFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("LeviathanText:StorePath", Path.Combine(Path.GetTempPath(), "lt-attribution-" + Guid.NewGuid().ToString("N") + ".db"));
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LeviathanText:Auth:ApiKey"] = AdminKey,
            }));
        }
    }

    private static async Task<(HttpClient Client, string Id)> Reviewer(WebApplicationFactory<Program> factory, string name)
    {
        using var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Add("X-Api-Key", AdminKey);
        var created = await admin.PostAsJsonAsync("/api/v1/users", new { name, role = "reviewer" });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using var data = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", data.RootElement.GetProperty("apiKey").GetString());
        return (client, data.RootElement.GetProperty("account").GetProperty("id").GetString()!);
    }

    internal static NativeProject Seed(WebApplicationFactory<Program> factory)
    {
        var project = ApiOrderSnapshotTests.Seed(factory);
        var obj = project.Objects[0];
        var surface = obj.Surfaces[0];
        var line = surface.Lines[0];
        var revision = new TextRevision
        {
            Id = new(EntityKind.TranscriptionRevision, Guid.NewGuid().ToString("N")), ScopeId = line.Id,
            Layer = TextLayer.L1Diplomatic, Text = "αβ", Author = new AgentRef { Id = "source-catalogue", IsModel = false },
            CreatedAt = DateTimeOffset.UnixEpoch,
        };
        project = project with { Objects = [obj with { Surfaces = [surface with
        {
            Revisions = [revision], Lines = surface.Lines.Select(l => l.Id == line.Id ? l with { DiplomaticRevisionId = revision.Id } : l).ToArray(),
        }] }] };
        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IProjectStore>();
        store.SaveProject(project);
        return store.LoadProject(project.Id)!;
    }

    private static NativeSurface LoadSurface(WebApplicationFactory<Program> factory, EntityId projectId)
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IProjectStore>().LoadProject(projectId)!.Objects[0].Surfaces[0];
    }

    [Fact]
    public async Task One_authenticated_reviewer_cannot_manufacture_double_review_with_two_labels()
    {
        using var factory = new AuthFactory();
        var (client, reviewerId) = await Reviewer(factory, "one-reviewer");
        using (client)
        {
            var project = Seed(factory);
            var line = project.Objects[0].Surfaces[0].Lines[0];
            foreach (var reviewer in new[] { "specialist-A", "specialist-B" })
                Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/v1/lines/{line.Id}/review", new
                {
                    reviewer, outcome = "accept", revisionId = line.DiplomaticRevisionId.ToString(),
                })).StatusCode);

            using var summary = JsonDocument.Parse(await client.GetStringAsync($"/api/v1/projects/{project.Id}/review-summary"));
            Assert.Equal(0, summary.RootElement.GetProperty("doubleReviewedLines").GetInt32());
            using var scope = factory.Services.CreateScope();
            var saved = scope.ServiceProvider.GetRequiredService<IProjectStore>().LoadProject(project.Id)!;
            Assert.All(saved.Objects[0].Surfaces[0].ReviewDecisions, review => Assert.Equal(reviewerId, review.Reviewer));
            Assert.Equal("source-catalogue", saved.Objects[0].Surfaces[0].Revisions[0].Author.Id);
        }
    }

    [Fact]
    public async Task Two_actual_accounts_remain_distinct_even_when_they_supply_the_same_display_label()
    {
        using var factory = new AuthFactory();
        var (first, firstId) = await Reviewer(factory, "first-reviewer");
        var (second, secondId) = await Reviewer(factory, "second-reviewer");
        using (first)
        using (second)
        {
            var project = Seed(factory);
            var line = project.Objects[0].Surfaces[0].Lines[0];
            foreach (var client in new[] { first, second })
                Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/v1/lines/{line.Id}/review",
                    new { reviewer = "same-forged-label", outcome = "accept" })).StatusCode);
            using var summary = JsonDocument.Parse(await first.GetStringAsync($"/api/v1/projects/{project.Id}/review-summary"));
            Assert.Equal(1, summary.RootElement.GetProperty("doubleReviewedLines").GetInt32());
            Assert.Equal(new[] { firstId, secondId }.Order(), LoadSurface(factory, project.Id).ReviewDecisions.Select(r => r.Reviewer).Order());
        }
    }

    [Fact]
    public async Task Unauthenticated_local_mode_retains_caller_review_labels()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var project = Seed(factory);
        var line = project.Objects[0].Surfaces[0].Lines[0];
        foreach (var reviewer in new[] { "local-A", "local-B" })
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/v1/lines/{line.Id}/review", new { reviewer, outcome = "accept" })).StatusCode);
        Assert.Equal(new[] { "local-A", "local-B" }, LoadSurface(factory, project.Id).ReviewDecisions.Select(r => r.Reviewer));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Normal_and_merged_revision_authors_bind_to_the_authenticated_account_only(bool authenticated, bool merge)
    {
        using WebApplicationFactory<Program> factory = authenticated ? new AuthFactory() : new ApiFactory();
        var actor = authenticated ? await Reviewer(factory, "revision-writer") : (factory.CreateClient(), "caller-label");
        using var client = actor.Item1;
        var project = Seed(factory);
        var original = project.Objects[0].Surfaces[0].Lines[0];

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/v1/lines/{original.Id}/revisions", new
        {
            layer = "l1Diplomatic", text = "αγ", authorId = "caller-label", parentRevisionId = original.DiplomaticRevisionId.ToString(), merge,
        })).StatusCode);

        var saved = LoadSurface(factory, project.Id);
        Assert.Equal(actor.Item2, saved.Revisions[^1].Author.Id);
        Assert.False(saved.Revisions[^1].Author.IsModel);
        Assert.Equal("source-catalogue", saved.Revisions[0].Author.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Correction_annotation_and_retraction_authorship_follow_the_same_boundary(bool authenticated)
    {
        using WebApplicationFactory<Program> factory = authenticated ? new AuthFactory() : new ApiFactory();
        var actor = authenticated ? await Reviewer(factory, "annotation-reviewer") : (factory.CreateClient(), "caller-label");
        using var client = actor.Item1;
        var project = Seed(factory);
        var line = project.Objects[0].Surfaces[0].Lines[0];
        var created = await client.PostAsJsonAsync($"/api/v1/lines/{line.Id}/annotations", new
        {
            kind = "unclear", range = new { start = 0, end = 1 }, anchoredRevisionId = line.DiplomaticRevisionId.ToString(),
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using var annotation = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var annotationId = annotation.RootElement.GetProperty("annotationId").GetString();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/v1/annotations/{annotationId}/retract", new { reviewer = "caller-label" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/v1/lines/{line.Id}/review", new
        {
            reviewer = "caller-label", outcome = "correct", correctionText = "αγ", revisionId = line.DiplomaticRevisionId.ToString(),
        })).StatusCode);
        var saved = LoadSurface(factory, project.Id);
        Assert.Equal(authenticated ? actor.Item2 : null, saved.Annotations[0].Author?.Id);
        Assert.Equal(actor.Item2, saved.AnnotationRetractions[0].Reviewer);
        Assert.Equal(actor.Item2, saved.ReviewDecisions[0].Reviewer);
        Assert.Equal(actor.Item2, saved.Revisions[^1].Author.Id);
        Assert.Equal("source-catalogue", saved.Revisions[0].Author.Id);
    }

    public static IEnumerable<object[]> GeometryCases =>
        from action in new[] { "geometry", "line-orientation", "region-orientation", "add", "split", "merge", "delete", "restore", "region-order", "line-order" }
        from authenticated in new[] { false, true }
        select new object[] { action, authenticated };

    [Theory]
    [MemberData(nameof(GeometryCases))]
    public async Task Every_direct_geometry_audit_uses_authenticated_identity_without_altering_source_attribution(string action, bool authenticated)
    {
        using WebApplicationFactory<Program> factory = authenticated ? new AuthFactory() : new ApiFactory();
        var actor = authenticated ? await Reviewer(factory, "geometry-writer") : (factory.CreateClient(), "caller-label");
        using var client = actor.Item1;
        var project = Seed(factory);
        var surface = project.Objects[0].Surfaces[0];
        var line = surface.Lines[0];
        var free = surface.Lines[1];
        var polygon = new[] { new { x = 1, y = 1 }, new { x = 20, y = 1 }, new { x = 20, y = 8 }, new { x = 1, y = 8 } };
        var baseline = new[] { new { x = 1, y = 7 }, new { x = 20, y = 7 } };
        HttpResponseMessage response;
        switch (action)
        {
            case "geometry": response = await client.PutAsJsonAsync($"/api/v1/lines/{line.Id}/geometry", new { polygon, baseline, authorId = "caller-label" }); break;
            case "line-orientation": response = await client.PutAsJsonAsync($"/api/v1/lines/{line.Id}/orientation", new { direction = "rtl", authorId = "caller-label" }); break;
            case "region-orientation": response = await client.PutAsJsonAsync($"/api/v1/regions/{line.RegionId}/orientation", new { direction = "rtl", authorId = "caller-label" }); break;
            case "add": response = await client.PostAsJsonAsync($"/api/v1/surfaces/{surface.Id}/lines", new { polygon, baseline, regionId = line.RegionId.ToString(), authorId = "caller-label" }); break;
            case "split": response = await client.PostAsJsonAsync($"/api/v1/lines/{free.Id}/split", new { y = 14, authorId = "caller-label" }); break;
            case "merge": response = await client.PostAsJsonAsync($"/api/v1/lines/{free.Id}/merge", new { otherLineId = surface.Lines[2].Id.ToString(), authorId = "caller-label" }); break;
            case "delete": response = await client.DeleteAsync($"/api/v1/lines/{free.Id}?author=caller-label"); break;
            case "restore":
                Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/v1/lines/{line.Id}/geometry", new { polygon, baseline, authorId = "caller-label" })).StatusCode);
                var edit = LoadSurface(factory, project.Id).GeometryEdits[0].Id;
                response = await client.PostAsJsonAsync($"/api/v1/surfaces/{surface.Id}/geometry-history/{edit}/restore", new { authorId = "caller-label" }); break;
            case "region-order": response = await client.PutAsJsonAsync($"/api/v1/surfaces/{surface.Id}/reading-order", new { regionIds = surface.Regions.Reverse().Select(r => r.Id.ToString()), authorId = "caller-label" }); break;
            default: response = await client.PutAsJsonAsync($"/api/v1/regions/{line.RegionId}/line-order", new { lineIds = surface.Lines.Reverse().Select(l => l.Id.ToString()), authorId = "caller-label" }); break;
        }
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var saved = LoadSurface(factory, project.Id);
        Assert.NotEmpty(saved.GeometryEdits);
        Assert.All(saved.GeometryEdits, edit => Assert.Equal(actor.Item2, edit.Author));
        Assert.Equal("source-catalogue", saved.Revisions[0].Author.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Bulk_ai_acceptance_uses_the_account_as_acceptor_only_in_authenticated_mode(bool authenticated)
    {
        using WebApplicationFactory<Program> factory = authenticated ? new AuthFactory() : new ApiFactory();
        var actor = authenticated ? await Reviewer(factory, "accepting-reviewer") : (factory.CreateClient(), "caller-label");
        using var client = actor.Item1;
        var project = Seed(factory);
        var source = project.Objects[0].Surfaces[0].Revisions[0];
        AiProposal proposal;
        using (var scope = factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<NativeAiProposalService>();
            var prepared = service.Prepare(project, new AiProposalRequest
            {
                TargetRevisionId = source.Id, Range = new(0, 2), Task = AiTaskKind.Expansion,
                ProviderConfigRef = "generated", PromptVersion = "fixture-v1",
            });
            proposal = new AiProposal
            {
                Id = new(EntityKind.Proposal, Guid.NewGuid().ToString("N")), ProposedText = "αγ", ProviderId = "generated", ModelId = "fixture", PromptVersion = "fixture-v1",
                Confidence = Confidence.Of(0.5, ConfidenceScale.RawModelProbability), CreatedAt = DateTimeOffset.UnixEpoch, Request = prepared.Request,
            };
            service.Issue(project, prepared, proposal);
        }
        var body = ContractJson.ToJson(new { proposals = new[] { proposal }, acceptorId = "caller-label" });
        var response = await client.PostAsync("/api/v1/proposals/bulk-accept", new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = LoadSurface(factory, project.Id);
        Assert.Equal(actor.Item2, saved.Revisions.Single(r => r.Layer == TextLayer.L2Editorial).Author.Id);
        Assert.Equal("source-catalogue", saved.Revisions.Single(r => r.Layer == TextLayer.L1Diplomatic).Author.Id);
    }
}
