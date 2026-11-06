using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application.Tests;

public sealed class CaptureGeometryTests
{
    private static readonly PointD[] Polygon = [new(10, 10), new(80, 10), new(80, 40), new(10, 40)];
    private static readonly PointD[] Baseline = [new(10, 38), new(80, 38)];
    private static EntityId Id(EntityKind kind, string suffix) => new(kind, suffix);

    private static (NativeProject Project, NativeSurface Surface, InMemoryStore Store, GeometryService Service) Fixture()
    {
        var project = SampleProjectBuilder.Build();
        var original = project.Objects[0].Surfaces[0];
        var primary = original.Captures[0] with
        {
            Frame = original.Captures[0].Frame with { ImageRef = Id(EntityKind.Capture, "frame-primary") },
            WidthPx = 100, HeightPx = 60,
        };
        var secondary = primary with
        {
            Id = Id(EntityKind.Capture, "secondary"),
            Frame = primary.Frame with { ImageRef = Id(EntityKind.Capture, "frame-secondary") },
        };
        var region = original.Regions[0] with { FrameRef = secondary.Frame.ImageRef };
        var surface = original with { Captures = [primary, secondary], Regions = [region], Lines = [], Revisions = [] };
        project = project with { Objects = [project.Objects[0] with { Surfaces = [surface] }] };
        var store = new InMemoryStore();
        project = project with { Version = store.SaveProject(project) };
        return (project, surface, store, new GeometryService(store));
    }

    [Fact]
    public void Legacy_implicit_region_uses_primary_frame_identity_instead_of_an_unrelated_region()
    {
        var f = Fixture();
        var (_, surface, line) = f.Service.AddLine(f.Project, f.Surface.Id, Polygon, Baseline);
        Assert.Equal(f.Surface.Captures[0].Frame.ImageRef, line.FrameRef);
        Assert.NotEqual(f.Surface.Regions[0].Id, line.RegionId);
        Assert.Equal(2, surface.Regions.Count);
    }

    [Fact]
    public void Merge_refuses_different_frames_even_when_a_malformed_region_contains_both()
    {
        var f = Fixture();
        var a = new NativeLine { Id = Id(EntityKind.Line, "a"), RegionId = f.Surface.Regions[0].Id,
            FrameRef = f.Surface.Captures[0].Frame.ImageRef, Polygon = Polygon, Baseline = Baseline };
        var b = a with { Id = Id(EntityKind.Line, "b"), FrameRef = f.Surface.Captures[1].Frame.ImageRef };
        var surface = f.Surface with { Lines = [a, b] };
        var project = f.Project with { Objects = [f.Project.Objects[0] with { Surfaces = [surface] }] };
        project = project with { Version = f.Store.SaveProject(project) };
        var before = NativeJson.Save(f.Store.LoadProject(project.Id)!);
        var error = Assert.Throws<LeviathanException>(() => f.Service.MergeLines(project, a.Id, b.Id));
        Assert.Equal(ErrorCode.FrameUnregistered, error.Error.Code);
        Assert.Equal(before, NativeJson.Save(f.Store.LoadProject(project.Id)!));
    }

    private static CaptureDisplayDescriptor Display(NativeProject project, NativeSurface surface, int index = 1)
    {
        var capture = surface.Captures[index];
        return new(1, project.Id.ToString(), surface.Id.ToString(), capture.Id.ToString(), capture.Frame.ImageRef.ToString(),
            capture.Frame.ImageChecksum, "verified", SampleProjectBuilder.Sum, 100, 60, 100, 60, 1, 1,
            "raw", "image/png", new string('b', 64), SampleProjectBuilder.Sum);
    }

    [Fact]
    public void Secondary_creation_uses_exact_frame_and_records_observation_without_rewriting_native_checksum()
    {
        var f = Fixture(); var display = Display(f.Project, f.Surface);
        var (project, surface, line) = f.Service.AddLineInCapture(f.Project, f.Surface.Id, f.Surface.Captures[1].Id,
            display, Polygon, Baseline, [], f.Surface.Regions.Select(r => r.Id).ToArray(), author: "editor");
        Assert.Equal(f.Surface.Captures[1].Frame.ImageRef, line.FrameRef);
        Assert.Equal(f.Surface.Regions[0].Id, line.RegionId);
        Assert.Equal(Polygon, line.Polygon); Assert.Equal(Baseline, line.Baseline);
        var edit = Assert.Single(surface.GeometryEdits);
        Assert.Contains(display.DerivativeId, edit.Note); Assert.Contains("observedSourceChecksum", edit.Note);
        Assert.Equal("editor", edit.Author);
        Assert.Equal(f.Surface.Captures, surface.Captures);
        Assert.Equal(surface.Captures, f.Store.LoadProject(project.Id)!.Objects[0].Surfaces[0].Captures);
    }

    [Fact]
    public void Explicit_primary_capture_creates_a_whole_frame_region_without_borrowing_secondary()
    {
        var f = Fixture();
        var (_, surface, line) = f.Service.AddLineInCapture(f.Project, f.Surface.Id, f.Surface.Captures[0].Id,
            Display(f.Project, f.Surface, 0), Polygon, Baseline, [], f.Surface.Regions.Select(r => r.Id).ToArray());
        var region = surface.Regions.Single(r => r.Id == line.RegionId);
        Assert.Equal(f.Surface.Captures[0].Frame.ImageRef, region.FrameRef);
        Assert.Equal([new PointD(0, 0), new(100, 0), new(100, 60), new(0, 60)], region.Polygon);
        Assert.Equal(1, region.ReadingOrderIndex);
    }

    [Theory]
    [InlineData("wrong-region")] [InlineData("wrong-display")] [InlineData("outside-polygon")]
    [InlineData("outside-baseline")] [InlineData("stale-lines")] [InlineData("stale-regions")]
    public void Invalid_or_stale_creation_never_mutates_the_project(string scenario)
    {
        var f = Fixture(); var display = Display(f.Project, f.Surface, 0);
        var before = NativeJson.Save(f.Store.LoadProject(f.Project.Id)!);
        var polygon = scenario == "outside-polygon" ? Polygon.Select(p => p with { X = p.X - 11 }).ToArray() : Polygon;
        var baseline = scenario == "outside-baseline" ? new PointD[] { new(0, 61), new(1, 61) } : Baseline;
        if (scenario == "wrong-display") display = display with { CaptureId = f.Surface.Captures[1].Id.ToString() };
        var error = Assert.Throws<LeviathanException>(() => f.Service.AddLineInCapture(f.Project, f.Surface.Id,
            f.Surface.Captures[0].Id, display, polygon, baseline,
            scenario == "stale-lines" ? [Id(EntityKind.Line, "foreign")] : [],
            scenario == "stale-regions" ? [] : f.Surface.Regions.Select(r => r.Id).ToArray(),
            scenario == "wrong-region" ? f.Surface.Regions[0].Id : null));
        Assert.Equal(scenario.StartsWith("stale") ? ErrorCode.RevisionConflict : ErrorCode.FrameUnregistered, error.Error.Code);
        Assert.Equal(before, NativeJson.Save(f.Store.LoadProject(f.Project.Id)!));
    }

    [Fact]
    public void A_retried_creation_with_the_old_structure_is_rejected_without_duplicate_lines()
    {
        var f = Fixture(); var display = Display(f.Project, f.Surface);
        var regions = f.Surface.Regions.Select(r => r.Id).ToArray();
        var (updated, _, _) = f.Service.AddLineInCapture(f.Project, f.Surface.Id, f.Surface.Captures[1].Id,
            display, Polygon, Baseline, [], regions);
        var error = Assert.Throws<LeviathanException>(() => f.Service.AddLineInCapture(updated, f.Surface.Id,
            f.Surface.Captures[1].Id, display, Polygon, Baseline, [], regions));
        Assert.Equal(ErrorCode.RevisionConflict, error.Error.Code);
        Assert.Single(f.Store.LoadProject(updated.Id)!.Objects[0].Surfaces[0].Lines);
    }

    [Fact]
    public void Frame_region_order_replaces_only_its_existing_global_slots()
    {
        var f = Fixture(); var primary = f.Surface.Captures[0].Frame.ImageRef; var secondary = f.Surface.Captures[1].Frame.ImageRef;
        var regions = Enumerable.Range(0, 5).Select(i => f.Surface.Regions[0] with
        {
            Id = Id(EntityKind.Region, "r" + i), FrameRef = i % 2 == 0 ? primary : secondary, ReadingOrderIndex = i * 10,
        }).ToArray();
        var surface = f.Surface with { Regions = regions };
        var project = f.Project with { Objects = [f.Project.Objects[0] with { Surfaces = [surface] }] };
        project = project with { Version = f.Store.SaveProject(project) };
        var expected = regions.Select(r => r.Id).ToArray();
        var (_, reordered) = f.Service.SetCaptureReadingOrder(project, surface.Id, f.Surface.Captures[0].Id,
            [regions[4].Id, regions[0].Id, regions[2].Id], expected);
        Assert.Equal([regions[4].Id, regions[1].Id, regions[0].Id, regions[3].Id, regions[2].Id],
            reordered.Regions.OrderBy(r => r.ReadingOrderIndex).Select(r => r.Id));
        Assert.Equal(GeometryEditKind.RegionOrder, Assert.Single(reordered.GeometryEdits).Kind);
    }

    [Theory]
    [InlineData("foreign")] [InlineData("duplicate")] [InlineData("incomplete")] [InlineData("stale")]
    public void Frame_order_refuses_invalid_subsets_and_stale_global_order_without_mutation(string mode)
    {
        var f = Fixture(); var before = NativeJson.Save(f.Store.LoadProject(f.Project.Id)!);
        var region = f.Surface.Regions[0].Id;
        var error = Assert.Throws<LeviathanException>(() => f.Service.SetCaptureReadingOrder(f.Project, f.Surface.Id,
            f.Surface.Captures[1].Id,
            mode == "foreign" ? [Id(EntityKind.Region, "elsewhere")] : mode == "duplicate" ? [region, region] : mode == "incomplete" ? [] : [region],
            mode == "stale" ? [] : [region]));
        Assert.Equal(mode == "stale" ? ErrorCode.RevisionConflict : ErrorCode.StageContractViolation, error.Error.Code);
        Assert.Equal(before, NativeJson.Save(f.Store.LoadProject(f.Project.Id)!));
    }

    [Fact]
    public void Frame_line_order_preserves_other_frame_and_other_region_slots()
    {
        var f = Fixture(); var region = f.Surface.Regions[0];
        var a = new NativeLine { Id = Id(EntityKind.Line, "a"), RegionId = region.Id, FrameRef = region.FrameRef };
        var b = a with { Id = Id(EntityKind.Line, "b"), FrameRef = f.Surface.Captures[0].Frame.ImageRef };
        var c = a with { Id = Id(EntityKind.Line, "c") };
        var otherRegion = region with { Id = Id(EntityKind.Region, "other") };
        var other = a with { Id = Id(EntityKind.Line, "outside"), RegionId = otherRegion.Id };
        var surface = f.Surface with { Regions = [region, otherRegion], Lines = [a, other, b, c] };
        var project = f.Project with { Objects = [f.Project.Objects[0] with { Surfaces = [surface] }] };
        project = project with { Version = f.Store.SaveProject(project) };
        var (_, reordered) = f.Service.SetCaptureLineOrder(project, surface.Id, surface.Captures[1].Id, region.Id,
            [c.Id, a.Id], [a.Id, b.Id, c.Id]);
        Assert.Equal([c.Id, other.Id, b.Id, a.Id], reordered.Lines.Select(l => l.Id));
        Assert.Equal(b, reordered.Lines[2]); Assert.Equal(other, reordered.Lines[1]);
    }
}
