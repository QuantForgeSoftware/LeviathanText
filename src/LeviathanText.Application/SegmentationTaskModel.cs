using LeviathanText.Layout;
using SkiaSharp;

namespace LeviathanText.Application;

/// <summary>Segmentation output: line bands, optional column groups and the detector
/// parameters needed to reproduce the result.</summary>
public sealed record SegmentationTaskResult(
    LayoutResult Layout,
    IReadOnlyList<LayoutColumn> Columns);

/// <summary>
/// Segmentation task wrapper over the bounded projection detector. It does not claim a
/// learned region model; columns are grouped only when the line-center gutter test passes.
/// </summary>
public sealed class SegmentationTaskModel
{
    public SegmentationTaskResult Predict(
        SKBitmap page,
        ProjectionProfileLayout.Options? options = null,
        bool detectColumns = true)
    {
        ArgumentNullException.ThrowIfNull(page);
        var layout = ProjectionProfileLayout.Detect(page, options);
        IReadOnlyList<LayoutColumn> columns;
        if (layout.Lines.Count == 0)
            columns = [];
        else if (detectColumns)
            columns = ColumnSegmentation.Split(layout.Lines, page.Width);
        else
            columns = [new LayoutColumn(0, layout.Lines.Min(line => line.Left), layout.Lines.Max(line => line.Right), layout.Lines)];
        return new SegmentationTaskResult(layout, columns);
    }
}
