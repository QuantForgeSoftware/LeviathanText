using System.Xml.Linq;
using LeviathanText.Contracts;

namespace LeviathanText.Formats;

/// <summary>
/// Bounded ALTO XML adapter (todo Phase 4: ALTO among later format adapters; also a
/// migration path for legacy OCR exports).
///
/// Supported subset: Layout/Page, PrintSpace/TextBlock, TextLine, Shape/Polygon and
/// String@CONTENT. We do not synthesize word segmentation: a line without word boxes is
/// exported as one String carrying the line text (round-trips exactly), and imports
/// concatenate String contents. Unsupported elements are reported, never flattened
/// silently. External entities/DTDs are refused.
/// </summary>
public static class AltoXml
{
    private static readonly XNamespace Alto = "http://www.loc.gov/standards/alto/ns-v4#";

    public sealed record AltoImportResult(
        IReadOnlyList<NativeRegion> Regions,
        IReadOnlyList<NativeLine> Lines,
        IReadOnlyList<TextRevision> Revisions,
        IReadOnlyList<PageXml.ImportIssue> Unsupported)
    {
        /// <summary>Declared page width; zero/missing is treated as unavailable.</summary>
        public int? WidthPx { get; init; }

        /// <summary>Declared page height; zero/missing is treated as unavailable.</summary>
        public int? HeightPx { get; init; }
    }

    public static string Export(NativeSurface surface, NativeCapture capture, IReadOnlyList<TextRevision> revisions)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(capture);

        var revisionById = revisions.ToDictionary(r => r.Id);

        XElement LineElement(NativeLine line)
        {
            XElement? text = null;
            if (line.DiplomaticRevisionId is { } rid && revisionById.TryGetValue(rid, out var revision))
            {
                var first = line.Baseline.Count > 0 ? line.Baseline[0] : new PointD(0, 0);
                text = new XElement(Alto + "String",
                    new XAttribute("ID", line.Id.Suffix + "-s1"),
                    new XAttribute("CONTENT", revision.Text),
                    new XAttribute("HPOS", first.X),
                    new XAttribute("VPOS", first.Y));
            }

            var polygon = new XElement(Alto + "Polygon",
                new XAttribute("POINTS", string.Join(" ", line.Polygon.Select(p => $"{p.X:0.#},{p.Y:0.#}"))));

            var attributes = new List<XAttribute> { new("ID", line.Id.Suffix) };
            if (line.Languages.Count > 0) attributes.Add(new XAttribute("LANG", string.Join(' ', line.Languages)));
            if (DirectionAttribute(line.Direction) is { } lineDirection) attributes.Add(lineDirection);
            if (line.Baseline.Count >= 2)
                attributes.Add(new XAttribute("BASELINE", string.Join(' ', line.Baseline.Select(p => $"{p.X:0.#} {p.Y:0.#}"))));
            return new XElement(Alto + "TextLine", attributes,
                new XElement(Alto + "Shape", polygon),
                text);
        }

        var blocks = surface.Regions.OrderBy(r => r.ReadingOrderIndex).Select(region =>
        {
            var attributes = new List<XAttribute> { new("ID", region.Id.Suffix) };
            if (region.Languages.Count > 0) attributes.Add(new XAttribute("LANG", string.Join(' ', region.Languages)));
            if (DirectionAttribute(region.Direction) is { } regionDirection) attributes.Add(regionDirection);
            return new XElement(Alto + "TextBlock", attributes,
                surface.Lines.Where(l => l.RegionId == region.Id).Select(LineElement));
        });

        var page = new XElement(Alto + "Page",
            new XAttribute("ID", surface.Id.Suffix),
            new XAttribute("WIDTH", capture.WidthPx ?? 0),
            new XAttribute("HEIGHT", capture.HeightPx ?? 0),
            new XElement(Alto + "PrintSpace", blocks));

        var alto = new XElement(Alto + "alto",
            new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
            new XElement(Alto + "Description",
                new XElement(Alto + "MeasurementUnit", "pixel")),
            new XElement(Alto + "Layout", page));

        return new XDocument(new XDeclaration("1.0", "UTF-8", null), alto).ToString();
    }

    public static AltoImportResult Import(string xml, EntityId frameEntity, Checksum frameChecksum)
    {
        var doc = XDocument.Parse(xml, LoadOptions.None);
        if (doc.DocumentType is not null)
            throw new InvalidDataException("DOCTYPE/external entities are not accepted in ALTO imports.");

        var root = doc.Root ?? throw new InvalidDataException("Missing ALTO root.");
        if (root.Name.LocalName != "alto")
            throw new InvalidDataException($"Unexpected root {root.Name.LocalName}; expected ALTO.");
        if (root.Name.Namespace.NamespaceName != Alto.NamespaceName)
            throw new InvalidDataException($"ALTO namespace {root.Name.Namespace} is not the pinned v4 namespace.");

        var unsupported = new List<PageXml.ImportIssue>();
        var regions = new List<NativeRegion>();
        var lines = new List<NativeLine>();
        var revisions = new List<TextRevision>();
        var tagMap = root.Descendants(Alto + "OtherTag")
            .Where(e => (string?)e.Attribute("ID") is not null)
            .ToDictionary(e => (string)e.Attribute("ID")!, e => (
                Type: (string?)e.Attribute("TYPE"),
                Label: (string?)e.Attribute("LABEL")));
        var page = root.Descendants(Alto + "Page").FirstOrDefault();
        var pageMetadata = MetadataOf(page ?? root, tagMap);
        int regionOrder = 0;

        foreach (var block in root.Descendants(Alto + "TextBlock"))
        {
            var blockId = ExternalIds.Sanitize((string?)block.Attribute("ID") ?? $"altoregion{regionOrder}");
            var blockMetadata = pageMetadata.Merge(MetadataOf(block, tagMap));
            regions.Add(new NativeRegion
            {
                Id = new EntityId(EntityKind.Region, blockId),
                FrameRef = frameEntity,
                LayoutSource = LayoutSource.Imported,
                ReadingOrderIndex = regionOrder,
                Languages = blockMetadata.Languages,
                Tags = blockMetadata.Tags,
                Split = blockMetadata.Split,
                Direction = blockMetadata.Direction,
            });
            regionOrder++;

            foreach (var line in block.Elements(Alto + "TextLine"))
            {
                var lineId = ExternalIds.Sanitize((string?)line.Attribute("ID") ?? $"altoline{lines.Count}");
                var lineMetadata = blockMetadata.Merge(MetadataOf(line, tagMap));
                var polygon = ParsePolygon((string?)line.Element(Alto + "Shape")?.Element(Alto + "Polygon")?.Attribute("POINTS"));
                var text = string.Concat(line.Elements(Alto + "String")
                    .Select(s => (string?)s.Attribute("CONTENT") ?? ""));

                var lineEntityId = new EntityId(EntityKind.Line, lineId);
                TextRevision? revision = null;
                if (!string.IsNullOrEmpty(text))
                {
                    revision = new TextRevision
                    {
                        Id = new EntityId(EntityKind.TranscriptionRevision, ExternalIds.Sanitize($"alto-{lineId}")),
                        ScopeId = lineEntityId,
                        Layer = TextLayer.L1Diplomatic,
                        Author = new AgentRef { Id = "alto-import", IsModel = false },
                        CreatedAt = DateTimeOffset.UtcNow,
                        Text = text,
                    };
                    revisions.Add(revision);
                }

                lines.Add(new NativeLine
                {
                    Id = lineEntityId,
                    RegionId = new EntityId(EntityKind.Region, blockId),
                    FrameRef = frameEntity,
                    Polygon = polygon,
                    Baseline = ParseBaseline((string?)line.Attribute("BASELINE")) is { Count: >= 2 } baseline
                        ? baseline
                        : polygon.Count >= 2 ? [polygon[^1], polygon[^2]] : [],
                    DiplomaticRevisionId = revision?.Id,
                    Languages = lineMetadata.Languages,
                    Tags = lineMetadata.Tags,
                    Split = lineMetadata.Split,
                    Direction = lineMetadata.Direction,
                });

                foreach (var child in line.Elements())
                    if (child.Name != Alto + "Shape" && child.Name != Alto + "String")
                        unsupported.Add(new PageXml.ImportIssue(child.Name.LocalName,
                            "ALTO element outside the bounded subset (word/hyphenation/attributes not mapped)"));
            }
        }

        return new AltoImportResult(regions, lines, revisions, unsupported)
        {
            WidthPx = PositiveInt((string?)page?.Attribute("WIDTH")),
            HeightPx = PositiveInt((string?)page?.Attribute("HEIGHT")),
        };
    }

    private sealed record Metadata(IReadOnlyList<string> Languages,
        IReadOnlyDictionary<string, IReadOnlyList<string>> Tags, string? Split,
        TextDirection? Direction)
    {
        public Metadata Merge(Metadata other) => new(
            Languages.Concat(other.Languages).Distinct(StringComparer.Ordinal).ToArray(),
            Tags.Concat(other.Tags).GroupBy(p => p.Key)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.SelectMany(p => p.Value).Distinct(StringComparer.Ordinal).ToArray()),
            other.Split ?? Split,
            other.Direction ?? Direction);
    }

    private static Metadata MetadataOf(XElement element,
        IReadOnlyDictionary<string, (string? Type, string? Label)> tagMap)
    {
        var languages = ((string?)element.Attribute("LANG") ?? "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var tags = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        string? split = null;
        foreach (var id in ((string?)element.Attribute("TAGREFS") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!tagMap.TryGetValue(id, out var tag)) continue;
            if (tag.Type == "language") { if (!string.IsNullOrWhiteSpace(tag.Label)) languages.Add(tag.Label); continue; }
            if (tag.Type == "split") { split = tag.Label; continue; }
            var key = tag.Type ?? "tag";
            if (!string.IsNullOrWhiteSpace(tag.Label))
            {
                if (!tags.TryGetValue(key, out var list)) tags[key] = list = [];
                list.Add(tag.Label);
            }
        }
        return new Metadata(languages.Distinct(StringComparer.Ordinal).ToArray(),
            tags.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value.ToArray()), split,
            ParseDirection((string?)element.Attribute("BASEDIRECTION")));
    }

    private static TextDirection? ParseDirection(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "ltr" => TextDirection.Ltr,
        "rtl" => TextDirection.Rtl,
        "ttb" => TextDirection.VerticalTopToBottom,
        "btt" => TextDirection.VerticalBottomToTop,
        _ => null,
    };

    private static XAttribute? DirectionAttribute(TextDirection? direction) => direction switch
    {
        TextDirection.Ltr => new XAttribute("BASEDIRECTION", "ltr"),
        TextDirection.Rtl => new XAttribute("BASEDIRECTION", "rtl"),
        TextDirection.VerticalTopToBottom => new XAttribute("BASEDIRECTION", "ttb"),
        TextDirection.VerticalBottomToTop => new XAttribute("BASEDIRECTION", "btt"),
        _ => null,
    };

    private static int? PositiveInt(string? value)
    {
        if (value is null) return null;
        if (!int.TryParse(value, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed) || parsed < 0)
            throw new InvalidDataException($"Declared page dimension '{value}' is not a non-negative integer.");
        return parsed > 0 ? parsed : null;
    }

    private static List<PointD> ParseBaseline(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        var numbers = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<PointD>();
        for (int i = 0; i + 1 < numbers.Length; i += 2)
            if (double.TryParse(numbers[i], System.Globalization.CultureInfo.InvariantCulture, out var x)
                && double.TryParse(numbers[i + 1], System.Globalization.CultureInfo.InvariantCulture, out var y))
                result.Add(new PointD(x, y));
        return result;
    }

    private static List<PointD> ParsePolygon(string? points)
    {
        if (string.IsNullOrWhiteSpace(points)) return [];
        var result = new List<PointD>();
        foreach (var pair in points.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split(',');
            if (parts.Length != 2) continue;
            if (double.TryParse(parts[0], System.Globalization.CultureInfo.InvariantCulture, out var x)
                && double.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out var y))
                result.Add(new PointD(x, y));
        }
        return result;
    }
}
