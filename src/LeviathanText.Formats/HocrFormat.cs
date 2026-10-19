using System.Text;
using System.Xml.Linq;
using LeviathanText.Contracts;

namespace LeviathanText.Formats;

/// <summary>
/// Bounded hOCR adapter (Phase 4 parity + legacy-OCR migration). Supported subset:
/// one <c>ocr_page</c> with <c>ocr_line</c> spans carrying a bbox in the title and the
/// line text inside. We do not synthesize word segmentation: exports put the line text in
/// a single <c>ocrx_word</c> with the line bbox (round-trips exactly). Word-level tags on
/// import are joined; unsupported structural classes are reported, never flattened
/// silently. External entities/DTDs are refused.
/// </summary>
public static class HocrFormat
{
    private static readonly XNamespace Xhtml = "http://www.w3.org/1999/xhtml";

    public sealed record HocrImportResult(
        IReadOnlyList<NativeRegion> Regions,
        IReadOnlyList<NativeLine> Lines,
        IReadOnlyList<TextRevision> Revisions,
        IReadOnlyList<PageXml.ImportIssue> Unsupported);

    public static string Export(NativeSurface surface, NativeCapture capture, IReadOnlyList<TextRevision> revisions)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(capture);

        var revisionById = revisions.ToDictionary(r => r.Id);
        var lines = new List<XElement>();

        foreach (var line in surface.Lines)
        {
            var (x0, y0, x1, y1) = Bounds(line);
            var text = line.DiplomaticRevisionId is { } rid && revisionById.TryGetValue(rid, out var revision)
                ? revision.Text
                : string.Empty;

            var word = string.IsNullOrEmpty(text)
                ? null
                : new XElement(Xhtml + "span",
                    new XAttribute("class", "ocrx_word"),
                    new XAttribute("id", $"word_{line.Id.Suffix}"),
                    new XAttribute("title", $"bbox {x0} {y0} {x1} {y1}"),
                    text);

            var baseline = line.Baseline.Count >= 2
                ? (int)Math.Round(line.Baseline[^1].Y - line.Baseline[0].Y)
                : 0;

            lines.Add(new XElement(Xhtml + "span",
                new XAttribute("class", "ocr_line"),
                new XAttribute("id", $"line_{line.Id.Suffix}"),
                new XAttribute("title", $"bbox {x0} {y0} {x1} {y1}; baseline 0 {baseline}"),
                word));
        }

        var pageDiv = new XElement(Xhtml + "div",
            new XAttribute("class", "ocr_page"),
            new XAttribute("id", "page_1"),
            new XAttribute("title",
                $"image \"{capture.ImageLocation}\"; bbox 0 0 {capture.WidthPx ?? 0} {capture.HeightPx ?? 0}"),
            new XElement(Xhtml + "div",
                new XAttribute("class", "ocr_carea"),
                new XAttribute("id", "block_1"),
                new XElement(Xhtml + "p",
                    new XAttribute("class", "ocr_par"),
                    new XAttribute("id", "par_1"),
                    lines)));

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(Xhtml + "html",
                new XElement(Xhtml + "head",
                    new XElement(Xhtml + "title", "LeviathanText hOCR export"),
                    new XElement(Xhtml + "meta",
                        new XAttribute("http-equiv", "Content-Type"),
                        new XAttribute("content", "text/html; charset=utf-8"))),
                new XElement(Xhtml + "body", pageDiv)));

        return doc.ToString();
    }

    public static HocrImportResult Import(string html, EntityId frameEntity, Checksum frameChecksum)
    {
        var doc = XDocument.Parse(html, LoadOptions.None);
        if (doc.DocumentType is not null)
            throw new InvalidDataException("DOCTYPE/external entities are not accepted in hOCR imports.");

        var root = doc.Root ?? throw new InvalidDataException("Missing hOCR root.");
        if (root.Name.LocalName != "html")
            throw new InvalidDataException($"Unexpected root {root.Name.LocalName}; expected XHTML html.");
        if (!root.Descendants().Any(e => (string?)e.Attribute("class") == "ocr_page"))
            throw new InvalidDataException("No ocr_page element found; not an hOCR document.");

        var unsupported = new List<PageXml.ImportIssue>();
        var regionId = new EntityId(EntityKind.Region, "hocr-region-1");
        var regions = new List<NativeRegion>
        {
            new()
            {
                Id = regionId,
                FrameRef = frameEntity,
                LayoutSource = LayoutSource.Imported,
                ReadingOrderIndex = 0,
            },
        };
        var lines = new List<NativeLine>();
        var revisions = new List<TextRevision>();

        foreach (var element in root.Descendants().Where(e => (string?)e.Attribute("class") == "ocr_line"))
        {
            var title = (string?)element.Attribute("title") ?? "";
            var (left, top, right, bottom) = ParseBbox(title);
            var text = TextOf(element).Trim();
            var suffix = (string?)element.Attribute("id") ?? $"hocr-line-{lines.Count}";
            var lineId = new EntityId(EntityKind.Line, suffix);

            TextRevision? revision = null;
            if (text.Length > 0)
            {
                revision = new TextRevision
                {
                    Id = new EntityId(EntityKind.TranscriptionRevision, $"hocr-{suffix}"),
                    ScopeId = lineId,
                    Layer = TextLayer.L1Diplomatic,
                    Author = new AgentRef { Id = "hocr-import", IsModel = false },
                    CreatedAt = DateTimeOffset.UtcNow,
                    Text = text,
                };
                revisions.Add(revision);
            }

            lines.Add(new NativeLine
            {
                Id = lineId,
                RegionId = regionId,
                FrameRef = frameEntity,
                Polygon =
                [
                    new PointD(left, top), new PointD(right, top),
                    new PointD(right, bottom), new PointD(left, bottom),
                ],
                Baseline = [new PointD(left, bottom), new PointD(right, bottom)],
                DiplomaticRevisionId = revision?.Id,
            });

            foreach (var child in element.Descendants())
            {
                var cls = (string?)child.Attribute("class") ?? "";
                if (cls.StartsWith("ocrx_word", StringComparison.Ordinal)) continue;
                if (cls.StartsWith("ocr_", StringComparison.Ordinal) || cls.StartsWith("ocrx_", StringComparison.Ordinal))
                    unsupported.Add(new PageXml.ImportIssue(cls,
                        "hOCR element outside the bounded subset (detected language/direction/attributes not mapped)"));
            }
        }

        if (lines.Count == 0)
            throw new InvalidDataException("hOCR document contains no ocr_line elements.");

        return new HocrImportResult(regions, lines, revisions, unsupported);
    }

    private static string TextOf(XElement element)
    {
        // Word-level tags are joined with single spaces (hOCR convention); <br> is a space.
        var parts = element.Descendants()
            .Where(e => e.Name.LocalName is "span" or "br")
            .Select(e => e.Name.LocalName == "br" ? " " : e.Value.Trim())
            .Where(s => s.Length > 0)
            .ToList();
        return parts.Count == 0 ? element.Value.Trim() : string.Join(" ", parts);
    }

    private static (int Left, int Top, int Right, int Bottom) ParseBbox(string title)
    {
        // hOCR title convention: "bbox x0 y0 x1 y1; baseline ..."
        var marker = title.IndexOf("bbox ", StringComparison.Ordinal);
        if (marker < 0) return (0, 0, 0, 0);
        var parts = title[(marker + 5)..]
            .Split(';')[0]
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4
            || !int.TryParse(parts[0], out var x0) || !int.TryParse(parts[1], out var y0)
            || !int.TryParse(parts[2], out var x1) || !int.TryParse(parts[3], out var y1))
            return (0, 0, 0, 0);
        return (x0, y0, x1, y1);
    }

    private static (int X0, int Y0, int X1, int Y1) Bounds(NativeLine line)
    {
        if (line.Polygon.Count == 0) return (0, 0, 0, 0);
        return (
            (int)Math.Round(line.Polygon.Min(p => p.X)),
            (int)Math.Round(line.Polygon.Min(p => p.Y)),
            (int)Math.Round(line.Polygon.Max(p => p.X)),
            (int)Math.Round(line.Polygon.Max(p => p.Y)));
    }
}
