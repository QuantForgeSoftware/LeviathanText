using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application.Tests;

// Pure generated revision/native records; no models, source collections or providers.
public sealed class ReviewIdentityIsolationTests
{
    static EntityId Id(EntityKind kind, string value) => new(kind, value);
    static NativeSurface Surface(string value) {
        var line = new NativeLine { Id = Id(EntityKind.Line, value), RegionId = default, FrameRef = default,
            DiplomaticRevisionId = Id(EntityKind.TranscriptionRevision, value) };
        return new NativeSurface { Id = Id(EntityKind.Surface, value), Kind = "generated", Captures = [], Regions = [], Lines = [line],
            Revisions = [new TextRevision { Id = line.DiplomaticRevisionId!.Value, ScopeId = line.Id,
                Layer = TextLayer.L1Diplomatic, Author = new AgentRef { Id = "generated", IsModel = false },
                CreatedAt = DateTimeOffset.UnixEpoch, Text = "BASE-" + value }] };
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
    [Theory]
    [InlineData("duplicate-surface")]
    [InlineData("duplicate-line-local")]
    [InlineData("duplicate-line-global")]
    [InlineData("duplicate-revision")]
    [InlineData("wrong-revision-scope")]
    [InlineData("wrong-revision-layer")]
    public void Invalid_target_or_current_revision_cannot_append_a_review_or_replace_other_evidence(string mode) {
        var surface = Surface("selected"); var other = Surface("other");
        if (mode == "duplicate-surface") other = other with { Id = surface.Id };
        if (mode == "duplicate-line-local") surface = surface with { Lines = [surface.Lines[0], surface.Lines[0]] };
        if (mode == "duplicate-line-global") other = other with { Lines = [other.Lines[0] with { Id = surface.Lines[0].Id }] };
        if (mode == "duplicate-revision") surface = surface with { Revisions = [surface.Revisions[0], surface.Revisions[0] with { Text = "OTHER-EVIDENCE" }] };
        if (mode == "wrong-revision-scope") surface = surface with { Revisions = [surface.Revisions[0] with { ScopeId = other.Lines[0].Id }] };
        if (mode == "wrong-revision-layer") surface = surface with { Revisions = [surface.Revisions[0] with { Layer = TextLayer.L2Editorial }] };
        var project = Project(surface, other); var store = new Store(project); var before = NativeJson.Save(project);
        var failure = Record.Exception(() => new ReviewService(store).Record(project, surface.Lines[0].Id,
            "generated-reviewer", ReviewOutcome.Correct, correctionText: "GENERATED-CORRECTION"));
        Assert.True(failure is LeviathanException, $"Expected typed refusal; actual={failure?.GetType().Name ?? "success"}; writes={store.Writes}; nativeUnchanged={before == NativeJson.Save(store.Current)}");
        var error = (LeviathanException)failure!;
        Assert.Equal(ErrorCode.StageContractViolation, error.Error.Code);
        Assert.Equal(0, store.Writes); Assert.Equal(before, NativeJson.Save(store.Current));
    }
    [Fact]
    public void Unambiguous_review_preserves_other_surface_and_appends_exact_scoped_correction() {
        var surface = Surface("selected"); var other = Surface("other"); var project = Project(surface, other); var store = new Store(project);
        var (updated, changed, decision, revision) = new ReviewService(store).Record(project, surface.Lines[0].Id,
            "generated-reviewer", ReviewOutcome.Correct, correctionText: "GENERATED-CORRECTION");
        Assert.Equal(1, store.Writes); Assert.Equal(NativeJson.Save(Project(other)), NativeJson.Save(Project(updated.Objects[0].Surfaces[1])));
        Assert.Equal(surface.Lines[0].DiplomaticRevisionId, revision!.ParentRevisionId); Assert.Equal(surface.Lines[0].Id, revision.ScopeId);
        Assert.Equal(TextLayer.L1Diplomatic, revision.Layer); Assert.Equal(revision.Id, changed.Lines[0].DiplomaticRevisionId);
        Assert.Equal(revision.Id, decision.CorrectedRevisionId); Assert.Equal(2, changed.Revisions.Count);
    }
}
