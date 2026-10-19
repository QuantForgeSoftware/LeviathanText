using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using Xunit;
using Xunit.Abstractions;

namespace LeviathanText.Pilot.Tests;

/// <summary>
/// Auto-layout stage on a real page: an image-only surface gets detected lines, and the
/// stage refuses to overwrite existing (manual) layout. Skipped without artifacts.
/// </summary>
public sealed class AutoLayoutPilotTests(ITestOutputHelper output)
{
    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LeviathanText.slnx")))
            dir = dir.Parent;
        return dir?.FullName;
    }

    [SkippableFact]
    public void Detect_layout_on_a_real_page_and_refuse_overwrite()
    {
        var root = FindRepoRoot();
        Skip.IfNot(root is not null, "repo root not found");
        var image = Path.Combine(root!, ".artifacts", "datasets", "p01-v0.9", "P.Cair.Zen.II.59263r.jpg");
        Skip.IfNot(File.Exists(image), "P01 image not available");

        var captureId = new EntityId(EntityKind.Capture, "cap-auto");
        var surface = new NativeSurface
        {
            Id = new EntityId(EntityKind.Surface, "surf-auto"),
            Kind = "papyrus-side",
            Captures = [new NativeCapture
            {
                Id = captureId,
                Frame = new CoordinateFrame { ImageRef = captureId, ImageChecksum = new Checksum { Algorithm = "unavailable", Digest = "pending" } },
                ImageLocation = image,
            }],
            Regions = [],
            Lines = [],
        };
        var project = new NativeProject
        {
            Id = new EntityId(EntityKind.Collection, "col-auto"),
            Name = "auto-layout",
            Objects = [new NativeObject { Id = new EntityId(EntityKind.PhysicalObject, "obj-auto"), Surfaces = [surface] }],
        };

        var store = new InMemoryStore();
        store.SaveProject(project);
        var service = new LayoutService(store);

        var outcome = service.Detect(project, surface.Id);
        output.WriteLine($"detected {outcome.LineCount} lines ({outcome.Method})");
        Assert.True(outcome.LineCount > 0, "a real page must produce detected lines");
        Assert.Equal(LayoutSource.Automatic, outcome.Surface.Regions[0].LayoutSource);
        Assert.All(outcome.Surface.Lines, l => Assert.Equal(outcome.Surface.Regions[0].Id, l.RegionId));

        // Persisted.
        var reloaded = store.LoadProject(project.Id)!;
        Assert.True(reloaded.Objects[0].Surfaces[0].Lines.Count > 0);

        // Refuses to overwrite existing layout.
        var ex = Assert.Throws<LeviathanException>(() => { service.Detect(reloaded, surface.Id); });
        Assert.Equal(ErrorCode.StageContractViolation, ex.Error.Code);
        Assert.Contains("never overwrites", ex.Error.Message);
    }

    private sealed class InMemoryStore : IProjectStore
    {
        private readonly Dictionary<string, string> _projects = [];

        public void SaveProject(NativeProject project) => _projects[project.Id.ToString()] = NativeJson.Save(project);

        public NativeProject? LoadProject(EntityId id) =>
            _projects.TryGetValue(id.ToString(), out var json) ? NativeJson.Load(json) : null;

        public IReadOnlyList<(EntityId Id, string Name)> ListProjects() =>
            [.. _projects.Select(kv => (EntityId.Parse(kv.Key), NativeJson.Load(kv.Value).Name))];
    }
}
