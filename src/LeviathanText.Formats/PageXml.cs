using System.Xml.Linq;
using LeviathanText.Contracts;

namespace LeviathanText.Formats;

/// <summary>
/// Bounded PAGE XML adapter (todo Phase 1: supported PAGE subset for annotation
/// interchange). Supported: Pc/Page, TextRegion (with Coords), TextLine (Coords +
/// Baseline), TextEquiv/Unicode text. Unsupported elements are never silently
/// flattened: they are collected into the import report (interchange-and-api.md §1.2).
/// </summary>
public static class PageXml
{
    private static readonly XNamespace Pc = "http://schema.primaresearch.org/PAGE/gts/pagecontent/2019-07-15";
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

    public sealed record PageImportResult(
        IReadOnlyList<NativeRegion> Regions,
        IReadOnlyList<NativeLine> Lines,
        IReadOnlyList<TextRevision> Revisions,
        IReadOnlyList<ImportIssue> Unsupported);

    public sealed record ImportIssue(string Element, string Description);

    public static PageImportResult Import(string xml, EntityId frameEntity, Checksum frameChecksum)
    {
        var doc = XDocument.Parse(xml, LoadOptions.None);
        var unsupported = new List<ImportIssue>();

        var pcGts = doc.Root ?? throw new InvalidDataException("Missing root element.");
        if (pcGts.Name != Pc + "PcGts") throw new InvalidDataException($"Unexpected root {pcGts.Name}; expected PAGE PcGts.");
        RejectExternalEntities(doc);

        var page = pcGts.Element(Pc + "Page") ?? throw new InvalidDataException("Missing Page element.");

        var regions = new List<NativeRegion>();
        var lines = new List<NativeLine>();
        var revisions = new List<TextRevision>();
        var frame = new CoordinateFrame { ImageRef = frameEntity, ImageChecksum = frameChecksum };
        int regionOrder = 0;

        foreach (var region in page.Elements(Pc + "TextRegion"))
        {
            var regionId = RequireId(region, "TextRegion");
            var readingOrder = (int?)region.Element(Pc + "ReadingOrder")?.Attribute("index");
            var poly = ParsePoints(region.Element(Pc + "Coords")?.Attribute("points")?.Value);
            regions.Add(new NativeRegion
            {
                Id = new EntityId(EntityKind.Region, regionId),
                FrameRef = frameEntity,
                Polygon = poly,
                LayoutSource = LayoutSource.Imported,
                ReadingOrderIndex = readingOrder ?? regionOrder,
            });
            regionOrder++;

            foreach (var issue in UnsupportedChildren(region, ["Coords", "TextLine"]))
                unsupported.Add(issue);

            foreach (var line in region.Elements(Pc + "TextLine"))
            {
                var lineId = RequireId(line, "TextLine");
                var baseline = ParsePoints(line.Element(Pc + "Baseline")?.Attribute("points")?.Value);
                var text = line.Elements(Pc + "TextEquiv").FirstOrDefault(e => (string?)e.Attribute("conf") == null)?.Element(Pc + "Unicode")?.Value
                           ?? line.Elements(Pc + "TextEquiv").FirstOrDefault()?.Element(Pc + "Unicode")?.Value;

                var lineEntityId = new EntityId(EntityKind.Line, lineId);
                TextRevision? revision = null;
                if (!string.IsNullOrEmpty(text))
                {
                    revision = new TextRevision
                    {
                        Id = new EntityId(EntityKind.TranscriptionRevision, $"pg-{lineId}"),
                        ScopeId = lineEntityId,
                        Layer = TextLayer.L1Diplomatic,
                        Author = new AgentRef { Id = "page-import", IsModel = false },
                        CreatedAt = DateTimeOffset.UtcNow,
                        Text = text,
                    };
                    revisions.Add(revision);
                }

                lines.Add(new NativeLine
                {
                    Id = lineEntityId,
                    RegionId = new EntityId(EntityKind.Region, regionId),
                    FrameRef = frameEntity,
                    Baseline = baseline,
                    DiplomaticRevisionId = revision?.Id,
                });

                foreach (var issue in UnsupportedChildren(line, ["Coords", "Baseline", "TextEquiv"]))
                    unsupported.Add(issue);
            }
        }

        foreach (var element in page.Elements())
            if (element.Name != Pc + "TextRegion")
                unsupported.Add(new ImportIssue(element.Name.LocalName, "unsupported PAGE element is preserved in the source attachment, not flattened"));

        return new PageImportResult(regions, lines, revisions, unsupported);
    }

    /// <summary>Export a surface's regions/lines (geometry + text via TextEquiv) as a bounded PAGE document.</summary>
    public static string Export(NativeSurface surface, NativeCapture capture, IReadOnlyList<TextRevision> revisions)
    {
        var revisionById = revisions.ToDictionary(r => r.Id);

        IEnumerable<XElement> LinesOf(NativeRegion region) =>
            surface.Lines.Where(l => l.RegionId == region.Id).Select(line =>
            {
                XElement? textEquiv = null;
                if (line.DiplomaticRevisionId is { } rid && revisionById.TryGetValue(rid, out var revision))
                    textEquiv = new XElement(Pc + "TextEquiv", new XElement(Pc + "Unicode", revision.Text));

                return new XElement(Pc + "TextLine",
                    new XAttribute("id", line.Id.Suffix),
                    new XElement(Pc + "Coords", new XAttribute("points", Points(line.Baseline))),
                    new XElement(Pc + "Baseline", new XAttribute("points", Points(line.Baseline))),
                    textEquiv);
            });

        var regions = surface.Regions.OrderBy(r => r.ReadingOrderIndex).Select(region =>
            new XElement(Pc + "TextRegion",
                new XAttribute("id", region.Id.Suffix),
                new XElement(Pc + "Coords", new XAttribute("points", Points(region.Polygon))),
                LinesOf(region)));

        var page = new XElement(Pc + "Page",
            new XAttribute("imageFilename", capture.ImageLocation),
            regions);
        var pcGts = new XElement(Pc + "PcGts",
            new XAttribute(XNamespace.Xmlns + "xsi", Xsi),
            page);

        return new XDocument(new XDeclaration("1.0", "UTF-8", null), pcGts).ToString();
    }

    private static string Points(IReadOnlyList<PointD> points) =>
        string.Join(" ", points.Select(p => $"{p.X:0.###},{p.Y:0.###}"));

    private static IReadOnlyList<PointD> ParsePoints(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        return value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split(','))
            .Where(parts => parts.Length == 2
                            && double.TryParse(parts[0], System.Globalization.CultureInfo.InvariantCulture, out _)
                            && double.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out _))
            .Select(parts => new PointD(
                double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
                double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture)))
            .ToList();
    }

    private static IEnumerable<ImportIssue> UnsupportedChildren(XElement element, HashSet<string> supported)
    {
        foreach (var child in element.Elements())
            if (!supported.Contains(child.Name.LocalName))
                yield return new ImportIssue(child.Name.LocalName, $"child of {element.Name.LocalName} is outside the supported PAGE subset");
    }

    private static void RejectExternalEntities(XDocument doc)
    {
        // DTD/external entities are disabled by refusing DOCTYPE outright (todo §7 security).
        if (doc.DocumentType is not null)
            throw new InvalidDataException("DOCTYPE/external entities are not accepted in PAGE imports.");
    }

    private static string RequireId(XElement element, string kind)
    {
        var id = (string?)element.Attribute("id");
        if (string.IsNullOrEmpty(id)) throw new InvalidDataException($"{kind} is missing an id.");
        return id;
    }
}
