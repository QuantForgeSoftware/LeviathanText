using System.Globalization;
using System.Xml.Linq;
using LeviathanText.Contracts;

namespace LeviathanText.Formats;

/// <summary>
/// Bounded ABBYY FineReader XML adapter (FineReader 10 namespace). Supported subset: one
/// page, text blocks, text lines with l/t/r/b, baseline and plain character content.
/// Character boxes are emitted as one charParams per line (no word segmentation synthesis);
/// imports concatenate recognized charParams text without recognition alternatives.
/// Unsupported structures and discarded metadata are reported as import losses.
/// </summary>
public static class AbbyyXml
{
    public const string NamespaceUri = "http://www.abbyy.com/FineReader_xml/FineReader10-schema-v1.xml";
    private static readonly XNamespace Ns = NamespaceUri;

    public sealed record ImportResult(
        IReadOnlyList<NativeRegion> Regions,
        IReadOnlyList<NativeLine> Lines,
        IReadOnlyList<TextRevision> Revisions,
        IReadOnlyList<PageXml.ImportIssue> Unsupported);

    public static string Export(NativeSurface surface, NativeCapture capture, IReadOnlyList<TextRevision> revisions)
    {
        var revisionById = revisions.ToDictionary(r => r.Id);
        var blocks = surface.Regions.OrderBy(r => r.ReadingOrderIndex).Select((region, index) =>
        {
            var lines = surface.Lines.Where(l => l.RegionId == region.Id).Select(line =>
            {
                var bounds = Bounds(line.Polygon.Count > 0 ? line.Polygon : line.Baseline);
                var text = line.DiplomaticRevisionId is { } rid && revisionById.TryGetValue(rid, out var revision)
                    ? revision.Text
                    : "";
                var baseline = line.Baseline.Count > 0 ? (int)Math.Round(line.Baseline.Min(p => p.Y)) : bounds.Bottom;
                var lineElement = new XElement(Ns + "line",
                    new XAttribute("baseline", baseline),
                    new XAttribute("l", bounds.Left), new XAttribute("r", bounds.Right),
                    new XAttribute("t", bounds.Top), new XAttribute("b", bounds.Bottom));
                var formatting = new XElement(Ns + "formatting", new XAttribute("lang", ""));
                if (!string.IsNullOrEmpty(text))
                    formatting.Add(new XElement(Ns + "charParams",
                        new XAttribute("l", bounds.Left), new XAttribute("r", bounds.Right),
                        new XAttribute("t", bounds.Top), new XAttribute("b", bounds.Bottom),
                        new XAttribute("wordStart", "1"), new XAttribute("charConfidence", "0"), text));
                lineElement.Add(formatting);
                return lineElement;
            });
            return new XElement(Ns + "block",
                new XAttribute("blockType", "Text"),
                new XElement(Ns + "text", new XElement(Ns + "par", lines)));
        });

        var page = new XElement(Ns + "page",
            new XAttribute("width", capture.WidthPx ?? 0),
            new XAttribute("height", capture.HeightPx ?? 0),
            new XAttribute("resolution", "0"),
            new XAttribute("originalCoords", "1"),
            blocks);
        var document = new XElement(Ns + "document",
            new XAttribute("version", "1.0"),
            new XAttribute("producer", "LeviathanText"),
            page);
        return new XDocument(new XDeclaration("1.0", "UTF-8", null), document).ToString();
    }

    public static ImportResult Import(string xml, EntityId frameEntity, Checksum frameChecksum)
    {
        // A standalone space charParams is recognized content, not XML indentation.
        var doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        if (doc.DocumentType is not null)
            throw new InvalidDataException("DOCTYPE/external entities are not accepted in ABBYY imports.");
        var root = doc.Root ?? throw new InvalidDataException("Missing ABBYY document root.");
        if (root.Name.LocalName != "document" || root.Name.NamespaceName != Ns.NamespaceName)
            throw new InvalidDataException("Expected a FineReader 10 ABBYY document.");
        var page = root.Element(Ns + "page") ?? throw new InvalidDataException("Missing ABBYY page.");

        var regions = new List<NativeRegion>();
        var lines = new List<NativeLine>();
        var revisions = new List<TextRevision>();
        var unsupported = new List<PageXml.ImportIssue>();
        var reported = new HashSet<string>(StringComparer.Ordinal);
        void Report(string element, string description, PageXml.IssueSeverity severity = PageXml.IssueSeverity.Loss)
        {
            if (reported.Add(element)) unsupported.Add(new(element, description, severity));
        }
        if (root.Elements(Ns + "page").Skip(1).Any())
            Report("page", "Only the first page is imported; retain the source XML for additional pages.");
        if (root.Element(Ns + "documentData") is not null)
            Report("documentData", "Document style metadata is not represented in the bounded native import.");
        int regionOrder = 0;
        foreach (var block in page.Elements(Ns + "block"))
        {
            if (block.Attribute("blockType") is { Value: not "Text" } blockType)
            {
                Report("blockType", $"Non-text blocks (including {blockType.Value}) are not imported; retain the source XML.");
                continue;
            }
            var blockId = ExternalIds.Sanitize((string?)block.Attribute("id") ?? $"abbyy-block-{regionOrder}");
            var regionId = new EntityId(EntityKind.Region, blockId);
            var blockBounds = ReadBounds(block);
            if (blockBounds is null && new[] { "l", "t", "r", "b" }.Any(name => block.Attribute(name) is not null))
                Report("block-geometry", "Invalid or incomplete block bounds were omitted; coordinates were not synthesized.");
            if (block.Element(Ns + "region") is not null)
                Report("block-region", "Detailed block regions are not represented; only valid enclosing block bounds are imported.");
            regions.Add(new NativeRegion
            {
                Id = regionId,
                FrameRef = frameEntity,
                Polygon = blockBounds is { } bb ? Rectangle(bb) : [],
                LayoutSource = LayoutSource.Imported,
                ReadingOrderIndex = regionOrder++,
            });
            foreach (var line in block.Elements(Ns + "text").Elements(Ns + "par").Elements(Ns + "line"))
            {
                var lineId = ExternalIds.Sanitize((string?)line.Attribute("id") ?? $"abbyy-line-{lines.Count}");
                var lineBounds = ReadBounds(line);
                if (lineBounds is null)
                    Report("line-geometry", "Invalid or incomplete line bounds were omitted; coordinates were not synthesized.");
                var baseline = ReadInt(line, "baseline");
                if (line.Attribute("baseline") is not null && baseline is null)
                    Report("baseline", "An invalid declared baseline was omitted.");
                else if (baseline is null && lineBounds is { } fallbackBounds)
                {
                    baseline = fallbackBounds.Bottom;
                    Report("baseline-fallback", "A line without a declared baseline uses its bottom edge for compatibility.", PageXml.IssueSeverity.Note);
                }
                // Descendant traversal would also accept chars from word/character alternatives.
                var formatting = line.Elements(Ns + "formatting").ToArray();
                var characters = formatting.Elements(Ns + "charParams").ToArray();
                var text = string.Concat(characters.SelectMany(e => e.Nodes().OfType<XText>()).Select(n => n.Value));
                if (line.Descendants().Any(e => e.Name.LocalName is "charRecVariants" or "charRecVariant" or "wordRecVariants" or "wordRecVariant"))
                    Report("recognition-variants", "Recognition alternatives are not selected or appended to diplomatic text; retain the source XML.");
                if (characters.Any(e => e.Attributes().Any()))
                    Report("character-metadata", "Character boxes, confidence, word boundaries and other character attributes are not represented in the line-level import.");
                if (formatting.Any(e => e.Attributes().Any(a => !string.IsNullOrEmpty(a.Value))))
                    Report("formatting-metadata", "Source language and formatting attributes are not represented in the bounded import.");
                if (formatting.Length == 0 && !line.HasElements)
                    text = line.Value.Trim(); // Legacy plain-line compatibility, never variant text.
                var lineEntity = new EntityId(EntityKind.Line, lineId);
                TextRevision? revision = null;
                if (!string.IsNullOrEmpty(text))
                {
                    revision = new TextRevision
                    {
                        Id = new EntityId(EntityKind.TranscriptionRevision, ExternalIds.Sanitize($"abbyy-{lineId}")),
                        ScopeId = lineEntity, Layer = TextLayer.L1Diplomatic,
                        Author = new AgentRef { Id = "abbyy-import", IsModel = false },
                        CreatedAt = DateTimeOffset.UtcNow, Text = text,
                    };
                    revisions.Add(revision);
                }
                lines.Add(new NativeLine
                {
                    Id = lineEntity, RegionId = regionId, FrameRef = frameEntity,
                    Polygon = lineBounds is { } lb ? Rectangle(lb) : [],
                    Baseline = lineBounds is { } valid && baseline is { } y
                        ? [new(valid.Left, y), new(valid.Right, y)] : [],
                    DiplomaticRevisionId = revision?.Id,
                });
            }
        }
        return new ImportResult(regions, lines, revisions, unsupported);
    }

    private static int? ReadInt(XElement element, string name) =>
        int.TryParse((string?)element.Attribute(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static (int Left, int Top, int Right, int Bottom)? ReadBounds(XElement element) =>
        ReadInt(element, "l") is { } left && ReadInt(element, "t") is { } top &&
        ReadInt(element, "r") is { } right && ReadInt(element, "b") is { } bottom && right >= left && bottom >= top
            ? (left, top, right, bottom) : null;

    private static PointD[] Rectangle((int Left, int Top, int Right, int Bottom) bounds) =>
        [new(bounds.Left, bounds.Top), new(bounds.Right, bounds.Top), new(bounds.Right, bounds.Bottom), new(bounds.Left, bounds.Bottom)];

    private static (int Left, int Top, int Right, int Bottom) Bounds(IReadOnlyList<PointD> points) =>
        points.Count == 0 ? (0, 0, 0, 0) : ((int)Math.Round(points.Min(p => p.X)), (int)Math.Round(points.Min(p => p.Y)),
            (int)Math.Round(points.Max(p => p.X)), (int)Math.Round(points.Max(p => p.Y)));
}
