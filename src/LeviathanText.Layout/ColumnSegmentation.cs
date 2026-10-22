namespace LeviathanText.Layout;

public sealed record LayoutColumn(int Index, double Left, double Right, IReadOnlyList<DetectedLine> Lines);

/// <summary>
/// Bounded column grouping from already-detected line bands. A clean two-column split is
/// accepted only when the largest gap between line centers exceeds a page-relative gutter
/// and both sides have at least two lines; otherwise the page remains one column. This is
/// a layout helper, not a learned region model and not a claim about rotated layouts.
/// </summary>
public static class ColumnSegmentation
{
    public static IReadOnlyList<LayoutColumn> Split(
        IReadOnlyList<DetectedLine> lines,
        int pageWidth,
        double minGutterFraction = 0.06)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (pageWidth <= 0) throw new ArgumentOutOfRangeException(nameof(pageWidth));
        if (lines.Count == 0) return [];

        var ordered = lines.OrderBy(line => (line.Left + line.Right) / 2.0).ThenBy(line => line.Top).ToList();
        double minGutter = pageWidth * minGutterFraction;
        int split = -1;
        double bestGap = 0;
        for (int i = 1; i < ordered.Count; i++)
        {
            double leftCenter = (ordered[i - 1].Left + ordered[i - 1].Right) / 2.0;
            double rightCenter = (ordered[i].Left + ordered[i].Right) / 2.0;
            double gap = rightCenter - leftCenter;
            if (gap > bestGap && i >= 2 && ordered.Count - i >= 2)
            {
                bestGap = gap;
                split = i;
            }
        }

        if (split < 0 || bestGap < minGutter)
            return [Column(0, ordered)];

        var left = ordered.Take(split).OrderBy(line => line.Top).ThenBy(line => line.Left).ToList();
        var right = ordered.Skip(split).OrderBy(line => line.Top).ThenBy(line => line.Left).ToList();
        return [Column(0, left), Column(1, right)];
    }

    private static LayoutColumn Column(int index, IReadOnlyList<DetectedLine> lines) =>
        new(index, lines.Min(line => line.Left), lines.Max(line => line.Right), lines);
}
