using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Layout;
using Xunit;

namespace LeviathanText.Layout.Tests;

public class HierarchicalReadingOrderTests
{
    private static NativeRegion Region(string id) => new()
    {
        Id = new EntityId(EntityKind.Region, id),
        FrameRef = new EntityId(EntityKind.Capture, "cap"),
    };

    private static NativeLine Line(string id, string region) => new()
    {
        Id = new EntityId(EntityKind.Line, id),
        RegionId = new EntityId(EntityKind.Region, region),
        FrameRef = new EntityId(EntityKind.Capture, "cap"),
    };

    [Fact]
    public void Explicit_group_expands_regions_to_lines_in_declared_order()
    {
        var regions = new[] { Region("r1"), Region("r2"), Region("r3") };
        var lines = new[] { Line("l1", "r1"), Line("l2", "r1"), Line("l3", "r2"), Line("l4", "r3") };
        var groups = new[]
        {
            new PageXml.ReadingOrderGroup("ro", "main", true, ["r2", "r1"]),
            new PageXml.ReadingOrderGroup("ro-margin", "margin", false, ["r3"]),
        };

        var order = HierarchicalReadingOrder.Build(groups, regions, lines);

        Assert.Equal(2, order.Count);
        Assert.Equal(["l3", "l1", "l2"], order[0].LineIds.Select(id => id.Suffix).ToArray());
        Assert.True(order[0].IsTotal);
        Assert.Equal(["l4"], order[1].LineIds.Select(id => id.Suffix).ToArray());
        Assert.False(order[1].IsTotal);
    }

    [Fact]
    public void Unknown_region_references_are_ignored_not_fabricated()
    {
        var order = HierarchicalReadingOrder.Build(
            [new PageXml.ReadingOrderGroup("ro", null, true, ["missing", "r1"])],
            [Region("r1")],
            [Line("l1", "r1")]);

        Assert.Equal(["r1"], order[0].RegionIds.Select(id => id.Suffix).ToArray());
        Assert.Equal(["l1"], order[0].LineIds.Select(id => id.Suffix).ToArray());
    }
}
