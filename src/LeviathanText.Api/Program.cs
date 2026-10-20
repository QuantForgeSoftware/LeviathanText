using LeviathanText.Api;
using LeviathanText.AI;
using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Training;
using LeviathanText.Core.Unicode;
using LeviathanText.Contracts.Serialization;
using LeviathanText.Core.Jobs;
using LeviathanText.Core.Revisions;
using LeviathanText.Formats;
using LeviathanText.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
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
builder.Services.AddSingleton<ReviewService>();
builder.Services.AddSingleton(sp =>
{
    var cache = builder.Configuration["LeviathanText:ImageCache"]
        ?? Path.Combine(Path.GetDirectoryName(builder.Configuration["LeviathanText:StorePath"] ?? "leviathan.db")!, "images");
    return new ImageResolver(sp.GetRequiredService<IProjectStore>(), cache);
});
builder.Services.AddSingleton(sp => new LayoutService(sp.GetRequiredService<IProjectStore>(), sp.GetRequiredService<ImageResolver>()));
builder.Services.AddSingleton<LayoutModelRegistry>();
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
builder.Services.AddSingleton<PresenceService>();

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
    catch (FormatException)
    {
        // Malformed ids are a client error, not a server fault.
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(ContractJson.ToJson(new LeviathanError
        {
            Code = ErrorCode.StageContractViolation,
            Scope = context.Request.Path,
            Message = "Malformed identifier or value in the request.",
        }));
    }
});

// Optional static workspace hosting for packaged releases. The API keeps `/` as the
// service/version endpoint; Angular is served under WebPath (default `/ui`) with an SPA
// fallback for client routes. WebRoot is deliberately opt-in so a bare API does not
// accidentally expose a filesystem directory.
var webRoot = builder.Configuration["LeviathanText:WebRoot"];
var webPath = builder.Configuration["LeviathanText:WebPath"] ?? "/ui";
if (!string.IsNullOrWhiteSpace(webRoot) && Directory.Exists(webRoot))
{
    var fullRoot = Path.GetFullPath(webRoot);
    var provider = new PhysicalFileProvider(fullRoot);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = provider, RequestPath = webPath });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = provider, RequestPath = webPath });

    var indexPath = Path.Combine(fullRoot, "index.html");
    IResult ServeWebFile(string? path)
    {
        var relative = (path ?? string.Empty).TrimStart('/');
        var candidate = Path.GetFullPath(Path.Combine(fullRoot, relative));
        if (!candidate.Equals(fullRoot, StringComparison.Ordinal)
            && !candidate.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            return Results.NotFound();
        if (Directory.Exists(candidate)) candidate = Path.Combine(candidate, "index.html");
        if (File.Exists(candidate))
        {
            var contentTypes = new FileExtensionContentTypeProvider();
            return Results.File(candidate,
                contentTypes.TryGetContentType(candidate, out var contentType) ? contentType : "application/octet-stream");
        }
        return Path.HasExtension(relative)
            ? Results.NotFound()
            : Results.File(indexPath, "text/html");
    }

    if (File.Exists(indexPath))
    {
        app.Use(async (context, next) =>
        {
            if ((!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
                || !context.Request.Path.StartsWithSegments(webPath))
            {
                await next();
                return;
            }

            var relative = context.Request.Path.Value![webPath.Length..];
            var result = ServeWebFile(relative);
            await result.ExecuteAsync(context);
        });
    }
}

// Shared-deployment guard (opt-in). Authentication is required when an admin bootstrap key
// or any user accounts exist. Roles: reader (GET only), annotator (mutations), reviewer
// (+ review decisions), admin (+ user management and project deletion). The configured key is
// an admin bootstrap credential; user keys are stored only as SHA-256 hashes. SSE clients may
// pass ?api_key= because EventSource cannot set headers. Without any credential the server is
// LOCAL-ONLY and logs a warning.
var authApiKey = builder.Configuration["LeviathanText:Auth:ApiKey"];
var authReadOnly = builder.Configuration.GetValue<bool>("LeviathanText:Auth:ReadOnly");
if (string.IsNullOrEmpty(authApiKey))
    app.Logger.LogWarning("no bootstrap API key configured; authentication applies only once user accounts exist");

app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/api/v1"))
    {
        await next();
        return;
    }

    var store = context.RequestServices.GetRequiredService<LocalStore>();
    var hasUsers = store.LoadUsers().Count > 0;
    if (string.IsNullOrEmpty(authApiKey) && !hasUsers)
    {
        // Local-only mode (no credentials configured).
        await next();
        return;
    }

    var provided = context.Request.Headers.Authorization.ToString();
    if (provided.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        provided = provided["Bearer ".Length..].Trim();
    if (string.IsNullOrEmpty(provided))
        provided = context.Request.Headers["X-Api-Key"].ToString();
    if (string.IsNullOrEmpty(provided))
        provided = context.Request.Query["api_key"].ToString();

    UserAccount? principal = null;
    if (!string.IsNullOrEmpty(provided) && !string.IsNullOrEmpty(authApiKey)
        && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(provided),
            System.Text.Encoding.UTF8.GetBytes(authApiKey)))
    {
        principal = new UserAccount
        {
            Id = "usr-bootstrap",
            Name = "bootstrap-admin",
            Role = UserRole.Admin,
            CreatedAt = DateTimeOffset.MinValue,
        };
    }
    else if (!string.IsNullOrEmpty(provided))
    {
        principal = store.FindUserByKeyHash(LeviathanText.Infrastructure.UserKeys.Hash(provided));
    }

    if (principal is null)
    {
        store.AppendAuthAudit(new AuthAuditEntry
        {
            At = DateTimeOffset.UtcNow,
            Action = "unauthorized",
            Method = context.Request.Method,
            Path = context.Request.Path,
            Status = StatusCodes.Status401Unauthorized,
        });
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { code = "unauthorized", message = "Missing or invalid API key." });
        return;
    }

    bool readOnlyRequest = HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method);
    if (authReadOnly && !readOnlyRequest)
    {
        store.AppendAuthAudit(AuditOf(context, principal, "denied", StatusCodes.Status403Forbidden));
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { code = "read_only", message = "This deployment is read-only." });
        return;
    }

    var path = context.Request.Path.Value ?? string.Empty;
    bool adminOnly = IsAdminOnly(path, context.Request.Method);
    bool allowed;
    if (adminOnly)
    {
        allowed = principal.Role == UserRole.Admin;
    }
    else if (readOnlyRequest)
    {
        allowed = true;
    }
    else if (IsReviewDecision(context.Request.Method, path))
    {
        allowed = principal.Role is UserRole.Reviewer or UserRole.Admin;
    }
    else
    {
        allowed = principal.Role is UserRole.Annotator or UserRole.Reviewer or UserRole.Admin;
    }
    if (!allowed)
    {
        store.AppendAuthAudit(AuditOf(context, principal, "denied", StatusCodes.Status403Forbidden));
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { code = "forbidden", message = "This account lacks the required role." });
        return;
    }

    var membershipEnabled = context.RequestServices.GetRequiredService<IConfiguration>()
        .GetValue<bool>("LeviathanText:Auth:ProjectMembership");
    if (membershipEnabled && principal.Role != UserRole.Admin)
    {
        var projectId = ProjectIdForMembership(context, store);
        if (projectId is { } scoped && !store.IsProjectMember(scoped, principal.Id))
        {
            store.AppendAuthAudit(AuditOf(context, principal, "denied", StatusCodes.Status403Forbidden));
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { code = "forbidden", message = "You are not a member of this project." });
            return;
        }
    }

    context.Items["user"] = principal;
    await next();
    if (!readOnlyRequest)
        store.AppendAuthAudit(AuditOf(context, principal, "authenticated", context.Response.StatusCode));
});

static AuthAuditEntry AuditOf(HttpContext context, UserAccount principal, string action, int status) => new()
{
    At = DateTimeOffset.UtcNow,
    UserId = principal.Id,
    UserName = principal.Name,
    Role = principal.Role,
    Action = action,
    Method = context.Request.Method,
    Path = context.Request.Path,
    Status = status,
};

static bool IsReviewDecision(string method, string path) =>
    HttpMethods.IsPost(method) && path.EndsWith("/review", StringComparison.OrdinalIgnoreCase);

static bool IsAdminOnly(string path, string method) =>
    path.StartsWith("/api/v1/users", StringComparison.OrdinalIgnoreCase)
    || path.StartsWith("/api/v1/audit", StringComparison.OrdinalIgnoreCase)
    || path.Contains("/members", StringComparison.OrdinalIgnoreCase)
    || (HttpMethods.IsDelete(method) && path.StartsWith("/api/v1/projects/", StringComparison.OrdinalIgnoreCase));

static EntityId? ProjectIdForMembership(HttpContext context, LocalStore store)
{
    var segments = (context.Request.Path.Value ?? "").Split('/', StringSplitOptions.RemoveEmptyEntries);
    if (segments.Length >= 4 && segments[0] == "api" && segments[1] == "v1" && segments[2] == "projects")
        return EntityId.TryParse(segments[3], EntityKind.Collection, out var projectId) ? projectId : null;

    if (segments.Length >= 4 && segments[0] == "api" && segments[1] == "v1"
        && segments[2] is "lines" or "surfaces" or "regions" or "annotations")
    {
        var kind = segments[2] switch
        {
            "lines" => EntityKind.Line,
            "surfaces" => EntityKind.Surface,
            "regions" => EntityKind.Region,
            _ => EntityKind.Annotation,
        };
        if (EntityId.TryParse(segments[3], kind, out var entity)) return store.FindProjectForEntity(entity);
    }
    return null;
}

app.MapControllers();

// Versioned REST (interchange-and-api.md §2). All bodies use the contract JSON conventions.
var api = app.MapGroup("/api/v1");

api.MapGet("projects", (ProjectService projects, LocalStore store, HttpContext http, IConfiguration configuration) =>
{
    var principal = http.Items["user"] as UserAccount;
    bool scope = configuration.GetValue<bool>("LeviathanText:Auth:ProjectMembership")
                 && principal is not null && principal.Role != UserRole.Admin;
    var rows = projects.List()
        .Where(p => !scope || store.IsProjectMember(p.Id, principal!.Id))
        .Select(p => new { id = p.Id.ToString(), name = p.Name });
    return Results.Ok(rows);
});

api.MapPost("projects", (ProjectService projects, LocalStore store, HttpContext http, [FromBody] CreateProjectRequest request) =>
{
    var project = projects.Create(request.Name, track: request.Track);
    if (http.Items["user"] is UserAccount principal) store.AddProjectMember(project.Id, principal.Id);
    return Results.Created($"/api/v1/projects/{project.Id}", new { id = project.Id.ToString(), name = project.Name });
});

api.MapDelete("projects/{id}", (ProjectService projects, string id) =>
{
    projects.Delete(EntityId.Parse(id, EntityKind.Collection));
    return Results.NoContent();
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

api.MapPost("projects/{id}/import-pdf", (ProjectService projects, string id, [FromBody] ImportPdfRequest request) =>
{
    var project = projects.Load(EntityId.Parse(id, EntityKind.Collection));
    if (!Path.IsPathRooted(request.PdfPath) || !File.Exists(request.PdfPath))
        return Results.BadRequest(new LeviathanError
        {
            Code = ErrorCode.InputMissing,
            Scope = request.PdfPath,
            Message = "PDF path must be absolute and readable on the server host.",
        });

    var outputDirectory = request.OutputDirectory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".leviathantext", "pdf", project.Id.Suffix,
        $"{Path.GetFileNameWithoutExtension(request.PdfPath)}-{Guid.NewGuid().ToString("N")[..8]}");
    if (!Path.IsPathRooted(outputDirectory))
        return Results.BadRequest(new LeviathanError
        {
            Code = ErrorCode.StageContractViolation,
            Scope = outputDirectory,
            Message = "PDF output directory must be absolute on the server host.",
        });

    var (imported, pages, warnings) = new InterchangeService(projects).ImportPdf(
        project, request.PdfPath, outputDirectory, request.Kind ?? "page",
        request.Dpi, request.MaxPages, request.ExtractText);
    return Results.Ok(new
    {
        objectId = imported.Id.ToString(),
        surfaceIds = imported.Surfaces.Select(s => s.Id.ToString()),
        pageCount = pages.Count,
        lineCount = imported.Surfaces.Sum(s => s.Lines.Count),
        outputDirectory,
        warnings,
    });
});

api.MapPost("projects/{id}/import-line-text", (ProjectService projects, IProjectStore store, string id, [FromBody] ImportLineTextRequest request) =>
{
    var project = projects.Load(EntityId.Parse(id, EntityKind.Collection));
    if (string.IsNullOrWhiteSpace(request.CsvText))
        throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = "csv", Message = "CSV text is empty." });

    var result = LineTextImport.Import(project, request.CsvText, request.Layer,
        string.IsNullOrWhiteSpace(request.AuthorId) ? "csv-import" : request.AuthorId,
        new TranscriptionService(store), store);
    return Results.Ok(new
    {
        applied = result.Applied,
        skipped = result.Skipped,
        rows = result.Rows.Take(50),
    });
});

api.MapPost("projects/{id}/import-epidoc", (ProjectService projects, string id, [FromBody] ImportEpiDocRequest request) =>
{
    var project = projects.Load(EntityId.Parse(id, EntityKind.Collection));
    var (imported, notes, imageUrl) = new InterchangeService(projects)
        .ImportEpiDoc(project, request.DocumentPath, request.ImagePath, request.Kind ?? "inscription-face");
    return Results.Ok(new
    {
        objectId = imported.Id.ToString(),
        surfaceId = imported.Surfaces[0].Id.ToString(),
        lineCount = imported.Surfaces[0].Lines.Count,
        imageUrl,
        notes,
    });
});

api.MapPost("surfaces/{surfaceId}/presence", (ProjectService projects, PresenceService presence, string surfaceId,
    [FromBody] PresenceRequest request) =>
{
    var surfaceEntityId = EntityId.Parse(surfaceId, EntityKind.Surface);
    _ = projects.LoadProjectForSurface(surfaceEntityId)
        ?? throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = surfaceId, Message = "Surface not found in any project." });
    return Results.Ok(new { editors = presence.Touch(surfaceEntityId, request.EditorId) });
});

api.MapGet("surfaces/{surfaceId}/presence", (ProjectService projects, PresenceService presence, string surfaceId) =>
{
    var surfaceEntityId = EntityId.Parse(surfaceId, EntityKind.Surface);
    _ = projects.LoadProjectForSurface(surfaceEntityId)
        ?? throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = surfaceId, Message = "Surface not found in any project." });
    return Results.Ok(new { editors = presence.List(surfaceEntityId) });
});

api.MapDelete("surfaces/{surfaceId}/presence/{editorId}", (PresenceService presence, string surfaceId, string editorId) =>
    Results.Ok(new { editors = presence.Leave(EntityId.Parse(surfaceId, EntityKind.Surface), editorId) }));

api.MapGet("surfaces/{surfaceId}/unplaced-lines", (ProjectService projects, string surfaceId) =>
{
    var surfaceEntityId = EntityId.Parse(surfaceId, EntityKind.Surface);
    var project = projects.LoadProjectForSurface(surfaceEntityId)
        ?? throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = surfaceId, Message = "Surface not found in any project." });
    var unplaced = new InterchangeService(projects).UnplacedLines(project, surfaceEntityId);
    return Results.Ok(unplaced.Select((entry, index) => new
    {
        lineId = entry.Line.Id.ToString(),
        order = index,
        referenceText = entry.ReferenceText,
    }));
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

    var (_, _, line) = geometry.SetLineGeometry(project, lineEntityId, request.Polygon, request.Baseline,
        request.AuthorId ?? "api");
    return Results.Ok(new
    {
        lineId = line.Id.ToString(),
        polygon = line.Polygon,
        baseline = line.Baseline,
    });
});

api.MapPut("lines/{lineId}/orientation", (ProjectService projects, GeometryService geometry, string lineId, [FromBody] OrientationRequest request) =>
{
    var lineEntityId = EntityId.Parse(lineId, EntityKind.Line);
    var project = projects.LoadProjectForLine(lineEntityId)
        ?? throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = lineId, Message = "Line not found in any project." });

    var (_, _, line) = geometry.SetLineOrientation(project, lineEntityId,
        request.Direction, request.Script, request.AuthorId ?? "api");
    return Results.Ok(new { lineId = line.Id.ToString(), direction = line.Direction, script = line.Script });
});

api.MapPut("regions/{regionId}/orientation", (ProjectService projects, GeometryService geometry, string regionId, [FromBody] OrientationRequest request) =>
{
    var regionEntityId = EntityId.Parse(regionId, EntityKind.Region);
    var project = projects.LoadProjectForRegion(regionEntityId)
        ?? throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = regionId, Message = "Region not found in any project." });

    var (_, _, region) = geometry.SetRegionOrientation(project, regionEntityId,
        request.Direction, request.Script, request.AuthorId ?? "api");
    return Results.Ok(new { regionId = region.Id.ToString(), direction = region.Direction, script = region.Script });
});

api.MapPost("surfaces/{surfaceId}/lines", (ProjectService projects, GeometryService geometry, string surfaceId, [FromBody] AddLineRequest request) =>
{
    var surfaceEntityId = EntityId.Parse(surfaceId, EntityKind.Surface);
    var project = projects.LoadProjectForSurface(surfaceEntityId)
        ?? throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = surfaceId, Message = "Surface not found in any project." });

    var (_, _, line) = geometry.AddLine(project, surfaceEntityId, request.Polygon, request.Baseline,
        request.RegionId, request.AuthorId ?? "api");
    return Results.Created($"/api/v1/lines/{line.Id}", new
    {
        lineId = line.Id.ToString(),
        regionId = line.RegionId.ToString(),
        polygon = line.Polygon,
        baseline = line.Baseline,
    });
});

api.MapPost("lines/{lineId}/split", (ProjectService projects, GeometryService geometry, string lineId, [FromBody] SplitLineRequest request) =>
{
    var lineEntityId = EntityId.Parse(lineId, EntityKind.Line);
    var project = projects.LoadProjectForLine(lineEntityId)
        ?? throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = lineId, Message = "Line not found in any project." });
    var (_, _, top, bottom) = geometry.SplitLine(project, lineEntityId, request.Y, request.AuthorId ?? "api");
    return Results.Ok(new { topLineId = top.Id.ToString(), bottomLineId = bottom.Id.ToString() });
});

api.MapPost("lines/{lineId}/merge", (ProjectService projects, GeometryService geometry, string lineId, [FromBody] MergeLinesRequest request) =>
{
    var lineEntityId = EntityId.Parse(lineId, EntityKind.Line);
    var project = projects.LoadProjectForLine(lineEntityId)
        ?? throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = lineId, Message = "Line not found in any project." });
    var (_, _, merged) = geometry.MergeLines(project, lineEntityId,
        EntityId.Parse(request.OtherLineId, EntityKind.Line), request.AuthorId ?? "api");
    return Results.Ok(new { lineId = merged.Id.ToString() });
});

api.MapDelete("lines/{lineId}", (ProjectService projects, GeometryService geometry, string lineId, string? author) =>
{
    var lineEntityId = EntityId.Parse(lineId, EntityKind.Line);
    var project = projects.LoadProjectForLine(lineEntityId)
        ?? throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = lineId, Message = "Line not found in any project." });

    geometry.DeleteLine(project, lineEntityId, author ?? "api");
    return Results.NoContent();
});

api.MapGet("surfaces/{surfaceId}/geometry-history", (ProjectService projects, string surfaceId) =>
{
    var surfaceEntityId = EntityId.Parse(surfaceId, EntityKind.Surface);
    var project = projects.LoadProjectForSurface(surfaceEntityId)
        ?? throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = surfaceId, Message = "Surface not found in any project." });
    var surface = project.Objects.SelectMany(o => o.Surfaces).Single(s => s.Id == surfaceEntityId);
    return Results.Ok(surface.GeometryEdits);
});

api.MapPut("surfaces/{surfaceId}/reading-order", (ProjectService projects, GeometryService geometry, string surfaceId, [FromBody] ReadingOrderRequest request) =>
{
    var surfaceEntityId = EntityId.Parse(surfaceId, EntityKind.Surface);
    var project = projects.LoadProjectForSurface(surfaceEntityId)
        ?? throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = surfaceId, Message = "Surface not found in any project." });

    var (_, surface) = geometry.SetReadingOrder(project, surfaceEntityId, request.RegionIds,
        request.AuthorId ?? "api");
    return Results.Ok(new
    {
        surfaceId = surface.Id.ToString(),
        order = surface.Regions.OrderBy(r => r.ReadingOrderIndex).Select(r => r.Id.ToString()),
    });
});

api.MapGet("projects/{id}/annotations.csv", (ProjectService projects, string id) =>
{
    var project = projects.Load(EntityId.Parse(id, EntityKind.Collection));
    return Results.Text(AnnotationCsv.Export(project), "text/csv");
});

api.MapGet("projects/{id}/lines.csv", (ProjectService projects, string id) =>
{
    var project = projects.Load(EntityId.Parse(id, EntityKind.Collection));
    return Results.Text(LineCsv.Export(project), "text/csv");
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

// Model packages are discovered under one configured directory; clients name a package,
// never a path. Listing is read-only and verifies package shape.
string ModelsRoot(IConfiguration configuration) =>
    configuration["LeviathanText:Models:Directory"]
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".leviathantext", "models");

api.MapGet("models", (IConfiguration configuration) =>
{
    var root = ModelsRoot(configuration);
    if (!Directory.Exists(root)) return Results.Ok(Array.Empty<object>());

    var packages = Directory.GetDirectories(root).Select(directory =>
    {
        var manifest = Path.Combine(directory, "run-manifest.json");
        var metadata = Directory.GetFiles(directory, "*.best.json");
        if (!File.Exists(manifest) && metadata.Length != 1) return null;
        return new
        {
            name = Path.GetFileName(directory),
            modifiedAt = Directory.GetLastWriteTimeUtc(directory),
            hasManifest = File.Exists(manifest),
        };
    }).Where(p => p is not null).ToList();
    return Results.Ok(packages);
});

api.MapPost("surfaces/{surfaceId}/recognize", (IConfiguration configuration, ProjectService projects, IProjectStore store, ImageResolver imageResolver, string surfaceId, [FromBody] RecognizeRequest request) =>
{
    var root = ModelsRoot(configuration);
    if (string.IsNullOrWhiteSpace(request.Model) || request.Model.Contains('/') || request.Model.Contains('\\')
        || request.Model.Contains("..", StringComparison.Ordinal))
        throw new LeviathanException(new LeviathanError
        {
            Code = ErrorCode.StageContractViolation,
            Scope = request.Model ?? "",
            Message = "Model must be a package name under the configured model directory.",
        });

    var directory = Path.Combine(root, request.Model);
    if (!Directory.Exists(directory))
        throw new LeviathanException(new LeviathanError
        {
            Code = ErrorCode.InputMissing,
            Scope = request.Model,
            Message = "No such model package on the server.",
        });

    var surfaceEntityId = EntityId.Parse(surfaceId, EntityKind.Surface);
    var project = projects.LoadProjectForSurface(surfaceEntityId)
        ?? throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = surfaceId, Message = "Surface not found in any project." });

    var loaded = RecognitionModelLoader.Load(directory);
    var pipeline = new RecognitionPipeline(store, imageResolver);
    var outcome = pipeline.Run(project, surfaceEntityId, new RecognitionPipeline.Options
    {
        ModelDirectory = directory,
        ModelName = loaded.Manifest?.ModelName ?? request.Model,
        ModelVersion = loaded.Manifest?.CheckpointDigest is { Length: >= 12 } digest ? digest[..12] : "package",
        ModelFactory = () => loaded.Model,
        LabelSetFactory = () => loaded.Labels,
        RunId = Guid.NewGuid().ToString("N")[..10],
    });

    return Results.Ok(new
    {
        surfaceId = surfaceId,
        model = request.Model,
        lines = outcome.Lines.Count,
        skipped = outcome.SkippedLines.Count,
        reviewQueue = ReviewQueue.Build(outcome.Lines).Take(20).Select(e => new
        {
            lineId = e.LineId.ToString(),
            meanConfidence = e.MeanConfidence,
            emptyOutput = e.EmptyOutput,
        }),
    });
});

api.MapGet("projects/{id}/review-queue", (ProjectService projects, string id, string? surfaceId) =>
{
    var project = projects.Load(EntityId.Parse(id, EntityKind.Collection));
    var wanted = string.IsNullOrEmpty(surfaceId) ? (EntityId?)null : EntityId.Parse(surfaceId, EntityKind.Surface);
    return Results.Ok(PersistedReviewQueue.Build(project, wanted));
});

api.MapGet("projects/{id}/stats", (ProjectService projects, string id) =>
{
    var project = projects.Load(EntityId.Parse(id, EntityKind.Collection));
    return Results.Ok(ProjectStats.Build(project));
});

api.MapGet("projects/{id}/search", (ProjectService projects, string id, string q, string? mode) =>
{
    var project = projects.Load(EntityId.Parse(id, EntityKind.Collection));
    return Results.Ok(SearchService.Search(project, q, mode ?? "literal"));
});

api.MapGet("projects/{id}/profile-report", (ProjectService projects, string id) =>
{
    var project = projects.Load(EntityId.Parse(id, EntityKind.Collection));
    return Results.Ok(ProfileReport.Build(project));
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

    var (_, surface) = geometry.SetLineOrder(project, regionEntityId, request.LineIds,
        request.AuthorId ?? "api");
    return Results.Ok(new
    {
        regionId = regionId,
        order = surface.Lines.Where(l => l.RegionId == regionEntityId).Select(l => l.Id.ToString()),
    });
});

api.MapPost("surfaces/{surfaceId}/detect-layout", (ProjectService projects, LayoutService layout, LayoutModelRegistry models, string surfaceId) =>
{
    var surfaceEntityId = EntityId.Parse(surfaceId, EntityKind.Surface);
    var project = projects.LoadProjectForSurface(surfaceEntityId)
        ?? throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = surfaceId, Message = "Surface not found in any project." });

    var surface = project.Objects.SelectMany(o => o.Surfaces).Single(s => s.Id == surfaceEntityId);
    var outcome = layout.Detect(project, surfaceEntityId, model: models.Resolve(surface.Kind));
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
    var all = transcription.AllRevisions(project, lineEntityId);
    var surfaceForReviews = project.Objects.SelectMany(o => o.Surfaces).Single(s => s.Lines.Any(l => l.Id == lineEntityId));
    var lineRecord = surfaceForReviews.Lines.Single(l => l.Id == lineEntityId);
    var reviews = surfaceForReviews.ReviewDecisions
        .Where(d => d.LineId == lineEntityId)
        .OrderBy(d => d.At)
        .Select(d => new
        {
            id = d.Id,
            reviewer = d.Reviewer,
            outcome = d.Outcome,
            at = d.At,
            note = d.Note,
            revisionId = d.RevisionId.ToString(),
            correctedRevisionId = d.CorrectedRevisionId?.ToString(),
        });
    return Results.Ok(new
    {
        line = lineId,
        diplomatic = history.LastOrDefault()?.Text,
        direction = lineRecord.Direction,
        script = lineRecord.Script,
        reviews,
        history = history.Select(r => new { id = r.Id.ToString(), text = r.Text, author = r.Author.Id, at = r.CreatedAt }),
        revisions = all.Select(r => new
        {
            id = r.Id.ToString(),
            layer = r.Layer.ToString(),
            text = r.Text,
            author = r.Author.Id,
            at = r.CreatedAt,
            confidence = r.Confidence,
            graphemeConfidences = r.GraphemeConfidences.Select(c => new
            {
                start = c.Range.Start,
                end = c.Range.End,
                confidence = c.Confidence,
            }),
        }),
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

api.MapPost("lines/{lineId}/review", (ProjectService projects, ReviewService reviews, string lineId, [FromBody] ReviewRequest request) =>
{
    var lineEntityId = EntityId.Parse(lineId, EntityKind.Line);
    var project = projects.LoadProjectForLine(lineEntityId)
        ?? throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = lineId, Message = "Line not found in any project." });

    var (_, _, decision, correction) = reviews.Record(project, lineEntityId, request.Reviewer,
        request.Outcome, request.Note, request.CorrectionText);
    return Results.Ok(new
    {
        reviewId = decision.Id,
        outcome = decision.Outcome.ToString(),
        revisionId = decision.RevisionId.ToString(),
        correctedRevisionId = correction?.Id.ToString(),
    });
});

api.MapGet("projects/{id}/review-summary", (ProjectService projects, ReviewService reviews, string id, string? surfaceId) =>
{
    var project = projects.Load(EntityId.Parse(id, EntityKind.Collection));
    var wanted = string.IsNullOrEmpty(surfaceId) ? (EntityId?)null : EntityId.Parse(surfaceId, EntityKind.Surface);
    return Results.Ok(reviews.Summarize(project, wanted));
});

api.MapGet("projects/{id}/adjudication.csv", (ProjectService projects, ReviewService reviews, string id) =>
{
    var project = projects.Load(EntityId.Parse(id, EntityKind.Collection));
    return Results.Text(reviews.ExportAdjudicationCsv(project), "text/csv");
});

api.MapPost("annotations/{annotationId}/retract", (ProjectService projects, TranscriptionService transcription, string annotationId, [FromBody] RetractAnnotationRequest request) =>
{
    var id = EntityId.Parse(annotationId, EntityKind.Annotation);
    var project = projects.LoadProjectForAnnotation(id)
        ?? throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = annotationId, Message = "Annotation not found in any project." });
    var (_, _, retraction) = transcription.RetractAnnotation(project, id, request.Reviewer, request.Reason);
    return Results.Ok(new { retractionId = retraction.Id, annotationId = annotationId });
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

// Jobs: durable creation with idempotency keys; progress via server-sent events (project-plan §4),
// with the job resource remaining authoritative.
api.MapGet("me", (HttpContext http) =>
{
    var user = http.Items["user"] as UserAccount;
    return Results.Ok(user is null
        ? new { authenticated = false, name = "local", role = "admin" }
        : new { authenticated = true, name = user.Name, role = user.Role.ToString() });
});

api.MapGet("audit", (LocalStore store, int? limit) => Results.Ok(store.LoadAuthAudit(limit ?? 100)));

api.MapGet("projects/{id}/members", (ProjectService projects, LocalStore store, string id) =>
{
    var projectId = EntityId.Parse(id, EntityKind.Collection);
    _ = projects.Load(projectId);
    return Results.Ok(store.LoadProjectMembers(projectId));
});

api.MapPost("projects/{id}/members", (ProjectService projects, LocalStore store, string id, [FromBody] AddProjectMemberRequest request) =>
{
    var projectId = EntityId.Parse(id, EntityKind.Collection);
    _ = projects.Load(projectId);
    if (string.IsNullOrWhiteSpace(request.UserId))
        throw new LeviathanException(new LeviathanError { Code = ErrorCode.StageContractViolation, Scope = "members", Message = "userId is required." });
    store.AddProjectMember(projectId, request.UserId);
    return Results.Ok(store.LoadProjectMembers(projectId));
});

api.MapDelete("projects/{id}/members/{userId}", (ProjectService projects, LocalStore store, string id, string userId) =>
{
    var projectId = EntityId.Parse(id, EntityKind.Collection);
    _ = projects.Load(projectId);
    store.RemoveProjectMember(projectId, userId);
    return Results.NoContent();
});

api.MapGet("runs", (LocalStore store, string? kind, int? limit) =>
    Results.Ok(store.LoadRuns(kind, limit ?? 100)));

api.MapGet("runs/{runId}", (LocalStore store, string runId) =>
{
    var run = store.LoadRun(runId);
    return run is null
        ? Results.NotFound(new { code = "inputMissing", message = "No such run." })
        : Results.Ok(run);
});

api.MapGet("users", (LocalStore store) => Results.Ok(store.LoadUsers()));

api.MapPost("users", (LocalStore store, [FromBody] CreateUserRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Name))
        throw new LeviathanException(new LeviathanError { Code = ErrorCode.StageContractViolation, Scope = "user", Message = "User name is required." });
    var key = LeviathanText.Infrastructure.UserKeys.NewKey();
    var account = store.CreateUser(request.Name, request.Role, LeviathanText.Infrastructure.UserKeys.Hash(key));
    return Results.Ok(new { account, apiKey = key });
});

api.MapPost("users/{userId}/disable", (LocalStore store, string userId) =>
{
    store.DisableUser(userId);
    return Results.NoContent();
});

api.MapPost("users/{userId}/rotate", (LocalStore store, string userId) =>
{
    var key = LeviathanText.Infrastructure.UserKeys.NewKey();
    store.UpdateUserKey(userId, LeviathanText.Infrastructure.UserKeys.Hash(key));
    return Results.Ok(new { apiKey = key });
});

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
    else if (request.Type == "layout.project" && !string.IsNullOrEmpty(request.ProjectId))
    {
        inputs = [new StageArtifact(EntityId.Parse(request.ProjectId, EntityKind.Collection),
            new Checksum { Algorithm = "project", Digest = "" })];
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
        inputs: inputs, idempotencyKey: request.IdempotencyKey, priority: request.Priority);
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

public sealed record PresenceRequest(string EditorId);

public sealed record ImportPdfRequest(string PdfPath, string? OutputDirectory = null, string? Kind = null,
    int Dpi = 200, int MaxPages = 500, bool ExtractText = false);

public sealed record ImportEpiDocRequest(string DocumentPath, string ImagePath, string? Kind = null);

public sealed record ImportLineTextRequest(string CsvText, TextLayer Layer = TextLayer.L1Diplomatic, string? AuthorId = null);
public sealed record RecognizeRequest(string Model);

public sealed record CreateJobRequest(string Type, string OwnerId, string? IdempotencyKey, string? SurfaceId = null,
    string? ModelPackagePath = null, string? ProjectId = null, string? SourceDirectory = null, int Priority = 0);
public sealed record AppendRevisionRequest(TextLayer Layer, string Text, string AuthorId, string? ParentRevisionId);

public sealed record CreateUserRequest(string Name, UserRole Role);

public sealed record AddProjectMemberRequest(string UserId);

public sealed record RetractAnnotationRequest(string Reviewer, string? Reason = null);

public sealed record ReviewRequest(string Reviewer, ReviewOutcome Outcome, string? Note = null, string? CorrectionText = null);

public sealed record AddAnnotationRequest(
    AnnotationKind Kind,
    ScalarRange Range,
    string? Id = null,
    string? AnchoredRevisionId = null,
    GapExtent? GapExtent = null,
    string? AlternativeText = null,
    string? HandLabel = null,
    string? Description = null);

public sealed record LineGeometryRequest(IReadOnlyList<PointD> Polygon, IReadOnlyList<PointD> Baseline, string? AuthorId = null);

public sealed record OrientationRequest(TextDirection? Direction = null, string? Script = null, string? AuthorId = null);

public sealed record ReadingOrderRequest(IReadOnlyList<EntityId> RegionIds, string? AuthorId = null);

public sealed record LineOrderRequest(IReadOnlyList<EntityId> LineIds, string? AuthorId = null);

public sealed record SplitLineRequest(double Y, string? AuthorId = null);

public sealed record MergeLinesRequest(string OtherLineId, string? AuthorId = null);

public sealed record AddLineRequest(IReadOnlyList<PointD> Polygon, IReadOnlyList<PointD> Baseline, EntityId? RegionId = null, string? AuthorId = null);

/// <summary>Server-side credential resolution: environment variables only, referenced by name in settings (project-plan §2).</summary>
file sealed class EnvironmentCredentialSource : IAiCredentialSource
{
    public string? Resolve(string reference) => Environment.GetEnvironmentVariable(reference);
}

public partial class Program;
