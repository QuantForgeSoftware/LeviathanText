using LeviathanText.Formats;

namespace LeviathanText.Layout;

/// <summary>
/// Bounded geometric reading-order utilities. This is not a claim of full multi-column layout:
/// it orders already-detected line boxes by vertical/horizontal direction and supports a
/// caller-supplied partial-order topological sort. Text direction changes horizontal ordering
/// only; vertical line order remains top-to-bottom unless a different policy is chosen upstream.
/// </summary>
public static class ReadingOrder
{
    public readonly record struct Box(double Left, double Top, double Right, double Bottom);

    public static Box Bounds(IReadOnlyList<PointD> points)
    {
        if (points.Count == 0) return default;
        return new Box(points.Min(p => p.X), points.Min(p => p.Y), points.Max(p => p.X), points.Max(p => p.Y));
    }

    public static bool IsInRegion(IReadOnlyList<PointD> line, IReadOnlyList<PointD> region)
    {
        if (line.Count < 2 || region.Count < 3) return false;
        foreach (var point in line)
            if (!PointInPolygon(point, region)) return false;
        for (int i = 1; i < line.Count; i++)
        {
            var mid = new PointD((line[i - 1].X + line[i].X) / 2, (line[i - 1].Y + line[i].Y) / 2);
            if (!PointInPolygon(mid, region)) return false;
        }
        return true;
    }

    public static IReadOnlyList<int> Order(IReadOnlyList<IReadOnlyList<PointD>> lines,
        TextDirection direction = TextDirection.Ltr)
    {
        var boxes = lines.Select(Bounds).ToList();
        var indices = Enumerable.Range(0, lines.Count);
        indices = direction switch
        {
            TextDirection.Rtl => indices.OrderBy(i => boxes[i].Top).ThenByDescending(i => boxes[i].Right),
            TextDirection.VerticalTopToBottom => indices.OrderBy(i => boxes[i].Left).ThenBy(i => boxes[i].Top),
            TextDirection.VerticalBottomToTop => indices.OrderByDescending(i => boxes[i].Left).ThenBy(i => boxes[i].Top),
            _ => indices.OrderBy(i => boxes[i].Top).ThenBy(i => boxes[i].Left),
        };
        return indices.ToArray();
    }

    /// <summary>Kahn topological sort of a partial-order matrix; matrix[i,j] means i precedes j.</summary>
    public static IReadOnlyList<int> TopologicalSort(bool[,] before)
    {
        int n = before.GetLength(0);
        if (before.GetLength(1) != n)
            throw new ArgumentException("Partial-order matrix must be square.", nameof(before));
        var indegree = new int[n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                if (i != j && before[i, j]) indegree[j]++;

        var queue = new SortedSet<int>(Enumerable.Range(0, n).Where(i => indegree[i] == 0));
        var result = new List<int>(n);
        while (queue.Count > 0)
        {
            int next = queue.Min;
            queue.Remove(next);
            result.Add(next);
            for (int j = 0; j < n; j++)
            {
                if (next == j || !before[next, j]) continue;
                if (--indegree[j] == 0) queue.Add(j);
            }
        }
        if (result.Count != n)
            throw new InvalidOperationException("Partial-order matrix contains a cycle.");
        return result;
    }

    private static bool PointInPolygon(PointD p, IReadOnlyList<PointD> polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var a = polygon[i];
            var b = polygon[j];
            if (OnSegment(p, a, b)) return true;
            if ((a.Y > p.Y) != (b.Y > p.Y)
                && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }
        return inside;
    }

    private static bool OnSegment(PointD p, PointD a, PointD b)
    {
        const double eps = 1e-9;
        double cross = (p.Y - a.Y) * (b.X - a.X) - (p.X - a.X) * (b.Y - a.Y);
        if (Math.Abs(cross) > eps) return false;
        return p.X >= Math.Min(a.X, b.X) - eps && p.X <= Math.Max(a.X, b.X) + eps
            && p.Y >= Math.Min(a.Y, b.Y) - eps && p.Y <= Math.Max(a.Y, b.Y) + eps;
    }
}
