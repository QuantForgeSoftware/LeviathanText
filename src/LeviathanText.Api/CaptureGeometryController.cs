using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace LeviathanText.Api;

[ApiController]
[Route("api/v1/projects/{projectId}/surfaces/{surfaceId}/captures/{captureId}")]
public sealed class CaptureGeometryController(ICaptureDisplayService display, GeometryService geometry,
    LocalStore store, IConfiguration configuration) : ControllerBase
{
    public sealed record CreateLineRequest(IReadOnlyList<PointD> Polygon, IReadOnlyList<PointD> Baseline,
        IReadOnlyList<EntityId> ExpectedLineIds, IReadOnlyList<EntityId> ExpectedRegionIds,
        EntityId? RegionId = null, string? AuthorId = null);

    public sealed record RegionOrderRequest(IReadOnlyList<EntityId> RegionIds,
        IReadOnlyList<EntityId> ExpectedSurfaceRegionIds, string? AuthorId = null);
    public sealed record CaptureLineOrderRequest(IReadOnlyList<EntityId> LineIds,
        IReadOnlyList<EntityId> ExpectedRegionLineIds, string? AuthorId = null);
    public sealed record UpdateGeometryRequest(IReadOnlyList<PointD> Polygon, IReadOnlyList<PointD> Baseline,
        LineGeometrySnapshot ExpectedGeometry, string? AuthorId = null);

    [HttpPut("lines/{lineId}/geometry")]
    public Task<IActionResult> UpdateGeometry(string projectId, string surfaceId, string captureId, string lineId,
        UpdateGeometryRequest request, CancellationToken cancellationToken) =>
        Mutate(projectId, surfaceId, captureId, cancellationToken, (project, surface, capture, descriptor) =>
        {
            var (_, _, line) = geometry.SetLineGeometryInCapture(project, surface, capture, descriptor,
                EntityId.Parse(lineId, EntityKind.Line), request.Polygon, request.Baseline, request.ExpectedGeometry,
                MutationAttribution.Actor(HttpContext, request.AuthorId));
            return Ok(new { lineId = line.Id.ToString(), frameRef = line.FrameRef.ToString(), polygon = line.Polygon, baseline = line.Baseline });
        });

    [HttpPost("lines")]
    public Task<IActionResult> Create(string projectId, string surfaceId, string captureId,
        CreateLineRequest request, CancellationToken cancellationToken) =>
        Mutate(projectId, surfaceId, captureId, cancellationToken, (project, surface, capture, descriptor) =>
        {
            var (_, _, line) = geometry.AddLineInCapture(project, surface, capture, descriptor,
                request.Polygon, request.Baseline, request.ExpectedLineIds, request.ExpectedRegionIds,
                request.RegionId, MutationAttribution.Actor(HttpContext, request.AuthorId));
            return Created($"/api/v1/lines/{line.Id}", new
            {
                lineId = line.Id.ToString(), regionId = line.RegionId.ToString(), frameRef = line.FrameRef.ToString(),
                polygon = line.Polygon, baseline = line.Baseline,
            });
        });

    [HttpPut("reading-order")]
    public Task<IActionResult> ReadingOrder(string projectId, string surfaceId, string captureId,
        RegionOrderRequest request, CancellationToken cancellationToken) =>
        Mutate(projectId, surfaceId, captureId, cancellationToken, (project, surface, capture, _) =>
        {
            var (_, updated) = geometry.SetCaptureReadingOrder(project, surface, capture, request.RegionIds,
                request.ExpectedSurfaceRegionIds, MutationAttribution.Actor(HttpContext, request.AuthorId));
            return Ok(new { surfaceId = surface.ToString(), regionIds = updated.Regions.OrderBy(r => r.ReadingOrderIndex).Select(r => r.Id.ToString()) });
        });

    [HttpPut("regions/{regionId}/line-order")]
    public Task<IActionResult> LineOrder(string projectId, string surfaceId, string captureId, string regionId,
        CaptureLineOrderRequest request, CancellationToken cancellationToken) =>
        Mutate(projectId, surfaceId, captureId, cancellationToken, (project, surface, capture, _) =>
        {
            var region = EntityId.Parse(regionId, EntityKind.Region);
            var (_, updated) = geometry.SetCaptureLineOrder(project, surface, capture, region, request.LineIds,
                request.ExpectedRegionLineIds, MutationAttribution.Actor(HttpContext, request.AuthorId));
            return Ok(new { regionId, lineIds = updated.Lines.Where(l => l.RegionId == region).Select(l => l.Id.ToString()) });
        });

    private async Task<IActionResult> Mutate(string projectId, string surfaceId, string captureId,
        CancellationToken cancellationToken, Func<NativeProject, EntityId, EntityId, CaptureDisplayDescriptor, IActionResult> mutation)
    {
        var project = EntityId.Parse(projectId, EntityKind.Collection);
        var surface = EntityId.Parse(surfaceId, EntityKind.Surface);
        var capture = EntityId.Parse(captureId, EntityKind.Capture);
        bool preparing = true;
        try
        {
            var initial = CaptureAccessPolicy.LoadAuthorized(HttpContext, store, configuration, project, write: true);
            var selected = CaptureDisplayService.ResolveCapture(initial, surface, capture);
            if (!CaptureAccessPolicy.ValidMatchHeader(Request.Headers.IfMatch.ToString())) return Changed();
            using var rendered = await display.RenderAsync(initial, surface, capture, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var current = CaptureAccessPolicy.LoadAuthorized(HttpContext, store, configuration, project, write: true);
            if (CaptureDisplayService.ResolveCapture(current, surface, capture) != selected
                || rendered.SourceCapture != selected
                || Request.Headers.IfMatch.ToString() != '"' + rendered.Descriptor.DerivativeId + '"') return Changed();
            CaptureDisplayService.ValidateObservedFrameDimensions(current, selected.Frame.ImageRef,
                rendered.Descriptor.SourceWidth, rendered.Descriptor.SourceHeight);
            CaptureAccessPolicy.Require(HttpContext, store, configuration, project, write: true);
            Response.Headers.CacheControl = "no-store";
            preparing = false;
            return mutation(current, surface, capture, rendered.Descriptor);
        }
        catch (Exception error) when (error is CaptureAccessDeniedException or NativeAiAccessDeniedException)
        {
            return Denied();
        }
        catch (LeviathanException error)
        {
            try { CaptureAccessPolicy.Require(HttpContext, store, configuration, project, write: true); }
            catch (CaptureAccessDeniedException) { return Denied(); }
            if (preparing && error.Error.Code is ErrorCode.ChecksumMismatch or ErrorCode.FrameUnregistered)
                return Changed();
            throw;
        }
    }

    private IActionResult Changed() => StatusCode(StatusCodes.Status412PreconditionFailed, new
    {
        code = "capture_display_changed", message = "The source/display identity changed. Refresh the capture preview before drawing.",
    });
    private IActionResult Denied() => StatusCode(StatusCodes.Status403Forbidden, new
    {
        code = "forbidden", message = "Current project write access was denied.",
    });
}
