using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LeviathanText.Contracts;
using LeviathanText.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.TestHost;

namespace LeviathanText.Api.Tests;

public sealed partial class ExternalWorkerEventTests
{
    [Fact]
    public async Task Anonymous_stream_closes_when_the_first_account_enables_authentication()
    {
        using var factory = new Factory(); using var client = factory.CreateClient();
        using var created = await client.PostAsJsonAsync("/api/v1/jobs", new { type = "noop", ownerId = "local" });
        var id = EntityId.Parse((await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        using var response = await client.GetAsync($"/api/v1/jobs/{id}/events", HttpCompletionOption.ResponseHeadersRead, deadline.Token);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(deadline.Token));
        Assert.Equal("job.created", (await ReadEvent(reader, deadline.Token)).Kind);
        using var writer = LocalStore.OpenExisting(factory.Database);
        _ = writer.CreateUser("new-admin", UserRole.Admin, UserKeys.Hash("generated-fixture-key"));
        factory.Services.GetRequiredService<LeviathanText.Api.JobEventBus>().Publish(id, "probe", "must-not-be-disclosed");
        var remainder = await reader.ReadToEndAsync(deadline.Token);
        Assert.DoesNotContain("must-not-be-disclosed", remainder);
    }

    [Fact]
    public async Task Stream_observes_independent_store_writer_progress_and_completion()
    {
        using var factory = new Factory(); using var client = factory.CreateClient();
        using var created = await client.PostAsJsonAsync("/api/v1/jobs", new { type = "noop", ownerId = "test" });
        var id = EntityId.Parse((await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        using var response = await client.GetAsync($"/api/v1/jobs/{id}/events", HttpCompletionOption.ResponseHeadersRead, deadline.Token);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(deadline.Token));
        Assert.Equal("job.created", (await ReadEvent(reader, deadline.Token)).Kind);
        using var writer = LocalStore.OpenExisting(factory.Database);
        writer.UpdateJob(id, current => current with { Status = JobStatus.Running, Progress = 0.5 });
        var progress = await ReadUntil(reader, "job.progress", deadline.Token);
        Assert.Equal("running", JsonDocument.Parse(progress.Data).RootElement.GetProperty("status").GetString());
        Assert.Null(progress.Id); // Persisted observations have no invented durable event cursor.
        writer.UpdateJob(id, current => current with { Status = JobStatus.Succeeded, Progress = 1 });
        var complete = await ReadUntil(reader, "job.completed", deadline.Token);
        Assert.Equal("succeeded", JsonDocument.Parse(complete.Data).RootElement.GetProperty("status").GetString());
        Assert.Null(complete.Id);
        Assert.Equal("", await reader.ReadToEndAsync(deadline.Token));
    }

    [Theory]
    [InlineData(JobStatus.Succeeded, "job.completed")]
    [InlineData(JobStatus.Failed, "job.failed")]
    [InlineData(JobStatus.Cancelled, "job.cancelled")]
    public async Task Already_terminal_job_is_observed_without_an_in_process_event(JobStatus state, string kind)
    {
        using var factory = new Factory(); using var client = factory.CreateClient();
        _ = await client.GetAsync("/api/v1/jobs"); // Initialize the store, as an earlier API process would.
        using var writer = LocalStore.OpenExisting(factory.Database);
        var id = new EntityId(EntityKind.Job, "persisted-terminal");
        writer.SaveJob(new JobRecord
        {
            Id = id, Type = "noop", Owner = new AgentRef { Id = "test", IsModel = false }, Inputs = [], Status = state,
        });
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var response = await client.GetAsync($"/api/v1/jobs/{id}/events", HttpCompletionOption.ResponseHeadersRead, deadline.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(deadline.Token));
        Assert.Equal(kind, (await ReadEvent(reader, deadline.Token)).Kind);
        Assert.Equal("", await reader.ReadToEndAsync(deadline.Token));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("nonsense")]
    [InlineData("9223372036854775808")]
    public async Task Invalid_replay_cursor_is_rejected_before_opening_a_stream(string cursor)
    {
        using var factory = new Factory(); using var client = factory.CreateClient();
        using var created = await client.PostAsJsonAsync("/api/v1/jobs", new { type = "noop", ownerId = "test" });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/jobs/{id}/events");
        request.Headers.Add("Last-Event-ID", cursor);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Replay_starts_after_the_supplied_cursor()
    {
        using var factory = new Factory(); using var client = factory.CreateClient();
        using var created = await client.PostAsJsonAsync("/api/v1/jobs", new { type = "noop", ownerId = "test" });
        var id = EntityId.Parse((await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!);
        var bus = factory.Services.GetRequiredService<LeviathanText.Api.JobEventBus>();
        var first = bus.Publish(id, "probe", "first"); var second = bus.Publish(id, "probe", "second");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/jobs/{id}/events");
        request.Headers.Add("Last-Event-ID", first.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(deadline.Token));
        var item = await ReadEvent(reader, deadline.Token);
        Assert.Equal("second", item.Data);
        Assert.Equal(second.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture), item.Id);
    }

    private sealed record Event(string Kind, string Data, string? Id);
    private static async Task<Event> ReadUntil(StreamReader reader, string kind, CancellationToken cancellation)
    {
        while (true)
        {
            var item = await ReadEvent(reader, cancellation);
            if (item.Kind == kind) return item;
        }
    }
    private static async Task<Event> ReadEvent(StreamReader reader, CancellationToken cancellation)
    {
        string kind = "message"; string? id = null; var data = new List<string>();
        while (await reader.ReadLineAsync(cancellation) is { } line)
        {
            if (line.Length == 0 && data.Count > 0) return new(kind, string.Join('\n', data), id);
            if (line.StartsWith("event: ")) kind = line[7..];
            if (line.StartsWith("data: ")) data.Add(line[6..]);
            if (line.StartsWith("id: ")) id = line[4..];
        }
        throw new EndOfStreamException("Expected an SSE event.");
    }
    private sealed class Factory(bool authenticated = false, JobEventBus? bus = null, string? database = null) : WebApplicationFactory<Program>
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"lt-worker-events-{Guid.NewGuid():N}");
        public string Database => database ?? Path.Combine(_root, "store.db");
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("LeviathanText:StorePath", Database);
            if (authenticated) builder.UseSetting("LeviathanText:Auth:ApiKey", "generated-job-bootstrap");
            if (bus is not null) builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<JobEventBus>(); services.AddSingleton(bus);
            });
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
    }
}
