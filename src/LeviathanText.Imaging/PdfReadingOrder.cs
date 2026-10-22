namespace LeviathanText.Imaging;

/// <summary>
/// Bounded reading-order inference for extracted PDF text lines. It recognizes a clean
/// two-column split when no line crosses the central gutter; otherwise it falls back to
/// top-to-bottom row order. This is a mechanical aid, not a claim about complex layouts,
/// rotated text or mixed-direction columns.
/// </summary>
public static class PdfReadingOrder
{
    public sealed record OrderedLine(PdfTextExtractor.TextLine Line, int Column, int Order);

    public static IReadOnlyList<OrderedLine> Order(
        IReadOnlyList<PdfTextExtractor.TextLine> lines,
        double pageWidth)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Count == 0) return [];

        var tolerance = Math.Max(2.0, pageWidth * 0.02);
        var middle = pageWidth / 2;
        var left = lines.Where(line => line.Right <= middle + tolerance).ToList();
        var right = lines.Where(line => line.Left >= middle - tolerance).ToList();
        var isCleanTwoColumn = left.Count >= 2 && right.Count >= 2 && left.Count + right.Count == lines.Count;

        if (isCleanTwoColumn)
        {
            var ordered = new List<OrderedLine>(lines.Count);
            AddColumn(ordered, left, 0);
            AddColumn(ordered, right, 1);
            return ordered;
        }

        return [.. lines
            .OrderByDescending(line => (line.Top + line.Bottom) / 2)
            .ThenBy(line => line.Left)
            .Select((line, index) => new OrderedLine(line, 0, index))];

    }

    private static void AddColumn(List<OrderedLine> ordered, IReadOnlyList<PdfTextExtractor.TextLine> lines, int column)
    {
        foreach (var line in lines
                     .OrderByDescending(line => (line.Top + line.Bottom) / 2)
                     .ThenBy(line => line.Left))
            ordered.Add(new OrderedLine(line, column, ordered.Count));
    }
}
