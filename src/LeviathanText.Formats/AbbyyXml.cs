using System.Xml.Linq;
using LeviathanText.Contracts;

namespace LeviathanText.Formats;

/// <summary>
/// Bounded ABBYY FineReader XML adapter (FineReader 10 namespace). Supported subset: one
/// page, text blocks, text lines with l/t/r/b, baseline and plain character content.
/// Character boxes are emitted as one charParams per line (no word segmentation synthesis);
/// imports concatenate charParams/line text. Unsupported structural data is not interpreted.
/// </summary>
public static class AbbyyXml
{
    private static readonly XNamespace Ns = "http://www.abbyy.com/FineReader_xml/FineReader10-schema-v1.xml";

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
        var doc = XDocument.Parse(xml, LoadOptions.None);
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
        int regionOrder = 0;
        foreach (var block in page.Elements(Ns + "block"))
        {
            var blockId = ExternalIds.Sanitize((string?)block.Attribute("id") ?? $"abbyy-block-{regionOrder}");
            var regionId = new EntityId(EntityKind.Region, blockId);
            regions.Add(new NativeRegion
            {
                Id = regionId,
                FrameRef = frameEntity,
                LayoutSource = LayoutSource.Imported,
                ReadingOrderIndex = regionOrder++,
            });
            foreach (var line in block.Descendants(Ns + "line"))
            {
                var lineId = ExternalIds.Sanitize((string?)line.Attribute("id") ?? $"abbyy-line-{lines.Count}");
                int left = IntAttr(line, "l"), top = IntAttr(line, "t"), right = IntAttr(line, "r"), bottom = IntAttr(line, "b");
                var text = string.Concat(line.Descendants(Ns + "charParams").Select(e => e.Value));
                if (string.IsNullOrEmpty(text)) text = line.Value.Trim();
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
                    Polygon = [new(left, top), new(right, top), new(right, bottom), new(left, bottom)],
                    Baseline = [new(left, bottom), new(right, bottom)],
                    DiplomaticRevisionId = revision?.Id,
                });
            }
        }
        return new ImportResult(regions, lines, revisions, unsupported);
    }

    private static int IntAttr(XElement element, string name) =>
        int.TryParse((string?)element.Attribute(name), out var value) ? value : 0;

    private static (int Left, int Top, int Right, int Bottom) Bounds(IReadOnlyList<PointD> points) =>
        points.Count == 0 ? (0, 0, 0, 0) : ((int)Math.Round(points.Min(p => p.X)), (int)Math.Round(points.Min(p => p.Y)),
            (int)Math.Round(points.Max(p => p.X)), (int)Math.Round(points.Max(p => p.Y)));
}
