using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace LeviathanText.Api;

[ApiController]
[Route("api/v1/projects/{projectId}/surfaces/{surfaceId}/captures/{captureId}/display")]
public sealed class CaptureDisplayController(ICaptureDisplayService display, LocalStore store,
    IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> Describe(string projectId, string surfaceId, string captureId, CancellationToken cancellationToken) =>
        Deliver(projectId, surfaceId, captureId, image: false, cancellationToken);

    [HttpGet("image")]
    public Task<IActionResult> Image(string projectId, string surfaceId, string captureId, CancellationToken cancellationToken) =>
        Deliver(projectId, surfaceId, captureId, image: true, cancellationToken);

    private async Task<IActionResult> Deliver(string projectId, string surfaceId, string captureId,
        bool image, CancellationToken cancellationToken)
    {
        var project = EntityId.Parse(projectId, EntityKind.Collection);
        var surface = EntityId.Parse(surfaceId, EntityKind.Surface);
        var capture = EntityId.Parse(captureId, EntityKind.Capture);
        CaptureDisplayResult? buffered = null;
        try
        {
            var initial = LoadAuthorized(project);
            // Resolve before any asynchronous work, and revalidate the complete
            // selected record after buffering. No first-match cross-project fallback.
            var selected = CaptureDisplayService.ResolveCapture(initial, surface, capture);
            if (image && !CaptureAccessPolicy.ValidMatchHeader(Request.Headers.IfMatch.ToString()))
                return StatusCode(StatusCodes.Status412PreconditionFailed, new { code = "capture_display_changed", message = "Request a current display descriptor and send its quoted derivativeId in If-Match." });
            var result = buffered = await display.RenderAsync(initial, surface, capture, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var current = LoadAuthorized(project);
            if (CaptureDisplayService.ResolveCapture(current, surface, capture) != selected || result.SourceCapture != selected)
                return StatusCode(StatusCodes.Status412PreconditionFailed, new { code = "capture_display_changed", message = "The selected capture changed. Request a new display descriptor." });
            CaptureDisplayService.ValidateObservedFrameDimensions(current, selected.Frame.ImageRef,
                result.Descriptor.SourceWidth, result.Descriptor.SourceHeight);
            var etag = '"' + result.Descriptor.DerivativeId + '"';
            if (image && !string.Equals(Request.Headers.IfMatch.ToString(), etag, StringComparison.Ordinal))
                return StatusCode(StatusCodes.Status412PreconditionFailed, new { code = "capture_display_changed", message = "The source/display identity changed. Request a new display descriptor." });
            RequireReadAccess(project);
            Response.Headers.CacheControl = "no-store";
            Response.Headers.ETag = etag;
            IActionResult response = image ? File(result.PngBytes, "image/png") : Ok(result.Descriptor);
            var ownedResponse = new CaptureDisplayActionResult(response, result);
            buffered = null; // Execution owns admission through completed/failed output.
            return ownedResponse;
        }
        catch (CaptureAccessDeniedException)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "forbidden", message = "Current project read access was denied." });
        }
        catch (LeviathanException error)
        {
            try { RequireReadAccess(project); }
            catch (CaptureAccessDeniedException)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { code = "forbidden", message = "Current project read access was denied." });
            }
            if (image && error.Error.Code is ErrorCode.ChecksumMismatch or ErrorCode.FrameUnregistered)
                return StatusCode(StatusCodes.Status412PreconditionFailed, new { code = "capture_display_changed", message = "The source/display identity changed. Request a new display descriptor." });
            throw;
        }
        finally { buffered?.Dispose(); }
    }

    private NativeProject LoadAuthorized(EntityId projectId) =>
        CaptureAccessPolicy.LoadAuthorized(HttpContext, store, configuration, projectId);

    private void RequireReadAccess(EntityId projectId) =>
        CaptureAccessPolicy.Require(HttpContext, store, configuration, projectId);

}

/// <summary>Keep capture admission until buffered response execution ends, including failed/cancelled writes.</summary>
public sealed class CaptureDisplayActionResult(IActionResult response, CaptureDisplayResult buffer,
    TimeSpan? deliveryTimeout = null) : IActionResult
{
    public async Task ExecuteResultAsync(ActionContext context)
    {
        try
        {
            var originalAbort = context.HttpContext.RequestAborted;
            using var delivery = CancellationTokenSource.CreateLinkedTokenSource(originalAbort);
            delivery.CancelAfter(deliveryTimeout ?? TimeSpan.FromSeconds(30));
            context.HttpContext.RequestAborted = delivery.Token;
            using var abort = delivery.Token.Register(context.HttpContext.Abort);
            try { await response.ExecuteResultAsync(context); }
            finally { context.HttpContext.RequestAborted = originalAbort; }
        }
        finally { buffer.Dispose(); }
    }
}
