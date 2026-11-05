using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LeviathanText.Contracts;
using LeviathanText.Core.Jobs;
using LeviathanText.Core.Revisions;
using LeviathanText.Formats;
using LeviathanText.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace LeviathanText.Api.Tests;

public sealed class SqliteRequestScopeTests
{
    private sealed class StoreFactory(string database, TaskCompletionSource? eventsDisposed = null,
        int maxActivePerOwner = 0) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("LeviathanText:StorePath", database);
            builder.UseSetting("LeviathanText:Auth:ApiKey", "");
            builder.UseSetting("LeviathanText:Jobs:MaxActivePerOwner", maxActivePerOwner.ToString());
            if (eventsDisposed is not null)
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddScoped(_ => new EventScopeProbe(eventsDisposed));
                    services.AddSingleton<IStartupFilter>(new EventScopeProbeFilter());
                });
            }
        }
    }

    private sealed class EventScopeProbe(TaskCompletionSource disposed) : IDisposable
    {
        public void Dispose() => disposed.TrySetResult();
    }

    private sealed class EventScopeProbeFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> configure) => app =>
        {
            app.Use(async (context, next) =>
            {
                if (context.Request.Path.Value?.EndsWith("/events", StringComparison.Ordinal) == true)
                    _ = context.RequestServices.GetRequiredService<EventScopeProbe>();
                await next();
            });
            configure(app);
        };
    }

    [Fact]
    public async Task In_memory_database_survives_request_scopes_and_hosts_remain_isolated()
    {
        using var factory = new StoreFactory(":memory:");
        using var client = factory.CreateClient();
        using var first = factory.Services.CreateScope();
        using var second = factory.Services.CreateScope();
        var one = first.ServiceProvider.GetRequiredService<LocalStore>();
        var two = second.ServiceProvider.GetRequiredService<LocalStore>();
        Assert.NotSame(one, two);
        Assert.NotSame(first.ServiceProvider.GetRequiredService<IProjectStore>(),
            second.ServiceProvider.GetRequiredService<IProjectStore>());
        Assert.Same(first.ServiceProvider.GetRequiredService<JobEventBus>(),
            second.ServiceProvider.GetRequiredService<JobEventBus>());
        Assert.Same(first.ServiceProvider.GetRequiredService<JobTable>(),
            second.ServiceProvider.GetRequiredService<JobTable>());
        Assert.Same(first.ServiceProvider.GetRequiredService<RevisionChain>(),
            second.ServiceProvider.GetRequiredService<RevisionChain>());

        using var created = await client.PostAsJsonAsync("/api/v1/projects", new { name = "first host" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("first host", Assert.Single(two.ListProjects()).Name);
        var jobId = await SeedJob(client, 0);
        using var cancelled = await client.PostAsync($"/api/v1/jobs/{jobId}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        var job = Assert.Single(one.LoadJobs());
        var events = first.ServiceProvider.GetRequiredService<JobEventBus>().Replay(job.Id, 0);
        Assert.Equal(["job.created", "job.cancelled"], events.Select(entry => entry.Kind));

        using var otherFactory = new StoreFactory(":memory:");
        using var other = otherFactory.CreateClient();
        Assert.Empty(await other.GetFromJsonAsync<JsonElement[]>("/api/v1/projects") ?? []);
        Assert.Empty(await other.GetFromJsonAsync<JsonElement[]>("/api/v1/jobs") ?? []);
    }

    [Fact]
    public async Task Parallel_project_creation_and_job_cancellation_keep_each_result()
    {
        var directory = Directory.CreateTempSubdirectory("lt-api-request-store-");
        var database = Path.Combine(directory.FullName, "store.db");
        try
        {
            using var factory = new StoreFactory(database);
            using var client = factory.CreateClient();
            const int jobs = 16;
            var ids = new List<string>();
            for (int index = 0; index < jobs; index++) ids.Add(await SeedJob(client, index));

            await Task.WhenAll(ids.SelectMany((jobId, index) => new[]
            {
                Task.Run(async () =>
                {
                    using var response = await client.PostAsJsonAsync("/api/v1/projects", new { name = $"parallel-{index}" });
                    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
                }),
                Task.Run(async () =>
                {
                    using var response = await client.PostAsync($"/api/v1/jobs/{jobId}/cancel", null);
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                }),
            }));

            var projects = await client.GetFromJsonAsync<JsonElement[]>("/api/v1/projects");
            var persistedJobs = await client.GetFromJsonAsync<JsonElement[]>("/api/v1/jobs");
            Assert.Equal(jobs, projects?.Length);
            Assert.Equal(jobs, persistedJobs?.Length);
            Assert.All(persistedJobs!, job => Assert.Equal("cancelled", job.GetProperty("status").GetString()));
        }
        finally
        {
            foreach (var mode in new[] { SqliteOpenMode.ReadWriteCreate, SqliteOpenMode.ReadWrite })
            {
                using var poolKey = new SqliteConnection(new SqliteConnectionStringBuilder
                {
                    DataSource = database,
                    Mode = mode,
                }.ToString());
                SqliteConnection.ClearPool(poolKey);
            }
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Disconnecting_an_idle_job_event_stream_disposes_the_request_scope()
    {
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var factory = new StoreFactory(":memory:", disposed);
        using var client = factory.CreateClient();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var jobId = await SeedJob(client, 0);
        try
        {
            using (var response = await client.GetAsync($"/api/v1/jobs/{jobId}/events",
                       HttpCompletionOption.ResponseHeadersRead, deadline.Token))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(deadline.Token));
                var eventType = await reader.ReadLineAsync(deadline.Token);
                while (!string.IsNullOrEmpty(eventType) && !eventType.StartsWith("event:", StringComparison.Ordinal))
                    eventType = await reader.ReadLineAsync(deadline.Token);
                Assert.NotNull(eventType);
                Assert.StartsWith("event:", eventType);
                Assert.Equal("job.created", eventType["event:".Length..].Trim());
            }
            await disposed.Task.WaitAsync(TimeSpan.FromSeconds(5), deadline.Token);
        }
        finally
        {
            if (!disposed.Task.IsCompleted)
            {
                // Wake a disconnected baseline iterator before disposing its host.
                factory.Services.GetRequiredService<JobEventBus>()
                    .Publish(EntityId.Parse(jobId, EntityKind.Job), "test.wake", "{}");
            }
        }
    }

    [Fact]
    public async Task Parallel_same_owner_enqueues_return_the_winning_job_and_announce_once()
    {
        using var factory = new StoreFactory(":memory:", maxActivePerOwner: 1);
        using var client = factory.CreateClient();
        var replies = await Task.WhenAll(Enumerable.Range(0, 16).Select(priority => Task.Run(async () =>
        {
            using var response = await client.PostAsJsonAsync("/api/v1/jobs",
                new { type = "noop", ownerId = "one-owner", idempotencyKey = "same-key", priority });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await response.Content.ReadAsStringAsync();
        })));

        var winner = Assert.Single(replies.Distinct(StringComparer.Ordinal));
        using var scope = factory.Services.CreateScope();
        var saved = Assert.Single(scope.ServiceProvider.GetRequiredService<LocalStore>().LoadJobs());
        using var decoded = JsonDocument.Parse(winner);
        Assert.Equal(saved.Id.ToString(), decoded.RootElement.GetProperty("id").GetString());
        var events = scope.ServiceProvider.GetRequiredService<JobEventBus>().Replay(saved.Id, 0);
        Assert.Equal("job.created", Assert.Single(events).Kind);
    }

    private static async Task<string> SeedJob(HttpClient client, int index)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/jobs", new { type = "noop", ownerId = $"test-{index}" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var job = await response.Content.ReadFromJsonAsync<JsonElement>();
        return job.GetProperty("id").GetString()!;
    }
}
