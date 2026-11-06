using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application.Tests;

public sealed class OrderSnapshotTests
{
    private static NativeProject Seed(InMemoryStore store)
    {
        var frame = new EntityId(EntityKind.Capture, "capture");
        var firstRegion = new EntityId(EntityKind.Region, "first");
        var project = new NativeProject
        {
            Id = new(EntityKind.Collection, "orders"), Name = "generated orders", Objects = [new NativeObject
            {
                Id = new(EntityKind.PhysicalObject, "object"), Surfaces = [new NativeSurface
                {
                    Id = new(EntityKind.Surface, "page"), Kind = "page", Captures = [],
                    Regions = [
                        new NativeRegion { Id = firstRegion, FrameRef = frame, ReadingOrderIndex = 0 },
                        new NativeRegion { Id = new(EntityKind.Region, "unset"), FrameRef = frame },
                        new NativeRegion { Id = new(EntityKind.Region, "tied"), FrameRef = frame, ReadingOrderIndex = 0 },
                    ],
                    Lines = [
                        new NativeLine { Id = new(EntityKind.Line, "a"), RegionId = firstRegion, FrameRef = frame },
                        new NativeLine { Id = new(EntityKind.Line, "other"), RegionId = new(EntityKind.Region, "tied"), FrameRef = frame },
                        new NativeLine { Id = new(EntityKind.Line, "b"), RegionId = firstRegion, FrameRef = frame },
                        new NativeLine { Id = new(EntityKind.Line, "c"), RegionId = firstRegion, FrameRef = frame },
                    ],
                }],
            }],
        };
        var obj = project.Objects[0];
        var surface = obj.Surfaces[0];
        var revisions = surface.Lines.Select((line, i) => new TextRevision
        {
            Id = new(EntityKind.TranscriptionRevision, $"r{i}"), ScopeId = line.Id, Layer = TextLayer.L1Diplomatic,
            Text = $"α{i}", Author = new AgentRef { Id = "generated-fixture", IsModel = false }, CreatedAt = DateTimeOffset.UnixEpoch,
        }).ToArray();
        project = project with { Objects = [obj with { Surfaces = [surface with
        {
            Revisions = revisions,
            Lines = surface.Lines.Select((line, i) => line with
            {
                DiplomaticRevisionId = revisions[i].Id,
                Polygon = [new(2, i * 10), new(20, i * 10), new(20, i * 10 + 8), new(2, i * 10 + 8)],
                Baseline = [new(2, i * 10 + 7), new(20, i * 10 + 7)],
            }).ToArray(),
        }] }] };
        store.SaveProject(project);
        return store.LoadProject(project.Id)!;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Duplicate_desired_ids_are_rejected_without_dropping_entities_or_appending_audit(bool regions)
    {
        var store = new InMemoryStore();
        var project = Seed(store);
        var surface = project.Objects[0].Surfaces[0];
        var service = new GeometryService(store);
        var desired = regions ? surface.Regions.Select(r => r.Id).ToArray()
            : surface.Lines.Where(l => l.RegionId == surface.Regions[0].Id).Select(l => l.Id).ToArray();
        desired = [desired[0], desired[1], desired[0], .. desired.Skip(2)];

        var error = Assert.Throws<LeviathanException>(() =>
        {
            if (regions) service.SetReadingOrder(project, surface.Id, desired);
            else service.SetLineOrder(project, surface.Regions[0].Id, desired);
        });

        Assert.Equal(ErrorCode.StageContractViolation, error.Error.Code);
        Assert.Equal(NativeJson.Save(project), NativeJson.Save(store.LoadProject(project.Id)!));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Missing_entities_cannot_be_hidden_by_duplicate_desired_ids(bool regions)
    {
        var store = new InMemoryStore();
        var project = Seed(store);
        var surface = project.Objects[0].Surfaces[0];
        var service = new GeometryService(store);
        var original = regions ? surface.Regions.Select(r => r.Id).ToArray()
            : surface.Lines.Where(l => l.RegionId == surface.Regions[0].Id).Select(l => l.Id).ToArray();
        EntityId[] desired = [original[0], original[0], original[2]];
        var error = Assert.Throws<LeviathanException>(() =>
        {
            if (regions) service.SetReadingOrder(project, surface.Id, desired);
            else service.SetLineOrder(project, surface.Regions[0].Id, desired);
        });
        Assert.Equal(ErrorCode.StageContractViolation, error.Error.Code);
        Assert.Equal(NativeJson.Save(project), NativeJson.Save(store.LoadProject(project.Id)!));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_reloaded_handler_rejects_a_stale_ui_snapshot_without_changes(bool regions)
    {
        var store = new InMemoryStore();
        var original = Seed(store);
        var surface = original.Objects[0].Surfaces[0];
        var service = new GeometryService(store);
        var expected = regions ? surface.Regions.OrderBy(r => r.ReadingOrderIndex).Select(r => r.Id).ToArray()
            : surface.Lines.Where(l => l.RegionId == surface.Regions[0].Id).Select(l => l.Id).ToArray();
        var desired = expected.Reverse().ToArray();
        if (regions) service.SetReadingOrder(original, surface.Id, desired, expectedRegionIds: expected);
        else service.SetLineOrder(original, surface.Regions[0].Id, desired, expectedLineIds: expected);
        var current = store.LoadProject(original.Id)!;

        var error = Assert.Throws<LeviathanException>(() =>
        {
            if (regions) service.SetReadingOrder(current, surface.Id, expected, expectedRegionIds: expected);
            else service.SetLineOrder(current, surface.Regions[0].Id, expected, expectedLineIds: expected);
        });

        Assert.Equal(ErrorCode.RevisionConflict, error.Error.Code);
        Assert.Equal(NativeJson.Save(current), NativeJson.Save(store.LoadProject(original.Id)!));
    }

    [Fact]
    public void Region_snapshot_uses_null_first_and_stable_native_tie_order()
    {
        var store = new InMemoryStore();
        var project = Seed(store);
        var surface = project.Objects[0].Surfaces[0];
        EntityId[] expected = [surface.Regions[1].Id, surface.Regions[0].Id, surface.Regions[2].Id];
        var (_, saved) = new GeometryService(store).SetReadingOrder(project, surface.Id, expected.Reverse().ToArray(), expectedRegionIds: expected);
        Assert.Equal(expected.Reverse(), saved.Regions.OrderBy(r => r.ReadingOrderIndex).Select(r => r.Id));
    }

    [Fact]
    public void Matching_line_snapshot_preserves_other_region_slots_and_store_version_guard()
    {
        var store = new InMemoryStore();
        var project = Seed(store);
        var surface = project.Objects[0].Surfaces[0];
        var region = surface.Regions[0].Id;
        var expected = surface.Lines.Where(l => l.RegionId == region).Select(l => l.Id).ToArray();
        var service = new GeometryService(store);
        var (_, saved) = service.SetLineOrder(project, region, expected.Reverse().ToArray(), expectedLineIds: expected);
        Assert.Equal(surface.Lines[1], saved.Lines[1]);
        var error = Assert.Throws<LeviathanException>(() => service.SetLineOrder(project, region, expected.Reverse().ToArray(), expectedLineIds: expected));
        Assert.Equal(ErrorCode.RevisionConflict, error.Error.Code);
    }
}
