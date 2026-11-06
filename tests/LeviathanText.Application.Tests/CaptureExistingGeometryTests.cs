using LeviathanText.Contracts;
using LeviathanText.Contracts.Serialization;
using LeviathanText.Core.Revisions;
using LeviathanText.Formats;

namespace LeviathanText.Application.Tests;

// Generated records only: no image/data/model loading and no scholarly decisions.
public sealed class CaptureExistingGeometryTests
{
    private static readonly PointD[] Original = [new(10, 10), new(40, 10), new(40, 25), new(10, 25)];
    private static readonly PointD[] OriginalBaseline = [new(10, 23), new(40, 23)];
    private static readonly PointD[] Updated = [new(15, 15), new(45, 15), new(45, 30), new(15, 30)];
    private static readonly PointD[] UpdatedBaseline = [new(15, 28), new(45, 28)];
    private static EntityId Id(EntityKind kind, string suffix) => new(kind, suffix);

    private static (NativeProject Project, NativeSurface Surface, CaptureDisplayDescriptor Display, SpyStore Store, GeometryService Service) Fixture()
    {
        var checksum = new Checksum { Algorithm = "unavailable", Digest = "generated" };
        var capture = new NativeCapture { Id = Id(EntityKind.Capture, "scoped"), ImageLocation = "/generated/not-opened.png",
            Frame = new CoordinateFrame { ImageRef = Id(EntityKind.Derivative, "raw-frame"), ImageChecksum = checksum }, WidthPx = 100, HeightPx = 60 };
        var region = new NativeRegion { Id = Id(EntityKind.Region, "scoped"), FrameRef = capture.Frame.ImageRef,
            Polygon = [new(0, 0), new(100, 0), new(100, 60), new(0, 60)], ReadingOrderIndex = 4, Languages = ["und"] };
        var lineId = Id(EntityKind.Line, "scoped");
        var source = new RevisionChain().Append(lineId, TextLayer.L1Diplomatic, "GENERATED", new AgentRef { Id = "generated-human", IsModel = false });
        var line = new NativeLine { Id = lineId, RegionId = region.Id, FrameRef = capture.Frame.ImageRef, Polygon = Original,
            Baseline = OriginalBaseline, DiplomaticRevisionId = source.Id, LineNumber = 7, Direction = TextDirection.Rtl,
            Script = "generated", Languages = ["und"], Tags = new Dictionary<string, IReadOnlyList<string>> { ["fixture"] = ["generated"] }, Split = "generated" };
        var surface = new NativeSurface { Id = Id(EntityKind.Surface, "scoped"), Kind = "generated", Captures = [capture], Regions = [region],
            Lines = [line], Revisions = [source],
            Annotations = [new Annotation { Id = Id(EntityKind.Annotation, "generated"), Kind = AnnotationKind.Unclear, AnchoredRevisionId = source.Id, Range = new(0, 3) }],
            ReviewDecisions = [new ReviewDecision { Id = "generated-review", LineId = line.Id, RevisionId = source.Id,
                Reviewer = "generated-reviewer", Outcome = ReviewOutcome.Accept, At = DateTimeOffset.UnixEpoch }],
            GeometryEdits = [new GeometryEdit { Id = "generated-history", Kind = GeometryEditKind.LineGeometry,
                At = DateTimeOffset.UnixEpoch, Author = "generated", LineId = line.Id, Note = "preserve generated history" }] };
        var project = new NativeProject { Id = Id(EntityKind.Collection, "scoped"), Name = "generated",
            Objects = [new NativeObject { Id = Id(EntityKind.PhysicalObject, "scoped"), Surfaces = [surface] }] };
        var store = new SpyStore(); project = project with { Version = store.SaveProject(project) };
        var display = new CaptureDisplayDescriptor(1, project.Id.ToString(), surface.Id.ToString(), capture.Id.ToString(), capture.Frame.ImageRef.ToString(),
            checksum, "unavailable", new Checksum { Algorithm = "sha256", Digest = new string('a', 64) }, 100, 60, 100, 60, 1, 1, "raw", "image/png", new string('b', 64), checksum);
        return (project, surface, display, store, new GeometryService(store));
    }

    [Fact]
    public void Scoped_update_changes_only_target_geometry_and_appends_separate_observation_evidence()
    {
        var f = Fixture(); var before = f.Surface; var line = before.Lines[0];
        var (project, surface, changed) = f.Service.SetLineGeometryInCapture(f.Project, before.Id, before.Captures[0].Id,
            f.Display, line.Id, Updated, UpdatedBaseline, new(Original, OriginalBaseline), "generated-editor");
        Assert.Equal(ContractJson.ToJson(line with { Polygon = Updated, Baseline = UpdatedBaseline }), ContractJson.ToJson(changed));
        Assert.Equal(2, surface.GeometryEdits.Count); Assert.Equal("generated-editor", surface.GeometryEdits[^1].Author);
        Assert.Contains(f.Display.DerivativeId, surface.GeometryEdits[^1].Note);
        Assert.Contains(f.Display.ObservedSourceChecksum.Digest, surface.GeometryEdits[^1].Note);
        var expected = before with { Lines = [line with { Polygon = Updated, Baseline = UpdatedBaseline }], GeometryEdits = surface.GeometryEdits };
        Assert.Equal(ContractJson.ToJson(expected), ContractJson.ToJson(surface));
        Assert.Equal(2, f.Store.Writes); Assert.Equal(f.Project.Version + 1, project.Version);
        Assert.Equal(ContractJson.ToJson(surface), ContractJson.ToJson(f.Store.LoadProject(project.Id)!.Objects[0].Surfaces[0]));
    }

    [Theory]
    [InlineData("wrong-frame")] [InlineData("wrong-surface")] [InlineData("duplicate-local")]
    [InlineData("duplicate-global")] [InlineData("region-missing")] [InlineData("region-duplicate")]
    [InlineData("region-frame")] [InlineData("display-project")] [InlineData("display-surface")]
    [InlineData("display-capture")] [InlineData("display-frame")] [InlineData("outside-polygon")]
    [InlineData("outside-baseline")] [InlineData("nan")] [InlineData("stale-expected")]
    [InlineData("null-expected")] [InlineData("null-expected-polygon")]
    public void Invalid_scope_identity_bounds_or_expected_geometry_cannot_reach_a_store_write(string scenario)
    {
        var f = Fixture(); var surface = f.Surface; var project = f.Project; var display = f.Display;
        var line = surface.Lines[0]; var polygon = Updated; var baseline = UpdatedBaseline;
        LineGeometrySnapshot expected = new(Original, OriginalBaseline);
        if (scenario == "wrong-frame") surface = surface with { Lines = [line with { FrameRef = Id(EntityKind.Derivative, "foreign") }] };
        if (scenario == "duplicate-local") surface = surface with { Lines = [line, line] };
        if (scenario == "region-missing") surface = surface with { Regions = [] };
        if (scenario == "region-duplicate") surface = surface with { Regions = [surface.Regions[0], surface.Regions[0]] };
        if (scenario == "region-frame") surface = surface with { Regions = [surface.Regions[0] with { FrameRef = Id(EntityKind.Derivative, "foreign") }] };
        project = project with { Objects = [project.Objects[0] with { Surfaces = [surface] }] };
        if (scenario is "wrong-surface" or "duplicate-global")
        {
            var otherCapture = surface.Captures[0] with { Id = Id(EntityKind.Capture, "other"), Frame = surface.Captures[0].Frame with { ImageRef = Id(EntityKind.Derivative, "other") } };
            var other = surface with { Id = Id(EntityKind.Surface, "other"), Captures = [otherCapture], Lines = scenario == "duplicate-global" ? [line] : [] };
            project = project with { Objects = [project.Objects[0] with { Surfaces = [surface, other] }] };
            if (scenario == "wrong-surface") { surface = other; display = display with { SurfaceId = other.Id.ToString(), CaptureId = otherCapture.Id.ToString(), SourceFrameRef = otherCapture.Frame.ImageRef.ToString() }; }
        }
        if (scenario == "display-project") display = display with { ProjectId = "col-other" };
        if (scenario == "display-surface") display = display with { SurfaceId = "surf-other" };
        if (scenario == "display-capture") display = display with { CaptureId = "cap-other" };
        if (scenario == "display-frame") display = display with { SourceFrameRef = "der-other" };
        if (scenario == "outside-polygon") polygon = [new(-1, 10), new(20, 10), new(20, 20), new(-1, 20)];
        if (scenario == "outside-baseline") baseline = [new(10, 61), new(20, 61)];
        if (scenario == "nan") polygon = [new(double.NaN, 10), new(20, 10), new(20, 20)];
        if (scenario == "stale-expected") expected = new(Updated, OriginalBaseline);
        if (scenario == "null-expected") expected = null!;
        if (scenario == "null-expected-polygon") expected = new(null!, OriginalBaseline);
        var before = NativeJson.Save(f.Store.LoadProject(project.Id)!);
        var error = Assert.Throws<LeviathanException>(() => f.Service.SetLineGeometryInCapture(project, surface.Id,
            surface.Captures[0].Id, display, line.Id, polygon, baseline, expected));
        if (scenario == "stale-expected") Assert.Equal(ErrorCode.RevisionConflict, error.Error.Code);
        if (scenario.StartsWith("region", StringComparison.Ordinal)) Assert.Contains("registration", error.Error.Message);
        Assert.Equal(1, f.Store.Writes); Assert.Equal(before, NativeJson.Save(f.Store.LoadProject(project.Id)!));
    }

    [Fact]
    public void Explicit_unplaced_line_update_preserves_its_transcription_and_supports_a_confirming_noop_retry()
    {
        var f = Fixture(); var line = f.Surface.Lines[0] with { Polygon = [], Baseline = [] };
        var surface = f.Surface with { Lines = [line] };
        var project = f.Project with { Objects = [f.Project.Objects[0] with { Surfaces = [surface] }] };
        var (placed, updated, _) = f.Service.SetLineGeometryInCapture(project, surface.Id, surface.Captures[0].Id,
            f.Display, line.Id, Updated, UpdatedBaseline, new([], []));
        var (_, retried, _) = f.Service.SetLineGeometryInCapture(placed, surface.Id, surface.Captures[0].Id,
            f.Display, line.Id, Updated, UpdatedBaseline, new(Updated, UpdatedBaseline));
        Assert.Equal(ContractJson.ToJson(f.Surface.Revisions), ContractJson.ToJson(retried.Revisions));
        Assert.Equal(updated.Lines[0].DiplomaticRevisionId, retried.Lines[0].DiplomaticRevisionId);
        Assert.Equal(3, retried.GeometryEdits.Count);
    }

    private sealed class SpyStore : IProjectStore
    {
        private string? _json;
        public int Writes { get; private set; }
        public long SaveProject(NativeProject project) { Writes++; var version = project.Version + 1; _json = NativeJson.Save(project with { Version = version }); return version; }
        public NativeProject? LoadProject(EntityId id) => _json is null ? null : NativeJson.Load(_json);
        public IReadOnlyList<(EntityId Id, string Name)> ListProjects() => [];
    }
}
