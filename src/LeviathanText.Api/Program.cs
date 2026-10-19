using LeviathanText.Api;
using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Contracts.Serialization;
using LeviathanText.Core.Jobs;
using LeviathanText.Formats;
using LeviathanText.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net.ServerSentEvents;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddSingleton<IProjectStore>(sp => new LocalStore(builder.Configuration["LeviathanText:StorePath"] ?? "leviathan.db"));
builder.Services.AddSingleton(sp => (LocalStore)sp.GetRequiredService<IProjectStore>());
builder.Services.AddSingleton<ProjectService>();
builder.Services.AddSingleton<InterchangeService>();
builder.Services.AddSingleton(sp => new RecognitionPipeline(sp.GetRequiredService<IProjectStore>()));
builder.Services.AddSingleton<JobTable>();
builder.Services.AddSingleton<JobEventBus>();
builder.Services.AddSingleton<JobEnqueuer>();

var app = builder.Build();

// Versioned REST (interchange-and-api.md §2). All bodies use the contract JSON conventions.
var api = app.MapGroup("/api/v1");

api.MapGet("projects", (ProjectService projects) =>
    Results.Ok(projects.List().Select(p => new { id = p.Id.ToString(), name = p.Name })));

api.MapPost("projects", (ProjectService projects, [FromBody] CreateProjectRequest request) =>
{
    var project = projects.Create(request.Name, track: request.Track);
    return Results.Created($"/api/v1/projects/{project.Id}", new { id = project.Id.ToString(), name = project.Name });
});

api.MapGet("projects/{id}", (ProjectService projects, string id) =>
{
    var project = projects.Load(EntityId.Parse(id, EntityKind.Collection));
    return Results.Json(project, ContractJson.Options);
});

api.MapPost("projects/{id}/import-page", (ProjectService projects, string id, [FromBody] ImportPageRequest request) =>
{
    var project = projects.Load(EntityId.Parse(id, EntityKind.Collection));
    var (imported, unsupported) = projects is not null
        ? new InterchangeService(projects).ImportPage(project, request.PageXmlPath)
        : throw new InvalidOperationException();
    return Results.Ok(new
    {
        objectId = imported.Id.ToString(),
        surfaceId = imported.Surfaces[0].Id.ToString(),
        lineCount = imported.Surfaces[0].Lines.Count,
        unsupported,
    });
});

api.MapGet("projects/{id}/surfaces/{surfaceId}/diplomatic", (ProjectService projects, string id, string surfaceId) =>
{
    var project = projects.Load(EntityId.Parse(id, EntityKind.Collection));
    var (text, report) = new InterchangeService(projects).ExportDiplomatic(project, EntityId.Parse(surfaceId, EntityKind.Surface));
    return Results.Ok(new { text, lossReport = report });
});

api.MapGet("projects/{id}/surfaces/{surfaceId}/tei", (ProjectService projects, string id, string surfaceId) =>
{
    var project = projects.Load(EntityId.Parse(id, EntityKind.Collection));
    return Results.Text(
        new InterchangeService(projects).ExportTei(project, EntityId.Parse(surfaceId, EntityKind.Surface), $"LeviathanText export {surfaceId}"),
        "application/xml");
});

// Jobs: durable creation with idempotency keys; progress via server-sent events (todo §4),
// with the job resource remaining authoritative.
api.MapPost("jobs", (JobEnqueuer enqueuer, [FromBody] CreateJobRequest request) =>
{
    var job = enqueuer.Enqueue(request.Type, new AgentRef { Id = request.OwnerId, IsModel = false },
        idempotencyKey: request.IdempotencyKey);
    return Results.Content(ContractJson.ToJson(job), "application/json");
});

api.MapGet("jobs/{id}", (LocalStore store, string id) =>
{
    var job = store.LoadJob(EntityId.Parse(id, EntityKind.Job))
        ?? throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = id, Message = "Unknown job." });
    return Results.Content(ContractJson.ToJson(job), "application/json");
});

api.MapPost("jobs/{id}/cancel", (JobEnqueuer enqueuer, string id) =>
    Results.Content(ContractJson.ToJson(enqueuer.Cancel(EntityId.Parse(id, EntityKind.Job))), "application/json"));

api.MapGet("jobs/{id}/events", (JobEventBus bus, string id) =>
{
    var jobId = EntityId.Parse(id, EntityKind.Job);
    return Results.ServerSentEvents(JobEvents(bus, jobId));
});

static async IAsyncEnumerable<SseItem<string>> JobEvents(JobEventBus bus, EntityId jobId)
{
    // Subscribe first so nothing is missed between replay and live delivery.
    var channel = System.Threading.Channels.Channel.CreateUnbounded<JobEventBus.JobEvent>();
    using var subscription = bus.Subscribe(jobId, evt => channel.Writer.TryWrite(evt));

    long cursor = 0;
    foreach (var evt in bus.Replay(jobId, cursor))
    {
        cursor = evt.Sequence;
        yield return new SseItem<string>(evt.Payload, evt.Kind) { EventId = evt.Sequence.ToString() };
    }

    var reader = channel.Reader;
    while (await reader.WaitToReadAsync().ConfigureAwait(false))
    {
        while (reader.TryRead(out var evt))
        {
            if (evt.Sequence <= cursor) continue;
            cursor = evt.Sequence;
            yield return new SseItem<string>(evt.Payload, evt.Kind) { EventId = evt.Sequence.ToString() };
        }
    }
}

app.MapGet("/", () => Results.Ok(new { service = "LeviathanText", contract = ContractVersion.Current }));

app.Run();

public sealed record CreateProjectRequest(string Name, MaterialTrack? Track);
public sealed record ImportPageRequest(string PageXmlPath);
public sealed record CreateJobRequest(string Type, string OwnerId, string? IdempotencyKey);

public partial class Program;
