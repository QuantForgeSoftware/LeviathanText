using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Imaging;
using LeviathanText.Layout;

namespace LeviathanText.Application;

/// <summary>
/// Automatic layout stage (todo Phase 1: layout inference; "manual line/region
/// annotation whenever automatic layout fails"). Runs the measured projection detector
/// on a surface's capture and creates one page region with detected lines
/// (LayoutSource = Automatic, confidence none — a heuristic has no probability claim).
///
/// Refuses to overwrite existing layout: detection appends only to a surface without
/// regions, so manual work is never silently replaced.
/// </summary>
public sealed class LayoutService(IProjectStore store, ImageResolver? imageResolver = null)
{
    public sealed record Outcome(NativeProject Project, NativeSurface Surface, int LineCount, string Method);

    public Outcome Detect(NativeProject project, EntityId surfaceId, long maxPixels = 12_000_000,
        PatchLayoutModel? model = null)
    {
        var surface = project.Objects.SelectMany(o => o.Surfaces).FirstOrDefault(s => s.Id == surfaceId)
            ?? throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = surfaceId.ToString(),
                Message = "No such surface in the project.",
            });

        if (surface.Regions.Count > 0)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = surfaceId.ToString(),
                Details = new Dictionary<string, string> { ["regions"] = surface.Regions.Count.ToString() },
                Message = "Surface already has layout; automatic detection never overwrites manual work.",
            });

        var capture = surface.Captures.FirstOrDefault()
            ?? throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = surfaceId.ToString(),
                Message = "Surface has no capture to detect layout against.",
            });

        // Remote captures (e.g. IIIF imports) are fetched once into the checksummed cache.
        if (!File.Exists(capture.ImageLocation) && imageResolver is not null)
        {
            var resolved = imageResolver.EnsureLocal(project, surfaceId);
            project = resolved.Project;
            surface = resolved.Surface;
            capture = resolved.Capture;
        }

        if (!File.Exists(capture.ImageLocation))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = capture.ImageLocation,
                Message = "Capture image is not readable on this host.",
            });

        // Bound memory: very large captures are detected on a scaled preview and all
        // coordinates are mapped back to the original frame. Recognition still requires
        // full-resolution pixels for crops (tiled region decode is a known open item).
        using var decoded = ImageDecoder.DecodeBounded(capture.ImageLocation, maxPixels);
        // Learned models are opt-in per material; they were trained on a page-level split and
        // are not a general layout claim (see docs/evaluation/layout-patch-learning.md).
        var result = model is null
            ? ProjectionProfileLayout.Detect(decoded.Bitmap)
            : model.Detect(decoded.Bitmap);
        double scaleX = decoded.ScaleX, scaleY = decoded.ScaleY;
        int originalWidth = decoded.OriginalWidth, originalHeight = decoded.OriginalHeight;

        var region = new NativeRegion
        {
            Id = new EntityId(EntityKind.Region, Guid.NewGuid().ToString("N")[..12]),
            FrameRef = capture.Id,
            Polygon =
            [
                new PointD(0, 0),
                new PointD(originalWidth, 0),
                new PointD(originalWidth, originalHeight),
                new PointD(0, originalHeight),
            ],
            LayoutSource = LayoutSource.Automatic,
            ReadingOrderIndex = 0,
        };

        var lines = result.Lines.Select((detected, index) => new NativeLine
        {
            Id = new EntityId(EntityKind.Line, Guid.NewGuid().ToString("N")[..12]),
            RegionId = region.Id,
            FrameRef = capture.Id,
            Polygon =
            [
                new PointD(detected.Left / scaleX, detected.Top / scaleY),
                new PointD(detected.Right / scaleX, detected.Top / scaleY),
                new PointD(detected.Right / scaleX, detected.Bottom / scaleY),
                new PointD(detected.Left / scaleX, detected.Bottom / scaleY),
            ],
            Baseline =
            [
                new PointD(detected.Left / scaleX, detected.Bottom / scaleY),
                new PointD(detected.Right / scaleX, detected.Bottom / scaleY),
            ],
        }).ToList();

        var updated = surface with { Regions = [region], Lines = lines };
        var withUpdated = project with
        {
            Objects = [.. project.Objects.Select(o => o.Surfaces.Any(s => s.Id == surfaceId)
                ? o with { Surfaces = [.. o.Surfaces.Select(s => s.Id == surfaceId ? updated : s)] }
                : o)],
        };
        store.SaveProject(withUpdated);
        return new Outcome(withUpdated, updated, lines.Count, result.Method);
    }
}
