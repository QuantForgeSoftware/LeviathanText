using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application;

/// <summary>
/// Geometry and reading-order corrections (todo Phase 2: geometry/order correction;
/// "permit manual line/region annotation whenever automatic layout fails").
///
/// Provenance: edits are validated and persisted into the native document, and every
/// response carries the stage envelope semantics (manual layout is a first-class input,
/// never indistinguishable from automatic). Text-level revision history does not yet
/// cover geometry; client-side undo during editing plus project-file backups are the
/// current recovery path — recorded as a known gap in the handoff, not hidden.
/// </summary>
public sealed class GeometryService(IProjectStore store)
{
    public (NativeProject Project, NativeSurface Surface, NativeLine Line) SetLineGeometry(
        NativeProject project, EntityId lineId,
        IReadOnlyList<PointD> polygon, IReadOnlyList<PointD> baseline)
    {
        Validate(polygon, baseline, out var validatedPolygon, out var validatedBaseline);
        var (foundProject, surface, line) = FindLine(project, lineId);

        surface = surface with
        {
            Lines = [.. surface.Lines.Select(l => l.Id == lineId
                ? l with { Polygon = validatedPolygon, Baseline = validatedBaseline }
                : l)],
        };
        foundProject = WithSurface(foundProject, surface);
        store.SaveProject(foundProject);
        return (foundProject, surface, surface.Lines.Single(l => l.Id == lineId));
    }

    /// <summary>
    /// Add a manually drawn line (the fallback when automatic layout misses one). A manual
    /// region is created when the surface has none; explicit geometry is required.
    /// </summary>
    public (NativeProject Project, NativeSurface Surface, NativeLine Line) AddLine(
        NativeProject project, EntityId surfaceId,
        IReadOnlyList<PointD> polygon, IReadOnlyList<PointD> baseline, EntityId? regionId = null)
    {
        Validate(polygon, baseline, out var validatedPolygon, out var validatedBaseline);
        var (foundProject, surface) = FindSurface(project, surfaceId);

        NativeRegion? region = null;
        if (regionId is { } requested)
        {
            region = surface.Regions.FirstOrDefault(r => r.Id == requested)
                ?? throw new LeviathanException(new LeviathanError
                {
                    Code = ErrorCode.InputMissing,
                    Scope = requested.ToString(),
                    Message = "No such region on the surface.",
                });
        }

        if (region is null)
        {
            region = surface.Regions.Count > 0
                ? surface.Regions.OrderBy(r => r.ReadingOrderIndex).First()
                : new NativeRegion
                {
                    Id = new EntityId(EntityKind.Region, Guid.NewGuid().ToString("N")[..12]),
                    FrameRef = surface.Captures.FirstOrDefault()?.Id ?? default,
                    Polygon =
                    [
                        new PointD(0, 0),
                        new PointD(polygon.Max(p => p.X), 0),
                        new PointD(polygon.Max(p => p.X), polygon.Max(p => p.Y)),
                        new PointD(0, polygon.Max(p => p.Y)),
                    ],
                    LayoutSource = LayoutSource.Manual,
                    ReadingOrderIndex = 0,
                };
        }

        var line = new NativeLine
        {
            Id = new EntityId(EntityKind.Line, Guid.NewGuid().ToString("N")[..12]),
            RegionId = region.Id,
            FrameRef = region.FrameRef,
            Polygon = validatedPolygon,
            Baseline = validatedBaseline,
        };

        var updated = surface with
        {
            Regions = surface.Regions.Any(r => r.Id == region.Id)
                ? surface.Regions
                : [.. surface.Regions, region],
            Lines = [.. surface.Lines, line],
        };
        foundProject = WithSurface(foundProject, updated);
        store.SaveProject(foundProject);
        return (foundProject, updated, line);
    }

    /// <summary>
    /// Delete a line that carries no transcription evidence. Lines with an L1 revision are
    /// refused: removing the scope would orphan reviewed text (delete the revision history
    /// deliberately instead, once a review workflow exists).
    /// </summary>
    public (NativeProject Project, NativeSurface Surface) DeleteLine(NativeProject project, EntityId lineId)
    {
        var (foundProject, surface, line) = FindLine(project, lineId);
        if (line.DiplomaticRevisionId is not null)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.RevisionConflict,
                Scope = lineId.ToString(),
                RemedyHint = RemedyHint.RebaseRevision,
                Message = "Line has a diplomatic revision; refusing to delete transcribed evidence.",
            });

        var updated = surface with { Lines = [.. surface.Lines.Where(l => l.Id != lineId)] };
        foundProject = WithSurface(foundProject, updated);
        store.SaveProject(foundProject);
        return (foundProject, updated);
    }

    public (NativeProject Project, NativeSurface Surface) SetReadingOrder(
        NativeProject project, EntityId surfaceId, IReadOnlyList<EntityId> orderedRegionIds)
    {
        var (foundProject, surface) = FindSurface(project, surfaceId);

        var existing = surface.Regions.Select(r => r.Id).ToHashSet();
        var requested = orderedRegionIds.ToHashSet();
        if (existing.Count != requested.Count || !existing.SetEquals(requested))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = surfaceId.ToString(),
                Details = new Dictionary<string, string>
                {
                    ["existing"] = string.Join(',', existing.Select(e => e.ToString())),
                    ["requested"] = string.Join(',', requested.Select(e => e.ToString())),
                },
                Message = "Reading order must list every region of the surface exactly once.",
            });

        var index = orderedRegionIds.Select((id, position) => (id, position)).ToDictionary(p => p.id, p => p.position);
        surface = surface with
        {
            Regions = [.. surface.Regions.Select(r => r with { ReadingOrderIndex = index[r.Id] })],
        };
        foundProject = WithSurface(foundProject, surface);
        store.SaveProject(foundProject);
        return (foundProject, surface);
    }

    private static void Validate(
        IReadOnlyList<PointD> polygon, IReadOnlyList<PointD> baseline,
        out IReadOnlyList<PointD> validatedPolygon, out IReadOnlyList<PointD> validatedBaseline)
    {
        static bool Finite(IReadOnlyList<PointD> points) =>
            points.All(p => double.IsFinite(p.X) && double.IsFinite(p.Y));

        if (polygon.Count < 3)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = "geometry",
                Message = "A line polygon needs at least three points.",
            });
        if (baseline.Count != 0 && baseline.Count < 2)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = "geometry",
                Message = "A baseline needs at least two points (or none when geometry is absent).",
            });
        if (!Finite(polygon) || !Finite(baseline))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = "geometry",
                Message = "Geometry points must be finite numbers.",
            });

        validatedPolygon = [.. polygon];
        validatedBaseline = [.. baseline];
    }

    public (NativeProject Project, NativeSurface Surface) SetLineOrder(
        NativeProject project, EntityId regionId, IReadOnlyList<EntityId> orderedLineIds)
    {
        var surface = project.Objects.SelectMany(o => o.Surfaces).FirstOrDefault(s => s.Regions.Any(r => r.Id == regionId))
            ?? throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = regionId.ToString(),
                Message = "No such region in the project.",
            });

        var expected = surface.Lines.Where(l => l.RegionId == regionId).Select(l => l.Id).ToHashSet();
        var requested = orderedLineIds.ToHashSet();
        if (expected.Count != requested.Count || !expected.SetEquals(requested))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = regionId.ToString(),
                Message = "Line order must list every line of the region exactly once.",
            });

        // Rebuild the surface line list: lines of this region in the requested order,
        // other regions' lines keep their relative positions.
        var ordered = new List<NativeLine>();
        var requestedList = orderedLineIds.ToList();
        var pointer = 0;
        foreach (var line in surface.Lines)
        {
            if (line.RegionId == regionId)
            {
                ordered.Add(surface.Lines.Single(l => l.Id == requestedList[pointer]));
                pointer++;
            }
            else
            {
                ordered.Add(line);
            }
        }

        var updated = surface with { Lines = ordered };
        var foundProject = WithSurface(project, updated);
        store.SaveProject(foundProject);
        return (foundProject, updated);
    }

    private static (NativeProject Project, NativeSurface Surface) FindSurface(NativeProject project, EntityId surfaceId)
    {
        foreach (var obj in project.Objects)
            foreach (var surface in obj.Surfaces)
                if (surface.Id == surfaceId)
                    return (project, surface);
        throw new LeviathanException(new LeviathanError
        {
            Code = ErrorCode.InputMissing,
            Scope = surfaceId.ToString(),
            Message = "No such surface in the project.",
        });
    }

    private static (NativeProject Project, NativeSurface Surface, NativeLine Line) FindLine(NativeProject project, EntityId lineId)
    {
        foreach (var obj in project.Objects)
            foreach (var surface in obj.Surfaces)
                foreach (var line in surface.Lines)
                    if (line.Id == lineId)
                        return (project, surface, line);
        throw new LeviathanException(new LeviathanError
        {
            Code = ErrorCode.InputMissing,
            Scope = lineId.ToString(),
            Message = "No such line in the project.",
        });
    }

    private static NativeProject WithSurface(NativeProject project, NativeSurface surface) =>
        project with
        {
            Objects = [.. project.Objects.Select(o => o.Surfaces.Any(s => s.Id == surface.Id)
                ? o with { Surfaces = [.. o.Surfaces.Select(s => s.Id == surface.Id ? surface : s)] }
                : o)],
        };
}
