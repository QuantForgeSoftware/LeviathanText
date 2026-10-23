using LeviathanText.Application;
using SkiaSharp;
using Xunit;

namespace LeviathanText.Application.Tests;

public class SegmentationTaskModelTests
{
    private static SKBitmap Page()
    {
        var page = new SKBitmap(200, 120);
        page.Erase(SKColors.White);
        for (int i = 0; i < 3; i++)
            for (int y = 15 + i * 30; y < 25 + i * 30; y++)
                for (int x = 15; x < 185; x++)
                    page.SetPixel(x, y, SKColors.Black);
        return page;
    }

    [Fact]
    public void Predict_returns_lines_and_a_single_column_for_a_simple_page()
    {
        using var page = Page();
        var result = new SegmentationTaskModel().Predict(page);

        Assert.NotEmpty(result.Layout.Lines);
        Assert.Single(result.Columns);
        Assert.Equal(result.Layout.Lines.Count, result.Columns[0].Lines.Count);
    }

    [Fact]
    public void Empty_page_returns_no_lines_or_columns()
    {
        using var page = new SKBitmap(100, 80);
        page.Erase(SKColors.White);
        var result = new SegmentationTaskModel().Predict(page);
        Assert.Empty(result.Layout.Lines);
        Assert.Empty(result.Columns);
    }
}
