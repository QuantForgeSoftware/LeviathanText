using LeviathanText.Api;
using LeviathanText.AI;
using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Contracts.Serialization;
using LeviathanText.Core.Jobs;
using LeviathanText.Core.Revisions;
using LeviathanText.Formats;
using LeviathanText.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Net.ServerSentEvents;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers().AddJsonOptions(options =>
{
    // Contract conventions become the MVC serializer settings.
    var contract = ContractJson.Options;
    options.JsonSerializerOptions.PropertyNamingPolicy = contract.PropertyNamingPolicy;
    options.JsonSerializerOptions.NumberHandling = contract.NumberHandling;
    foreach (var converter in contract.Converters)
        options.JsonSerializerOptions.Converters.Add(converter);
});
builder.Services.AddSingleton<IProjectStore>(sp => new LocalStore(builder.Configuration["LeviathanText:StorePath"] ?? "leviathan.db"));
builder.Services.AddSingleton(sp => (LocalStore)sp.GetRequiredService<IProjectStore>());
builder.Services.AddSingleton<ProjectService>();
builder.Services.AddSingleton<InterchangeService>();
builder.Services.AddSingleton<TranscriptionService>();
builder.Services.AddSingleton(sp => new RecognitionPipeline(sp.GetRequiredService<IProjectStore>()));
builder.Services.AddSingleton<JobTable>();
builder.Services.AddSingleton<JobEventBus>();
builder.Services.AddSingleton<JobEnqueuer>();
builder.Services.AddSingleton<RevisionChain>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton(sp =>
{
    var settings = builder.Configuration.GetSection("LeviathanText:AiProviders").GetChildren()
        .Select(s => s.Get<AiProviderSettings>()!)
        .Where(s => s is not null && s.ProviderId is not null);
    return new AiProviderRegistry(settings, new HttpClient(), new EnvironmentCredentialSource());
});
builder.Services.AddSingleton<BulkProposalAcceptanceService>();

var app = builder.Build();
app.MapControllers();

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

// Transcription writes (Phase 2): layer-respecting revisions with revision-based undo,
// and scholarly annotations with stale-anchor rejection.
api.MapGet("lines/{lineId}", (ProjectService projects, TranscriptionService transcription, string lineId) =>
{
    var lineEntityId = EntityId.Parse(lineId, EntityKind.Line);
    var project = projects.LoadProjectForLine(lineEntityId)
        ?? throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = lineId, Message = "Line not found in any project." });
    var history = transcription.RevisionHistory(project, lineEntityId, TextLayer.L1Diplomatic);
    return Results.Ok(new
    {
        line = lineId,
        diplomatic = history.LastOrDefault()?.Text,
        history = history.Select(r => new { id = r.Id.ToString(), text = r.Text, author = r.Author.Id, at = r.CreatedAt }),
    });
});

api.MapPost("lines/{lineId}/revisions", (ProjectService projects, TranscriptionService transcription, string lineId, [FromBody] AppendRevisionRequest request) =>
{
    var lineEntityId = EntityId.Parse(lineId, EntityKind.Line);
    var project = projects.LoadProjectForLine(lineEntityId)
        ?? throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = lineId, Message = "Line not found in any project." });

    var (_, surface, revision) = transcription.AppendLineRevision(project, lineEntityId, request.Layer,
        request.Text,
        new AgentRef { Id = request.AuthorId, IsModel = false },
        string.IsNullOrEmpty(request.ParentRevisionId) ? null : EntityId.Parse(request.ParentRevisionId, EntityKind.TranscriptionRevision));

    return Results.Ok(new
    {
        revisionId = revision.Id.ToString(),
        surfaceId = surface.Id.ToString(),
        layer = revision.Layer.ToString(),
    });
});

api.MapGet("lines/{lineId}/revisions", (ProjectService projects, TranscriptionService transcription, string lineId, string? layer) =>
{
    var lineEntityId = EntityId.Parse(lineId, EntityKind.Line);
    var project = projects.LoadProjectForLine(lineEntityId)
        ?? throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = lineId, Message = "Line not found in any project." });
    var history = transcription.RevisionHistory(project, lineEntityId,
        layer is null ? TextLayer.L1Diplomatic : Enum.Parse<TextLayer>(layer));
    return Results.Ok(history.Select(r => new
    {
        id = r.Id.ToString(), layer = r.Layer.ToString(), text = r.Text, author = r.Author.Id, at = r.CreatedAt,
    }));
});

api.MapPost("lines/{lineId}/annotations", (ProjectService projects, TranscriptionService transcription, string lineId, [FromBody] Annotation annotation) =>
{
    var lineEntityId = EntityId.Parse(lineId, EntityKind.Line);
    var project = projects.LoadProjectForLine(lineEntityId)
        ?? throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = lineId, Message = "Line not found in any project." });

    var (_, _, anchored) = transcription.AddAnnotation(project, lineEntityId, annotation);
    return Results.Ok(new
    {
        annotationId = anchored.Id.ToString(),
        anchoredRevisionId = anchored.AnchoredRevisionId.ToString(),
    });
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
public sealed record AppendRevisionRequest(TextLayer Layer, string Text, string AuthorId, string? ParentRevisionId);

/// <summary>Server-side credential resolution: environment variables only, referenced by name in settings (todo §2).</summary>
file sealed class EnvironmentCredentialSource : IAiCredentialSource
{
    public string? Resolve(string reference) => Environment.GetEnvironmentVariable(reference);
}

public partial class Program;
