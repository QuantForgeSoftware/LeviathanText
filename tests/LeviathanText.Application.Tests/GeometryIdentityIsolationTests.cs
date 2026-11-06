using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application.Tests;

// Native records only. No image, model, provider, training or collection data is opened.
public sealed class GeometryIdentityIsolationTests
{
    static EntityId Id(EntityKind kind, string value) => new(kind, value);
    static NativeSurface Surface(string suffix, EntityId? region = null) {
        var capture = new NativeCapture { Id = Id(EntityKind.Capture, suffix), ImageLocation = "/generated/not-opened.png",
            Frame = new CoordinateFrame { ImageRef = Id(EntityKind.Capture, suffix),
                ImageChecksum = new Checksum { Algorithm = "unavailable", Digest = "generated" } } };
        return new NativeSurface { Id = Id(EntityKind.Surface, suffix), Kind = suffix, Captures = [capture],
            Regions = [new NativeRegion { Id = region ?? Id(EntityKind.Region, suffix), FrameRef = capture.Frame.ImageRef }], Lines = [] };
    }
    static NativeProject Project(params NativeSurface[] surfaces) => new() {
        Id = Id(EntityKind.Collection, "generated"), Name = "generated", Version = 1,
        Objects = [new NativeObject { Id = Id(EntityKind.PhysicalObject, "generated"), Surfaces = surfaces }] };
    sealed class Store(NativeProject project) : IProjectStore {
        public NativeProject Current { get; private set; } = project;
        public int Writes { get; private set; }
        public long SaveProject(NativeProject next) { Writes++; Current = next with { Version = next.Version + 1 }; return Current.Version; }
        public NativeProject? LoadProject(EntityId id) => Current.Id == id ? Current : null;
        public IReadOnlyList<(EntityId Id, string Name)> ListProjects() => [(Current.Id, Current.Name)];
    }
    static void RefusesWithoutWrite(NativeProject project, Action<GeometryService> action) {
        var store = new Store(project); string before = NativeJson.Save(project);
        var failure = Record.Exception(() => action(new GeometryService(store)));
        Assert.True(failure is LeviathanException, $"Expected typed refusal; actual={failure?.GetType().Name ?? "success"}; writes={store.Writes}; nativeUnchanged={before == NativeJson.Save(store.Current)}");
        var error = (LeviathanException)failure!;
        Assert.Equal(ErrorCode.FrameUnregistered, error.Error.Code);
        Assert.Equal(0, store.Writes); Assert.Equal(before, NativeJson.Save(store.Current));
    }
    [Fact]
    public void Capture_line_order_cannot_resolve_a_shared_region_to_another_surface() {
        var first = Surface("first"); var selected = Surface("selected", first.Regions[0].Id);
        var project = Project(first, selected);
        RefusesWithoutWrite(project, service => service.SetCaptureLineOrder(project, selected.Id,
            selected.Captures[0].Id, selected.Regions[0].Id, [], []));
    }
    [Fact]
    public void Region_orientation_cannot_choose_first_of_globally_duplicate_regions() {
        var first = Surface("first"); var selected = Surface("selected", first.Regions[0].Id);
        var project = Project(first, selected);
        RefusesWithoutWrite(project, service => service.SetRegionOrientation(project, first.Regions[0].Id, TextDirection.Rtl, "Grek"));
    }
    [Fact]
    public void Line_orientation_cannot_choose_first_of_globally_duplicate_lines() {
        var first = Surface("first"); var second = Surface("second"); var lineId = Id(EntityKind.Line, "shared");
        first = first with { Lines = [new NativeLine { Id = lineId, RegionId = first.Regions[0].Id, FrameRef = first.Captures[0].Id }] };
        second = second with { Lines = [new NativeLine { Id = lineId, RegionId = second.Regions[0].Id, FrameRef = second.Captures[0].Id, Script = "preserve" }] };
        var project = Project(first, second);
        RefusesWithoutWrite(project, service => service.SetLineOrientation(project, lineId, TextDirection.Rtl, "Grek"));
    }
    [Fact]
    public void Duplicate_surface_identity_cannot_replace_other_surface_evidence() {
        var first = Surface("first"); var other = Surface("other") with { Id = first.Id };
        var project = Project(first, other);
        RefusesWithoutWrite(project, service => service.SetReadingOrder(project, first.Id, [first.Regions[0].Id]));
    }
    [Fact]
    public void Capture_region_order_refuses_duplicate_local_region_ids_before_slot_replacement() {
        var surface = Surface("generated"); surface = surface with { Regions = [surface.Regions[0], surface.Regions[0]] };
        var project = Project(surface);
        RefusesWithoutWrite(project, service => service.SetCaptureReadingOrder(project, surface.Id, surface.Captures[0].Id,
            [surface.Regions[0].Id], [surface.Regions[0].Id, surface.Regions[0].Id]));
    }
    [Fact]
    public void Unambiguous_capture_line_order_updates_only_its_selected_surface() {
        var first = Surface("first"); var selected = Surface("selected");
        var project = Project(first, selected); var store = new Store(project);
        var (saved, changed) = new GeometryService(store).SetCaptureLineOrder(project, selected.Id,
            selected.Captures[0].Id, selected.Regions[0].Id, [], []);
        Assert.Equal(selected.Id, changed.Id); Assert.Equal(1, store.Writes);
        Assert.Equal(NativeJson.Save(Project(first)), NativeJson.Save(Project(saved.Objects[0].Surfaces[0])));
        Assert.Single(changed.GeometryEdits); Assert.Empty(saved.Objects[0].Surfaces[0].GeometryEdits);
    }
}
