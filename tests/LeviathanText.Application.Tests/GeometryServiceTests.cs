using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application.Tests;

public sealed class GeometryServiceTests
{
    private static (EntityId LineId, EntityId SurfaceId, GeometryService Service, InMemoryStore Store) Setup()
    {
        var store = new InMemoryStore();
        store.SaveProject(SampleProjectBuilder.Build());
        var project = SampleProjectBuilder.Build();
        var surface = project.Objects[0].Surfaces[0];
        return (surface.Lines[0].Id, surface.Id, new GeometryService(store), store);
    }

    private static NativeProject Load(InMemoryStore store, EntityId id) =>
        store.LoadProject(id) ?? throw new InvalidOperationException("project missing");

    [Fact]
    public void SetLineGeometry_updates_and_persists()
    {
        var (lineId, _, service, store) = Setup();
        var project = Load(store, SampleProjectBuilder.Build().Id);
        IReadOnlyList<PointD> polygon = [new(5, 5), new(120, 5), new(120, 40), new(5, 40)];
        IReadOnlyList<PointD> baseline = [new(6, 38), new(118, 36)];

        var (updated, _, line) = service.SetLineGeometry(project, lineId, polygon, baseline);

        Assert.Equal(polygon, line.Polygon);
        Assert.Equal(baseline, line.Baseline);
        var reloaded = Load(store, updated.Id).Objects[0].Surfaces[0].Lines[0];
        Assert.Equal(4, reloaded.Polygon.Count);
        Assert.Equal(2, reloaded.Baseline.Count);
    }

    [Fact]
    public void Polygon_with_fewer_than_three_points_is_rejected()
    {
        var (lineId, _, service, store) = Setup();
        var project = Load(store, SampleProjectBuilder.Build().Id);
        var ex = Assert.Throws<LeviathanException>(() =>
            service.SetLineGeometry(project, lineId, [new(0, 0), new(1, 1)], []));
        Assert.Equal(ErrorCode.StageContractViolation, ex.Error.Code);
    }

    [Fact]
    public void Infinite_coordinates_are_rejected()
    {
        var (lineId, _, service, store) = Setup();
        var project = Load(store, SampleProjectBuilder.Build().Id);
        var ex = Assert.Throws<LeviathanException>(() =>
            service.SetLineGeometry(project, lineId,
                [new(0, 0), new(double.PositiveInfinity, 1), new(1, 1)], []));
        Assert.Equal(ErrorCode.StageContractViolation, ex.Error.Code);
        Assert.Contains("finite", ex.Error.Message);
    }

    [Fact]
    public void Baseline_with_one_point_is_rejected()
    {
        var (lineId, _, service, store) = Setup();
        var project = Load(store, SampleProjectBuilder.Build().Id);
        var ex = Assert.Throws<LeviathanException>(() =>
            service.SetLineGeometry(project, lineId,
                [new(0, 0), new(10, 0), new(10, 10)], [new(1, 1)]));
        Assert.Contains("two points", ex.Error.Message);
    }

    [Fact]
    public void SetReadingOrder_reorders_regions_and_persists()
    {
        var (_, surfaceId, service, store) = Setup();
        var project = Load(store, SampleProjectBuilder.Build().Id);
        // Fixture has one region; add a second region to the surface for a real reorder.
        var original = project.Objects[0].Surfaces[0];
        var second = new NativeRegion
        {
            Id = new EntityId(EntityKind.Region, "reg2"),
            FrameRef = original.Regions[0].FrameRef,
            Polygon = [new(1, 1), new(50, 1), new(50, 20), new(1, 20)],
            ReadingOrderIndex = 1,
        };
        var withTwo = project with
        {
            Objects =
            [
                project.Objects[0] with
                {
                    Surfaces = [original with { Regions = [.. original.Regions, second] }],
                },
            ],
        };
        store.SaveProject(withTwo);

        var (updated, surface) = service.SetReadingOrder(withTwo, surfaceId,
            [new EntityId(EntityKind.Region, "reg2"), new EntityId(EntityKind.Region, "reg1")]);

        Assert.Equal(0, surface.Regions.Single(r => r.Id == new EntityId(EntityKind.Region, "reg2")).ReadingOrderIndex);
        Assert.Equal(1, surface.Regions.Single(r => r.Id == new EntityId(EntityKind.Region, "reg1")).ReadingOrderIndex);
        var reloaded = Load(store, updated.Id).Objects[0].Surfaces[0];
        Assert.Equal(0, reloaded.Regions.Single(r => r.Id == new EntityId(EntityKind.Region, "reg2")).ReadingOrderIndex);
    }

    [Fact]
    public void SetReadingOrder_rejects_incomplete_or_foreign_sets()
    {
        var (_, surfaceId, service, store) = Setup();
        var project = Load(store, SampleProjectBuilder.Build().Id);

        var missing = Assert.Throws<LeviathanException>(() =>
            service.SetReadingOrder(project, surfaceId, []));
        Assert.Equal(ErrorCode.StageContractViolation, missing.Error.Code);

        var foreign = Assert.Throws<LeviathanException>(() =>
            service.SetReadingOrder(project, surfaceId,
                [new EntityId(EntityKind.Region, "reg1"), new EntityId(EntityKind.Region, "ghost")]));
        Assert.Contains("exactly once", foreign.Error.Message);
    }

    [Fact]
    public void SetLineOrder_reorders_lines_within_a_region_and_preserves_other_regions()
    {
        var (lineId, surfaceId, service, store) = Setup();
        var project = Load(store, SampleProjectBuilder.Build().Id);
        var surface = project.Objects[0].Surfaces[0];

        // Add a second region with one line, and a second line in the first region.
        var otherRegion = new NativeRegion
        {
            Id = new EntityId(EntityKind.Region, "reg-other"),
            FrameRef = surface.Regions[0].FrameRef,
            Polygon = [new(1, 1), new(30, 1), new(30, 20), new(1, 20)],
            ReadingOrderIndex = 1,
        };
        var otherLine = new NativeLine
        {
            Id = new EntityId(EntityKind.Line, "ln-other"),
            RegionId = otherRegion.Id,
            FrameRef = surface.Regions[0].FrameRef,
        };
        var secondLine = new NativeLine
        {
            Id = new EntityId(EntityKind.Line, "ln-second"),
            RegionId = surface.Regions[0].Id,
            FrameRef = surface.Regions[0].FrameRef,
        };
        var expanded = project with
        {
            Objects = [project.Objects[0] with
            {
                Surfaces = [surface with
                {
                    Regions = [.. surface.Regions, otherRegion],
                    Lines = [.. surface.Lines, secondLine, otherLine],
                }],
            }],
        };
        store.SaveProject(expanded);

        var (updated, updatedSurface) = service.SetLineOrder(expanded, surface.Regions[0].Id,
            [new EntityId(EntityKind.Line, "ln-second"), lineId]);

        var regionLines = updatedSurface.Lines
            .Where(l => l.RegionId == surface.Regions[0].Id)
            .Select(l => l.Id.Suffix)
            .ToList();
        Assert.Equal(["ln-second", "ln1"], regionLines);
        // The other region's line is untouched and still present.
        Assert.Contains(updatedSurface.Lines, l => l.Id.Suffix == "ln-other");

        var reloaded = Load(store, updated.Id).Objects[0].Surfaces[0];
        Assert.Equal("ln-second", reloaded.Lines.First(l => l.RegionId == surface.Regions[0].Id).Id.Suffix);
    }

    [Fact]
    public void AddLine_creates_a_manual_region_when_none_exists()
    {
        var store = new InMemoryStore();
        var captureId = new EntityId(EntityKind.Capture, "cap1");
        var surface = new NativeSurface
        {
            Id = new EntityId(EntityKind.Surface, "surf-empty"),
            Kind = "page",
            Captures = [new NativeCapture
            {
                Id = captureId,
                Frame = new CoordinateFrame { ImageRef = captureId, ImageChecksum = new Checksum { Algorithm = "unavailable", Digest = "pending" } },
                ImageLocation = "page.png",
            }],
            Regions = [],
            Lines = [],
        };
        var project = new NativeProject
        {
            Id = new EntityId(EntityKind.Collection, "col-empty"),
            Name = "manual",
            Objects = [new NativeObject { Id = new EntityId(EntityKind.PhysicalObject, "obj"), Surfaces = [surface] }],
        };
        store.SaveProject(project);

        var (updated, updatedSurface, line) = new GeometryService(store).AddLine(
            project, surface.Id,
            [new(10, 10), new(100, 10), new(100, 40), new(10, 40)],
            [new(10, 38), new(100, 38)]);

        Assert.Single(updatedSurface.Regions);
        Assert.Equal(LayoutSource.Manual, updatedSurface.Regions[0].LayoutSource);
        Assert.Equal(updatedSurface.Regions[0].Id, line.RegionId);
        Assert.Single(store.LoadProject(updated.Id)!.Objects[0].Surfaces[0].Lines);
    }

    [Fact]
    public void DeleteLine_removes_untranscribed_but_refuses_transcribed_evidence()
    {
        var (lineId, _, service, store) = Setup();
        var project = Load(store, SampleProjectBuilder.Build().Id);
        // The fixture line has an L1 revision: deletion is refused.
        var ex = Assert.Throws<LeviathanException>(() => service.DeleteLine(project, lineId));
        Assert.Equal(ErrorCode.RevisionConflict, ex.Error.Code);

        // An untranscribed line can be removed.
        var (withExtra, surfaceWithExtra, _) = service.AddLine(project, project.Objects[0].Surfaces[0].Id,
            [new(1, 1), new(30, 1), new(30, 20), new(1, 20)], [new(1, 18), new(30, 18)]);
        var added = surfaceWithExtra.Lines[^1];
        var (afterDelete, surfaceAfter) = service.DeleteLine(withExtra, added.Id);
        Assert.DoesNotContain(surfaceAfter.Lines, l => l.Id == added.Id);
        Assert.Single(store.LoadProject(afterDelete.Id)!.Objects[0].Surfaces[0].Lines);
    }

    [Fact]
    public void SetLineOrder_rejects_incomplete_sets()
    {
        var (_, _, service, store) = Setup();
        var project = Load(store, SampleProjectBuilder.Build().Id);
        var regionId = project.Objects[0].Surfaces[0].Regions[0].Id;

        var ex = Assert.Throws<LeviathanException>(() => service.SetLineOrder(project, regionId, []));
        Assert.Equal(ErrorCode.StageContractViolation, ex.Error.Code);
        Assert.Contains("exactly once", ex.Error.Message);
    }
}
