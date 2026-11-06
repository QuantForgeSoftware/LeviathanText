using System.Security.Cryptography;
using System.Text;
using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application.Tests;

public sealed class CaptureRelocationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"lt-relocation-{Guid.NewGuid():N}");
    private string Old => Path.Combine(_root, "old");
    private string New => Path.Combine(_root, "new");
    private readonly InMemoryStore _store = new();
    private static readonly EntityId ProjectId = new(EntityKind.Collection, "relocation");

    public CaptureRelocationTests()
    {
        Directory.CreateDirectory(Path.Combine(Old, "nested"));
        Directory.CreateDirectory(Path.Combine(New, "nested"));
    }

    private NativeProject Seed(bool recorded = true, int count = 1)
    {
        var captures = Enumerable.Range(0, count).Select(i =>
        {
            var bytes = Encoding.UTF8.GetBytes($"image-{i}");
            var path = Path.Combine(Old, "nested", $"{i}.png");
            File.WriteAllBytes(path, bytes);
            File.WriteAllBytes(Path.Combine(New, "nested", $"{i}.png"), bytes);
            var id = new EntityId(EntityKind.Capture, $"cap{i}");
            return new NativeCapture
            {
                Id = id, ImageLocation = path, WidthPx = 72, HeightPx = 48, RightsEvidenceRef = "rights:test",
                Frame = new CoordinateFrame
                {
                    ImageRef = id,
                    ImageChecksum = new Checksum
                    {
                        Algorithm = recorded ? "sha256" : "unavailable",
                        Digest = recorded ? Convert.ToHexStringLower(SHA256.HashData(bytes)) : "pending",
                    },
                },
            };
        }).ToArray();
        var project = new NativeProject
        {
            Id = ProjectId, Name = "relocation", Objects = [new NativeObject
            {
                Id = new(EntityKind.PhysicalObject, "obj"),
                Surfaces = [new NativeSurface
                {
                    Id = new(EntityKind.Surface, "surface"), Kind = "codex-page", Captures = captures,
                    Regions = [], Lines = [],
                }],
            }],
        };
        _store.SaveProject(project);
        return _store.LoadProject(ProjectId)!;
    }

    [Fact]
    public void Rebind_preserves_capture_frame_and_rights_and_saves_receipt_atomically()
    {
        var original = Seed();
        var report = new CaptureRelocation(_store).Rebind(ProjectId, Old, New);
        var saved = _store.LoadProject(ProjectId)!;
        Assert.True(report.Applied);
        Assert.Equal(original.Version + 1, saved.Version);
        var before = original.Objects[0].Surfaces[0].Captures[0];
        var after = saved.Objects[0].Surfaces[0].Captures[0];
        Assert.Equal(before with { ImageLocation = Path.Combine(New, "nested", "0.png") }, after);
        var receipt = Assert.Single(saved.Objects[0].Attachments);
        Assert.Equal("capture-relocation", receipt.Role);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(receipt.Content))), receipt.Sha256);
        Assert.Contains(before.Id.ToString(), receipt.Content);
        Assert.Contains("recorded-sha256", receipt.Content);
        Assert.False(new CaptureRelocation(_store).Rebind(ProjectId, Old, New).Applied);
        Assert.Equal(saved.Version, _store.LoadProject(ProjectId)!.Version);
    }

    [Fact]
    public void Dry_run_validates_without_saving()
    {
        var original = Seed();
        var report = new CaptureRelocation(_store).Rebind(ProjectId, Old, New, dryRun: true);
        Assert.False(report.Applied);
        Assert.Single(report.Captures);
        Assert.Equal(NativeJson.Save(original), NativeJson.Save(_store.LoadProject(ProjectId)!));
    }

    [Fact]
    public void Missing_original_is_allowed_only_with_recorded_sha256()
    {
        Seed();
        Directory.Delete(Old, true);
        Assert.True(new CaptureRelocation(_store).Rebind(ProjectId, Old, New).Applied);
    }

    [Fact]
    public void Legacy_capture_uses_readable_original_without_changing_frame()
    {
        var original = Seed(recorded: false);
        var report = new CaptureRelocation(_store).Rebind(ProjectId, Old, New);
        Assert.Equal("original-bytes", Assert.Single(report.Captures).Verification);
        Assert.Equal(original.Objects[0].Surfaces[0].Captures[0].Frame,
            _store.LoadProject(ProjectId)!.Objects[0].Surfaces[0].Captures[0].Frame);
    }

    [Fact]
    public void Missing_original_without_digest_fails_without_mutation()
    {
        var original = Seed(recorded: false);
        Directory.Delete(Old, true);
        Assert.Throws<InvalidDataException>(() => new CaptureRelocation(_store).Rebind(ProjectId, Old, New));
        Assert.Equal(NativeJson.Save(original), NativeJson.Save(_store.LoadProject(ProjectId)!));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Any_mismatched_candidate_leaves_all_paths_and_receipts_unchanged(bool alterOriginal)
    {
        var original = Seed(count: 2);
        File.WriteAllText(Path.Combine(alterOriginal ? Old : New, "nested", "1.png"), "different");
        Assert.Throws<InvalidDataException>(() => new CaptureRelocation(_store).Rebind(ProjectId, Old, New));
        Assert.Equal(NativeJson.Save(original), NativeJson.Save(_store.LoadProject(ProjectId)!));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Symlink_escape_is_rejected(bool sourceEscape)
    {
        var original = Seed();
        var outside = Path.Combine(_root, "outside.png");
        File.Copy(Path.Combine(Old, "nested", "0.png"), outside);
        var linked = Path.Combine(sourceEscape ? Old : New, "nested", "0.png");
        File.Delete(linked);
        File.CreateSymbolicLink(linked, outside);
        Assert.Throws<IOException>(() => new CaptureRelocation(_store).Rebind(ProjectId, Old, New));
        Assert.Equal(NativeJson.Save(original), NativeJson.Save(_store.LoadProject(ProjectId)!));
    }

    [Fact]
    public void Outside_and_remote_captures_are_unchanged()
    {
        var original = Seed();
        var obj = original.Objects[0];
        var surface = obj.Surfaces[0];
        var capture = surface.Captures[0];
        _store.SaveProject(original with { Objects = [obj with { Surfaces = [surface with
        {
            Captures = [capture with { ImageLocation = "https://example.test/image.png" },
                capture with { Id = new(EntityKind.Capture, "other"), ImageLocation = Path.Combine(_root, "other.png") }],
        }] }] });
        var before = _store.LoadProject(ProjectId)!;
        var report = new CaptureRelocation(_store).Rebind(ProjectId, Old, New);
        Assert.Empty(report.Captures);
        Assert.Equal(NativeJson.Save(before), NativeJson.Save(_store.LoadProject(ProjectId)!));
    }

    [Fact]
    public void Overlapping_roots_are_rejected()
    {
        Seed();
        Assert.Throws<ArgumentException>(() => new CaptureRelocation(_store).Rebind(ProjectId, _root, New));
    }

    [Fact]
    public void Concurrent_edit_is_preserved_when_relocation_save_conflicts()
    {
        Seed();
        var racing = new RacingStore(_store);
        var exception = Assert.Throws<LeviathanException>(() => new CaptureRelocation(racing).Rebind(ProjectId, Old, New));
        Assert.Equal(ErrorCode.RevisionConflict, exception.Error.Code);
        var saved = _store.LoadProject(ProjectId)!;
        Assert.Equal("concurrent change", saved.Name);
        Assert.Empty(saved.Objects[0].Attachments);
        Assert.StartsWith(Old, saved.Objects[0].Surfaces[0].Captures[0].ImageLocation);
    }

    [Theory]
    [InlineData("sha256", "broken")]
    [InlineData("md5", "0123456789abcdef0123456789abcdef")]
    public void Invalid_or_unsupported_declared_identity_is_never_ignored(string algorithm, string digest)
    {
        var project = Seed();
        var obj = project.Objects[0];
        var surface = obj.Surfaces[0];
        var capture = surface.Captures[0];
        _store.SaveProject(project with { Objects = [obj with { Surfaces = [surface with
        {
            Captures = [capture with { Frame = capture.Frame with
                { ImageChecksum = new Checksum { Algorithm = algorithm, Digest = digest } } }],
        }] }] });
        var before = _store.LoadProject(ProjectId)!;
        Assert.Throws<InvalidDataException>(() => new CaptureRelocation(_store).Rebind(ProjectId, Old, New));
        Assert.Equal(NativeJson.Save(before), NativeJson.Save(_store.LoadProject(ProjectId)!));
    }

    private sealed class RacingStore(InMemoryStore inner) : IProjectStore
    {
        public NativeProject? LoadProject(EntityId id) => inner.LoadProject(id);
        public IReadOnlyList<(EntityId Id, string Name)> ListProjects() => inner.ListProjects();
        public long SaveProject(NativeProject project)
        {
            var latest = inner.LoadProject(project.Id)!;
            inner.SaveProject(latest with { Name = "concurrent change" });
            return inner.SaveProject(project);
        }
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
