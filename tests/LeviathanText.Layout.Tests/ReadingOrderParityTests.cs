using LeviathanText.Formats;
using Xunit;

namespace LeviathanText.Layout.Tests;

/// <summary>Behavioral parity scenarios from Kraken test_readingorder.py, independently written.</summary>
public class ReadingOrderParityTests
{
    private static List<PointD> Rect(double left, double top, double right, double bottom) =>
        [new(left, top), new(right, top), new(right, bottom), new(left, bottom), new(left, top)];

    [Fact]
    public void Line_is_inside_region()
    {
        Assert.True(ReadingOrder.IsInRegion(
            [new(0, 0), new(1, 1)],
            Rect(0, 0, 1, 1)));
    }

    [Fact]
    public void Real_baseline_is_inside_its_polygon()
    {
        Assert.True(ReadingOrder.IsInRegion(
            [new(268, 656), new(888, 656)],
            [new(268, 656), new(265, 613), new(885, 611), new(888, 656), new(885, 675), new(265, 672)]));
    }

    [Fact]
    public void Line_outside_region_is_rejected()
    {
        Assert.False(ReadingOrder.IsInRegion(
            [new(2, 2), new(1, 1)],
            Rect(0, 0, 1, 1)));
    }

    [Fact]
    public void Over_under_lines_order_top_to_bottom_in_ltr_and_rtl()
    {
        var lines = new List<IReadOnlyList<PointD>>
        {
            Rect(10, 10, 100, 20),
            Rect(10, 30, 100, 40),
        };
        Assert.Equal([0, 1], ReadingOrder.Order(lines, TextDirection.Ltr));
        Assert.Equal([0, 1], ReadingOrder.Order(lines, TextDirection.Rtl));
    }

    [Fact]
    public void Touching_vertical_bands_order_top_to_bottom()
    {
        var lines = new List<IReadOnlyList<PointD>>
        {
            Rect(10, 10, 100, 30),
            Rect(10, 30, 100, 40),
        };
        Assert.Equal([0, 1], ReadingOrder.Order(lines));
    }

    [Fact]
    public void Left_right_lines_order_by_x_in_ltr_and_reverse_in_rtl()
    {
        var lines = new List<IReadOnlyList<PointD>>
        {
            Rect(10, 10, 100, 20),
            Rect(150, 10, 250, 20),
        };
        Assert.Equal([0, 1], ReadingOrder.Order(lines, TextDirection.Ltr));
        Assert.Equal([1, 0], ReadingOrder.Order(lines, TextDirection.Rtl));
    }

    [Fact]
    public void Real_reversed_rows_order_top_to_bottom()
    {
        var lower = new List<PointD>
        {
            new(474, 2712), new(466, 2669), new(1741, 2655), new(1749, 2696), new(1746, 2709), new(474, 2725),
        };
        var upper = new List<PointD>
        {
            new(493, 2409), new(488, 2374), new(1733, 2361), new(1741, 2395), new(1738, 2409), new(493, 2422),
        };
        Assert.Equal([1, 0], ReadingOrder.Order([lower, upper]));
        Assert.Equal([0, 1], ReadingOrder.Order([upper, lower]));
    }

    [Fact]
    public void Topological_sort_preserves_partial_order_and_ties_by_index()
    {
        Assert.Equal([0, 1, 2], ReadingOrder.TopologicalSort(new[,]
        {
            { true, true, true }, { false, true, true }, { false, false, true },
        }));
        Assert.Equal([0, 1, 2], ReadingOrder.TopologicalSort(new[,]
        {
            { false, true, true }, { false, false, true }, { false, false, false },
        }));
        Assert.Equal([0, 2, 1], ReadingOrder.TopologicalSort(new[,]
        {
            { true, true, true }, { false, true, false }, { false, true, true },
        }));
    }

    [Fact]
    public void Topological_sort_detects_cycles()
    {
        Assert.Throws<InvalidOperationException>(() => ReadingOrder.TopologicalSort(new[,]
        {
            { false, true }, { true, false },
        }));
    }
}
