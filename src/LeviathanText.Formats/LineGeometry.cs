namespace LeviathanText.Formats;

/// <summary>
/// Bounded baseline/bbox conversion used by interchange and UI geometry. A baseline is a
/// 2-point line; a boundary is the closed polygon. Text direction controls endpoint order and
/// the quadrant used for a baseline synthesized from a box. This is geometry only; it does not
/// claim recognition support for a direction.
/// </summary>
public static class LineGeometry
{
    public readonly record struct Box(double Left, double Top, double Right, double Bottom)
    {
        public double Width => Math.Max(0, Right - Left);
        public double Height => Math.Max(0, Bottom - Top);
    }

    public static Box Bounds(IReadOnlyList<PointD> points) =>
        points.Count == 0 ? default : new Box(points.Min(p => p.X), points.Min(p => p.Y),
            points.Max(p => p.X), points.Max(p => p.Y));

    public static IReadOnlyList<PointD> Boundary(Box box) =>
        [new(box.Left, box.Top), new(box.Right, box.Top), new(box.Right, box.Bottom),
         new(box.Left, box.Bottom), new(box.Left, box.Top)];

    public static IReadOnlyList<PointD> Baseline(Box box, TextDirection direction = TextDirection.Ltr, bool? topline = null)
    {
        if (direction is TextDirection.VerticalTopToBottom or TextDirection.VerticalBottomToTop)
        {
            double x = direction == TextDirection.VerticalTopToBottom
                ? box.Left + box.Width * (topline == true ? 0.75 : topline == false ? 0.25 : 0.5)
                : box.Left + box.Width * (topline == true ? 0.25 : topline == false ? 0.75 : 0.5);
            return [new(x, box.Top), new(x, box.Bottom)];
        }

        double y = box.Top + box.Height * (topline == true ? 0.25 : topline == false ? 0.75 : 0.5);
        return direction == TextDirection.Rtl
            ? [new(box.Right, y), new(box.Left, y)]
            : [new(box.Left, y), new(box.Right, y)];
    }
}
