using System.Xml.Linq;
using LeviathanText.Contracts;

namespace LeviathanText.Formats;

/// <summary>
/// Bounded PAGE XML adapter (todo Phase 1: supported PAGE subset for annotation
/// interchange). Supported: Pc/Page, TextRegion (with Coords and optional
/// readingDirection/primaryScript), TextLine (Coords + Baseline + the same optional
/// direction/script attributes), TextEquiv/Unicode text. Unsupported elements are never
/// silently flattened: they are collected into the import report (interchange-and-api.md §1.2).
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
        string? ImageFilename)
    {
        /// <summary>Declared page width; zero/missing is treated as unavailable.</summary>
        public int? WidthPx { get; init; }

        /// <summary>Declared page height; zero/missing is treated as unavailable.</summary>
        public int? HeightPx { get; init; }

        /// <summary>Explicit reading-order groups, including partial groups under an unordered parent.</summary>
        public IReadOnlyList<ReadingOrderGroup> ReadingOrders { get; init; } = [];
    }

    /// <summary>A source reading-order group, with region references in declared order.</summary>
    public sealed record ReadingOrderGroup(
        string Id,
        string? Caption,
        bool IsTotal,
        IReadOnlyList<string> RegionRefs);

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
        var pageMetadata = MetadataOf(page);
        int regionOrder = 0;

        // Consume Page/ReadingOrder (RegionRefIndexed) when present — real reading order, not a loss.
        var declaredOrder = page.Elements(Pc + "ReadingOrder")
            .SelectMany(ro => ro.Descendants(Pc + "RegionRefIndexed"))
            .GroupBy(rr => (string?)rr.Attribute("regionRef"))
            .ToDictionary(g => g.Key ?? "", g => (int?)g.First().Attribute("index"));
        var readingOrders = ParseReadingOrders(page, Pc);

        foreach (var region in page.Elements(Pc + "TextRegion"))
        {
            var regionId = ExternalIds.Sanitize(RequireId(region, "TextRegion"));
            var customIndex = TryParseCustomIndex(region);
            var regionMetadata = pageMetadata.Merge(MetadataOf(region));
            var poly = ParsePoints(region.Element(Pc + "Coords")?.Attribute("points")?.Value);
            regions.Add(new NativeRegion
            {
                Id = new EntityId(EntityKind.Region, regionId),
                FrameRef = frameEntity,
                Polygon = poly,
                LayoutSource = LayoutSource.Imported,
                ReadingOrderIndex = declaredOrder.GetValueOrDefault(regionId) ?? customIndex ?? regionOrder,
                Direction = ParseDirection((string?)region.Attribute("readingDirection")),
                Script = CleanScript((string?)region.Attribute("primaryScript")),
                Languages = regionMetadata.Languages,
                Tags = regionMetadata.Tags,
                Split = regionMetadata.Split,
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
                var lineId = ExternalIds.Sanitize(RequireId(line, "TextLine"));
                var lineMetadata = regionMetadata.Merge(MetadataOf(line));
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
                        Id = new EntityId(EntityKind.TranscriptionRevision, ExternalIds.Sanitize($"pg-{lineId}")),
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
                    Direction = ParseDirection((string?)line.Attribute("readingDirection")),
                    Script = CleanScript((string?)line.Attribute("primaryScript")),
                    Languages = lineMetadata.Languages,
                    Tags = lineMetadata.Tags,
                    Split = lineMetadata.Split,
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

        return new PageImportResult(regions, lines, revisions, unsupported, (string?)page.Attribute("imageFilename"))
        {
            WidthPx = PositiveInt((string?)page.Attribute("imageWidth")),
            HeightPx = PositiveInt((string?)page.Attribute("imageHeight")),
            ReadingOrders = readingOrders,
        };
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

                var lineAttributes = new List<XAttribute> { new("id", line.Id.Suffix) };
                if (DirectionAttribute("readingDirection", line.Direction) is { } lineDirection)
                    lineAttributes.Add(lineDirection);
                if (ScriptAttribute("primaryScript", line.Script) is { } lineScript)
                    lineAttributes.Add(lineScript);
                if (MetadataAttribute(line.Languages, line.Tags, line.Split) is { } lineMetadata)
                    lineAttributes.Add(lineMetadata);

                return new XElement(Pc + "TextLine", lineAttributes,
                    new XElement(Pc + "Coords", new XAttribute("points", Points(line.Polygon.Count > 0 ? line.Polygon : line.Baseline))),
                    new XElement(Pc + "Baseline", new XAttribute("points", Points(line.Baseline))),
                    textEquiv);
            });

        var orderedRegions = surface.Regions.OrderBy(r => r.ReadingOrderIndex).ToList();
        var readingOrder = new XElement(Pc + "ReadingOrder",
            orderedRegions.Select((region, index) => new XElement(Pc + "RegionRefIndexed",
                new XAttribute("index", index), new XAttribute("regionRef", region.Id.Suffix))));
        var regions = orderedRegions.Select(region =>
        {
            var regionAttributes = new List<XAttribute> { new("id", region.Id.Suffix) };
            if (DirectionAttribute("readingDirection", region.Direction) is { } regionDirection)
                regionAttributes.Add(regionDirection);
            if (ScriptAttribute("primaryScript", region.Script) is { } regionScript)
                regionAttributes.Add(regionScript);
            if (MetadataAttribute(region.Languages, region.Tags, region.Split) is { } regionMetadata)
                regionAttributes.Add(regionMetadata);

            return new XElement(Pc + "TextRegion", regionAttributes,
                new XElement(Pc + "Coords", new XAttribute("points", Points(region.Polygon))),
                LinesOf(region));
        });

        var page = new XElement(Pc + "Page",
            new XAttribute("imageFilename", capture.ImageLocation),
            capture.WidthPx is { } width ? new XAttribute("imageWidth", width) : null!,
            capture.HeightPx is { } height ? new XAttribute("imageHeight", height) : null!,
            readingOrder,
            regions);
        var pcGts = new XElement(Pc + "PcGts",
            new XAttribute(XNamespace.Xmlns + "xsi", Xsi),
            page);

        return new XDocument(new XDeclaration("1.0", "UTF-8", null), pcGts).ToString();
    }

    private sealed record Metadata(IReadOnlyList<string> Languages, IReadOnlyDictionary<string, IReadOnlyList<string>> Tags, string? Split)
    {
        public Metadata Merge(Metadata other) => new(
            Languages.Concat(other.Languages).Distinct(StringComparer.Ordinal).ToArray(),
            Tags.Concat(other.Tags).GroupBy(p => p.Key)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.SelectMany(p => p.Value).Distinct(StringComparer.Ordinal).ToArray()),
            other.Split ?? Split);
    }

    private static Metadata MetadataOf(XElement element)
    {
        var custom = DecodeUnicodeEscapes((string?)element.Attribute("custom") ?? "");
        var languages = System.Text.RegularExpressions.Regex.Matches(custom, @"language \{type:\s*([^;]+);\}")
            .Select(m => m.Groups[1].Value.Trim()).ToList();
        var primary = (string?)element.Attribute("primaryLanguage");
        if (!string.IsNullOrWhiteSpace(primary)) languages.AddRange(primary.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var split = System.Text.RegularExpressions.Regex.Match(custom, @"split \{type:\s*([^;]+);\}")
            .Groups[1].Value.Trim();
        var tags = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(custom, @"structure \{type:\s*([^;]+);\}"))
        {
            var value = match.Groups[1].Value.Trim();
            if (value.Length > 0)
            {
                if (!tags.TryGetValue("type", out var list)) tags["type"] = list = [];
                list.Add(value);
            }
        }
        return new Metadata(languages.Distinct(StringComparer.Ordinal).ToArray(),
            tags.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value.ToArray()), string.IsNullOrWhiteSpace(split) ? null : split);
    }

    private static string DecodeUnicodeEscapes(string value) =>
        System.Text.RegularExpressions.Regex.Replace(value, @"\\u([0-9a-fA-F]{4})",
            m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());

    private static XAttribute? MetadataAttribute(IReadOnlyList<string> languages,
        IReadOnlyDictionary<string, IReadOnlyList<string>> tags, string? split)
    {
        var parts = new List<string>();
        foreach (var language in languages) parts.Add($"language {{type: {language};}}");
        if (!string.IsNullOrWhiteSpace(split)) parts.Add($"split {{type: {split};}}");
        foreach (var (key, values) in tags)
            if (key == "type")
                foreach (var value in values) parts.Add($"structure {{type: {value};}}");
        return parts.Count == 0 ? null : new XAttribute("custom", string.Join(' ', parts));
    }

    private static string Points(IReadOnlyList<PointD> points) =>
        string.Join(" ", points.Select(p => $"{p.X:0.###},{p.Y:0.###}"));

    private static TextDirection? ParseDirection(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "left-to-right" or "ltr" => TextDirection.Ltr,
        "right-to-left" or "rtl" => TextDirection.Rtl,
        "top-to-bottom" or "ttb" => TextDirection.VerticalTopToBottom,
        "bottom-to-top" or "btt" => TextDirection.VerticalBottomToTop,
        _ => null,
    };

    private static string PageDirection(TextDirection direction) => direction switch
    {
        TextDirection.Ltr => "left-to-right",
        TextDirection.Rtl => "right-to-left",
        TextDirection.VerticalTopToBottom => "top-to-bottom",
        TextDirection.VerticalBottomToTop => "bottom-to-top",
        _ => throw new ArgumentOutOfRangeException(nameof(direction)),
    };

    private static XAttribute? DirectionAttribute(string name, TextDirection? direction) =>
        direction is { } value ? new XAttribute(name, PageDirection(value)) : null;

    private static XAttribute? ScriptAttribute(string name, string? script) =>
        string.IsNullOrWhiteSpace(script) ? null : new XAttribute(name, script.Trim());

    private static string? CleanScript(string? script) =>
        string.IsNullOrWhiteSpace(script) ? null : script.Trim();

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
    private static IReadOnlyList<ReadingOrderGroup> ParseReadingOrders(XElement page, XNamespace pc)
    {
        var groups = new List<ReadingOrderGroup>();
        foreach (var readingOrder in page.Elements(pc + "ReadingOrder"))
        {
            foreach (var child in readingOrder.Elements())
            {
                if (child.Name == pc + "OrderedGroup")
                    AddGroup(child, isTotal: true);
                else if (child.Name == pc + "UnorderedGroup")
                    foreach (var ordered in child.Elements(pc + "OrderedGroup"))
                        AddGroup(ordered, isTotal: false);
            }
        }
        return groups;

        void AddGroup(XElement group, bool isTotal)
        {
            var refs = group.Descendants(pc + "RegionRefIndexed")
                .Select((reference, fallback) => (Reference: reference, Fallback: fallback))
                .OrderBy(pair => (int?)pair.Reference.Attribute("index") ?? pair.Fallback)
                .Select(pair => ExternalIds.Sanitize((string?)pair.Reference.Attribute("regionRef") ?? ""))
                .Where(id => id.Length > 0)
                .ToArray();
            groups.Add(new ReadingOrderGroup(
                ExternalIds.Sanitize((string?)group.Attribute("id") ?? $"ro{groups.Count}"),
                (string?)group.Attribute("caption"),
                isTotal,
                refs));
        }
    }

    private static int? PositiveInt(string? value)
    {
        if (value is null) return null;
        if (!int.TryParse(value, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed) || parsed < 0)
            throw new InvalidDataException($"Declared page dimension '{value}' is not a non-negative integer.");
        return parsed > 0 ? parsed : null;
    }

    private static int? TryParseCustomIndex(XElement element)
    {
        var custom = (string?)element.Attribute("custom");
        if (custom is null) return null;
        var m = System.Text.RegularExpressions.Regex.Match(custom, @"readingOrder \{index:(\d+);?\}");
        return m.Success && int.TryParse(m.Groups[1].Value, out var index) ? index : null;
    }
}
