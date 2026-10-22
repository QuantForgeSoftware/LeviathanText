using LeviathanText.Formats;

namespace LeviathanText.Training;

/// <summary>
/// Multi-class mapping for segmentation targets. Sections are ordered label→index lists,
/// so filtering (omitting labels) and merging (sharing an index) are explicit rather than
/// inferred from a dictionary's incidental iteration order. The canonical label of a
/// merged index is the first label inserted for that index.
/// </summary>
public sealed class SegmentationClassMap
{
    private readonly Dictionary<string, Dictionary<string, int>> _bySection;

    private SegmentationClassMap(
        IReadOnlyList<(string Label, int Index)> aux,
        IReadOnlyList<(string Label, int Index)> baselines,
        IReadOnlyList<(string Label, int Index)> regions)
    {
        _bySection = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal)
        {
            ["aux"] = Build(aux),
            ["baselines"] = Build(baselines),
            ["regions"] = Build(regions),
        };
        NumClasses = _bySection.Values.SelectMany(section => section.Values).DefaultIfEmpty(-1).Max() + 1;
        Canonical = BuildCanonical();
        MergedClasses = BuildMerged();
    }

    public int NumClasses { get; }

    /// <summary>One canonical label per class index, first insertion wins.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> Canonical { get; }

    /// <summary>Canonical labels that have aliases, per section.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> MergedClasses { get; }

    public static SegmentationClassMap Create(
        IReadOnlyList<(string Label, int Index)> aux,
        IReadOnlyList<(string Label, int Index)> baselines,
        IReadOnlyList<(string Label, int Index)> regions)
    {
        ArgumentNullException.ThrowIfNull(aux);
        ArgumentNullException.ThrowIfNull(baselines);
        ArgumentNullException.ThrowIfNull(regions);
        return new SegmentationClassMap(aux, baselines, regions);
    }

    public bool TryGetIndex(string section, string label, out int index)
    {
        index = -1;
        return _bySection.TryGetValue(section, out var map) && map.TryGetValue(label, out index);
    }

    private static Dictionary<string, int> Build(IReadOnlyList<(string Label, int Index)> entries)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (label, index) in entries)
        {
            if (string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("Class labels must be non-empty.", nameof(entries));
            if (index < 0)
                throw new ArgumentException($"Class index for '{label}' must be non-negative.", nameof(entries));
            if (!result.TryAdd(label, index))
                throw new ArgumentException($"Duplicate class label '{label}'.", nameof(entries));
        }
        return result;
    }

    private IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> BuildCanonical()
    {
        var result = new Dictionary<string, IReadOnlyDictionary<string, int>>(StringComparer.Ordinal);
        foreach (var (section, map) in _bySection)
        {
            var canonical = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var (label, index) in map)
                if (!canonical.ContainsValue(index)) canonical[label] = index;
            result[section] = canonical;
        }
        return result;
    }

    private IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> BuildMerged()
    {
        var result = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal);
        foreach (var (section, map) in _bySection)
        {
            var groups = map.GroupBy(pair => pair.Value)
                .Where(group => group.Count() > 1)
                .ToList();
            var merged = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            foreach (var group in groups)
            {
                var ordered = group.Select(pair => pair.Key).ToList();
                merged[ordered[0]] = ordered.Skip(1).ToArray();
            }
            result[section] = merged;
        }
        return result;
    }
}

public sealed record SegmentationBaseline(string Label, IReadOnlyList<PointD> Points);

public sealed record SegmentationRegion(string Label, IReadOnlyList<PointD> Points);

/// <summary>
/// Rasterizes region polygons and baseline polylines into one target tensor per page:
/// <c>[class, height, width]</c>, with 1 where a class is present. Unknown labels are
/// filtered out; overlapping classes remain overlapping channels, matching a multi-label
/// target rather than silently choosing a winner.
/// </summary>
public static class SegmentationTargetBuilder
{
    public static float[,,] Build(
        int width,
        int height,
        SegmentationClassMap classes,
        IReadOnlyList<SegmentationRegion>? regions = null,
        IReadOnlyList<SegmentationBaseline>? baselines = null)
    {
        ArgumentNullException.ThrowIfNull(classes);
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Target dimensions must be positive.");

        var target = new float[classes.NumClasses, height, width];
        foreach (var region in regions ?? [])
        {
            if (!classes.TryGetIndex("regions", region.Label, out var index)) continue;
            FillPolygon(target, index, region.Points, width, height);
        }
        foreach (var baseline in baselines ?? [])
        {
            if (!classes.TryGetIndex("baselines", baseline.Label, out var index)) continue;
            DrawPolyline(target, index, baseline.Points, width, height);
        }
        return target;
    }

    private static void FillPolygon(float[,,] target, int index, IReadOnlyList<PointD> points, int width, int height)
    {
        if (points.Count < 3) return;
        int minX = Math.Max(0, (int)Math.Floor(points.Min(p => p.X)));
        int maxX = Math.Min(width - 1, (int)Math.Ceiling(points.Max(p => p.X)));
        int minY = Math.Max(0, (int)Math.Floor(points.Min(p => p.Y)));
        int maxY = Math.Min(height - 1, (int)Math.Ceiling(points.Max(p => p.Y)));
        for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
                if (Contains(points, x + 0.5, y + 0.5))
                    target[index, y, x] = 1;
    }

    private static bool Contains(IReadOnlyList<PointD> polygon, double x, double y)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var a = polygon[i];
            var b = polygon[j];
            if ((a.Y > y) != (b.Y > y)
                && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }
        return inside;
    }

    private static void DrawPolyline(float[,,] target, int index, IReadOnlyList<PointD> points, int width, int height)
    {
        for (int i = 1; i < points.Count; i++)
        {
            var a = points[i - 1];
            var b = points[i];
            int steps = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y))));
            for (int step = 0; step <= steps; step++)
            {
                double t = (double)step / steps;
                int x = (int)Math.Round(a.X + (b.X - a.X) * t);
                int y = (int)Math.Round(a.Y + (b.Y - a.Y) * t);
                if (x >= 0 && x < width && y >= 0 && y < height)
                    target[index, y, x] = 1;
            }
        }
    }
}
