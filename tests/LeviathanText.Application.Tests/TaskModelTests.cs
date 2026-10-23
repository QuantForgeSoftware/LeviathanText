using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Recognition;
using SkiaSharp;
using Xunit;

namespace LeviathanText.Application.Tests;

public class TaskModelTests
{
    private static readonly string[] Vocabulary = ["α", "β"];

    private static NativeLine Line(string id) => new()
    {
        Id = new EntityId(EntityKind.Line, id),
        RegionId = new EntityId(EntityKind.Region, "r1"),
        FrameRef = new EntityId(EntityKind.Capture, "cap"),
        Polygon = [new PointD(4, 4), new PointD(60, 4), new PointD(60, 24), new PointD(4, 24)],
    };

    private static SKBitmap Page()
    {
        var page = new SKBitmap(80, 32);
        page.Erase(SKColors.White);
        for (int y = 10; y < 20; y++)
            for (int x = 8; x < 72; x++)
                page.SetPixel(x, y, SKColors.Black);
        return page;
    }

    [Fact]
    public void Recognition_task_returns_one_geometry_bearing_record_per_line()
    {
        var labels = new LabelSet(Vocabulary);
        var task = new RecognitionTaskModel(new CrnnModel(labels.NumClasses, hiddenSize: 8), labels);
        using var page = Page();

        var records = task.Predict(page, [Line("l1"), Line("l2")]);

        Assert.Equal(2, records.Count);
        Assert.All(records, record => Assert.NotNull(record.Bbox));
        Assert.All(records, record => Assert.Equal(4, record.Bbox!.Value.Left));
        Assert.Empty(task.Predict(page, []));
    }

    [Fact]
    public void Forced_alignment_task_returns_records_and_skips_geometryless_lines()
    {
        var labels = new LabelSet(Vocabulary);
        var task = new ForcedAlignmentTaskModel(new CrnnModel(labels.NumClasses, hiddenSize: 8), labels);
        using var page = Page();
        var noGeometry = new NativeLine
        {
            Id = new EntityId(EntityKind.Line, "none"),
            RegionId = new EntityId(EntityKind.Region, "r1"),
            FrameRef = new EntityId(EntityKind.Capture, "cap"),
        };

        var records = task.Predict(page, [(Line("l1"), "αβ"), (noGeometry, "α")]);

        var record = Assert.Single(records);
        Assert.Equal("αβ", record.Text);
        Assert.Equal(2, record.Graphemes.Count);
    }
}
