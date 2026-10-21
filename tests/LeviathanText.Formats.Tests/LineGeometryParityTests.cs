using LeviathanText.Formats;
using Xunit;

namespace LeviathanText.Formats.Tests;

/// <summary>Behavioral parity scenarios adapted from Kraken test_container.py.</summary>
public class LineGeometryParityTests
{
    private static readonly LineGeometry.Box Box = new(10, 20, 110, 70);

    [Fact]
    public void Boundary_uses_box_extents_and_closes()
    {
        var boundary = LineGeometry.Boundary(Box);
        Assert.Equal(5, boundary.Count);
        Assert.Equal(new PointD(10, 20), boundary[0]);
        Assert.Equal(boundary[0], boundary[^1]);
        Assert.Equal(new PointD(110, 70), boundary[2]);
    }

    [Fact]
    public void Baseline_placement_honors_horizontal_direction_and_topline()
    {
        Assert.Equal([new PointD(10, 57.5), new PointD(110, 57.5)], LineGeometry.Baseline(Box, TextDirection.Ltr, false));
        Assert.Equal([new PointD(10, 32.5), new PointD(110, 32.5)], LineGeometry.Baseline(Box, TextDirection.Ltr, true));
        Assert.Equal([new PointD(110, 57.5), new PointD(10, 57.5)], LineGeometry.Baseline(Box, TextDirection.Rtl, false));
    }

    [Fact]
    public void Baseline_placement_honors_vertical_direction_and_topline()
    {
        Assert.Equal([new PointD(35, 20), new PointD(35, 70)], LineGeometry.Baseline(Box, TextDirection.VerticalTopToBottom, false));
        Assert.Equal([new PointD(85, 20), new PointD(85, 70)], LineGeometry.Baseline(Box, TextDirection.VerticalTopToBottom, true));
        Assert.Equal([new PointD(85, 20), new PointD(85, 70)], LineGeometry.Baseline(Box, TextDirection.VerticalBottomToTop, false));
        Assert.Equal([new PointD(35, 20), new PointD(35, 70)], LineGeometry.Baseline(Box, TextDirection.VerticalBottomToTop, true));
    }

    [Fact]
    public void Degenerate_box_produces_zero_length_baseline_without_throwing()
    {
        var box = new LineGeometry.Box(5, 5, 5, 5);
        Assert.Equal([new PointD(5, 5), new PointD(5, 5)], LineGeometry.Baseline(box));
        Assert.All(LineGeometry.Boundary(box), p => Assert.Equal(new PointD(5, 5), p));
    }

    [Fact]
    public void Bounds_prefers_boundary_and_falls_back_to_baseline()
    {
        Assert.Equal(new LineGeometry.Box(10, 30, 100, 55), LineGeometry.Bounds(
            [new(10, 30), new(100, 30), new(100, 55), new(10, 55)]));
        Assert.Equal(new LineGeometry.Box(5, 7, 90, 12), LineGeometry.Bounds(
            [new(5, 8), new(60, 12), new(90, 7)]));
        Assert.Equal(default, LineGeometry.Bounds([]));
    }
}
