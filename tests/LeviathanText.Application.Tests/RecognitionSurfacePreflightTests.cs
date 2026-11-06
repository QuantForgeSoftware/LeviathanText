using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application.Tests;

// Registration preflight only: no capture IO and no model/label factory is ever invoked.
public sealed class RecognitionSurfacePreflightTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ambiguous_surface_is_refused_before_capture_or_factory_access(bool acrossObjects)
    {
        var id = new EntityId(EntityKind.Surface, "duplicate");
        var first = new NativeSurface { Id = id, Kind = "generated-first", Captures = [], Regions = [], Lines = [] };
        var other = first with { Kind = "preserve-other" };
        var objects = acrossObjects
            ? new[] { new NativeObject { Id = new(EntityKind.PhysicalObject, "first"), Surfaces = [first] },
                      new NativeObject { Id = new(EntityKind.PhysicalObject, "other"), Surfaces = [other] } }
            : new[] { new NativeObject { Id = new(EntityKind.PhysicalObject, "first"), Surfaces = [first, other] } };
        var project = new NativeProject { Id = new(EntityKind.Collection, "generated"), Name = "generated", Objects = objects };
        var store = new UntouchedStore(); int modelCalls = 0, labelCalls = 0;
        var options = new RecognitionPipeline.Options {
            ModelFactory = () => { modelCalls++; throw new InvalidOperationException("Model factory must not run."); },
            LabelSetFactory = () => { labelCalls++; throw new InvalidOperationException("Label factory must not run."); } };
        var before = NativeJson.Save(project);
        var error = Assert.Throws<LeviathanException>(() => new RecognitionPipeline(store).Run(project, id, options));
        Assert.Equal(ErrorCode.StageContractViolation, error.Error.Code);
        Assert.Equal(0, modelCalls); Assert.Equal(0, labelCalls); Assert.Equal(0, store.Writes);
        Assert.Equal(before, NativeJson.Save(project));
    }
    [Fact]
    public void Missing_surface_retains_input_missing_without_factory_calls()
    {
        var project = new NativeProject { Id = new(EntityKind.Collection, "generated"), Name = "generated", Objects = [] };
        var store = new UntouchedStore(); int calls = 0;
        var options = new RecognitionPipeline.Options {
            ModelFactory = () => { calls++; throw new InvalidOperationException("No factory access."); },
            LabelSetFactory = () => { calls++; throw new InvalidOperationException("No factory access."); } };
        var error = Assert.Throws<LeviathanException>(() => new RecognitionPipeline(store).Run(project, new(EntityKind.Surface, "missing"), options));
        Assert.Equal(ErrorCode.InputMissing, error.Error.Code); Assert.Equal(0, calls); Assert.Equal(0, store.Writes);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Selected_line_identity_must_be_unique_locally_and_across_surfaces(bool global)
    {
        var line = new NativeLine { Id = new(EntityKind.Line, "duplicate"), RegionId = default, FrameRef = default };
        var selected = new NativeSurface { Id = new(EntityKind.Surface, "selected"), Kind = "generated", Captures = [], Regions = [],
            Lines = global ? [line] : [line, line] };
        var other = selected with { Id = new(EntityKind.Surface, "other"), Lines = [line] };
        var project = new NativeProject { Id = new(EntityKind.Collection, "generated"), Name = "generated",
            Objects = [new NativeObject { Id = new(EntityKind.PhysicalObject, "generated"), Surfaces = global ? [selected, other] : [selected] }] };
        var store = new UntouchedStore(); int calls = 0;
        var options = new RecognitionPipeline.Options {
            ModelFactory = () => { calls++; throw new InvalidOperationException("No factory access."); },
            LabelSetFactory = () => { calls++; throw new InvalidOperationException("No factory access."); } };
        var error = Assert.Throws<LeviathanException>(() => new RecognitionPipeline(store).Run(project, selected.Id, options));
        Assert.Equal(0, calls); Assert.Equal(0, store.Writes);
        Assert.Equal(ErrorCode.StageContractViolation, error.Error.Code);
    }
    sealed class UntouchedStore : IProjectStore {
        public int Writes { get; private set; }
        public long SaveProject(NativeProject project) { Writes++; throw new InvalidOperationException("No save permitted."); }
        public NativeProject? LoadProject(EntityId id) => throw new InvalidOperationException("No store read permitted.");
        public IReadOnlyList<(EntityId Id, string Name)> ListProjects() => throw new InvalidOperationException("No enumeration permitted.");
    }
}
