using SkiaSharp;
using LeviathanText.Layout;

namespace LeviathanText.Layout.Tests;

public class ProjectionProfileLayoutTests
{
    private static SKBitmap Page(params (int Top, int Height)[] bands)
    {
        using var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(200, bands.Max(b => b.Top + b.Height) + 40));
        var canvas = surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.White);
        using var paint = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.Black };
        foreach (var (top, h) in bands)
            canvas.DrawRect(new SkiaSharp.SKRect(30, top, 170, top + h), paint);
        using var image = surface.Snapshot();
        return SkiaSharp.SKBitmap.FromImage(image);
    }

    [Fact]
    public void Detects_three_distinct_bands_in_order()
    {
        // Three 12px bands with 20px blank gaps between them.
        using var page = Page((0, 12), (32, 12), (64, 12));
        var result = ProjectionProfileLayout.Detect(page);

        // Diagnostic: inline row profile.
        var info = page.Info;
        var bytes = page.Bytes;
        var sample = new List<string>();
        for (int row = 0; row < page.Height && row < 90; row += 4)
        {
            int ink = 0;
            for (int col = 0; col < page.Width; col++)
            {
                int b = (row * page.Width + col) * info.BytesPerPixel;
                float luminance = (0.299f * bytes[b + 2] + 0.587f * bytes[b + 1] + 0.114f * bytes[b]) / 255f;
                if (luminance < 0.5f) ink++;
            }
            sample.Add($"{row}:{(double)ink / page.Width:0.##}");
        }
        Assert.True(result.Lines.Count == 3, $"page {page.Width}x{page.Height} bpp={info.BytesPerPixel}; profile {string.Join(",", sample)}; detected {string.Join("; ", result.Lines)}");
        Assert.All(result.Lines, l => Assert.True(l.Height >= 6));
        // Reading order: top to bottom.
        var tops = result.Lines.Select(l => l.Top).ToList();
        Assert.Equal(tops.OrderBy(t => t), tops);

        // Confidence is None: no fabricated probability semantics.
        Assert.True(LeviathanText.Contracts.Confidence.None.IsNone);
        Assert.NotEmpty(result.ParametersHash);
        Assert.Equal(nameof(ProjectionProfileLayout), result.Method);
    }

    [Fact]
    public void Blank_page_yields_no_lines()
    {
        using var page = new SkiaSharp.SKBitmap(100, 100);
        page.Erase(SkiaSharp.SKColors.White);
        var result = ProjectionProfileLayout.Detect(page);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void Ink_density_is_bounded_and_nonzero_in_bands()
    {
        using var page = Page((0, 20));
        var result = ProjectionProfileLayout.Detect(page);

        var line = Assert.Single(result.Lines);
        Assert.InRange(line.InkDensity, 0.0, 1.0);
        Assert.True(line.InkDensity > 0.02);
        // x extent: the drawn rect spans x=30..170
        Assert.InRange(line.Left, 25, 35);
        Assert.InRange(line.Right, 165, 175);
    }
}

public class LayoutComparisonTests
{
    private static DetectedLine Line(int top, int bottom, int order = 0) => new()
    {
        Id = new LeviathanText.Contracts.EntityId(LeviathanText.Contracts.EntityKind.Region, $"l{order:x4}"),
        Top = top,
        Bottom = bottom,
        Left = 0,
        Right = 100,
        OrderIndex = order,
    };

    [Fact]
    public void Perfect_detection_scores_1_1()
    {
        var gt = new[] { (0, 20), (40, 60) };
        var detected = new[] { Line(0, 20), Line(40, 60) };
        var summary = LayoutComparison.Compare([("w", detected, gt)]);

        Assert.Equal(1.0, summary.MeanPrecision);
        Assert.Equal(1.0, summary.MeanRecall);
    }

    [Fact]
    public void Missed_and_extra_lines_lower_both()
    {
        var gt = new[] { (0, 20), (40, 60), (80, 100) }; // three true lines
        var detected = new[] { Line(0, 20), Line(41, 59), Line(200, 220) }; // one extra (no gt)
        var summary = LayoutComparison.Compare([("w", detected, gt)], iouThreshold: 0.5);

        Assert.Equal(2, summary.PerWitness[0].Matches);
        Assert.Equal(2.0 / 3.0, summary.MeanRecall, 5);
        Assert.Equal(2.0 / 3.0, summary.MeanPrecision, 5); // 2 matches / 3 detections
    }

    [Fact]
    public void Iou_threshold_is_disclosed()
    {
        var gt = new[] { (0, 20) };
        var nearMiss = new[] { Line(16, 36) }; // IoU = 4/36 = 0.111
        var summary = LayoutComparison.Compare([("w", nearMiss, gt)], iouThreshold: 0.5);
        Assert.Equal(0, summary.PerWitness[0].Matches);
        Assert.Equal(0.5, summary.IouThreshold);
    }
}
