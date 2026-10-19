using LeviathanText.Api;
using LeviathanText.AI;
using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Core.Unicode;
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

// Minimal-API endpoints bind and serialize with the SAME contract conventions
// (EntityId, ScalarRange, Confidence, string enums) — otherwise the two surfaces
// disagree on the wire format.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = ContractJson.Options.PropertyNamingPolicy;
    options.SerializerOptions.NumberHandling = ContractJson.Options.NumberHandling;
    foreach (var converter in ContractJson.Options.Converters)
        options.SerializerOptions.Converters.Add(converter);
});
builder.Services.AddSingleton<IProjectStore>(sp => new LocalStore(builder.Configuration["LeviathanText:StorePath"] ?? "leviathan.db"));
builder.Services.AddSingleton(sp => (LocalStore)sp.GetRequiredService<IProjectStore>());
builder.Services.AddSingleton<ProjectService>();
builder.Services.AddSingleton<InterchangeService>();
builder.Services.AddSingleton<TranscriptionService>();
builder.Services.AddSingleton<GeometryService>();
builder.Services.AddSingleton(sp =>
{
    var cache = builder.Configuration["LeviathanText:ImageCache"]
        ?? Path.Combine(Path.GetDirectoryName(builder.Configuration["LeviathanText:StorePath"] ?? "leviathan.db")!, "images");
    return new ImageResolver(sp.GetRequiredService<IProjectStore>(), cache);
});
builder.Services.AddSingleton(sp => new LayoutService(sp.GetRequiredService<IProjectStore>(), sp.GetRequiredService<ImageResolver>()));
builder.Services.AddSingleton(sp => new RecognitionPipeline(sp.GetRequiredService<IProjectStore>(), sp.GetRequiredService<ImageResolver>()));
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

// Map the contract error shape to HTTP semantics: unknown input 404, revision/anchor
// conflicts 409, rejected payload versions 422, everything else 422 or 500 as coded.
app.Use(async (context, next) =>
{
    try { await next(); }
    catch (LeviathanException ex)
    {
        var status = ex.Error.Code switch
        {
            ErrorCode.InputMissing => 404,
            ErrorCode.RevisionConflict or ErrorCode.StaleAnnotationAnchor => 409,
            ErrorCode.ChecksumMismatch or ErrorCode.FrameUnregistered or ErrorCode.ProfileVersionUnsupported
                or ErrorCode.SchemaVersionUnsupported or ErrorCode.LossyExportRequiredReport or ErrorCode.UnknownLabel
                or ErrorCode.ModelIncompatible or ErrorCode.StageContractViolation => 422,
            _ => 400,
        };
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(ContractJson.ToJson(ex.Error));
    }
});
// Pilot shared-deployment guard (opt-in). With an API key configured, every /api/v1
// request must present it (Bearer or X-Api-Key) and ReadOnly blocks non-GET methods.
// Without a key the server is LOCAL-ONLY: no accounts, roles or audit — do not expose it.
var authApiKey = builder.Configuration["LeviathanText:Auth:ApiKey"];
var authReadOnly = builder.Configuration.GetValue<bool>("LeviathanText:Auth:ReadOnly");
if (string.IsNullOrEmpty(authApiKey))
    app.Logger.LogWarning("authentication disabled: local-only deployment (set LeviathanText:Auth:ApiKey before sharing)");

app.Use(async (context, next) =>
{
    if (string.IsNullOrEmpty(authApiKey) || !context.Request.Path.StartsWithSegments("/api/v1"))
    {
        await next();
        return;
    }

    var provided = context.Request.Headers.Authorization.ToString();
    if (provided.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        provided = provided["Bearer ".Length..].Trim();
    if (string.IsNullOrEmpty(provided))
        provided = context.Request.Headers["X-Api-Key"].ToString();

    var matches = !string.IsNullOrEmpty(provided)
        && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(provided),
            System.Text.Encoding.UTF8.GetBytes(authApiKey));
    if (!matches)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { code = "unauthorized", message = "Missing or invalid API key." });
        return;
    }

    if (authReadOnly && !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { code = "read_only", message = "This deployment is read-only." });
        return;
    }

    await next();
});

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

    // Validate the path up front with an actionable 400 instead of a raw 500:
    // local imports take an absolute path (the web workspace will upload files instead).
    if (!Path.IsPathRooted(request.PageXmlPath) || !File.Exists(request.PageXmlPath))
        return Results.BadRequest(new LeviathanError
        {
            Code = ErrorCode.InputMissing,
            Scope = request.PageXmlPath,
            Message = "Page XML path must be absolute and readable on the server host.",
        });

    var (imported, unsupported) = new InterchangeService(projects).ImportPage(project, request.PageXmlPath, request.Kind ?? "page");
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

api.MapGet("projects/{id}/surfaces/{surfaceId}/alto", (ProjectService projects, string id, string surfaceId) =>
{
    var project = projects.Load(EntityId.Parse(id, EntityKind.Collection));
    return Results.Text(
        new InterchangeService(projects).ExportAlto(project, EntityId.Parse(surfaceId, EntityKind.Surface)),
        "application/xml");
});

api.MapGet("projects/{id}/surfaces/{surfaceId}/hocr", (ProjectService projects, string id, string surfaceId) =>
{
    var project = projects.Load(EntityId.Parse(id, EntityKind.Collection));
    return Results.Text(
        new InterchangeService(projects).ExportHocr(project, EntityId.Parse(surfaceId, EntityKind.Surface)),
        "text/html");
});

api.MapGet("projects/{id}/surfaces/{surfaceId}/tei", (ProjectService projects, string id, string surfaceId) =>
{
    var project = projects.Load(EntityId.Parse(id, EntityKind.Collection));
    return Results.Text(
        new InterchangeService(projects).ExportTei(project, EntityId.Parse(surfaceId, EntityKind.Surface), $"LeviathanText export {surfaceId}"),
        "application/xml");
});

api.MapPost("projects/{id}/import-image", (ProjectService projects, string id, [FromBody] ImportImageRequest request) =>
{
    var project = projects.Load(EntityId.Parse(id, EntityKind.Collection));
    if (!Path.IsPathRooted(request.ImagePath))
        return Results.BadRequest(new LeviathanError
        {
            Code = ErrorCode.InputMissing,
            Scope = request.ImagePath,
            Message = "Image path must be absolute on the server host.",
        });

    var imported = new InterchangeService(projects).ImportImage(project, request.ImagePath, request.Kind ?? "page");
    return Results.Ok(new
    {
        objectId = imported.Id.ToString(),
        surfaceId = imported.Surfaces[0].Id.ToString(),
        captureId = imported.Surfaces[0].Captures[0].Id.ToString(),
    });
});

api.MapPost("projects/{id}/import-legacy", (ProjectService projects, string id, [FromBody] ImportLegacyRequest request) =>
{
    var project = projects.Load(EntityId.Parse(id, EntityKind.Collection));
    if (!Path.IsPathRooted(request.DocumentPath) || !Path.IsPathRooted(request.ImagePath))
        return Results.BadRequest(new LeviathanError
        {
            Code = ErrorCode.InputMissing,
            Scope = "import-legacy",
            Message = "Document and image paths must be absolute on the server host.",
        });

    var (imported, unsupported) = new InterchangeService(projects)
        .ImportLegacyLayout(project, request.DocumentPath, request.ImagePath, request.Format, request.Kind ?? "page");
    return Results.Ok(new
    {
        objectId = imported.Id.ToString(),
        surfaceId = imported.Surfaces[0].Id.ToString(),
        lineCount = imported.Surfaces[0].Lines.Count,
        unsupported,
    });
});

api.MapPost("projects/{id}/import-iiif", (ProjectService projects, string id, [FromBody] ImportIiifRequest request) =>
{
    var project = projects.Load(EntityId.Parse(id, EntityKind.Collection));
    if (!Path.IsPathRooted(request.ManifestPath) || !File.Exists(request.ManifestPath))
        return Results.BadRequest(new LeviathanError
        {
            Code = ErrorCode.InputMissing,
            Scope = request.ManifestPath,
            Message = "Manifest path must be absolute and readable on the server host.",
        });

    var (imported, manifest, warnings) = new InterchangeService(projects).ImportIiif(project, request.ManifestPath);
    return Results.Ok(new
    {
        objectId = imported.Id.ToString(),
        surfaceId = imported.Surfaces[0].Id.ToString(),
        captureCount = imported.Surfaces[0].Captures.Count,
        manifestLabel = manifest.ManifestLabel,
        rights = manifest.RightsStatement,
        warnings,
    });
});

api.MapPut("lines/{lineId}/geometry", (ProjectService projects, GeometryService geometry, string lineId, [FromBody] LineGeometryRequest request) =>
{
    var lineEntityId = EntityId.Parse(lineId, EntityKind.Line);
    var project = projects.LoadProjectForLine(lineEntityId)
        ?? throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = lineId, Message = "Line not found in any project." });

    var (_, _, line) = geometry.SetLineGeometry(project, lineEntityId, request.Polygon, request.Baseline);
    return Results.Ok(new
    {
        lineId = line.Id.ToString(),
        polygon = line.Polygon,
        baseline = line.Baseline,
    });
});

api.MapPost("surfaces/{surfaceId}/lines", (ProjectService projects, GeometryService geometry, string surfaceId, [FromBody] AddLineRequest request) =>
{
    var surfaceEntityId = EntityId.Parse(surfaceId, EntityKind.Surface);
    var project = projects.LoadProjectForSurface(surfaceEntityId)
        ?? throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = surfaceId, Message = "Surface not found in any project." });

    var (_, _, line) = geometry.AddLine(project, surfaceEntityId, request.Polygon, request.Baseline, request.RegionId);
    return Results.Created($"/api/v1/lines/{line.Id}", new
    {
        lineId = line.Id.ToString(),
        regionId = line.RegionId.ToString(),
        polygon = line.Polygon,
        baseline = line.Baseline,
    });
});

api.MapDelete("lines/{lineId}", (ProjectService projects, GeometryService geometry, string lineId) =>
{
    var lineEntityId = EntityId.Parse(lineId, EntityKind.Line);
    var project = projects.LoadProjectForLine(lineEntityId)
        ?? throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = lineId, Message = "Line not found in any project." });

    geometry.DeleteLine(project, lineEntityId);
    return Results.NoContent();
});

api.MapPut("surfaces/{surfaceId}/reading-order", (ProjectService projects, GeometryService geometry, string surfaceId, [FromBody] ReadingOrderRequest request) =>
{
    var surfaceEntityId = EntityId.Parse(surfaceId, EntityKind.Surface);
    var project = projects.LoadProjectForSurface(surfaceEntityId)
        ?? throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = surfaceId, Message = "Surface not found in any project." });

    var (_, surface) = geometry.SetReadingOrder(project, surfaceEntityId, request.RegionIds);
    return Results.Ok(new
    {
        surfaceId = surface.Id.ToString(),
        order = surface.Regions.OrderBy(r => r.ReadingOrderIndex).Select(r => r.Id.ToString()),
    });
});

api.MapGet("projects/{id}/bundle", (ProjectService projects, string id) =>
{
    var project = projects.Load(EntityId.Parse(id, EntityKind.Collection));
    var path = Path.Combine(Path.GetTempPath(), $"leviathantext-{project.Id}.zip");
    ProjectBundle.Export(project, path);
    var bytes = File.ReadAllBytes(path);
    File.Delete(path);
    return Results.File(bytes, "application/zip", $"{project.Id}.zip");
});

api.MapGet("projects/{id}/profile-report", (ProjectService projects, string id) =>
{
    var project = projects.Load(EntityId.Parse(id, EntityKind.Collection));
    var surfaces = project.Objects.SelectMany(o => o.Surfaces).Select(surface =>
    {
        var revisions = surface.Revisions.ToDictionary(r => r.Id);
        int lines = 0, flagged = 0;
        var issues = new List<object>();
        foreach (var line in surface.Lines)
        {
            if (line.DiplomaticRevisionId is not { } rid || !revisions.TryGetValue(rid, out var revision)) continue;
            lines++;
            var report = GreekTextProfile.Inspect(revision.Text);
            if (report.IsClean) continue;
            flagged++;
            foreach (var issue in report.Issues.Take(20))
                issues.Add(new
                {
                    lineId = line.Id.ToString(),
                    scalarOffset = issue.ScalarOffset,
                    category = issue.Category.ToString(),
                    scalar = issue.Scalar,
                });
        }
        return new
        {
            surfaceId = surface.Id.ToString(),
            lines,
            flagged,
            issues,
        };
    }).ToList();

    return Results.Ok(new
    {
        profile = GreekTextProfile.ProfileId,
        version = GreekTextProfile.Version,
        surfaces,
    });
});

api.MapGet("projects/{id}/training-export", (ProjectService projects, string id) =>
{
    var project = projects.Load(EntityId.Parse(id, EntityKind.Collection));
    return Results.Content(TrainingDataExport.ToJson(TrainingDataExport.Export(project)), "application/json");
});

api.MapPut("regions/{regionId}/line-order", (ProjectService projects, GeometryService geometry, string regionId, [FromBody] LineOrderRequest request) =>
{
    var regionEntityId = EntityId.Parse(regionId, EntityKind.Region);
    var project = projects.LoadProjectForRegion(regionEntityId)
        ?? throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = regionId, Message = "Region not found in any project." });

    var (_, surface) = geometry.SetLineOrder(project, regionEntityId, request.LineIds);
    return Results.Ok(new
    {
        regionId = regionId,
        order = surface.Lines.Where(l => l.RegionId == regionEntityId).Select(l => l.Id.ToString()),
    });
});

api.MapPost("surfaces/{surfaceId}/detect-layout", (ProjectService projects, LayoutService layout, string surfaceId) =>
{
    var surfaceEntityId = EntityId.Parse(surfaceId, EntityKind.Surface);
    var project = projects.LoadProjectForSurface(surfaceEntityId)
        ?? throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = surfaceId, Message = "Surface not found in any project." });

    var outcome = layout.Detect(project, surfaceEntityId);
    return Results.Ok(new
    {
        surfaceId = outcome.Surface.Id.ToString(),
        lineCount = outcome.LineCount,
        method = outcome.Method,
    });
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
        layer is null ? TextLayer.L1Diplomatic : Enum.Parse<TextLayer>(layer, ignoreCase: true));
    return Results.Ok(history.Select(r => new
    {
        id = r.Id.ToString(), layer = r.Layer.ToString(), text = r.Text, author = r.Author.Id, at = r.CreatedAt,
    }));
});

api.MapGet("lines/{lineId}/annotations", (ProjectService projects, TranscriptionService transcription, string lineId) =>
{
    var lineEntityId = EntityId.Parse(lineId, EntityKind.Line);
    var project = projects.LoadProjectForLine(lineEntityId)
        ?? throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = lineId, Message = "Line not found in any project." });
    return Results.Ok(transcription.AnnotationsFor(project, lineEntityId));
});

api.MapPost("lines/{lineId}/annotations", (ProjectService projects, TranscriptionService transcription, string lineId, [FromBody] AddAnnotationRequest request) =>
{
    var lineEntityId = EntityId.Parse(lineId, EntityKind.Line);
    var project = projects.LoadProjectForLine(lineEntityId)
        ?? throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = lineId, Message = "Line not found in any project." });

    var annotation = new Annotation
    {
        Id = string.IsNullOrEmpty(request.Id)
            ? new EntityId(EntityKind.Annotation, Guid.NewGuid().ToString("N")[..12])
            : EntityId.Parse(request.Id, EntityKind.Annotation),
        Kind = request.Kind,
        AnchoredRevisionId = string.IsNullOrEmpty(request.AnchoredRevisionId)
            ? default // null/omitted = bind to the line's current revision (server-side)
            : EntityId.Parse(request.AnchoredRevisionId, EntityKind.TranscriptionRevision),
        Range = request.Range,
        GapExtent = request.GapExtent,
        AlternativeText = request.AlternativeText,
        HandLabel = request.HandLabel,
        Description = request.Description,
    };

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
    // Jobs may carry one input artifact: a surface (layout/recognition) or a project
    // (bulk import). Recognition jobs also name the model package; the worker verifies it
    // before loading, and no path is trusted beyond the worker host's own check.
    IReadOnlyList<StageArtifact>? inputs = null;
    if (!string.IsNullOrEmpty(request.SurfaceId))
    {
        inputs = [new StageArtifact(EntityId.Parse(request.SurfaceId, EntityKind.Surface),
            new Checksum
            {
                Algorithm = request.Type == "recognize.surface" ? "model-package" : "surface",
                Digest = request.Type == "recognize.surface" && !string.IsNullOrEmpty(request.ModelPackagePath)
                    ? "pkg:" + request.ModelPackagePath
                    : "",
            })];
    }
    else if (request.Type == "recognize.project" && !string.IsNullOrEmpty(request.ProjectId) && !string.IsNullOrEmpty(request.ModelPackagePath))
    {
        inputs = [new StageArtifact(EntityId.Parse(request.ProjectId, EntityKind.Collection),
            new Checksum { Algorithm = "model-package", Digest = "pkg:" + request.ModelPackagePath })];
    }
    else if (request.Type == "import.dir" && !string.IsNullOrEmpty(request.ProjectId) && !string.IsNullOrEmpty(request.SourceDirectory))
    {
        inputs = [new StageArtifact(EntityId.Parse(request.ProjectId, EntityKind.Collection),
            new Checksum { Algorithm = "page-xml-directory", Digest = "dir:" + request.SourceDirectory })];
    }

    var job = enqueuer.Enqueue(request.Type, new AgentRef { Id = request.OwnerId, IsModel = false },
        inputs: inputs, idempotencyKey: request.IdempotencyKey);
    return Results.Content(ContractJson.ToJson(job), "application/json");
});

api.MapGet("jobs", (LocalStore store, string? status) =>
{
    if (string.IsNullOrEmpty(status))
        return Results.Ok(store.LoadJobs());

    if (!Enum.TryParse<JobStatus>(status, ignoreCase: true, out var parsed))
        throw new LeviathanException(new LeviathanError
        {
            Code = ErrorCode.StageContractViolation,
            Scope = status,
            Message = "Unknown job status filter.",
        });
    return Results.Ok(store.LoadJobsByStatus(parsed));
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
public sealed record ImportPageRequest(string PageXmlPath, string? Kind = null);
public sealed record ImportIiifRequest(string ManifestPath);

public sealed record ImportLegacyRequest(string DocumentPath, string ImagePath, string Format, string? Kind = null);

public sealed record ImportImageRequest(string ImagePath, string? Kind = null);
public sealed record CreateJobRequest(string Type, string OwnerId, string? IdempotencyKey, string? SurfaceId = null, string? ModelPackagePath = null, string? ProjectId = null, string? SourceDirectory = null);
public sealed record AppendRevisionRequest(TextLayer Layer, string Text, string AuthorId, string? ParentRevisionId);

public sealed record AddAnnotationRequest(
    AnnotationKind Kind,
    ScalarRange Range,
    string? Id = null,
    string? AnchoredRevisionId = null,
    GapExtent? GapExtent = null,
    string? AlternativeText = null,
    string? HandLabel = null,
    string? Description = null);

public sealed record LineGeometryRequest(IReadOnlyList<PointD> Polygon, IReadOnlyList<PointD> Baseline);

public sealed record ReadingOrderRequest(IReadOnlyList<EntityId> RegionIds);

public sealed record LineOrderRequest(IReadOnlyList<EntityId> LineIds);

public sealed record AddLineRequest(IReadOnlyList<PointD> Polygon, IReadOnlyList<PointD> Baseline, EntityId? RegionId = null);

/// <summary>Server-side credential resolution: environment variables only, referenced by name in settings (todo §2).</summary>
file sealed class EnvironmentCredentialSource : IAiCredentialSource
{
    public string? Resolve(string reference) => Environment.GetEnvironmentVariable(reference);
}

public partial class Program;
