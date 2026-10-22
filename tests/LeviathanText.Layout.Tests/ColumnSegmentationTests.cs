using LeviathanText.Contracts;
using LeviathanText.Layout;
using Xunit;

namespace LeviathanText.Layout.Tests;

public class ColumnSegmentationTests
{
    private static DetectedLine Line(int left, int top, int right, int bottom) => new()
    {
        Id = new EntityId(EntityKind.Region, $"{left}-{top}"),
        Left = left, Top = top, Right = right, Bottom = bottom,
    };

    [Fact]
    public void Four_lines_with_a_clean_gutter_split_into_two_columns()
    {
        var columns = ColumnSegmentation.Split(
        [
            Line(0, 10, 40, 20), Line(0, 30, 40, 40),
            Line(60, 10, 100, 20), Line(60, 30, 100, 40),
        ], pageWidth: 100);

        Assert.Equal(2, columns.Count);
        Assert.Equal(2, columns[0].Lines.Count);
        Assert.Equal(2, columns[1].Lines.Count);
        Assert.Equal(10, columns[0].Lines[0].Top);
        Assert.Equal(10, columns[1].Lines[0].Top);
    }

    [Fact]
    public void Single_column_or_too_few_lines_stays_one_column()
    {
        Assert.Single(ColumnSegmentation.Split(
            [Line(10, 10, 90, 20), Line(10, 30, 90, 40)], pageWidth: 100));
        Assert.Single(ColumnSegmentation.Split(
            [Line(10, 10, 30, 20), Line(60, 10, 90, 20), Line(80, 20, 95, 30)], pageWidth: 100));
    }
}
