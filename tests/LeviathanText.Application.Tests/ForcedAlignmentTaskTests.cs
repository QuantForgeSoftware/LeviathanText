using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Recognition;
using Xunit;

namespace LeviathanText.Application.Tests;

public class ForcedAlignmentTaskTests
{
    private static readonly string[] Vocabulary = ["α", "β"];

    [Fact]
    public void Record_keeps_line_region_bbox_and_grapheme_spans()
    {
        var labels = new LabelSet(Vocabulary);
        var model = new CrnnModel(labels.NumClasses, hiddenSize: 8);
        var line = Line("line1", "region1");

        var record = ForcedAlignmentTask.Align(
            model, labels, line, new LineImage(16, 64, new float[16 * 64]), "αβ");

        Assert.Equal(line.Id, record.LineId);
        Assert.Equal(line.RegionId, record.RegionId);
        Assert.Equal("αβ", record.Text);
        Assert.Equal(4, record.Box.Count);
        Assert.Equal(2, record.Graphemes.Count);
        Assert.True(record.Box[2].X > record.Box[0].X);
        Assert.True(record.Box[2].Y > record.Box[0].Y);
    }

    [Fact]
    public void Multiple_lines_return_one_record_each()
    {
        var labels = new LabelSet(Vocabulary);
        var model = new CrnnModel(labels.NumClasses, hiddenSize: 8);
        var image = new LineImage(16, 64, new float[16 * 64]);

        var records = ForcedAlignmentTask.Align(model, labels,
        [
            (Line("l1", "r1"), image, "α"),
            (Line("l2", "r2"), image, "β"),
        ]);

        Assert.Equal(2, records.Count);
        Assert.Equal(["l1", "l2"], records.Select(r => r.LineId.Suffix).ToArray());
    }

    [Fact]
    public void Empty_input_returns_no_records()
    {
        var labels = new LabelSet(Vocabulary);
        var model = new CrnnModel(labels.NumClasses, hiddenSize: 8);
        Assert.Empty(ForcedAlignmentTask.Align(model, labels, []));
    }

    [Fact]
    public void Unencodable_text_fails_loudly()
    {
        var labels = new LabelSet(Vocabulary);
        var model = new CrnnModel(labels.NumClasses, hiddenSize: 8);
        var ex = Assert.Throws<LeviathanException>(() => ForcedAlignmentTask.Align(
            model, labels, Line("l1", "r1"), new LineImage(16, 64, new float[16 * 64]), "γ"));
        Assert.Equal(ErrorCode.UnknownLabel, ex.Error.Code);
    }

    private static NativeLine Line(string id, string region) => new()
    {
        Id = new EntityId(EntityKind.Line, id),
        RegionId = new EntityId(EntityKind.Region, region),
        FrameRef = new EntityId(EntityKind.Capture, "cap"),
        Polygon = [new PointD(10, 20), new PointD(90, 20), new PointD(90, 50), new PointD(10, 50)],
    };
}
