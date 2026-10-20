using LeviathanText.Contracts;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace LeviathanText.Imaging;

/// <summary>
/// Bounded PDF text extraction (PdfPig). Output is a coordinate-bearing **L2 reference**
/// only: PDF text may be editorial, normalized or synthesized, so it must never be imported
/// as visible-text/L1 labels. Word boxes use PDF points with a bottom-left origin; callers
/// map them onto rendered page pixels. Rotated/mixed-direction pages are not interpreted.
/// </summary>
public static class PdfTextExtractor
{
    public sealed record TextLine(string Text, double Left, double Bottom, double Right, double Top);

    public sealed record PageText(int PageNumber, double Width, double Height, IReadOnlyList<TextLine> Lines);

    public const long MaxEncodedBytes = PdfRenderer.MaxEncodedBytes;
    public const int MaxPages = PdfRenderer.MaxPages;

    public static IReadOnlyList<PageText> Extract(string pdfPath, int maxPages = MaxPages)
    {
        if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = pdfPath,
                Message = "PDF file is not readable on this host.",
            });
        var file = new FileInfo(pdfPath);
        if (file.Length > MaxEncodedBytes)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.RejectedArchive,
                Scope = pdfPath,
                Message = "PDF exceeds the configured size limit.",
            });

        var pages = new List<PageText>();
        try
        {
            using var document = PdfDocument.Open(pdfPath);
            foreach (var page in document.GetPages().Take(Math.Clamp(maxPages, 1, MaxPages)))
            {
                var words = page.GetWords()
                    .Where(w => !string.IsNullOrWhiteSpace(w.Text))
                    .ToList();
                var groups = GroupLines(words, page.Width);
                var lines = groups.Select(group =>
                {
                    var left = group.Min(w => w.BoundingBox.Left);
                    var right = group.Max(w => w.BoundingBox.Right);
                    var bottom = group.Min(w => w.BoundingBox.Bottom);
                    var top = group.Max(w => w.BoundingBox.Top);
                    var text = string.Join(" ", group.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text.Trim()));
                    return new TextLine(text, left, bottom, right, top);
                }).Where(l => !string.IsNullOrWhiteSpace(l.Text)).ToList();

                pages.Add(new PageText(page.Number, page.Width, page.Height, lines));
            }
        }
        catch (LeviathanException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.RejectedArchive,
                Scope = pdfPath,
                Details = new Dictionary<string, string> { ["reason"] = ex.GetType().Name },
                Message = "PDF text could not be extracted; it may be encrypted, malformed or use unsupported fonts.",
            });
        }

        return pages;
    }

    private static List<List<Word>> GroupLines(IReadOnlyList<Word> words, double pageWidth)
    {
        var ordered = words
            .OrderByDescending(w => w.BoundingBox.Bottom + w.BoundingBox.Height / 2)
            .ThenBy(w => w.BoundingBox.Left)
            .ToList();
        var rows = new List<List<Word>>();
        foreach (var word in ordered)
        {
            var center = word.BoundingBox.Bottom + word.BoundingBox.Height / 2;
            var tolerance = Math.Max(2.0, word.BoundingBox.Height * 0.6);
            var group = rows.LastOrDefault(g =>
            {
                var groupCenter = g.Average(w => w.BoundingBox.Bottom + w.BoundingBox.Height / 2);
                return Math.Abs(groupCenter - center) <= tolerance;
            });
            if (group is null) rows.Add([word]);
            else group.Add(word);
        }

        // Split large horizontal gaps into separate candidate lines (for example facing
        // columns). This is line segmentation only; it does not claim reading order.
        var gapThreshold = Math.Max(8.0, pageWidth * 0.04);
        var lines = new List<List<Word>>();
        foreach (var row in rows)
        {
            var current = new List<Word>();
            Word? previous = null;
            foreach (var word in row.OrderBy(w => w.BoundingBox.Left))
            {
                if (previous is not null && word.BoundingBox.Left - previous.BoundingBox.Right > gapThreshold)
                {
                    if (current.Count > 0) lines.Add(current);
                    current = [];
                }
                current.Add(word);
                previous = word;
            }
            if (current.Count > 0) lines.Add(current);
        }
        return lines;
    }
}
