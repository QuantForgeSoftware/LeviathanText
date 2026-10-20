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
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

    /// <summary>Pinned accepted PAGE schema versions (project-plan §3: pin schema versions).</summary>
    private static readonly string[] AcceptedNamespaces =
    [
        "http://schema.primaresearch.org/PAGE/gts/pagecontent/2013-07-15",
        "http://schema.primaresearch.org/PAGE/gts/pagecontent/2019-07-15",
    ];

    public sealed record PageImportResult(
        IReadOnlyList<NativeRegion> Regions,
        IReadOnlyList<NativeLine> Lines,
        IReadOnlyList<TextRevision> Revisions,
        IReadOnlyList<ImportIssue> Unsupported,
        string? ImageFilename);

    public enum IssueSeverity { Loss, Note }

    public sealed record ImportIssue(string Element, string Description, IssueSeverity Severity = IssueSeverity.Loss);

    public static PageImportResult Import(string xml, EntityId frameEntity, Checksum frameChecksum)
    {
        var doc = XDocument.Parse(xml, LoadOptions.None);
        var unsupported = new List<ImportIssue>();

        var pcGts = doc.Root ?? throw new InvalidDataException("Missing root element.");
        if (pcGts.Name.LocalName != "PcGts") throw new InvalidDataException($"Unexpected root {pcGts.Name}; expected PAGE PcGts.");
        if (!AcceptedNamespaces.Contains(pcGts.Name.Namespace.NamespaceName))
            throw new InvalidDataException($"PAGE namespace {pcGts.Name.Namespace} is not in the accepted, pinned set.");
        var Pc = pcGts.Name.Namespace;
        RejectExternalEntities(doc);

        var page = pcGts.Element(Pc + "Page") ?? throw new InvalidDataException("Missing Page element.");

        var regions = new List<NativeRegion>();
        var lines = new List<NativeLine>();
        var revisions = new List<TextRevision>();
        var frame = new CoordinateFrame { ImageRef = frameEntity, ImageChecksum = frameChecksum };
        int regionOrder = 0;

        // Consume Page/ReadingOrder (RegionRefIndexed) when present — real reading order, not a loss.
        var declaredOrder = page.Elements(Pc + "ReadingOrder")
            .SelectMany(ro => ro.Descendants(Pc + "RegionRefIndexed"))
            .GroupBy(rr => (string?)rr.Attribute("regionRef"))
            .ToDictionary(g => g.Key ?? "", g => (int?)g.First().Attribute("index"));

        foreach (var region in page.Elements(Pc + "TextRegion"))
        {
            var regionId = RequireId(region, "TextRegion");
            var customIndex = TryParseCustomIndex(region);
            var poly = ParsePoints(region.Element(Pc + "Coords")?.Attribute("points")?.Value);
            regions.Add(new NativeRegion
            {
                Id = new EntityId(EntityKind.Region, regionId),
                FrameRef = frameEntity,
                Polygon = poly,
                LayoutSource = LayoutSource.Imported,
                ReadingOrderIndex = declaredOrder.GetValueOrDefault(regionId) ?? customIndex ?? regionOrder,
            });
            regionOrder++;

            foreach (var issue in UnsupportedChildren(region, ["Coords", "TextLine", "TextEquiv"]))
                unsupported.Add(issue);
            if (region.Element(Pc + "TextEquiv") is not null)
                unsupported.Add(new ImportIssue("region-TextEquiv",
                    "region-level default text is recognized but not mapped: per-line diplomatic labels are authoritative",
                    IssueSeverity.Note));

            foreach (var line in region.Elements(Pc + "TextLine"))
            {
                var lineId = RequireId(line, "TextLine");
                var baseline = ParsePoints(line.Element(Pc + "Baseline")?.Attribute("points")?.Value);
                var linePolygon = ParsePoints(line.Element(Pc + "Coords")?.Attribute("points")?.Value);

                // Line text: the line's own TextEquiv when present; otherwise assembled
                // from the Word elements' TextEquiv values (Transkribus convention, e.g.
                // M02a). Assembly is recorded in the revision author reference.
                var lineText = line.Elements(Pc + "TextEquiv").FirstOrDefault(e => (string?)e.Attribute("conf") == null)?.Element(Pc + "Unicode")?.Value
                               ?? line.Elements(Pc + "TextEquiv").FirstOrDefault()?.Element(Pc + "Unicode")?.Value;
                string? assemblyNote = null;
                if (string.IsNullOrEmpty(lineText))
                {
                    var words = line.Elements(Pc + "Word")
                        .Select(w => w.Elements(Pc + "TextEquiv").FirstOrDefault()?.Element(Pc + "Unicode")?.Value)
                        .Where(v => !string.IsNullOrEmpty(v)).ToList();
                    if (words.Count > 0)
                    {
                        lineText = string.Join(" ", words);
                        assemblyNote = "assembled-from-words";
                    }
                }

                var lineEntityId = new EntityId(EntityKind.Line, lineId);
                TextRevision? revision = null;
                if (!string.IsNullOrEmpty(lineText))
                {
                    revision = new TextRevision
                    {
                        Id = new EntityId(EntityKind.TranscriptionRevision, $"pg-{lineId}"),
                        ScopeId = lineEntityId,
                        Layer = TextLayer.L1Diplomatic,
                        Author = new AgentRef
                        {
                            Id = assemblyNote is null ? "page-import" : $"page-import({assemblyNote})",
                            IsModel = false,
                        },
                        CreatedAt = DateTimeOffset.UtcNow,
                        Text = lineText,
                    };
                    revisions.Add(revision);
                }

                lines.Add(new NativeLine
                {
                    Id = lineEntityId,
                    RegionId = new EntityId(EntityKind.Region, regionId),
                    FrameRef = frameEntity,
                    Baseline = baseline,
                    Polygon = linePolygon,
                    DiplomaticRevisionId = revision?.Id,
                });

                foreach (var issue in UnsupportedChildren(line, ["Coords", "Baseline", "TextEquiv", "Word"]))
                    unsupported.Add(issue);
                foreach (var word in line.Elements(Pc + "Word"))
                    foreach (var issue in UnsupportedChildren(word, ["Coords", "TextEquiv"]))
                        unsupported.Add(issue);
            }
        }

        foreach (var element in page.Elements())
            if (element.Name != Pc + "TextRegion" && element.Name != Pc + "ReadingOrder")
                unsupported.Add(new ImportIssue(element.Name.LocalName, "unsupported PAGE element is preserved in the source attachment, not flattened"));

        return new PageImportResult(regions, lines, revisions, unsupported, (string?)page.Attribute("imageFilename"));
    }

    /// <summary>Export a surface's regions/lines (geometry + text via TextEquiv) as a bounded PAGE document (pinned 2019-07-15).</summary>
    public static string Export(NativeSurface surface, NativeCapture capture, IReadOnlyList<TextRevision> revisions)
    {
        var Pc = XNamespace.Get("http://schema.primaresearch.org/PAGE/gts/pagecontent/2019-07-15");
        var revisionById = revisions.ToDictionary(r => r.Id);

        IEnumerable<XElement> LinesOf(NativeRegion region) =>
            surface.Lines.Where(l => l.RegionId == region.Id).Select(line =>
            {
                XElement? textEquiv = null;
                if (line.DiplomaticRevisionId is { } rid && revisionById.TryGetValue(rid, out var revision))
                    textEquiv = new XElement(Pc + "TextEquiv", new XElement(Pc + "Unicode", revision.Text));

                return new XElement(Pc + "TextLine",
                    new XAttribute("id", line.Id.Suffix),
                    new XElement(Pc + "Coords", new XAttribute("points", Points(line.Polygon.Count > 0 ? line.Polygon : line.Baseline))),
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
            capture.WidthPx is { } width ? new XAttribute("imageWidth", width) : null!,
            capture.HeightPx is { } height ? new XAttribute("imageHeight", height) : null!,
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
        // DTD/external entities are disabled by refusing DOCTYPE outright (project-plan §7 security).
        if (doc.DocumentType is not null)
            throw new InvalidDataException("DOCTYPE/external entities are not accepted in PAGE imports.");
    }

    private static string RequireId(XElement element, string kind)
    {
        var id = (string?)element.Attribute("id");
        if (string.IsNullOrEmpty(id)) throw new InvalidDataException($"{kind} is missing an id.");
        return id;
    }

    /// <summary>PAGE `custom="readingOrder {index:N;}"` attribute (Transkribus convention).</summary>
    private static int? TryParseCustomIndex(XElement element)
    {
        var custom = (string?)element.Attribute("custom");
        if (custom is null) return null;
        var m = System.Text.RegularExpressions.Regex.Match(custom, @"readingOrder \{index:(\d+);?\}");
        return m.Success && int.TryParse(m.Groups[1].Value, out var index) ? index : null;
    }
}
