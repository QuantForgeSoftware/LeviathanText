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

    [Theory]
    [InlineData(SKColorType.Gray8, 0, false, false)]
    [InlineData(SKColorType.Gray8, 31, false, false)]
    [InlineData(SKColorType.Gray8, 31, true, false)]
    [InlineData(SKColorType.Gray8, 31, true, true)]
    [InlineData(SKColorType.Bgra8888, 32, true, false)]
    [InlineData(SKColorType.Rgba8888, 32, true, true)]
    public void Raster_format_and_row_padding_preserve_projection(
        SKColorType colorType, int padding, bool columns, bool rightToLeft)
    {
        using var expectedPage = TwoColumnRaster(SKColorType.Bgra8888, 0);
        using var actualPage = TwoColumnRaster(colorType, padding);
        var options = new ProjectionProfileLayout.Options
        {
            DetectColumns = columns,
            RightToLeftColumns = rightToLeft,
        };

        var expected = ProjectionProfileLayout.Detect(expectedPage, options);
        var actual = ProjectionProfileLayout.Detect(actualPage, options);

        Assert.Equal(columns ? 6 : 3, expected.Lines.Count);
        Assert.Equal(expected.Lines.ToArray(), actual.Lines.ToArray());
        Assert.Equal(expected.Method, actual.Method);
        Assert.Equal(expected.ParametersHash, actual.ParametersHash);
        if (columns)
        {
            Assert.Equal(rightToLeft ? 152 : 25, actual.Lines[0].Left);
            Assert.Equal(rightToLeft ? 25 : 152, actual.Lines[3].Left);
        }
    }

    [Theory]
    [InlineData(186)]
    [InlineData(233)]
    public void Midtone_gray_pixels_use_the_same_feature_threshold_as_equivalent_color(byte value)
    {
        using var expectedPage = new SKBitmap(new SKImageInfo(40, 40, SKColorType.Bgra8888, SKAlphaType.Opaque));
        using var actualPage = new SKBitmap(new SKImageInfo(40, 40, SKColorType.Gray8, SKAlphaType.Opaque));
        var tone = new SKColor(value, value, value);
        expectedPage.Erase(tone);
        actualPage.Erase(tone);

        var expected = RowFeatureExtractor.Profile(expectedPage);
        var actual = RowFeatureExtractor.Profile(actualPage);

        Assert.Equal(expected.InkThreshold, actual.InkThreshold);
        Assert.Equal(expected.RowDensity, actual.RowDensity);
    }

    [Theory]
    [InlineData(SKColorType.Gray8, 0)]
    [InlineData(SKColorType.Gray8, 31)]
    [InlineData(SKColorType.Bgra8888, 32)]
    [InlineData(SKColorType.Rgba8888, 32)]
    public void Row_features_match_packed_color_for_the_same_pixels(SKColorType colorType, int padding)
    {
        using var expectedPage = TwoColumnRaster(SKColorType.Bgra8888, 0);
        using var actualPage = TwoColumnRaster(colorType, padding);

        var expected = RowFeatureExtractor.Profile(expectedPage);
        var actual = RowFeatureExtractor.Profile(actualPage);

        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
        Assert.Equal(expected.InkThreshold, actual.InkThreshold);
        Assert.Equal(expected.RowDensity, actual.RowDensity);
        Assert.Equal(expected.InkRuns, actual.InkRuns);
        Assert.Equal(expected.MaxRun, actual.MaxRun);
    }

    [Theory]
    [InlineData(SKColorType.Gray8, 0)]
    [InlineData(SKColorType.Gray8, 31)]
    [InlineData(SKColorType.Bgra8888, 32)]
    [InlineData(SKColorType.Rgba8888, 32)]
    public void Row_feature_ink_bounds_ignore_row_padding(SKColorType colorType, int padding)
    {
        using var page = TwoColumnRaster(colorType, padding);
        var bounds = RowFeatureExtractor.InkBounds(page, 20, 32, 0.6f, 10, 230);
        Assert.Equal((25, 214, 130.0 / 220), bounds);
    }

    private static SKBitmap TwoColumnRaster(SKColorType colorType, int padding)
    {
        var info = new SKImageInfo(241, 188, colorType, SKAlphaType.Opaque);
        var bitmap = new SKBitmap(info, info.RowBytes + padding);
        var row = new byte[bitmap.RowBytes];
        for (int y = 0; y < bitmap.Height; y++)
        {
            bool textRow = y is >= 20 and < 32 or >= 69 and < 81 or >= 122 and < 134;
            for (int x = 0; x < bitmap.Width; x++)
            {
                bool surround = x < 8 || x >= 233 || y < 8 || y >= 180;
                bool text = textRow && (x is >= 25 and < 92 or >= 152 and < 215);
                byte value = surround || text ? (byte)0 : (byte)255;
                int offset = x * info.BytesPerPixel;
                row[offset] = value;
                if (colorType != SKColorType.Gray8)
                {
                    row[offset + 1] = value;
                    row[offset + 2] = value;
                    row[offset + 3] = 255;
                }
            }

            // Leave padding black so interpreting it as image pixels changes the layout.
            System.Runtime.InteropServices.Marshal.Copy(row, 0,
                bitmap.GetPixels() + y * bitmap.RowBytes, y + 1 == bitmap.Height ? info.RowBytes : row.Length);
        }
        return bitmap;
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

public class ProjectionColumnTests
{
    private static SKBitmap TwoColumnPage()
    {
        using var surface = SKSurface.Create(new SKImageInfo(800, 600));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        using var paint = new SKPaint { Color = SKColors.Black };
        foreach (var top in new[] { 80, 160, 240, 320 })
        {
            canvas.DrawRect(new SKRect(80, top, 340, top + 30), paint);
            canvas.DrawRect(new SKRect(460, top, 720, top + 30), paint);
        }
        using var image = surface.Snapshot();
        return SKBitmap.FromImage(image);
    }

    [Fact]
    public void Opt_in_column_detection_splits_a_clear_gutter_and_orders_left_then_right()
    {
        using var page = TwoColumnPage();

        var single = ProjectionProfileLayout.Detect(page);
        Assert.Equal(4, single.Lines.Count); // rows span both columns in the baseline

        var columns = ProjectionProfileLayout.Detect(page,
            new ProjectionProfileLayout.Options { DetectColumns = true });

        Assert.Equal(8, columns.Lines.Count);
        Assert.Equal("ProjectionProfileLayout-columns", columns.Method);
        Assert.All(columns.Lines.Take(4), line => Assert.True(line.Right < 400));
        Assert.All(columns.Lines.Skip(4), line => Assert.True(line.Left > 400));
        Assert.Equal(Enumerable.Range(0, 8), columns.Lines.Select(line => line.OrderIndex));
        Assert.Equal(columns.Lines.OrderBy(line => line.Top).Select(line => line.Top).Distinct().Count(),
            columns.Lines.Take(4).Select(line => line.Top).Distinct().Count());
    }

    [Fact]
    public void Rtl_column_order_places_the_right_column_first()
    {
        using var page = TwoColumnPage();

        var result = ProjectionProfileLayout.Detect(page,
            new ProjectionProfileLayout.Options { DetectColumns = true, RightToLeftColumns = true });

        Assert.Equal(8, result.Lines.Count);
        Assert.All(result.Lines.Take(4), line => Assert.True(line.Left > 400));
        Assert.All(result.Lines.Skip(4), line => Assert.True(line.Right < 400));
        Assert.Equal(Enumerable.Range(0, 8), result.Lines.Select(line => line.OrderIndex));
    }

    [Fact]
    public void Column_detection_falls_back_when_there_is_no_gutter()
    {
        using var page = new SKBitmap(400, 240);
        using (var canvas = new SKCanvas(page))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.Black };
            canvas.DrawRect(new SKRect(40, 40, 360, 70), paint);
            canvas.DrawRect(new SKRect(40, 110, 360, 140), paint);
            canvas.DrawRect(new SKRect(40, 180, 360, 210), paint);
        }

        var result = ProjectionProfileLayout.Detect(page,
            new ProjectionProfileLayout.Options { DetectColumns = true });

        Assert.Equal(3, result.Lines.Count);
        Assert.Equal("ProjectionProfileLayout", result.Method);
    }
}
