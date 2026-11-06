using System.Text.Json;
using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application;

public sealed record LineGeometrySnapshot(IReadOnlyList<PointD> Polygon, IReadOnlyList<PointD> Baseline);

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
        string author = "manual-editor", LineGeometrySnapshot? expectedGeometry = null)
    {
        Validate(polygon, baseline, out var validatedPolygon, out var validatedBaseline);
        return SetLineGeometryCore(project, lineId, validatedPolygon, validatedBaseline, author, expectedGeometry, null);
    }

    /// <summary>Update existing geometry only in the exact capture/raw frame whose display was observed.</summary>
    public (NativeProject Project, NativeSurface Surface, NativeLine Line) SetLineGeometryInCapture(
        NativeProject project, EntityId surfaceId, EntityId captureId, CaptureDisplayDescriptor display,
        EntityId lineId, IReadOnlyList<PointD> polygon, IReadOnlyList<PointD> baseline,
        LineGeometrySnapshot expectedGeometry, string author = "manual-editor")
    {
        var capture = RequireDisplayFrame(project, surfaceId, captureId, display);
        var matches = project.Objects.SelectMany(obj => obj.Surfaces)
            .SelectMany(surface => surface.Lines.Where(line => line.Id == lineId).Select(line => (Surface: surface, Line: line)))
            .ToArray();
        if (matches.Length != 1)
            throw InvalidFrame(lineId, "The line identity is missing or ambiguous; repair its native registration before editing.");
        var target = matches[0];
        if (target.Surface.Id != surfaceId || target.Line.FrameRef != capture.Frame.ImageRef)
            throw InvalidFrame(lineId, "The line does not belong to the selected surface and raw source frame.");
        var regions = target.Surface.Regions.Where(region => region.Id == target.Line.RegionId).ToArray();
        if (regions.Length != 1 || regions[0].FrameRef != capture.Frame.ImageRef)
            throw InvalidFrame(lineId, "The line's region registration is inconsistent with this capture. Repair its native registration before editing.");
        if (expectedGeometry is null)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation, Scope = lineId.ToString(),
                Message = "Capture-bound edits require the exact loaded geometry as an expected snapshot.",
            });
        Validate(polygon, baseline, out var validatedPolygon, out var validatedBaseline);
        RequireRawBounds(surfaceId, display, validatedPolygon, validatedBaseline);
        return SetLineGeometryCore(project, lineId, validatedPolygon, validatedBaseline, author, expectedGeometry, display);
    }

    private (NativeProject Project, NativeSurface Surface, NativeLine Line) SetLineGeometryCore(
        NativeProject project, EntityId lineId, IReadOnlyList<PointD> validatedPolygon, IReadOnlyList<PointD> validatedBaseline,
        string author, LineGeometrySnapshot? expectedGeometry, CaptureDisplayDescriptor? display)
    {
        var (foundProject, surface, line) = FindLine(project, lineId);
        if (expectedGeometry is not null)
        {
            if (expectedGeometry.Polygon is null || expectedGeometry.Baseline is null)
                throw new LeviathanException(new LeviathanError
                {
                    Code = ErrorCode.StageContractViolation,
                    Scope = lineId.ToString(),
                    Message = "Expected geometry requires polygon and baseline arrays.",
                });
            if (!line.Polygon.SequenceEqual(expectedGeometry.Polygon)
                || !line.Baseline.SequenceEqual(expectedGeometry.Baseline))
                throw new LeviathanException(new LeviathanError
                {
                    Code = ErrorCode.RevisionConflict,
                    Scope = lineId.ToString(),
                    Message = "Line geometry changed since it was loaded.",
                });
        }

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
                    Note = CaptureDisplayEvidence(display),
                },
            ],
        };
        foundProject = WithSurface(foundProject, surface);
        foundProject = foundProject with { Version = store.SaveProject(foundProject) };
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
        foundProject = foundProject with { Version = store.SaveProject(foundProject) };
        return (foundProject, updated, updatedLine);
    }

    /// <summary>Set or clear a region's declared direction/script (line values override it in consumers that support inheritance).</summary>
    public (NativeProject Project, NativeSurface Surface, NativeRegion Region) SetRegionOrientation(
        NativeProject project, EntityId regionId, TextDirection? direction, string? script,
        string author = "api")
    {
        var (surface, region) = FindRegion(project, regionId);
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
        foundProject = foundProject with { Version = store.SaveProject(foundProject) };
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
        => AddLineCore(project, surfaceId, polygon, baseline, regionId, author, null);

    /// <summary>Create geometry in the exact raw frame whose bounded display the editor loaded.</summary>
    public (NativeProject Project, NativeSurface Surface, NativeLine Line) AddLineInCapture(
        NativeProject project, EntityId surfaceId, EntityId captureId, CaptureDisplayDescriptor display,
        IReadOnlyList<PointD> polygon, IReadOnlyList<PointD> baseline,
        IReadOnlyList<EntityId> expectedLineIds, IReadOnlyList<EntityId> expectedRegionIds,
        EntityId? regionId = null, string author = "manual-editor")
    {
        RequireDisplayFrame(project, surfaceId, captureId, display);
        var (_, surface) = FindSurface(project, surfaceId);
        if (expectedLineIds is null || expectedRegionIds is null)
            throw InvalidFrame(surfaceId, "The loaded surface line and region sequences are required.");
        RequireExpectedOrder(surface.Lines.Select(l => l.Id).ToArray(), expectedLineIds, surfaceId);
        RequireExpectedOrder(surface.Regions.Select(r => r.Id).ToArray(), expectedRegionIds, surfaceId);
        Validate(polygon, baseline, out var validatedPolygon, out var validatedBaseline);
        RequireRawBounds(surfaceId, display, validatedPolygon, validatedBaseline);
        return AddLineCore(project, surfaceId, validatedPolygon, validatedBaseline, regionId, author, display);
    }

    private (NativeProject Project, NativeSurface Surface, NativeLine Line) AddLineCore(
        NativeProject project, EntityId surfaceId,
        IReadOnlyList<PointD> polygon, IReadOnlyList<PointD> baseline, EntityId? regionId,
        string author, CaptureDisplayDescriptor? display)
    {
        Validate(polygon, baseline, out var validatedPolygon, out var validatedBaseline);
        var (foundProject, surface) = FindSurface(project, surfaceId);
        var targetFrame = display is not null
            ? EntityId.Parse(display.SourceFrameRef)
            : surface.Captures.FirstOrDefault()?.Frame.ImageRef;

        NativeRegion? region = null;
        if (regionId is { } requested)
        {
            var matches = surface.Regions.Where(r => r.Id == requested).ToArray();
            if (matches.Length > 1) throw InvalidFrame(requested, "The selected region identity is ambiguous.");
            region = matches.FirstOrDefault()
                ?? throw new LeviathanException(new LeviathanError
                {
                    Code = ErrorCode.InputMissing,
                    Scope = requested.ToString(),
                    Message = "No such region on the surface.",
                });
            if (display is not null && region.FrameRef != targetFrame)
                throw InvalidFrame(requested, "The selected region belongs to a different source frame.");
        }

        if (region is null)
        {
            if (targetFrame is null)
                throw InvalidFrame(surfaceId, "New geometry requires an explicit source frame.");
            region = surface.Regions.Where(r => r.FrameRef == targetFrame).OrderBy(r => r.ReadingOrderIndex).FirstOrDefault()
                ?? new NativeRegion
                {
                    Id = new EntityId(EntityKind.Region, Guid.NewGuid().ToString("N")[..12]),
                    FrameRef = targetFrame.Value,
                    Polygon =
                    [
                        new PointD(0, 0),
                        new PointD(display?.SourceWidth ?? polygon.Max(p => p.X), 0),
                        new PointD(display?.SourceWidth ?? polygon.Max(p => p.X), display?.SourceHeight ?? polygon.Max(p => p.Y)),
                        new PointD(0, display?.SourceHeight ?? polygon.Max(p => p.Y)),
                    ],
                    LayoutSource = LayoutSource.Manual,
                    ReadingOrderIndex = surface.Regions.Count == 0 ? 0 : checked(surface.Regions.Max(r => r.ReadingOrderIndex ?? -1) + 1),
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
                    Note = CaptureDisplayEvidence(display),
                },
            ],
        };
        foundProject = WithSurface(foundProject, updated);
        foundProject = foundProject with { Version = store.SaveProject(foundProject) };
        return (foundProject, updated, line);
    }

    private static LeviathanException InvalidFrame(EntityId scope, string message) => new(new LeviathanError
    {
        Code = ErrorCode.FrameUnregistered, Scope = scope.ToString(), Message = message,
    });

    private static NativeCapture RequireDisplayFrame(NativeProject project, EntityId surfaceId, EntityId captureId,
        CaptureDisplayDescriptor display)
    {
        var capture = CaptureDisplayService.ResolveCapture(project, surfaceId, captureId);
        if (display is null || display.ProjectId != project.Id.ToString() || display.SurfaceId != surfaceId.ToString()
            || display.CaptureId != captureId.ToString() || display.SourceFrameRef != capture.Frame.ImageRef.ToString()
            || display.Orientation != "raw" || display.SourceWidth <= 0 || display.SourceHeight <= 0)
            throw InvalidFrame(surfaceId, "The display does not identify this capture's raw source frame.");
        CaptureDisplayService.ValidateObservedFrameDimensions(project, capture.Frame.ImageRef, display.SourceWidth, display.SourceHeight);
        return capture;
    }

    private static void RequireRawBounds(EntityId scope, CaptureDisplayDescriptor display,
        IReadOnlyList<PointD> polygon, IReadOnlyList<PointD> baseline)
    {
        if (polygon.Concat(baseline).Any(p => p.X < 0 || p.Y < 0 || p.X > display.SourceWidth || p.Y > display.SourceHeight))
            throw InvalidFrame(scope, "Geometry must stay within the raw source frame.");
    }

    private static string? CaptureDisplayEvidence(CaptureDisplayDescriptor? display) => display is null ? null : JsonSerializer.Serialize(new
    {
        kind = "capture-display-evidence", captureId = display.CaptureId,
        sourceFrameRef = display.SourceFrameRef, observedSourceChecksum = display.ObservedSourceChecksum,
        derivativeId = display.DerivativeId, sourceWidth = display.SourceWidth, sourceHeight = display.SourceHeight,
    });

    /// <summary>
    /// Delete a line that carries no transcription evidence. Lines with an L1 revision are
    /// refused: removing the scope would orphan reviewed text (delete the revision history
    /// deliberately instead, once a review workflow exists).
    /// </summary>
    public (NativeProject Project, NativeSurface Surface) DeleteLine(
        NativeProject project, EntityId lineId, string author = "manual-editor")
    {
        var (foundProject, surface, line) = FindLine(project, lineId);
        RequireNoLineEvidence(surface, line);

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
        foundProject = foundProject with { Version = store.SaveProject(foundProject) };
        return (foundProject, updated);
    }

    public (NativeProject Project, NativeSurface Surface) SetReadingOrder(
        NativeProject project, EntityId surfaceId, IReadOnlyList<EntityId> orderedRegionIds,
        string author = "manual-editor", IReadOnlyList<EntityId>? expectedRegionIds = null)
    {
        var (foundProject, surface) = FindSurface(project, surfaceId);

        RequireUniqueOrder(surface.Regions.Select(r => r.Id).ToArray(), surfaceId);
        var existing = surface.Regions.Select(r => r.Id).ToHashSet();
        var requested = orderedRegionIds?.ToHashSet() ?? [];
        if (orderedRegionIds is null || orderedRegionIds.Count != existing.Count
            || existing.Count != requested.Count || !existing.SetEquals(requested))
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

        var currentOrder = surface.Regions.OrderBy(r => r.ReadingOrderIndex).Select(r => r.Id).ToArray();
        RequireExpectedOrder(currentOrder, expectedRegionIds, surfaceId);

        var index = orderedRegionIds.Select((id, position) => (id, position)).ToDictionary(p => p.id, p => p.position);
        var beforeOrder = string.Join(',', currentOrder);
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
        foundProject = foundProject with { Version = store.SaveProject(foundProject) };
        return (foundProject, surface);
    }

    /// <summary>Replace only this capture frame's slots in the complete surface order.</summary>
    public (NativeProject Project, NativeSurface Surface) SetCaptureReadingOrder(
        NativeProject project, EntityId surfaceId, EntityId captureId, IReadOnlyList<EntityId> orderedRegionIds,
        IReadOnlyList<EntityId> expectedSurfaceRegionIds, string author = "manual-editor")
    {
        var capture = CaptureDisplayService.ResolveCapture(project, surfaceId, captureId);
        var (_, surface) = FindSurface(project, surfaceId);
        var current = surface.Regions.OrderBy(r => r.ReadingOrderIndex).Select(r => r.Id).ToArray();
        RequireCompleteExpectedOrder(current, expectedSurfaceRegionIds, surfaceId);
        var selected = surface.Regions.Where(r => r.FrameRef == capture.Frame.ImageRef).Select(r => r.Id).ToHashSet();
        var replacement = ReplaceFrameSlots(current, selected, orderedRegionIds, surfaceId);
        return SetReadingOrder(project, surfaceId, replacement, author, expectedSurfaceRegionIds);
    }

    /// <summary>Replace only compatible line slots, leaving other-frame lines in place.</summary>
    public (NativeProject Project, NativeSurface Surface) SetCaptureLineOrder(
        NativeProject project, EntityId surfaceId, EntityId captureId, EntityId regionId,
        IReadOnlyList<EntityId> orderedLineIds, IReadOnlyList<EntityId> expectedRegionLineIds,
        string author = "manual-editor")
    {
        var capture = CaptureDisplayService.ResolveCapture(project, surfaceId, captureId);
        var (_, surface) = FindSurface(project, surfaceId);
        var matches = surface.Regions.Where(r => r.Id == regionId).ToArray();
        if (matches.Length > 1) throw InvalidFrame(regionId, "The selected region identity is ambiguous.");
        var region = matches.FirstOrDefault()
            ?? throw InvalidFrame(regionId, "The region is not on this surface.");
        if (region.FrameRef != capture.Frame.ImageRef)
            throw InvalidFrame(regionId, "The region belongs to a different source frame.");
        var lines = surface.Lines.Where(l => l.RegionId == regionId).ToArray();
        var current = lines.Select(l => l.Id).ToArray();
        RequireCompleteExpectedOrder(current, expectedRegionLineIds, regionId);
        var selected = lines.Where(l => l.FrameRef == capture.Frame.ImageRef).Select(l => l.Id).ToHashSet();
        var replacement = ReplaceFrameSlots(current, selected, orderedLineIds, regionId);
        return SetLineOrder(project, regionId, replacement, author, expectedRegionLineIds);
    }

    private static void RequireCompleteExpectedOrder(IReadOnlyList<EntityId> current,
        IReadOnlyList<EntityId>? expected, EntityId scope)
    {
        RequireUniqueOrder(current, scope);
        if (expected is null) throw InvalidFrame(scope, "The complete loaded order is required.");
        RequireExpectedOrder(current, expected, scope);
    }

    private static void RequireUniqueOrder(IReadOnlyList<EntityId> current, EntityId scope)
    {
        if (current.Distinct().Count() != current.Count)
            throw InvalidFrame(scope, "The stored order contains ambiguous native identities. Repair its registration explicitly before editing.");
    }

    private static IReadOnlyList<EntityId> ReplaceFrameSlots(IReadOnlyList<EntityId> current,
        HashSet<EntityId> selected, IReadOnlyList<EntityId>? requested, EntityId scope)
    {
        if (requested is null || requested.Count != selected.Count || requested.Distinct().Count() != requested.Count
            || !selected.SetEquals(requested))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation, Scope = scope.ToString(),
                Message = "Frame order must list every compatible item exactly once, without items from other frames.",
            });
        var pointer = 0;
        return current.Select(id => selected.Contains(id) ? requested[pointer++] : id).ToArray();
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

        if (polygon is null || baseline is null || polygon.Count < 3)
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
        string author = "manual-editor", IReadOnlyList<EntityId>? expectedLineIds = null)
    {
        var (surface, _) = FindRegion(project, regionId);
        var currentOrder = surface.Lines.Where(l => l.RegionId == regionId).Select(l => l.Id).ToArray();
        RequireUniqueOrder(currentOrder, regionId);
        var expected = currentOrder.ToHashSet();
        var requested = orderedLineIds?.ToHashSet() ?? [];
        if (orderedLineIds is null || orderedLineIds.Count != expected.Count
            || expected.Count != requested.Count || !expected.SetEquals(requested))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = regionId.ToString(),
                Message = "Line order must list every line of the region exactly once.",
            });

        RequireExpectedOrder(currentOrder, expectedLineIds, regionId);

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

        var beforeOrder = string.Join(',', currentOrder);
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
        foundProject = foundProject with { Version = store.SaveProject(foundProject) };
        return (foundProject, updated);
    }

    private static void RequireExpectedOrder(IReadOnlyList<EntityId> current, IReadOnlyList<EntityId>? expected, EntityId scope)
    {
        if (expected is null || current.SequenceEqual(expected)) return;
        throw new LeviathanException(new LeviathanError
        {
            Code = ErrorCode.RevisionConflict, Scope = scope.ToString(),
            RemedyHint = RemedyHint.RebaseRevision,
            Message = "Order changed since it was loaded; keep the draft and reload before reconciling.",
        });
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
        RequireNoLineEvidence(surface, line);
        RequirePlacedPolygon(line);

        double top = line.Polygon.Min(p => p.Y);
        double bottom = line.Polygon.Max(p => p.Y);
        double left = line.Polygon.Min(p => p.X);
        double right = line.Polygon.Max(p => p.X);
        if (!double.IsFinite(splitY) || splitY <= top + 2 || splitY >= bottom - 2)
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
        foundProject = foundProject with { Version = store.SaveProject(foundProject) };
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
        RequireNoLineEvidence(surface, first);
        var second = surface.Lines.FirstOrDefault(l => l.Id == secondLineId)
            ?? throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = secondLineId.ToString(),
                Message = "No such line on the first line's surface.",
            });
        RequireNoLineEvidence(surface, second);
        if (first.FrameRef != second.FrameRef)
            throw InvalidFrame(secondLineId, "Lines in different source frames cannot be merged.");
        if (second.RegionId != first.RegionId)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = secondLineId.ToString(),
                Message = "Lines belong to different regions.",
            });

        RequirePlacedPolygon(first);
        RequirePlacedPolygon(second);

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
        foundProject = foundProject with { Version = store.SaveProject(foundProject) };
        return (foundProject, updated, merged);
    }

    private static void RequireNoLineEvidence(NativeSurface surface, NativeLine line)
    {
        if (line.DiplomaticRevisionId is null && !surface.Revisions.Any(r => r.ScopeId == line.Id)
            && !surface.ReviewDecisions.Any(r => r.LineId == line.Id)) return;
        throw new LeviathanException(new LeviathanError
        {
            Code = ErrorCode.RevisionConflict, Scope = line.Id.ToString(), RemedyHint = RemedyHint.RebaseRevision,
            Message = "Line carries revision or review evidence; structural removal or repartition would detach it from its source geometry.",
        });
    }

    private static void RequirePlacedPolygon(NativeLine line)
    {
        if (line.Polygon.Count >= 3 && line.Polygon.All(p => double.IsFinite(p.X) && double.IsFinite(p.Y))
            && line.Polygon.Max(p => p.X) > line.Polygon.Min(p => p.X)
            && line.Polygon.Max(p => p.Y) > line.Polygon.Min(p => p.Y)) return;
        throw new LeviathanException(new LeviathanError
        {
            Code = ErrorCode.StageContractViolation, Scope = line.Id.ToString(),
            Message = "Structural repartition requires a finite placed polygon with nonzero bounds; place the line geometry first.",
        });
    }

    /// <summary>
    /// Restore a line's recorded geometry from an append-only audit entry. This is a bounded
    /// recovery path for <see cref="GeometryEditKind.LineGeometry"/> only; line add/delete,
    /// order, split/merge and orientation edits are not replayed. The restore is itself a new
    /// audit entry, so history remains append-only.
    /// </summary>
    public (NativeProject Project, NativeSurface Surface, GeometryEdit Edit) RestoreLineGeometry(
        NativeProject project, EntityId surfaceId, string editId, string author = "manual-editor")
    {
        var (foundProject, surface) = FindSurface(project, surfaceId);
        var source = surface.GeometryEdits.FirstOrDefault(e => e.Id == editId)
            ?? throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = editId,
                Message = "No such geometry edit on this surface.",
            });
        if (source.Kind != GeometryEditKind.LineGeometry || source.LineId is not { } lineId
            || string.IsNullOrWhiteSpace(source.Before))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = editId,
                Message = "Only a line-geometry edit with a recorded before-state can be restored.",
            });

        var snapshot = JsonSerializer.Deserialize<LineGeometrySnapshot>(source.Before,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = editId,
                Message = "The recorded geometry snapshot is unreadable.",
            });
        var line = surface.Lines.FirstOrDefault(l => l.Id == lineId)
            ?? throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = lineId.ToString(),
                Message = "The line no longer exists on this surface.",
            });

        var current = GeometryJson(line.Polygon, line.Baseline);
        var restored = GeometryJson(snapshot.Polygon, snapshot.Baseline);
        var edit = new GeometryEdit
        {
            Id = $"ge-{Guid.NewGuid().ToString("N")[..12]}",
            Kind = GeometryEditKind.LineGeometry,
            At = DateTimeOffset.UtcNow,
            Author = author,
            LineId = lineId,
            Before = current,
            After = restored,
            Note = $"restored {editId}",
        };
        var updated = surface with
        {
            Lines = [.. surface.Lines.Select(l => l.Id == lineId
                ? l with { Polygon = snapshot.Polygon, Baseline = snapshot.Baseline }
                : l)],
            GeometryEdits = [.. surface.GeometryEdits, edit],
        };
        foundProject = WithSurface(foundProject, updated);
        foundProject = foundProject with { Version = store.SaveProject(foundProject) };
        return (foundProject, updated, edit);
    }

    private static string GeometryJson(IReadOnlyList<PointD> polygon, IReadOnlyList<PointD> baseline) =>
        JsonSerializer.Serialize(new { polygon, baseline });

    private static (NativeProject Project, NativeSurface Surface) FindSurface(NativeProject project, EntityId surfaceId)
    {
        var matches = project.Objects.SelectMany(obj => obj.Surfaces).Where(surface => surface.Id == surfaceId).Take(2).ToArray();
        if (matches.Length > 1) throw InvalidFrame(surfaceId, "The surface identity is ambiguous; no native evidence was changed.");
        if (matches.Length == 1) return (project, matches[0]);
        throw new LeviathanException(new LeviathanError
        {
            Code = ErrorCode.InputMissing, Scope = surfaceId.ToString(), Message = "No such surface in the project.",
        });
    }

    private static (NativeProject Project, NativeSurface Surface, NativeLine Line) FindLine(NativeProject project, EntityId lineId)
    {
        var matches = project.Objects.SelectMany(obj => obj.Surfaces)
            .SelectMany(surface => surface.Lines.Where(line => line.Id == lineId).Select(line => (Surface: surface, Line: line)))
            .Take(2).ToArray();
        if (matches.Length > 1) throw InvalidFrame(lineId, "The line identity is ambiguous; no native evidence was changed.");
        if (matches.Length == 1)
        {
            FindSurface(project, matches[0].Surface.Id);
            return (project, matches[0].Surface, matches[0].Line);
        }
        throw new LeviathanException(new LeviathanError
        {
            Code = ErrorCode.InputMissing, Scope = lineId.ToString(), Message = "No such line in the project.",
        });
    }

    private static (NativeSurface Surface, NativeRegion Region) FindRegion(NativeProject project, EntityId regionId)
    {
        var matches = project.Objects.SelectMany(obj => obj.Surfaces)
            .SelectMany(surface => surface.Regions.Where(region => region.Id == regionId).Select(region => (Surface: surface, Region: region)))
            .Take(2).ToArray();
        if (matches.Length > 1) throw InvalidFrame(regionId, "The region identity is ambiguous; no native evidence was changed.");
        if (matches.Length == 1)
        {
            FindSurface(project, matches[0].Surface.Id);
            return matches[0];
        }
        throw new LeviathanException(new LeviathanError
        {
            Code = ErrorCode.InputMissing, Scope = regionId.ToString(), Message = "No such region in the project.",
        });
    }

    private static NativeProject WithSurface(NativeProject project, NativeSurface surface)
    {
        // Even callers that located a line/region must not replace multiple surfaces.
        FindSurface(project, surface.Id);
        return project with
        {
            Objects = [.. project.Objects.Select(o => o.Surfaces.Any(s => s.Id == surface.Id)
                ? o with { Surfaces = [.. o.Surfaces.Select(s => s.Id == surface.Id ? surface : s)] }
                : o)],
        };
    }
}
