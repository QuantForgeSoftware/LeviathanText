using System.Text.Json;
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
        IReadOnlyList<PointD> polygon, IReadOnlyList<PointD> baseline,
        string author = "manual-editor")
    {
        Validate(polygon, baseline, out var validatedPolygon, out var validatedBaseline);
        var (foundProject, surface, line) = FindLine(project, lineId);

        var before = GeometryJson(line.Polygon, line.Baseline);
        var after = GeometryJson(validatedPolygon, validatedBaseline);
        surface = surface with
        {
            Lines = [.. surface.Lines.Select(l => l.Id == lineId
                ? l with { Polygon = validatedPolygon, Baseline = validatedBaseline }
                : l)],
            GeometryEdits =
            [
                .. surface.GeometryEdits,
                new GeometryEdit
                {
                    Id = $"ge-{Guid.NewGuid().ToString("N")[..12]}",
                    Kind = GeometryEditKind.LineGeometry,
                    At = DateTimeOffset.UtcNow,
                    Author = author,
                    LineId = lineId,
                    Before = before,
                    After = after,
                },
            ],
        };
        foundProject = WithSurface(foundProject, surface);
        store.SaveProject(foundProject);
        return (foundProject, surface, surface.Lines.Single(l => l.Id == lineId));
    }

    /// <summary>
    /// Set or clear a line's declared logical direction and script. This is metadata about
    /// the witness, not a recognition/model claim; null means "inherit/undeclared".
    /// </summary>
    public (NativeProject Project, NativeSurface Surface, NativeLine Line) SetLineOrientation(
        NativeProject project, EntityId lineId, TextDirection? direction, string? script,
        string author = "api")
    {
        var (foundProject, surface, line) = FindLine(project, lineId);
        var validatedScript = ValidateScript(script);

        var before = OrientationJson(line.Direction, line.Script);
        var after = OrientationJson(direction, validatedScript);
        var updatedLine = line with { Direction = direction, Script = validatedScript };
        var updated = surface with
        {
            Lines = [.. surface.Lines.Select(l => l.Id == lineId ? updatedLine : l)],
            GeometryEdits =
            [
                .. surface.GeometryEdits,
                new GeometryEdit
                {
                    Id = $"ge-{Guid.NewGuid().ToString("N")[..12]}",
                    Kind = GeometryEditKind.LineOrientation,
                    At = DateTimeOffset.UtcNow,
                    Author = author,
                    LineId = lineId,
                    Before = before,
                    After = after,
                },
            ],
        };
        foundProject = WithSurface(foundProject, updated);
        store.SaveProject(foundProject);
        return (foundProject, updated, updatedLine);
    }

    /// <summary>Set or clear a region's declared direction/script (line values override it in consumers that support inheritance).</summary>
    public (NativeProject Project, NativeSurface Surface, NativeRegion Region) SetRegionOrientation(
        NativeProject project, EntityId regionId, TextDirection? direction, string? script,
        string author = "api")
    {
        var surface = project.Objects.SelectMany(o => o.Surfaces)
            .FirstOrDefault(s => s.Regions.Any(r => r.Id == regionId))
            ?? throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = regionId.ToString(),
                Message = "No such region in the project.",
            });
        var region = surface.Regions.Single(r => r.Id == regionId);
        var validatedScript = ValidateScript(script);

        var before = OrientationJson(region.Direction, region.Script);
        var after = OrientationJson(direction, validatedScript);
        var updatedRegion = region with { Direction = direction, Script = validatedScript };
        var updated = surface with
        {
            Regions = [.. surface.Regions.Select(r => r.Id == regionId ? updatedRegion : r)],
            GeometryEdits =
            [
                .. surface.GeometryEdits,
                new GeometryEdit
                {
                    Id = $"ge-{Guid.NewGuid().ToString("N")[..12]}",
                    Kind = GeometryEditKind.RegionOrientation,
                    At = DateTimeOffset.UtcNow,
                    Author = author,
                    RegionId = regionId,
                    Before = before,
                    After = after,
                },
            ],
        };
        var foundProject = WithSurface(project, updated);
        store.SaveProject(foundProject);
        return (foundProject, updated, updatedRegion);
    }

    /// <summary>
    /// Add a manually drawn line (the fallback when automatic layout misses one). A manual
    /// region is created when the surface has none; explicit geometry is required.
    /// </summary>
    public (NativeProject Project, NativeSurface Surface, NativeLine Line) AddLine(
        NativeProject project, EntityId surfaceId,
        IReadOnlyList<PointD> polygon, IReadOnlyList<PointD> baseline, EntityId? regionId = null,
        string author = "manual-editor")
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
            GeometryEdits =
            [
                .. surface.GeometryEdits,
                new GeometryEdit
                {
                    Id = $"ge-{Guid.NewGuid().ToString("N")[..12]}",
                    Kind = GeometryEditKind.LineAdded,
                    At = DateTimeOffset.UtcNow,
                    Author = author,
                    LineId = line.Id,
                    RegionId = region.Id,
                    After = GeometryJson(validatedPolygon, validatedBaseline),
                },
            ],
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
    public (NativeProject Project, NativeSurface Surface) DeleteLine(
        NativeProject project, EntityId lineId, string author = "manual-editor")
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

        var updated = surface with
        {
            Lines = [.. surface.Lines.Where(l => l.Id != lineId)],
            GeometryEdits =
            [
                .. surface.GeometryEdits,
                new GeometryEdit
                {
                    Id = $"ge-{Guid.NewGuid().ToString("N")[..12]}",
                    Kind = GeometryEditKind.LineDeleted,
                    At = DateTimeOffset.UtcNow,
                    Author = author,
                    LineId = lineId,
                    RegionId = line.RegionId,
                    Before = GeometryJson(line.Polygon, line.Baseline),
                },
            ],
        };
        foundProject = WithSurface(foundProject, updated);
        store.SaveProject(foundProject);
        return (foundProject, updated);
    }

    public (NativeProject Project, NativeSurface Surface) SetReadingOrder(
        NativeProject project, EntityId surfaceId, IReadOnlyList<EntityId> orderedRegionIds,
        string author = "manual-editor")
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
        var beforeOrder = string.Join(',', surface.Regions.OrderBy(r => r.ReadingOrderIndex).Select(r => r.Id));
        var afterOrder = string.Join(',', orderedRegionIds);
        surface = surface with
        {
            Regions = [.. surface.Regions.Select(r => r with { ReadingOrderIndex = index[r.Id] })],
            GeometryEdits =
            [
                .. surface.GeometryEdits,
                new GeometryEdit
                {
                    Id = $"ge-{Guid.NewGuid().ToString("N")[..12]}",
                    Kind = GeometryEditKind.RegionOrder,
                    At = DateTimeOffset.UtcNow,
                    Author = author,
                    Before = beforeOrder,
                    After = afterOrder,
                },
            ],
        };
        foundProject = WithSurface(foundProject, surface);
        store.SaveProject(foundProject);
        return (foundProject, surface);
    }

    private static string? ValidateScript(string? script)
    {
        if (string.IsNullOrWhiteSpace(script)) return null;
        var trimmed = script.Trim();
        if (trimmed.Length > 32 || trimmed.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = "script",
                Message = "Script must be a short code such as 'Grek' (max 32 characters, no whitespace/control characters).",
            });
        return trimmed;
    }

    private static string OrientationJson(TextDirection? direction, string? script) =>
        $"direction={direction?.ToString() ?? "inherit"};script={script ?? ""}";

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
        NativeProject project, EntityId regionId, IReadOnlyList<EntityId> orderedLineIds,
        string author = "manual-editor")
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

        var beforeOrder = string.Join(',', surface.Lines.Where(l => l.RegionId == regionId).Select(l => l.Id));
        var updated = surface with
        {
            Lines = ordered,
            GeometryEdits =
            [
                .. surface.GeometryEdits,
                new GeometryEdit
                {
                    Id = $"ge-{Guid.NewGuid().ToString("N")[..12]}",
                    Kind = GeometryEditKind.LineOrder,
                    At = DateTimeOffset.UtcNow,
                    Author = author,
                    RegionId = regionId,
                    Before = beforeOrder,
                    After = string.Join(',', orderedLineIds),
                },
            ],
        };
        var foundProject = WithSurface(project, updated);
        store.SaveProject(foundProject);
        return (foundProject, updated);
    }

    /// <summary>
    /// Split an untranscribed line at a horizontal y position (correction for merged
    /// detections). Refuses lines carrying diplomatic text: that evidence must not be
    /// re-partitioned silently.
    /// </summary>
    public (NativeProject Project, NativeSurface Surface, NativeLine Top, NativeLine Bottom) SplitLine(
        NativeProject project, EntityId lineId, double splitY, string author = "manual-editor")
    {
        var (foundProject, surface, line) = FindLine(project, lineId);
        if (line.DiplomaticRevisionId is not null)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.RevisionConflict,
                Scope = lineId.ToString(),
                Message = "Line has a diplomatic revision; splitting it would detach text from geometry.",
            });

        double top = line.Polygon.Min(p => p.Y);
        double bottom = line.Polygon.Max(p => p.Y);
        double left = line.Polygon.Min(p => p.X);
        double right = line.Polygon.Max(p => p.X);
        if (splitY <= top + 2 || splitY >= bottom - 2)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = lineId.ToString(),
                Details = new Dictionary<string, string> { ["splitY"] = splitY.ToString("0.#"), ["range"] = $"{top:0.#}..{bottom:0.#}" },
                Message = "Split position must be inside the line box (at least 2 px from each edge).",
            });

        var topLine = line with
        {
            Id = new EntityId(EntityKind.Line, Guid.NewGuid().ToString("N")[..12]),
            Polygon = [new(left, top), new(right, top), new(right, splitY), new(left, splitY)],
            Baseline = [new(left, splitY), new(right, splitY)],
        };
        var bottomLine = line with
        {
            Id = new EntityId(EntityKind.Line, Guid.NewGuid().ToString("N")[..12]),
            Polygon = [new(left, splitY), new(right, splitY), new(right, bottom), new(left, bottom)],
            Baseline = [new(left, bottom), new(right, bottom)],
        };

        var lines = new List<NativeLine>();
        foreach (var candidate in surface.Lines)
        {
            if (candidate.Id == lineId)
            {
                lines.Add(topLine);
                lines.Add(bottomLine);
            }
            else lines.Add(candidate);
        }

        var updated = surface with
        {
            Lines = lines,
            GeometryEdits =
            [
                .. surface.GeometryEdits,
                new GeometryEdit
                {
                    Id = $"ge-{Guid.NewGuid().ToString("N")[..12]}",
                    Kind = GeometryEditKind.LineSplit,
                    At = DateTimeOffset.UtcNow,
                    Author = author,
                    LineId = lineId,
                    Before = GeometryJson(line.Polygon, line.Baseline),
                    After = GeometryJson(topLine.Polygon, topLine.Baseline) + "|" + GeometryJson(bottomLine.Polygon, bottomLine.Baseline),
                },
            ],
        };
        foundProject = WithSurface(foundProject, updated);
        store.SaveProject(foundProject);
        return (foundProject, updated, topLine, bottomLine);
    }

    /// <summary>
    /// Merge two untranscribed lines of the same region (correction for split detections).
    /// The first line's position is kept.
    /// </summary>
    public (NativeProject Project, NativeSurface Surface, NativeLine Merged) MergeLines(
        NativeProject project, EntityId firstLineId, EntityId secondLineId, string author = "manual-editor")
    {
        if (firstLineId == secondLineId)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = firstLineId.ToString(),
                Message = "Cannot merge a line with itself.",
            });

        var (foundProject, surface, first) = FindLine(project, firstLineId);
        if (first.DiplomaticRevisionId is not null)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.RevisionConflict,
                Scope = firstLineId.ToString(),
                Message = "First line has a diplomatic revision; merging would detach text from geometry.",
            });
        var second = surface.Lines.FirstOrDefault(l => l.Id == secondLineId)
            ?? throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = secondLineId.ToString(),
                Message = "No such line on the first line's surface.",
            });
        if (second.DiplomaticRevisionId is not null)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.RevisionConflict,
                Scope = secondLineId.ToString(),
                Message = "Second line has a diplomatic revision; merging would detach text from geometry.",
            });
        if (second.RegionId != first.RegionId)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = secondLineId.ToString(),
                Message = "Lines belong to different regions.",
            });

        double left = Math.Min(first.Polygon.Min(p => p.X), second.Polygon.Min(p => p.X));
        double right = Math.Max(first.Polygon.Max(p => p.X), second.Polygon.Max(p => p.X));
        double top = Math.Min(first.Polygon.Min(p => p.Y), second.Polygon.Min(p => p.Y));
        double bottom = Math.Max(first.Polygon.Max(p => p.Y), second.Polygon.Max(p => p.Y));

        var merged = first with
        {
            Polygon = [new(left, top), new(right, top), new(right, bottom), new(left, bottom)],
            Baseline = [new(left, bottom), new(right, bottom)],
        };

        var lines = surface.Lines
            .Where(l => l.Id != secondLineId)
            .Select(l => l.Id == firstLineId ? merged : l)
            .ToList();

        var updated = surface with
        {
            Lines = lines,
            GeometryEdits =
            [
                .. surface.GeometryEdits,
                new GeometryEdit
                {
                    Id = $"ge-{Guid.NewGuid().ToString("N")[..12]}",
                    Kind = GeometryEditKind.LineMerged,
                    At = DateTimeOffset.UtcNow,
                    Author = author,
                    LineId = firstLineId,
                    Before = GeometryJson(first.Polygon, first.Baseline) + "|" + GeometryJson(second.Polygon, second.Baseline),
                    After = GeometryJson(merged.Polygon, merged.Baseline),
                },
            ],
        };
        foundProject = WithSurface(foundProject, updated);
        store.SaveProject(foundProject);
        return (foundProject, updated, merged);
    }

    private static string GeometryJson(IReadOnlyList<PointD> polygon, IReadOnlyList<PointD> baseline) =>
        JsonSerializer.Serialize(new { polygon, baseline });

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
