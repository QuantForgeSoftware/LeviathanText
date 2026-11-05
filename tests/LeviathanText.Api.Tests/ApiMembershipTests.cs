using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LeviathanText.Contracts;
using LeviathanText.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

    private sealed class MembershipFactory(bool enabled = true) : WebApplicationFactory<Program>
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
                    ["LeviathanText:Auth:ProjectMembership"] = enabled.ToString(),
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Project_job_reads_events_and_cancellation_follow_membership(bool enabled)
    {
        using var factory = new MembershipFactory(enabled);
        using var admin = WithKey(factory.CreateClient(), AdminKey);
        var (_, ownerKey) = await CreateUser(admin, "job-owner", "annotator");
        var (peerId, peerKey) = await CreateUser(admin, "job-peer", "annotator");
        var (_, outsiderKey) = await CreateUser(admin, "job-outsider", "annotator");
        using var owner = WithKey(factory.CreateClient(), ownerKey);
        using var peer = WithKey(factory.CreateClient(), peerKey);
        using var outsider = WithKey(factory.CreateClient(), outsiderKey);
        using var created = await owner.PostAsJsonAsync("/api/v1/projects", new { name = "job-private" });
        var projectId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        using var granted = await admin.PostAsJsonAsync($"/api/v1/projects/{projectId}/members", new { userId = peerId });
        Assert.Equal(HttpStatusCode.OK, granted.StatusCode);
        using var submitted = await owner.PostAsJsonAsync("/api/v1/jobs",
            new { type = "layout.project", ownerId = "ignored", projectId });
        var jobId = (await submitted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        using var globalSubmitted = await owner.PostAsJsonAsync("/api/v1/jobs", new { type = "noop", ownerId = "ignored" });
        var globalId = (await globalSubmitted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        var expected = enabled ? HttpStatusCode.Forbidden : HttpStatusCode.OK;
        using var read = await outsider.GetAsync($"/api/v1/jobs/{jobId}");
        Assert.Equal(expected, read.StatusCode);
        foreach (var suffix in new[] { "", "?status=Pending" })
        {
            using var listed = JsonDocument.Parse(await outsider.GetStringAsync("/api/v1/jobs" + suffix));
            var ids = listed.RootElement.EnumerateArray().Select(row => row.GetProperty("id").GetString()).ToList();
            Assert.Equal(!enabled, ids.Contains(jobId));
            Assert.Contains(globalId, ids);
        }
        using var eventsDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using (var events = await outsider.GetAsync($"/api/v1/jobs/{jobId}/events",
                   HttpCompletionOption.ResponseHeadersRead, eventsDeadline.Token))
            Assert.Equal(expected, events.StatusCode);
        using var deniedCancel = await outsider.PostAsync($"/api/v1/jobs/{jobId}/cancel", null);
        Assert.Equal(expected, deniedCancel.StatusCode);
        using var authorizedRead = await peer.GetAsync($"/api/v1/jobs/{jobId}");
        Assert.Equal(HttpStatusCode.OK, authorizedRead.StatusCode);
        var beforePeerCancel = await authorizedRead.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(enabled ? "pending" : "cancelled", beforePeerCancel.GetProperty("status").GetString());
        using var peerCancel = await peer.PostAsync($"/api/v1/jobs/{jobId}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, peerCancel.StatusCode);
        using var adminRead = await admin.GetAsync($"/api/v1/jobs/{jobId}");
        Assert.Equal(HttpStatusCode.OK, adminRead.StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Authenticated_creators_cannot_receive_another_owners_idempotent_job(bool membershipEnabled)
    {
        using var factory = new MembershipFactory(membershipEnabled);
        using var admin = WithKey(factory.CreateClient(), AdminKey);
        var users = new List<(string Id, HttpClient Client, string ProjectId)>();
        try
        {
            for (var i = 0; i < 2; i++)
            {
                var (id, key) = await CreateUser(admin, $"creator-{i}", "annotator");
                var client = WithKey(factory.CreateClient(), key);
                using var project = await client.PostAsJsonAsync("/api/v1/projects", new { name = $"creator-project-{i}" });
                var projectId = (await project.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
                users.Add((id, client, projectId));
            }

            var requests = users.Select(async user =>
            {
                using var response = await user.Client.PostAsJsonAsync("/api/v1/jobs", new
                {
                    type = "layout.project", ownerId = "ignored", projectId = user.ProjectId, idempotencyKey = "racing-creators",
                });
                return (user.Id, response.StatusCode, Body: await response.Content.ReadAsStringAsync());
            });
            var replies = await Task.WhenAll(requests);

            var winner = Assert.Single(replies, reply => reply.StatusCode == HttpStatusCode.OK);
            var loser = Assert.Single(replies, reply => reply.StatusCode == HttpStatusCode.Conflict);
            using var saved = JsonDocument.Parse(winner.Body);
            Assert.Equal(winner.Id, saved.RootElement.GetProperty("owner").GetProperty("id").GetString());
            Assert.DoesNotContain(saved.RootElement.GetProperty("id").GetString()!, loser.Body);
            using var duplicate = await users.Single(user => user.Id == winner.Id).Client.PostAsJsonAsync("/api/v1/jobs", new
            {
                type = "layout.project", ownerId = "ignored", projectId = users.Single(user => user.Id == winner.Id).ProjectId,
                idempotencyKey = "racing-creators",
            });
            Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
            Assert.Equal(winner.Body, await duplicate.Content.ReadAsStringAsync());
            using var administrative = await admin.PostAsJsonAsync("/api/v1/jobs", new
            {
                type = "layout.project", ownerId = "ignored", projectId = users[0].ProjectId,
                idempotencyKey = "racing-creators",
            });
            Assert.Equal(HttpStatusCode.OK, administrative.StatusCode);
            Assert.Equal(winner.Body, await administrative.Content.ReadAsStringAsync());
        }
        finally
        {
            foreach (var user in users) user.Client.Dispose();
        }
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
        Assert.Equal(HttpStatusCode.Forbidden, (await userA.PostAsJsonAsync($"/api/v1/projects/{project["id"]}/import-page",
            new { pageXmlPath = fixture })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/projects/{project["id"]}/import-page",
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Durable_project_jobs_apply_the_opt_in_membership_gate_before_enqueue(bool membershipEnabled)
    {
        using var factory = new MembershipFactory(membershipEnabled);
        using var admin = WithKey(factory.CreateClient(), AdminKey);
        var (ownerId, ownerKey) = await CreateUser(admin, "owner", "annotator");
        var (_, outsiderKey) = await CreateUser(admin, "outsider", "annotator");
        var (_, readerKey) = await CreateUser(admin, "reader", "reader");
        using var owner = WithKey(factory.CreateClient(), ownerKey);
        using var outsider = WithKey(factory.CreateClient(), outsiderKey);
        using var reader = WithKey(factory.CreateClient(), readerKey);

        using var created = await owner.PostAsJsonAsync("/api/v1/projects", new { name = "job-scope" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var projectId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        var fixture = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "LeviathanText.Formats.Tests", "Fixtures", "sample.page.xml"));
        using var imported = await admin.PostAsJsonAsync($"/api/v1/projects/{projectId}/import-page",
            new { pageXmlPath = fixture });
        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
        var surfaceId = (await imported.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("surfaceId").GetString()!;

        var expected = membershipEnabled ? HttpStatusCode.Forbidden : HttpStatusCode.OK;
        using var caseVariant = await outsider.GetAsync($"/API/V1/PROJECTS/{projectId}");
        Assert.Equal(expected, caseVariant.StatusCode);
        if (membershipEnabled)
        {
            using var directLayout = await outsider.PostAsync($"/API/V1/SURFACES/{surfaceId}/DETECT-LAYOUT", null);
            Assert.Equal(HttpStatusCode.Forbidden, directLayout.StatusCode);
        }

        var requests = new CreateJobRequest[]
        {
            new("layout.detect", "spoof", null, SurfaceId: surfaceId),
            new("layout.project", "spoof", null, ProjectId: projectId),
            new("recognize.surface", "spoof", null, SurfaceId: surfaceId, ModelPackagePath: "/fixture/package"),
            new("recognize.project", "spoof", null, ProjectId: projectId, ModelPackagePath: "/fixture/package"),
        };
        foreach (var request in requests)
        {
            using var response = await outsider.PostAsJsonAsync("/api/v1/jobs", request);
            Assert.Equal(expected, response.StatusCode);
        }

        var queued = await admin.GetFromJsonAsync<JsonElement[]>("/api/v1/jobs");
        Assert.Equal(membershipEnabled ? 0 : requests.Length, queued?.Length);
        using var readOnlyRole = await reader.PostAsJsonAsync("/api/v1/jobs", requests[0]);
        Assert.Equal(HttpStatusCode.Forbidden, readOnlyRole.StatusCode);

        var keyed = requests[0] with { IdempotencyKey = "bound-layout-job" };
        using var permitted = await owner.PostAsJsonAsync("/api/v1/jobs", keyed);
        Assert.Equal(HttpStatusCode.OK, permitted.StatusCode);
        var saved = await permitted.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(ownerId, saved.GetProperty("owner").GetProperty("id").GetString());
        Assert.Equal(membershipEnabled, saved.TryGetProperty("boundProjectId", out var binding));
        if (membershipEnabled)
        {
            Assert.Equal(projectId, binding.GetString());
            using var rejectedDuplicate = await outsider.PostAsJsonAsync("/api/v1/jobs", keyed);
            Assert.Equal(HttpStatusCode.Forbidden, rejectedDuplicate.StatusCode);
        }

        using var administrative = await admin.PostAsJsonAsync("/api/v1/jobs", requests[1]);
        Assert.Equal(HttpStatusCode.OK, administrative.StatusCode);
    }

    [Fact]
    public async Task Ambiguous_indirect_ids_fail_closed_while_explicit_project_access_stays_available()
    {
        using var factory = new MembershipFactory();
        using var admin = WithKey(factory.CreateClient(), AdminKey);
        var (_, memberKey) = await CreateUser(admin, "member", "annotator");
        using var member = WithKey(factory.CreateClient(), memberKey);
        using var created = await member.PostAsJsonAsync("/api/v1/projects", new { name = "z-member" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var projectId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        var fixture = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "LeviathanText.Formats.Tests", "Fixtures", "sample.page.xml"));
        using var imported = await admin.PostAsJsonAsync($"/api/v1/projects/{projectId}/import-page",
            new { pageXmlPath = fixture });
        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);

        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<LocalStore>();
        var originalId = EntityId.Parse(projectId, EntityKind.Collection);
        var original = store.LoadProject(originalId)!;
        var decoyId = new EntityId(EntityKind.Collection, "decoy");
        var decoyVersion = store.SaveProject(original with { Id = decoyId, Name = "a-decoy", Version = 0 });
        var surface = Assert.Single(Assert.Single(original.Objects).Surfaces);
        var lineId = surface.Lines[0].Id;
        using var explicitOwner = await member.GetAsync($"/api/v1/projects/{originalId}/surfaces/{surface.Id}/diplomatic");
        using var explicitDecoy = await member.GetAsync($"/api/v1/projects/{decoyId}");
        Assert.Equal(HttpStatusCode.OK, explicitOwner.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, explicitDecoy.StatusCode);

        using var read = await member.GetAsync($"/api/v1/lines/{lineId}");
        using var edit = await member.PostAsJsonAsync($"/api/v1/lines/{lineId}/revisions",
            new { layer = "L1Diplomatic", text = "ambiguous write", authorId = "member" });
        using var enqueue = await member.PostAsJsonAsync("/api/v1/jobs",
            new CreateJobRequest("layout.detect", "ignored", null, SurfaceId: surface.Id.ToString()));
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, enqueue.StatusCode);
        Assert.Equal(original.Version, store.LoadProject(originalId)!.Version);
        Assert.Equal(decoyVersion, store.LoadProject(decoyId)!.Version);
        Assert.Empty(store.LoadJobs());

        var legacyJob = new JobRecord
        {
            Id = new EntityId(EntityKind.Job, "legacy-surface"),
            Type = "layout.detect",
            Inputs = [new StageArtifact(surface.Id, new Checksum { Algorithm = "surface", Digest = "" })],
            Owner = new AgentRef { Id = "archived-creator", IsModel = false },
            Status = JobStatus.Pending,
        };
        var boundJob = legacyJob with { Id = new EntityId(EntityKind.Job, "bound-surface"), BoundProjectId = originalId };
        store.SaveJob(legacyJob);
        store.SaveJob(boundJob);
        using var legacyRead = await member.GetAsync($"/api/v1/jobs/{legacyJob.Id}");
        using var boundRead = await member.GetAsync($"/api/v1/jobs/{boundJob.Id}");
        using var legacyCancel = await member.PostAsync($"/api/v1/jobs/{legacyJob.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.Forbidden, legacyRead.StatusCode);
        Assert.Equal(HttpStatusCode.OK, boundRead.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, legacyCancel.StatusCode);
        Assert.Equal(JobStatus.Pending, store.LoadJob(legacyJob.Id)!.Status);

        using var adminRead = await admin.GetAsync($"/api/v1/lines/{lineId}");
        Assert.Equal(HttpStatusCode.OK, adminRead.StatusCode);
        using var removed = await admin.DeleteAsync($"/api/v1/projects/{decoyId}");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        using var unique = await member.GetAsync($"/api/v1/lines/{lineId}");
        Assert.Equal(HttpStatusCode.OK, unique.StatusCode);
        using var uniqueLegacy = await member.GetAsync($"/api/v1/jobs/{legacyJob.Id}");
        Assert.Equal(HttpStatusCode.OK, uniqueLegacy.StatusCode);
        using var missing = await member.GetAsync("/api/v1/lines/ln-not-present");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var missingEvents = await member.GetAsync("/api/v1/jobs/job-not-present/events",
            HttpCompletionOption.ResponseHeadersRead, deadline.Token);
        Assert.Equal(HttpStatusCode.NotFound, missingEvents.StatusCode);

        store.DeleteProject(originalId);
        using var removedProjectJob = await member.GetAsync($"/api/v1/jobs/{boundJob.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, removedProjectJob.StatusCode);
    }
}
