using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;
using LeviathanText.Contracts;
using LeviathanText.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LeviathanText.Api.Tests;

public sealed partial class ExternalWorkerEventTests
{
    [Theory]
    [InlineData("rotated-user-key")] [InlineData("disabled-user")]
    [InlineData("bootstrap-rotated")] [InlineData("bootstrap-cleared")]
    [InlineData("local-bootstrap-activated")]
    public async Task Live_job_stream_closes_after_current_credential_or_local_mode_is_revoked(string change)
    {
        bool local = change == "local-bootstrap-activated";
        using var factory = new Factory(authenticated: !local); using var client = factory.CreateClient();
        string? actor = null;
        if (change.StartsWith("bootstrap", StringComparison.Ordinal)) client.DefaultRequestHeaders.Add("X-Api-Key", "generated-job-bootstrap");
        else if (!local)
        {
            using var scope = factory.Services.CreateScope(); var store = scope.ServiceProvider.GetRequiredService<LocalStore>();
            actor = store.CreateUser("stream-owner", UserRole.Annotator, UserKeys.Hash("generated-job-member")).Id;
            client.DefaultRequestHeaders.Add("X-Api-Key", "generated-job-member");
        }
        using var created = await client.PostAsJsonAsync("/api/v1/jobs", new { type = "noop", ownerId = "generated" });
        created.EnsureSuccessStatusCode(); var id = EntityId.Parse((await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        using var response = await client.GetAsync($"/api/v1/jobs/{id}/events", HttpCompletionOption.ResponseHeadersRead, deadline.Token);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(deadline.Token));
        Assert.Equal("job.created", (await ReadEvent(reader, deadline.Token)).Kind);
        using (var writer = LocalStore.OpenExisting(factory.Database))
        {
            if (change == "rotated-user-key") writer.UpdateUserKey(actor!, UserKeys.Hash("replacement-job-key"));
            if (change == "disabled-user") writer.DisableUser(actor!);
        }
        if (change.StartsWith("bootstrap", StringComparison.Ordinal) || local)
            factory.Services.GetRequiredService<IConfiguration>()["LeviathanText:Auth:ApiKey"] = change == "bootstrap-cleared" ? null : "replacement-bootstrap";
        factory.Services.GetRequiredService<JobEventBus>().Publish(id, "probe", "must-not-be-disclosed-after-revocation");
        var remainder = await reader.ReadToEndAsync(deadline.Token);
        Assert.DoesNotContain("must-not-be-disclosed", remainder);
        Assert.Equal(0, factory.Services.GetRequiredService<JobEventBus>().InspectRetention().Subscribers);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Actual_HTTP_old_or_future_cursor_has_empty_id_resync_then_authoritative_terminal_snapshot(bool restarted)
    {
        var bus = new JobEventBus(new() { MaxEventsPerStream = 2 });
        using var factory = new Factory(bus: bus); using var client = factory.CreateClient();
        using var created = await client.PostAsJsonAsync("/api/v1/jobs", new { type = "noop", ownerId = "generated" });
        var id = EntityId.Parse((await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!);
        for (int i = 0; i < 5; i++) bus.Publish(id, "probe", "old-private" + i);
        using (var writer = LocalStore.OpenExisting(factory.Database)) writer.UpdateJob(id, job => job with { Status = JobStatus.Succeeded, Progress = 1 });
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/jobs/{id}/events");
        request.Headers.Add("Last-Event-ID", restarted ? "10000" : "1");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync(deadline.Token);
        Assert.Contains("event: job.resync-required", raw);
        Assert.Contains("id: \n", raw.Replace("\r\n", "\n"));
        Assert.DoesNotContain("old-private", raw);
        Assert.Contains("event: job.completed", raw);
        using var parsed = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(raw)));
        var gap = await ReadEvent(parsed, deadline.Token); var terminal = await ReadEvent(parsed, deadline.Token);
        Assert.Equal(JobEventBus.ResyncKind, gap.Kind); Assert.Equal(string.Empty, gap.Id);
        Assert.Equal("succeeded", JsonDocument.Parse(terminal.Data).RootElement.GetProperty("status").GetString());
        Assert.True(raw.IndexOf("job.resync-required", StringComparison.Ordinal) < raw.IndexOf("job.completed", StringComparison.Ordinal));
        Assert.Equal(0, bus.InspectRetention().Subscribers);
    }

    [Fact]
    public async Task A_restarted_API_clears_old_process_cursor_and_observes_the_same_SQLite_job()
    {
        var root = Directory.CreateTempSubdirectory("lt-job-restart-").FullName;
        try
        {
            var database = Path.Combine(root, "shared.db"); EntityId id; long cursor;
            using (var first = new Factory(database: database))
            using (var client = first.CreateClient())
            {
                using var created = await client.PostAsJsonAsync("/api/v1/jobs", new { type = "noop", ownerId = "generated" });
                id = EntityId.Parse((await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!);
                cursor = first.Services.GetRequiredService<JobEventBus>().Publish(id, "probe", "old-process-event").Sequence;
                using var writer = LocalStore.OpenExisting(database); writer.UpdateJob(id, job => job with { Status = JobStatus.Succeeded });
            }
            using var restarted = new Factory(database: database); using var next = restarted.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/jobs/{id}/events");
            request.Headers.Add("Last-Event-ID", cursor.ToString(System.Globalization.CultureInfo.InvariantCulture));
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var response = await next.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(deadline.Token));
            var gap = await ReadEvent(reader, deadline.Token); Assert.Equal(JobEventBus.ResyncKind, gap.Kind); Assert.Equal(string.Empty, gap.Id);
            var terminal = await ReadEvent(reader, deadline.Token); Assert.Equal("job.completed", terminal.Kind); Assert.Null(terminal.Id);
            Assert.DoesNotContain("old-process-event", terminal.Data); Assert.Equal("", await reader.ReadToEndAsync(deadline.Token));
        }
        finally { Directory.Delete(root, true); }
    }
}
