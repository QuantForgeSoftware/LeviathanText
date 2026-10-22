using LeviathanText.Layout;
using SkiaSharp;

namespace LeviathanText.Layout.Tests;

public class PageSegmentationTests
{
    [Fact]
    public void Color_page_is_rejected_instead_of_thresholded_silently()
    {
        using var page = new SKBitmap(40, 20);
        page.Erase(new SKColor(180, 30, 30));

        var ex = Assert.Throws<LeviathanText.Contracts.LeviathanException>(() =>
            PageSegmentation.Segment(page, "color.png"));
        Assert.Equal(LeviathanText.Contracts.ErrorCode.StageContractViolation, ex.Error.Code);
    }

    [Fact]
    public void Bw_page_returns_bbox_lines_inside_the_image()
    {
        using var page = new SKBitmap(180, 100);
        page.Erase(SKColors.White);
        for (int i = 0; i < 3; i++)
            for (int y = 15 + i * 25; y < 23 + i * 25; y++)
                for (int x = 15; x < 165; x++)
                    page.SetPixel(x, y, SKColors.Black);

        var result = PageSegmentation.Segment(page, "page.png");

        Assert.Equal("page.png", result.ImageName);
        Assert.Equal("bbox", result.Type);
        Assert.NotEmpty(result.Lines);
        Assert.All(result.Lines, line =>
        {
            Assert.InRange(line.Left, 0, page.Width - 1);
            Assert.InRange(line.Right, 1, page.Width);
            Assert.InRange(line.Top, 0, page.Height - 1);
            Assert.InRange(line.Bottom, 1, page.Height);
            Assert.True(line.Right > line.Left);
            Assert.True(line.Bottom > line.Top);
        });
    }
}
