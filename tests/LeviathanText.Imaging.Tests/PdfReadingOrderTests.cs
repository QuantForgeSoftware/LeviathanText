using LeviathanText.Imaging;
using Xunit;

namespace LeviathanText.Imaging.Tests;

public class PdfReadingOrderTests
{
    private static PdfTextExtractor.TextLine Line(string text, double left, double bottom, double right, double top) =>
        new(text, left, bottom, right, top);

    [Fact]
    public void Clean_two_columns_are_ordered_left_column_then_right_column()
    {
        var lines = new[]
        {
            Line("L2", 10, 10, 40, 20),
            Line("L1", 10, 30, 40, 40),
            Line("R2", 60, 10, 90, 20),
            Line("R1", 60, 30, 90, 40),
        };

        var ordered = PdfReadingOrder.Order(lines, pageWidth: 100);

        Assert.Equal(["L1", "L2", "R1", "R2"], ordered.Select(o => o.Line.Text).ToArray());
        Assert.Equal([0, 0, 1, 1], ordered.Select(o => o.Column).ToArray());
        Assert.Equal([0, 1, 2, 3], ordered.Select(o => o.Order).ToArray());
    }

    [Fact]
    public void Single_column_falls_back_to_top_to_bottom_order()
    {
        var lines = new[]
        {
            Line("bottom", 10, 5, 80, 15),
            Line("top", 10, 35, 80, 45),
            Line("middle", 10, 20, 80, 30),
        };

        var ordered = PdfReadingOrder.Order(lines, pageWidth: 100);

        Assert.Equal(["top", "middle", "bottom"], ordered.Select(o => o.Line.Text).ToArray());
        Assert.All(ordered, item => Assert.Equal(0, item.Column));
    }

    [Fact]
    public void A_line_crossing_the_gutter_prevents_column_guessing()
    {
        var lines = new[]
        {
            Line("cross", 10, 50, 90, 60),
            Line("left", 10, 30, 40, 40),
            Line("right", 60, 30, 90, 40),
        };

        var ordered = PdfReadingOrder.Order(lines, pageWidth: 100);

        Assert.All(ordered, item => Assert.Equal(0, item.Column));
        Assert.Equal("cross", ordered[0].Line.Text);
    }
}
