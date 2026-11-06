using LeviathanText.Contracts;
using LeviathanText.Contracts.Serialization;
using LeviathanText.Formats;
using LeviathanText.Recognition;
using SkiaSharp;

namespace LeviathanText.Application.Tests;

/// <summary>Generated inference only: no optimizer, fitting, collection or provider.</summary>
public sealed class RecognitionRevisionIdentityTests
{
    private const string Shared = "abcdefghijklmnopqrstuvwx";

    [Fact]
    public void Long_line_ids_with_shared_prefix_get_distinct_L0_and_retry_without_writes()
    {
        using var fixture = new Fixture([Shared + "-one", Shared + "-two"]);
        fixture.Run("fixed-job");
        var revisions = fixture.Raw();
        Assert.Equal(2, revisions.Length);
        Assert.Equal(2, revisions.Select(r => r.Id).Distinct().Count());
        Assert.Equal(fixture.LineIds.OrderBy(id => id.ToString()), revisions.Select(r => r.ScopeId).OrderBy(id => id.ToString()));
        var saved = ContractJson.ToJson(fixture.Store.Project);
        fixture.Run("fixed-job");
        Assert.Equal(saved, ContractJson.ToJson(fixture.Store.Project));
        Assert.Equal(1, fixture.Store.Saves);
    }

    [Fact]
    public void Maximum_length_job_and_line_ids_fit_revision_identity_and_remain_stable()
    {
        using var fixture = new Fixture([new string('l', 63)]);
        fixture.Run(new string('j', 63));
        var revision = Assert.Single(fixture.Raw());
        Assert.InRange(revision.Id.Suffix.Length, 1, 63);
        fixture.Run(new string('j', 63));
        Assert.Single(fixture.Raw()); Assert.Equal(1, fixture.Store.Saves);
    }

    [Fact]
    public void Unambiguous_historical_long_line_identity_is_reused_without_hiding_a_second_line()
    {
        using var fixture = new Fixture([Shared + "-one", Shared + "-two"]);
        var legacy = fixture.Legacy("fixed-job", fixture.LineIds[0]);
        fixture.Seed([legacy]);
        fixture.Run("fixed-job");
        Assert.Equal(2, fixture.Raw().Length);
        Assert.Contains(fixture.Raw(), item => item == legacy);
        Assert.Single(fixture.Raw(), item => item.ScopeId == fixture.LineIds[1]);
        var bytes = ContractJson.ToJson(fixture.Store.Project);
        fixture.Run("fixed-job");
        Assert.Equal(bytes, ContractJson.ToJson(fixture.Store.Project));
        Assert.Equal(1, fixture.Store.Saves);
    }

    [Fact]
    public void Ambiguous_historical_identity_is_refused_without_changing_history()
    {
        using var fixture = new Fixture([Shared + "-one", Shared + "-two"]);
        fixture.Seed([fixture.Legacy("fixed-job", fixture.LineIds[0]), fixture.Legacy("fixed-job", fixture.LineIds[1])]);
        var before = ContractJson.ToJson(fixture.Store.Project);
        var error = Assert.Throws<LeviathanException>(() => fixture.Run("fixed-job"));
        Assert.Equal(ErrorCode.StageContractViolation, error.Error.Code);
        Assert.Equal(before, ContractJson.ToJson(fixture.Store.Project)); Assert.Equal(0, fixture.Store.Saves);
    }

    [Fact]
    public void Short_legacy_id_preserves_exact_token_and_does_not_hide_an_unrelated_scope()
    {
        using var fixture = new Fixture(["short"]);
        fixture.Run("fixed-job");
        Assert.Equal("rev-l0-fixed-job-short", Assert.Single(fixture.Raw()).Id.ToString());
        var stolen = Assert.Single(fixture.Raw()) with { ScopeId = new(EntityKind.Line, "unrelated") };
        fixture.Seed([stolen]); fixture.Store.Saves = 0;
        var before = ContractJson.ToJson(fixture.Store.Project);
        Assert.Throws<LeviathanException>(() => fixture.Run("fixed-job"));
        Assert.Equal(before, ContractJson.ToJson(fixture.Store.Project)); Assert.Equal(0, fixture.Store.Saves);
    }

    [Fact]
    public void Complete_long_job_identity_distinguishes_runs_and_preserves_existing_source_history()
    {
        using var fixture = new Fixture([Shared + "-line"]);
        var source = fixture.Legacy("source", fixture.LineIds[0]) with
        {
            Id = new(EntityKind.TranscriptionRevision, "diplomatic"), Layer = TextLayer.L1Diplomatic,
            Text = "generated source remains unchanged", Author = new AgentRef { Id = "generated-human", IsModel = false },
        };
        fixture.Seed([source]);
        fixture.Run(new string('j', 62) + "a"); fixture.Run(new string('j', 62) + "b");
        Assert.Equal(2, fixture.Raw().Length);
        Assert.Equal(2, fixture.Raw().Select(r => r.Id).Distinct().Count());
        Assert.Equal(ContractJson.ToJson(source), ContractJson.ToJson(fixture.Store.Project.Objects[0].Surfaces[0].Revisions.Single(r => r.Id == source.Id)));
    }

    [Fact]
    public void Occupied_hashed_identity_cannot_hide_an_unrelated_scope()
    {
        using var fixture = new Fixture([Shared + "-line"]);
        fixture.Run("fixed-job");
        var occupied = Assert.Single(fixture.Raw()) with { ScopeId = new(EntityKind.Line, "other") };
        fixture.Seed([occupied]); fixture.Store.Saves = 0;
        var before = ContractJson.ToJson(fixture.Store.Project);
        Assert.Throws<LeviathanException>(() => fixture.Run("fixed-job"));
        Assert.Equal(before, ContractJson.ToJson(fixture.Store.Project)); Assert.Equal(0, fixture.Store.Saves);
    }

    [Fact]
    public void Duplicate_input_line_identity_is_refused_before_any_persistence()
    {
        using var fixture = new Fixture(["duplicate", "duplicate"]);
        var before = ContractJson.ToJson(fixture.Store.Project);
        Assert.Throws<LeviathanException>(() => fixture.Run("fixed-job"));
        Assert.Equal(before, ContractJson.ToJson(fixture.Store.Project)); Assert.Equal(0, fixture.Store.Saves);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Directory.CreateTempSubdirectory("lt-recognition-identity-").FullName;
        private readonly IDisposable _scope = TorchSharp.torch.NewDisposeScope();
        private readonly CrnnModel _model;
        private readonly LabelSet _labels = new(["a", "b"]);
        public MemoryStore Store { get; }
        public EntityId[] LineIds { get; }
        public Fixture(string[] suffixes)
        {
            _model = new CrnnModel(_labels.NumClasses, hiddenSize: 8);
            string image = Path.Combine(_root, "generated.png");
            using (var bitmap = new SKBitmap(200, 96))
            {
                bitmap.Erase(SKColors.White);
                using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = File.Create(image); data.SaveTo(stream);
            }
            LineIds = suffixes.Select(id => new EntityId(EntityKind.Line, id)).ToArray();
            var capture = new EntityId(EntityKind.Capture, "source"); var region = new EntityId(EntityKind.Region, "region");
            Store = new(new NativeProject
            {
                Id = new(EntityKind.Collection, "identity"), Name = "generated recognition identity", Version = 1,
                Objects = [new NativeObject
                {
                    Id = new(EntityKind.PhysicalObject, "object"),
                    Surfaces = [new NativeSurface
                    {
                        Id = new(EntityKind.Surface, "page"), Kind = "generated",
                        Captures = [new NativeCapture { Id = capture, ImageLocation = image,
                            Frame = new CoordinateFrame { ImageRef = capture, ImageChecksum = new Checksum { Algorithm = "unavailable", Digest = "generated" } } }],
                        Regions = [new NativeRegion { Id = region, FrameRef = capture, LayoutSource = LayoutSource.Manual, Polygon = [new(0,0),new(200,0),new(200,96),new(0,96)], ReadingOrderIndex = 0 }],
                        Lines = [.. LineIds.Select((id,index) => new NativeLine { Id = id, RegionId = region, FrameRef = capture,
                            Polygon = [new(8,8+index*35),new(192,8+index*35),new(192,38+index*35),new(8,38+index*35)], Baseline = [] })],
                    }],
                }],
            });
        }
        public TextRevision Legacy(string run, EntityId line) => new()
        {
            Id = new(EntityKind.TranscriptionRevision, $"l0-{run}-{line.Suffix[..Math.Min(24,line.Suffix.Length)]}"),
            ScopeId = line, Layer = TextLayer.L0RawRecognition, Text = "historical raw output",
            Author = new AgentRef { Id = "model:generated", IsModel = true, ModelIdentity = "generated@identity" },
            CreatedAt = DateTimeOffset.UnixEpoch,
        };
        public void Seed(IReadOnlyList<TextRevision> revisions)
        {
            var project = Store.Project; var obj = project.Objects[0]; var page = obj.Surfaces[0];
            Store.Project = project with { Objects = [obj with { Surfaces = [page with { Revisions = revisions }] }] };
        }
        public TextRevision[] Raw() => Store.Project.Objects[0].Surfaces[0].Revisions.Where(r => r.Layer == TextLayer.L0RawRecognition).ToArray();
        public void Run(string run) => new RecognitionPipeline(Store).Run(Store.Project, new(EntityKind.Surface, "page"), new()
        {
            ModelFactory = () => _model, LabelSetFactory = () => _labels, LeaveModelOpen = true,
            ModelName = "generated", ModelVersion = "identity", RunId = run,
        });
        public void Dispose() { _model.Dispose(); _scope.Dispose(); Directory.Delete(_root, true); }
    }
    public sealed class MemoryStore(NativeProject project) : IProjectStore
    {
        public NativeProject Project { get; set; } = project;
        public int Saves { get; set; }
        public long SaveProject(NativeProject value) { Saves++; Project = value with { Version = value.Version + 1 }; return Project.Version; }
        public NativeProject? LoadProject(EntityId id) => id == Project.Id ? Project : null;
        public IReadOnlyList<(EntityId Id, string Name)> ListProjects() => [(Project.Id, Project.Name)];
    }
}
