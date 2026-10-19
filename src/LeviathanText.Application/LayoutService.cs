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
public sealed class LayoutService(IProjectStore store)
{
    public sealed record Outcome(NativeProject Project, NativeSurface Surface, int LineCount, string Method);

    public Outcome Detect(NativeProject project, EntityId surfaceId)
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

        if (!File.Exists(capture.ImageLocation))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = capture.ImageLocation,
                Message = "Capture image is not readable on this host.",
            });

        using var decoded = ImageDecoder.Decode(capture.ImageLocation);
        var result = ProjectionProfileLayout.Detect(decoded.Bitmap);

        var region = new NativeRegion
        {
            Id = new EntityId(EntityKind.Region, Guid.NewGuid().ToString("N")[..12]),
            FrameRef = capture.Id,
            Polygon =
            [
                new PointD(0, 0),
                new PointD(decoded.Bitmap.Width, 0),
                new PointD(decoded.Bitmap.Width, decoded.Bitmap.Height),
                new PointD(0, decoded.Bitmap.Height),
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
                new PointD(detected.Left, detected.Top),
                new PointD(detected.Right, detected.Top),
                new PointD(detected.Right, detected.Bottom),
                new PointD(detected.Left, detected.Bottom),
            ],
            Baseline = [new PointD(detected.Left, detected.Bottom), new PointD(detected.Right, detected.Bottom)],
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
