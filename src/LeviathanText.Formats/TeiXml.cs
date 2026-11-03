using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using LeviathanText.Contracts;

namespace LeviathanText.Formats;

/// <summary>
/// Bounded TEI P5 / EpiDoc profile (project-plan §3 "scholarly interchange").
///
/// The profile is deliberately narrow but end-to-end: edition lines split at
/// &lt;lb/&gt;, facsimile/image links, stable line ids and numbers, L1 visible text with an
/// optional linked L2 editorial reading, abbreviations, unclear/illegible spans, gaps,
/// alternative readings, supplied text, additions/deletions, hand shifts and bounded
/// responsibility metadata. Unsupported constructs are reported by name and count, and the
/// complete source XML is returned so the caller can preserve it as an attachment.
///
/// Supplied text and expansion text never enter L1: they are carried by an L2 revision and
/// the corresponding annotation is anchored there. This keeps the diplomatic layer a
/// representation of what is visible.
/// </summary>
public static class TeiXml
{
    private static readonly XNamespace Tei = "http://www.tei-c.org/ns/1.0";
    private static readonly XNamespace Xml = XNamespace.Xml;

    /// <summary>Upper bound on the size of an original-markup attachment retained by callers.</summary>
    public const int MaxAttachmentBytes = 2 * 1024 * 1024;

    public sealed record TeiGraphic(string Url, string? N = null, int? Width = null, int? Height = null);

    public sealed record TeiIdno(string? Type, string Value);

    public sealed record TeiResponsibility(string? Role, string? Name, string? When);

    public sealed record TeiPageBreak(string? Number, string? Type);

    public sealed record TeiLineInfo(
        EntityId LineId,
        int? Number,
        string? XmlId,
        TextDirection? Direction,
        string? Script,
        IReadOnlyList<string> Languages,
        IReadOnlyList<string> Tags,
        int PageIndex = 0,
        string? PageNumber = null);

    public sealed record TeiDocumentInfo(
        string? Title,
        string? SurfaceId,
        IReadOnlyList<TeiGraphic> Graphics,
        IReadOnlyList<TeiIdno> Idnos,
        string? Licence,
        IReadOnlyList<TeiResponsibility> Responsibilities,
        IReadOnlyList<string> Languages,
        IReadOnlyList<TeiLineInfo> Lines,
        IReadOnlyList<TeiPageBreak>? PageBreaks = null);

    public sealed record TeiImportResult(
        IReadOnlyList<TextRevision> Revisions,
        IReadOnlyList<Annotation> Annotations,
        IReadOnlyList<PageXml.ImportIssue> Unsupported,
        IReadOnlyList<TeiLineOrientation>? Orientations = null,
        TeiDocumentInfo? Document = null,
        string? OriginalMarkup = null);

    public sealed record TeiLineOrientation(EntityId LineId, TextDirection? Direction, string? Script);

    /// <summary>Context that lets the exporter carry the non-text parts of a surface.</summary>
    public sealed record TeiExportContext(
        string? SurfaceId = null,
        IReadOnlyList<TeiGraphic>? Graphics = null,
        IReadOnlyList<TeiIdno>? Idnos = null,
        string? Licence = null,
        IReadOnlyList<TeiResponsibility>? Responsibilities = null,
        string? SourceHash = null,
        int GeometryCount = 0,
        IReadOnlyDictionary<EntityId, TeiLineOrientation>? Orientations = null,
        string? Folio = null,
        int PageBreakCount = 0);

    public sealed record TeiExportLine(
        EntityId LineId,
        TextRevision? L1,
        TextRevision? L2,
        IReadOnlyList<Annotation> Annotations,
        TeiLineInfo? Info = null);

    public sealed record TeiExportResult(string Xml, LossReport Report);

    // ---- Export ----

    /// <summary>
    /// Back-compatible single-layer export. New callers should use
    /// <see cref="ExportDocument"/> so both layers, line numbers and the surface image link
    /// are carried and a loss report is returned.
    /// </summary>
    public static string Export(
        IEnumerable<(EntityId LineId, TextRevision? Revision, IReadOnlyList<Annotation> Annotations)> lines,
        string title,
        IReadOnlyDictionary<EntityId, TeiLineOrientation>? orientations = null) =>
        ExportDocument(
            lines.Select(l => new TeiExportLine(
                l.LineId,
                l.Revision,
                null,
                l.Annotations,
                new TeiLineInfo(l.LineId, null, l.LineId.Suffix,
                    orientations?.GetValueOrDefault(l.LineId)?.Direction,
                    orientations?.GetValueOrDefault(l.LineId)?.Script,
                    [], []))).ToList(),
            title,
            new TeiExportContext(Orientations: orientations)).Xml;

    public static TeiExportResult ExportDocument(
        IReadOnlyList<TeiExportLine> lines,
        string title,
        TeiExportContext? context = null)
    {
        context ??= new TeiExportContext();
        var losses = new List<LossEntry>();

        var header = BuildHeader(title, context);
        var facsimile = BuildFacsimile(context);
        var editionLines = new List<XElement>();
        foreach (var line in lines)
        {
            var annotationLosses = new List<LossEntry>();
            editionLines.Add(ExportLine(line, context, annotationLosses));
            losses.AddRange(annotationLosses);
        }

        var edition = new XElement(Tei + "div",
            new XAttribute("type", "edition"),
            editionLines);
        var body = new XElement(Tei + "body");
        if (!string.IsNullOrWhiteSpace(context.Folio))
            body.Add(new XElement(Tei + "pb", new XAttribute("n", context.Folio!),
                new XAttribute("type", "folio")));
        body.Add(edition);
        var text = new XElement(Tei + "text", body);
        var tei = new XElement(Tei + "TEI", header);
        if (facsimile is not null) tei.Add(facsimile);
        tei.Add(text);

        if (context.GeometryCount > 0)
            losses.Add(new LossEntry("geometry", context.GeometryCount,
                "polygons and baselines are not representable in the bounded TEI profile"));
        if (context.Graphics is not { Count: > 0 })
            losses.Add(new LossEntry("image-link", 1,
                "no image link was supplied; a TEI export cannot carry the capture reference"));
        if (context.PageBreakCount > 1)
            losses.Add(new LossEntry("page-breaks", context.PageBreakCount - 1,
                "only the first folio is exported; import each page separately to retain all page links"));

        var grouped = losses
            .GroupBy(l => l.Category)
            .Select(g => new LossEntry(g.Key, g.Sum(x => x.Count), string.Join("; ", g.Select(x => x.Description).Distinct())))
            .ToList();
        var report = new LossReport
        {
            Format = "tei-p5-epidoc-bounded",
            ContractVersion = ContractVersion.Current,
            Entries = grouped,
        };

        var document = new XDocument(new XDeclaration("1.0", "UTF-8", null), tei);
        if (!report.IsLossless)
            document.AddFirst(new XComment($"loss-report: {report}"));
        // Pretty-printing adjacent inline elements inserts whitespace into witness text.
        return new TeiExportResult(document.ToString(SaveOptions.DisableFormatting), report);
    }

    private static XElement BuildHeader(string title, TeiExportContext context)
    {
        var titleStmt = new XElement(Tei + "titleStmt", new XElement(Tei + "title", title));
        if (context.Responsibilities is { Count: > 0 })
            titleStmt.Add(new XElement(Tei + "respStmt",
                context.Responsibilities.Select(r => new XElement(Tei + "resp",
                    new XAttribute("when", r.When ?? ""),
                    new XText($"{r.Role ?? "responsibility"}: {r.Name ?? "unattributed"}")))));

        var publicationStmt = new XElement(Tei + "publicationStmt",
            new XElement(Tei + "p", "LeviathanText native export; bounded TEI/EpiDoc profile"));
        if (!string.IsNullOrWhiteSpace(context.Licence))
            publicationStmt.Add(new XElement(Tei + "availability",
                new XElement(Tei + "licence", context.Licence)));

        var fileDesc = new XElement(Tei + "fileDesc", titleStmt, publicationStmt);
        var sourceDesc = new XElement(Tei + "sourceDesc",
            new XElement(Tei + "p", context.SourceHash is { Length: > 0 }
                ? $"Source project export; content hash sha256:{context.SourceHash}"
                : "Source project export"));
        if (context.Idnos is { Count: > 0 })
            sourceDesc.Add(context.Idnos.Select(i => new XElement(Tei + "idno",
                string.IsNullOrWhiteSpace(i.Type) ? null : new XAttribute("type", i.Type!),
                i.Value)));
        fileDesc.Add(sourceDesc);

        var header = new XElement(Tei + "teiHeader", fileDesc);
        if (context.Responsibilities is { Count: > 0 })
            header.Add(new XElement(Tei + "revisionDesc",
                new XElement(Tei + "listChange",
                    context.Responsibilities.Select(r => new XElement(Tei + "change",
                        new XAttribute("when", r.When ?? ""),
                        r.When is null ? null : new XAttribute("who", r.Name ?? ""),
                        $"{r.Role ?? "responsibility"}: {r.Name ?? "unattributed"}")))));
        return header;
    }

    private static XElement? BuildFacsimile(TeiExportContext context)
    {
        if (context.Graphics is not { Count: > 0 }) return null;
        var surface = new XElement(Tei + "surface");
        if (!string.IsNullOrWhiteSpace(context.SurfaceId))
            surface.Add(new XAttribute(Xml + "id", context.SurfaceId!));
        foreach (var graphic in context.Graphics)
        {
            var element = new XElement(Tei + "graphic", new XAttribute("url", graphic.Url));
            if (!string.IsNullOrWhiteSpace(graphic.N)) element.Add(new XAttribute("n", graphic.N!));
            if (graphic.Width is { } width) element.Add(new XAttribute("width", width));
            if (graphic.Height is { } height) element.Add(new XAttribute("height", height));
            surface.Add(element);
        }
        return new XElement(Tei + "facsimile", surface);
    }

    private static XElement ExportLine(TeiExportLine line, TeiExportContext context, List<LossEntry> losses)
    {
        var info = line.Info;
        var ab = new XElement(Tei + "ab");
        if (!string.IsNullOrWhiteSpace(info?.XmlId ?? line.LineId.Suffix))
            ab.Add(new XAttribute(Xml + "id", info?.XmlId ?? line.LineId.Suffix));
        var number = info?.Number?.ToString(System.Globalization.CultureInfo.InvariantCulture)
            ?? TagValue(info, "n");
        // Do not invent a witness line number from the native id; only source-declared
        // numbers/tags round-trip.
        if (number is not null)
            ab.Add(new XAttribute("n", number));
        if (TagValue(info, "part") is { } part) ab.Add(new XAttribute("part", part));
        AddOrientation(ab, info);
        ab.Add(new XElement(Tei + "lb"));

        var baseRevision = line.L2 ?? line.L1;
        if (baseRevision is null)
        {
            ab.Add(new XComment("not-yet-transcribed"));
            return ab;
        }

        // A surface exports all annotations; each line only renders annotations on its own revisions.
        var lineAnnotations = line.Annotations
            .Where(a => a.AnchoredRevisionId == line.L1?.Id || a.AnchoredRevisionId == line.L2?.Id)
            .ToList();
        // Ranges already carried by the rendered layer are authoritative. A cross-layer
        // mapping must not land on top of one of them: repeated witness text (for example a
        // deleted letter followed by an unclear letter) can otherwise be attached to the
        // wrong occurrence and duplicate text on re-import.
        var occupiedTargetRanges = lineAnnotations
            .Where(a => a.Kind != AnnotationKind.NotYetTranscribed
                        && a.AnchoredRevisionId == baseRevision.Id)
            .Select(a => a.Range)
            .ToList();
        var renderable = new List<RenderAnnotation>();
        foreach (var annotation in lineAnnotations)
        {
            if (annotation.Kind == AnnotationKind.NotYetTranscribed) continue;
            var mapped = MapAnnotation(annotation, line, baseRevision, losses, occupiedTargetRanges);
            if (mapped is not null) renderable.Add(mapped);
        }

        ab.Add(Render(baseRevision.Text, renderable, losses));

        // An L1-only export that has an L2 revision is lossy: say so explicitly.
        if (line.L2 is null && lineAnnotations.Any(a => a.Kind == AnnotationKind.SuppliedEditorial))
            losses.Add(new LossEntry("editorial-layer", 1,
                "supplied text has no L2 revision in this export; it is emitted as an empty marker"));
        return ab;
    }

    private sealed record RenderAnnotation(Annotation Source, ScalarRange Range, string? Inner, bool Milestone);

    private static RenderAnnotation? MapAnnotation(
        Annotation annotation, TeiExportLine line, TextRevision baseRevision, List<LossEntry> losses,
        IReadOnlyList<ScalarRange> occupiedTargetRanges)
    {
        // Annotations already anchored to the rendered revision are authoritative.
        if (annotation.AnchoredRevisionId == baseRevision.Id)
            return new RenderAnnotation(annotation, annotation.Range, null,
                annotation.Kind is AnnotationKind.DifferentHand or AnnotationKind.Added or AnnotationKind.Erased
                    && annotation.Range.IsEmpty);

        var anchored = annotation.AnchoredRevisionId == line.L1?.Id ? line.L1
            : annotation.AnchoredRevisionId == line.L2?.Id ? line.L2 : null;
        if (anchored is null)
        {
            losses.Add(new LossEntry("annotation-anchor", 1,
                $"annotation {annotation.Id} is anchored to a revision outside this line"));
            return null;
        }

        // An abbreviation is native-L1 state but its expansion is exactly the L2 span.
        if (annotation.Kind == AnnotationKind.Abbreviation && baseRevision == line.L2
            && annotation.ExpansionRange is { } expansion && annotation.ExpansionText is { } expansionText)
        {
            // The L2 span is the expansion, but the visible abbreviation must stay in <abbr>.
            var visible = ScalarText.Slice(anchored.Text, annotation.Range.Start, annotation.Range.End);
            return new RenderAnnotation(annotation, expansion, visible, false);
        }

        var sourceText = ScalarText.Slice(anchored.Text, annotation.Range.Start, annotation.Range.End);

        // An orig/reg (or sic/corr) pair maps to the editorial half in L2 while the
        // visible half is preserved as the element's inner text.
        if (annotation.Kind == AnnotationKind.AlternativeReading
            && annotation.Description is "choice/orig-reg" or "choice/sic-corr" or "app/orig-corr"
            && baseRevision == line.L2 && annotation.AlternativeText is { Length: > 0 } editorial)
        {
            var editorialPosition = anchored.Text == baseRevision.Text && sourceText == editorial
                ? annotation.Range.Start : FindUniqueScalarIndex(baseRevision.Text, editorial);
            if (editorialPosition is { } editorialStart)
                return new RenderAnnotation(annotation,
                    new ScalarRange(editorialStart, editorialStart + ScalarText.Count(editorial)),
                    sourceText, false);
            losses.Add(new LossEntry("annotation-mapping", 1,
                $"annotation {annotation.Id} ({annotation.Kind}) has no unique editorial span in the rendered layer"));
            return null;
        }

        // Equal layers share scalar coordinates even when the marked text repeats.
        if (anchored.Text == baseRevision.Text)
            return new RenderAnnotation(annotation, annotation.Range, null,
                annotation.Kind is AnnotationKind.DifferentHand or AnnotationKind.Added or AnnotationKind.Erased
                    && annotation.Range.IsEmpty);

        // Zero-width milestones follow their preceding context rather than a searchable span.
        if (annotation.Range.IsEmpty)
        {
            var prefix = ScalarText.Slice(anchored.Text, 0, annotation.Range.Start);
            var position = prefix.Length == 0 ? 0 : FindUniqueScalarIndex(baseRevision.Text, prefix);
            if (position is null)
            {
                losses.Add(new LossEntry("annotation-mapping", 1,
                    $"zero-width annotation {annotation.Id} ({annotation.Kind}) has no unique matching context"));
                return null;
            }
            return new RenderAnnotation(annotation,
                new ScalarRange(position.Value + ScalarText.Count(prefix), position.Value + ScalarText.Count(prefix)),
                null,
                annotation.Kind is AnnotationKind.DifferentHand or AnnotationKind.Added or AnnotationKind.Erased);
        }

        // A repeated reading is located by its surrounding text when that context is unique;
        // truly ambiguous locations are still reported instead of being attached to the first
        // matching letters.
        var located = FindContextualScalarIndex(baseRevision.Text, sourceText,
            anchored.Text, annotation.Range.Start, annotation.Range.End);
        if (located is not { } start)
        {
            losses.Add(new LossEntry("annotation-mapping", 1,
                $"annotation {annotation.Id} ({annotation.Kind}) has no unambiguous exact span in the rendered layer"));
            return null;
        }
        var mappedRange = new ScalarRange(start, start + ScalarText.Count(sourceText));
        if (occupiedTargetRanges.Any(range => Overlaps(mappedRange, range)))
        {
            losses.Add(new LossEntry("annotation-mapping", 1,
                $"annotation {annotation.Id} ({annotation.Kind}) maps onto a span already claimed by the rendered layer"));
            return null;
        }
        return new RenderAnnotation(annotation, mappedRange, null,
            annotation.Kind is AnnotationKind.DifferentHand or AnnotationKind.Added or AnnotationKind.Erased
                && annotation.Range.IsEmpty);
    }

    private static List<object> Render(string text, List<RenderAnnotation> annotations, List<LossEntry> losses)
    {
        var scalars = ScalarText.Count(text);
        var supported = annotations
            .Where(a => a.Source.Kind is AnnotationKind.Unclear or AnnotationKind.Illegible
                or AnnotationKind.Lost or AnnotationKind.AlternativeReading or AnnotationKind.SuppliedEditorial
                or AnnotationKind.Abbreviation or AnnotationKind.Added or AnnotationKind.Erased
                or AnnotationKind.DifferentHand or AnnotationKind.Sign)
            .OrderBy(a => a.Range.Start)
            // A marker at a span's start precedes that span; it must not disappear inside
            // an atomic reading such as <del> when the document is imported again.
            .ThenBy(a => a.Range.IsEmpty ? 0 : 1)
            .ThenByDescending(a => a.Range.End)
            // The importer supports nested inline markup inside a sign/overline.
            .ThenBy(a => a.Source.Kind == AnnotationKind.Sign ? 0 : 1)
            .ToList();
        foreach (var dropped in annotations.Where(a => !supported.Contains(a)))
            losses.Add(new LossEntry("annotation-kind", 1,
                $"annotation kind {dropped.Source.Kind} has no bounded TEI mapping"));

        var accepted = new List<RenderAnnotation>();
        foreach (var a in supported)
        {
            if (a.Range.End > scalars)
            {
                losses.Add(new LossEntry("annotation-mapping", 1,
                    $"annotation {a.Source.Id} is outside the rendered text"));
                continue;
            }
            var clash = accepted.FirstOrDefault(b =>
                a.Range.Start < b.Range.End && b.Range.Start < a.Range.End
                && !(b.Range.Start <= a.Range.Start && a.Range.End <= b.Range.End)
                && !(a.Range.Start <= b.Range.Start && b.Range.End <= a.Range.End));
            if (clash is null) accepted.Add(a);
            else losses.Add(new LossEntry("overlapping-uncertainty", 1,
                $"annotation {a.Source.Id} overlaps {clash.Source.Id}"));
        }

        int index = 0;
        return RenderRange(0, scalars, includeEndMarkers: true);

        List<object> RenderRange(int start, int end, bool includeEndMarkers)
        {
            var pieces = new List<object>();
            int cursor = start;
            while (index < accepted.Count)
            {
                var a = accepted[index];
                if (a.Range.Start > end || a.Range.End > end
                    || (a.Range.Start == end && !includeEndMarkers)) break;
                index++;
                if (a.Range.Start > cursor)
                    pieces.Add(ScalarText.Slice(text, cursor, a.Range.Start));
                cursor = a.Range.Start;
                if (a.Milestone)
                {
                    pieces.Add(Milestone(a.Source));
                    continue;
                }

                var childIndex = index;
                object inner = a.Range.IsEmpty ? "" : RenderRange(a.Range.Start, a.Range.End, includeEndMarkers: false);
                if (a.Inner is not null)
                {
                    // Replacement readings use a different coordinate space. Their nested
                    // spans cannot safely be applied to the visible/original reading.
                    if (index > childIndex)
                        losses.Add(new LossEntry("nested-annotation", index - childIndex,
                            $"annotations inside replacement reading {a.Source.Id} could not be mapped"));
                    inner = a.Inner;
                }
                else if (index > childIndex && a.Source.Kind != AnnotationKind.Sign)
                {
                    losses.Add(new LossEntry("nested-annotation", index - childIndex,
                        $"annotations inside {a.Source.Kind} cannot be represented by the bounded nested profile"));
                    inner = ScalarText.Slice(text, a.Range.Start, a.Range.End);
                }
                pieces.Add(Markup(a.Source, inner));
                cursor = a.Range.End;
            }
            if (cursor < end) pieces.Add(ScalarText.Slice(text, cursor, end));
            return pieces;
        }
    }

    private static XElement Milestone(Annotation a) => a.Kind switch
    {
        AnnotationKind.DifferentHand => new XElement(Tei + "handShift",
            new XAttribute("new", string.IsNullOrWhiteSpace(a.HandLabel) ? "unattributed" : a.HandLabel!)),
        AnnotationKind.Added => new XElement(Tei + "addSpan", new XAttribute("spanTo", a.Description ?? "")),
        AnnotationKind.Erased => new XElement(Tei + "delSpan", new XAttribute("spanTo", a.Description ?? "")),
        _ => throw new InvalidOperationException(a.Kind.ToString()),
    };

    private static XElement Markup(Annotation a, object inner) => a.Kind switch
    {
        AnnotationKind.Unclear => new XElement(Tei + "unclear",
            new XAttribute("reason", a.Description ?? "unclear"), inner),
        AnnotationKind.Illegible => new XElement(Tei + "unclear",
            new XAttribute("reason", a.Description ?? "illegible"), inner),
        AnnotationKind.SuppliedEditorial => new XElement(Tei + "supplied",
            new XAttribute("reason", a.Description ?? "editorial"), inner),
        AnnotationKind.Lost => a.GapExtent switch
        {
            GapExtent.Estimated => new XElement(Tei + "gap",
                new XAttribute("reason", a.Description ?? "lost"),
                new XAttribute("extent", "estimated"),
                new XAttribute("quantity", a.EstimatedScalarCount ?? 0)),
            GapExtent.Known => new XElement(Tei + "gap",
                new XAttribute("reason", a.Description ?? "lost"), new XAttribute("extent", "known")),
            _ => new XElement(Tei + "gap",
                new XAttribute("reason", a.Description ?? "lost"), new XAttribute("extent", "unknown")),
        },
        AnnotationKind.AlternativeReading => ExportChoice(a, inner),
        AnnotationKind.Abbreviation => new XElement(Tei + "choice",
            new XElement(Tei + "abbr", inner),
            new XElement(Tei + "expan", a.ExpansionText ?? "")),
        AnnotationKind.Added => new XElement(Tei + "add", inner),
        AnnotationKind.Erased => new XElement(Tei + "del", inner),
        AnnotationKind.Sign => new XElement(Tei + "hi",
            new XAttribute("rend", a.Description ?? "sign"), inner),
        _ => throw new InvalidOperationException(a.Kind.ToString()),
    };

    private static XElement ExportChoice(Annotation a, object inner) => a.Description switch
    {
        "choice/orig-reg" => new XElement(Tei + "choice",
            new XElement(Tei + "orig", inner),
            new XElement(Tei + "reg", a.AlternativeText ?? "")),
        "choice/sic-corr" => new XElement(Tei + "choice",
            new XElement(Tei + "sic", inner),
            new XElement(Tei + "corr", a.AlternativeText ?? "")),
        "app/orig-corr" => new XElement(Tei + "app",
            new XElement(Tei + "rdg", new XAttribute("type", "orig"),
                new XAttribute("hand", a.HandLabel ?? "firsthand"), inner),
            new XElement(Tei + "rdg", new XAttribute("type", "corr"),
                new XAttribute("hand", "corrector"), a.AlternativeText ?? "")),
        _ => new XElement(Tei + "app",
            new XElement(Tei + "lem", inner),
            new XElement(Tei + "rdg", new XAttribute("type", "alternative"), a.AlternativeText ?? "")),
    };

    private static void AddOrientation(XElement ab, TeiLineInfo? info)
    {
        if (info is null) return;
        var rend = new List<string>();
        if (info.Direction is { } direction)
            rend.Add($"direction:{direction switch
            {
                TextDirection.Ltr => "left-to-right",
                TextDirection.Rtl => "right-to-left",
                TextDirection.VerticalTopToBottom => "top-to-bottom",
                TextDirection.VerticalBottomToTop => "bottom-to-top",
                _ => throw new ArgumentOutOfRangeException(nameof(info)),
            }}");
        if (!string.IsNullOrWhiteSpace(info.Script)) rend.Add($"script:{info.Script.Trim()}");
        if (rend.Count > 0) ab.Add(new XAttribute("rend", string.Join(';', rend)));
        if (info.Languages.Count > 0) ab.Add(new XAttribute(Xml + "lang", info.Languages[0]));
    }

    /// <summary>
    /// Optional XML Schema validation for an operator-supplied TEI/EpiDoc XSD. The schema is
    /// not bundled or downloaded: the caller pins the exact file, and its SHA-256 is returned
    /// in the result notes so the validation basis is auditable. DOCTYPE/entity processing
    /// remains prohibited. This supplements, and does not replace, the bounded profile import.
    /// </summary>
    public static (IReadOnlyList<PageXml.ImportIssue> Issues, string SchemaSha256)
        ValidateAgainstSchema(string xml, string schemaPath)
    {
        if (!File.Exists(schemaPath))
            throw new InvalidDataException($"Schema file is not readable: {schemaPath}");
        var schemaBytes = File.ReadAllBytes(schemaPath);
        var schemaHash = Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(schemaBytes));
        var schemas = new XmlSchemaSet { XmlResolver = null };
        try
        {
            using var schemaReader = XmlReader.Create(new MemoryStream(schemaBytes),
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            schemas.Add(null, schemaReader);
            schemas.Compile();
        }
        catch (Exception ex) when (ex is XmlException or XmlSchemaException)
        {
            throw new InvalidDataException($"Schema is not a readable XSD: {ex.Message}", ex);
        }

        var issues = new List<PageXml.ImportIssue>
        {
            new("schema", $"validated against {Path.GetFileName(schemaPath)} (sha256:{schemaHash})",
                PageXml.IssueSeverity.Note),
        };
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            ValidationType = ValidationType.Schema,
            Schemas = schemas,
            MaxCharactersInDocument = 50 * 1024 * 1024,
        };
        settings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
        settings.ValidationEventHandler += (_, args) =>
        {
            if (issues.Count >= 101) return;
            issues.Add(new PageXml.ImportIssue("schema",
                args.Message,
                args.Severity == XmlSeverityType.Error
                    ? PageXml.IssueSeverity.Loss
                    : PageXml.IssueSeverity.Note));
        };
        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), settings);
            while (reader.Read()) { }
        }
        catch (XmlException ex)
        {
            issues.Add(new PageXml.ImportIssue("schema", ex.Message, PageXml.IssueSeverity.Loss));
        }
        return (issues, schemaHash);
    }

    // ---- Import ----

    public static TeiImportResult Import(string xml)
    {
        // Preserve whitespace: word spaces between <w>/<hi> children are content.
        var doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        if (doc.DocumentType is not null)
            throw new InvalidDataException("DOCTYPE/external entities are not accepted in TEI imports.");

        var body = doc.Descendants(Tei + "body").FirstOrDefault()
            ?? throw new InvalidDataException("Missing TEI body.");
        var edition = body.DescendantsAndSelf(Tei + "div")
                .FirstOrDefault(d => (string?)d.Attribute("type") == "edition"
                    && (string?)d.Attribute("subtype") == "primary")
            ?? body.DescendantsAndSelf(Tei + "div")
                .FirstOrDefault(d => (string?)d.Attribute("type") == "edition")
            ?? body.DescendantsAndSelf(Tei + "div").FirstOrDefault()
            ?? throw new InvalidDataException("No edition div found.");

        var provenance = ReadProvenance(doc, body, edition);
        var revisions = new List<TextRevision>();
        var annotations = new List<Annotation>();
        var unsupported = new List<PageXml.ImportIssue>();
        var lineInfo = new List<TeiLineInfo>();
        var unsupportedCounts = new Dictionary<string, (int Count, PageXml.IssueSeverity Severity, string Detail)>(StringComparer.Ordinal);
        if (provenance.PageBreaks.Any(pb => string.IsNullOrWhiteSpace(pb.Number)))
            ReportUnsupported(unsupportedCounts, "page-break-without-number", PageXml.IssueSeverity.Note,
                "a <pb> has no @n; the folio/page label cannot be recorded");
        var usedIds = new HashSet<string>(StringComparer.Ordinal);
        var importToken = Guid.NewGuid().ToString("N")[..8];
        int lineIndex = 0;

        var bodyOrder = body.DescendantsAndSelf().ToList();
        var positions = bodyOrder.Select((element, index) => (element, index))
            .ToDictionary(pair => pair.element, pair => pair.index);
        var pageBreakPositions = bodyOrder
            .Where(element => element.Name == Tei + "pb")
            .Select(element => (Position: positions[element], Number: (string?)element.Attribute("n")))
            .ToList();

        var duplicateIds = body.DescendantsAndSelf()
            .SelectMany(e => e.Attributes(Xml + "id"))
            .GroupBy(a => a.Value, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicateIds.Count > 0)
            ReportUnsupported(unsupportedCounts, "duplicate-xml-id", PageXml.IssueSeverity.Loss,
                $"duplicate @xml:id value(s) were uniquified: {string.Join(", ", duplicateIds.Take(5))}");

        foreach (var ab in edition.DescendantsAndSelf(Tei + "ab"))
        {
            var direction = ParseDirection(ab);
            var script = ParseScript(ab);
            var languages = ParseLanguages(ab);
            int abPosition = positions[ab];
            var precedingBreaks = pageBreakPositions.Where(pb => pb.Position < abPosition).ToList();
            int pageIndex = precedingBreaks.Count;
            string? pageNumber = precedingBreaks.LastOrDefault().Number;
            foreach (var segment in SplitSegments(ab))
            {
                lineIndex++;
                var suffix = StableSuffix(segment.XmlId, importToken, lineIndex, usedIds);
                var lineId = new EntityId(EntityKind.Line, suffix);
                var info = new TeiLineInfo(
                    lineId,
                    segment.Number ?? ParseLineNumber(ab),
                    segment.XmlId,
                    direction,
                    script,
                    languages,
                    ParseTags(ab),
                    pageIndex,
                    pageNumber);
                lineInfo.Add(info);

                var built = new SegmentBuilder()
                    .Build(segment.Nodes, lineId, unsupportedCounts);
                var l1 = new TextRevision
                {
                    Id = new EntityId(EntityKind.TranscriptionRevision, $"rev-{suffix}-l1"),
                    ScopeId = lineId,
                    Layer = TextLayer.L1Diplomatic,
                    Author = new AgentRef { Id = "tei-import", IsModel = false },
                    CreatedAt = DateTimeOffset.UtcNow,
                    Text = built.L1,
                };
                revisions.Add(l1);

                TextRevision? l2 = null;
                if (built.HasL2)
                {
                    l2 = new TextRevision
                    {
                        Id = new EntityId(EntityKind.TranscriptionRevision, $"rev-{suffix}-l2"),
                        ScopeId = lineId,
                        Layer = TextLayer.L2Editorial,
                        Author = new AgentRef { Id = "tei-import", IsModel = false },
                        CreatedAt = DateTimeOffset.UtcNow,
                        Text = built.L2,
                    };
                    revisions.Add(l2);
                }

                // Bind each annotation to the revision whose offsets it addresses.
                foreach (var pending in built.Annotations)
                {
                    var anchored = pending.Layer == TextLayer.L2Editorial && l2 is not null ? l2 : l1;
                    annotations.Add(pending.Annotation with
                    {
                        AnchoredRevisionId = anchored.Id,
                        ExpansionRevisionId = pending.Annotation.ExpansionRange is not null && l2 is not null
                            ? l2.Id
                            : pending.Annotation.ExpansionRevisionId,
                    });
                }
            }
        }

        // Block-level constructs outside an <ab> are not part of the line profile. Structural
        // containers and page breaks are expected; a column break is reported as a note and
        // anything else as a loss, so nothing outside the lines disappears silently.
        foreach (var element in body.Descendants())
        {
            if (element.AncestorsAndSelf(Tei + "ab").Any()) continue;
            switch (element.Name.LocalName)
            {
                case "div":
                case "pb":
                case "lb":
                case "p":
                    continue;
                case "cb":
                    ReportUnsupported(unsupportedCounts, "cb", PageXml.IssueSeverity.Note,
                        "column break recorded as unsupported; column structure is not modeled");
                    continue;
                default:
                    ReportUnsupported(unsupportedCounts, element.Name.LocalName, PageXml.IssueSeverity.Loss,
                        "block-level element outside the edition line profile");
                    continue;
            }
        }

        foreach (var (element, (count, severity, detail)) in unsupportedCounts)
            unsupported.Add(new PageXml.ImportIssue(element, $"{detail} ({count} occurrence{(count == 1 ? "" : "s")})", severity));

        var document = new TeiDocumentInfo(
            provenance.Title,
            provenance.SurfaceId,
            provenance.Graphics,
            provenance.Idnos,
            provenance.Licence,
            provenance.Responsibilities,
            provenance.Languages,
            lineInfo,
            provenance.PageBreaks);

        return new TeiImportResult(
            revisions,
            annotations,
            unsupported,
            [.. lineInfo.Select(i => new TeiLineOrientation(i.LineId, i.Direction, i.Script))],
            document,
            System.Text.Encoding.UTF8.GetByteCount(xml) <= MaxAttachmentBytes ? xml : null);
    }

    private sealed record Segment(
        int? Number,
        string? XmlId,
        List<XNode> Nodes,
        XElement Ab,
        bool BreakNo,
        Dictionary<string, string> Tags);

    private static IEnumerable<Segment> SplitSegments(XElement ab)
    {
        var current = new List<XNode>();
        int? number = ParseLineNumber(ab);
        string? xmlId = (string?)ab.Attribute(Xml + "id");
        bool started = false;
        var tags = ParseTags(ab).ToDictionary(t => t, t => t, StringComparer.Ordinal);

        IEnumerable<Segment> Flush()
        {
            if (!started && !current.Any(HasVisibleContent)) yield break;
            if (!current.Any(HasVisibleContent) && number is null) yield break;
            yield return new Segment(number, xmlId, [.. current], ab, false, tags);
        }

        foreach (var node in ab.Nodes())
        {
            if (node is XElement element && element.Name == Tei + "lb")
            {
                var lbBreak = (string?)element.Attribute("break");
                var lbNumber = ParseLineNumber(element);
                var lbId = (string?)element.Attribute(Xml + "id");
                if (lbBreak == "no")
                {
                    // Hyphenation-style continuation: the text is one visual line.
                    if (lbNumber is { } continuationNumber) number ??= continuationNumber;
                    started = true;
                    continue;
                }

                if (!started)
                {
                    // A leading <lb/> belongs to the line that follows; it is not an empty line.
                    number = lbNumber ?? number;
                    xmlId = lbId ?? xmlId;
                    started = true;
                    continue;
                }

                foreach (var segment in Flush()) yield return segment;
                current = [];
                number = lbNumber;
                xmlId = lbId;
                started = true;
                continue;
            }
            current.Add(node);
            if (HasVisibleContent(node)) started = true;
        }
        foreach (var segment in Flush()) yield return segment;
    }

    private static bool HasVisibleContent(XNode node) => node switch
    {
        XText text => !string.IsNullOrWhiteSpace(text.Value),
        XElement element => element.Name != Tei + "lb" && (!string.IsNullOrWhiteSpace(element.Value)
            || element.Name.LocalName is "gap" or "addSpan" or "delSpan" or "handShift" or "supplied"),
        _ => false,
    };

    private sealed record BuiltSegment(
        string L1,
        string L2,
        bool HasL2,
        List<PendingAnnotation> Annotations);

    private sealed record PendingAnnotation(Annotation Annotation, TextLayer Layer);

    private sealed class SegmentBuilder
    {
        private readonly System.Text.StringBuilder _l1 = new();
        private readonly System.Text.StringBuilder _l2 = new();
        private readonly List<PendingAnnotation> _annotations = [];
        private bool _hasL2;
        private EntityId _lineId = default;

        public BuiltSegment Build(IEnumerable<XNode> nodes, EntityId lineId,
            Dictionary<string, (int Count, PageXml.IssueSeverity Severity, string Detail)> unsupported)
        {
            _lineId = lineId;
            foreach (var node in nodes)
            {
                if (node is XText text) Append(text.Value);
                else if (node is XElement element) Walk(element, unsupported);
            }

            var l1Text = Normalize(_l1.ToString());
            var l2Text = Normalize(_l2.ToString());
            int l1Length = ScalarText.Count(l1Text);
            int l2Length = ScalarText.Count(l2Text);
            // Only trailing formatting whitespace is trimmed; clamp end-anchored spans to it.
            for (int i = 0; i < _annotations.Count; i++)
            {
                var pending = _annotations[i];
                int max = pending.Layer == TextLayer.L2Editorial ? l2Length : l1Length;
                if (pending.Annotation.Range.End <= max) continue;
                _annotations[i] = pending with
                {
                    Annotation = pending.Annotation with
                    {
                        Range = new ScalarRange(Math.Min(pending.Annotation.Range.Start, max), max),
                    },
                };
            }
            return new BuiltSegment(l1Text, l2Text, _hasL2, _annotations);
        }

        private void Append(string value, bool l1 = true, bool l2 = true)
        {
            var collapsed = Collapse(value);
            if (l1) AppendCollapsed(_l1, collapsed);
            if (l2) AppendCollapsed(_l2, collapsed);
        }

        private static void AppendCollapsed(System.Text.StringBuilder builder, string value)
        {
            if (value.Length == 0) return;
            var text = builder.Length == 0 ? value.TrimStart() : value;
            if (text.Length == 0) return;
            if (builder.Length > 0 && builder[^1] == ' ' && text[0] == ' ') text = text[1..];
            builder.Append(text);
        }

        private static string Collapse(string value)
        {
            if (value.Length == 0) return "";
            if (value.All(char.IsWhiteSpace)) return " ";
            var words = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return (char.IsWhiteSpace(value[0]) ? " " : "") + words
                + (char.IsWhiteSpace(value[^1]) ? " " : "");
        }

        private void Walk(XElement element,
            Dictionary<string, (int Count, PageXml.IssueSeverity Severity, string Detail)> unsupported)
        {
            int l1Start = ScalarText.Count(_l1.ToString());
            int l2Start = ScalarText.Count(_l2.ToString());
            switch (element.Name.LocalName)
            {
                case "lb":
                case "milestone":
                    break;
                case "unclear":
                case "supplied":
                {
                    var isSupplied = element.Name.LocalName == "supplied";
                    var reason = (string?)element.Attribute("reason");
                    var inner = VisibleText(element);
                    if (isSupplied)
                    {
                        Append(inner, l1: false, l2: true);
                        _hasL2 = true;
                        Add(AnnotationKind.SuppliedEditorial, TextLayer.L2Editorial, l2Start,
                            ScalarText.Count(_l2.ToString()), a => a with { Description = reason });
                    }
                    else
                    {
                        Append(inner);
                        var kind = reason?.Contains("illegible", StringComparison.OrdinalIgnoreCase) == true
                            ? AnnotationKind.Illegible
                            : AnnotationKind.Unclear;
                        Add(kind, TextLayer.L1Diplomatic, l1Start, ScalarText.Count(_l1.ToString()),
                            a => a with { Description = reason });
                    }
                    ReportNestedMarkup(element, unsupported);
                    break;
                }
                case "space":
                    Append(" ");
                    break;
                case "damage":
                {
                    var inner = VisibleText(element);
                    if (inner.Length > 0)
                    {
                        Append(inner);
                        Add(AnnotationKind.Unclear, TextLayer.L1Diplomatic, l1Start,
                            ScalarText.Count(_l1.ToString()), a => a with { Description = "damage" });
                    }
                    else
                    {
                        Add(AnnotationKind.Lost, TextLayer.L1Diplomatic, l1Start, l1Start,
                            a => a with { Description = "damage" });
                    }
                    Report("damage", PageXml.IssueSeverity.Note,
                        "damage is represented as unclear/lost; the element name is retained in the source attachment",
                        unsupported);
                    ReportNestedMarkup(element, unsupported);
                    break;
                }
                case "ex":
                {
                    var inner = VisibleText(element);
                    Append(inner, l1: false, l2: true);
                    _hasL2 = true;
                    Add(AnnotationKind.SuppliedEditorial, TextLayer.L2Editorial, l2Start,
                        ScalarText.Count(_l2.ToString()), a => a with { Description = "ex" });
                    Report("ex", PageXml.IssueSeverity.Note,
                        "editorial expansion is represented as supplied L2 text; the element name is retained in the source attachment",
                        unsupported);
                    ReportNestedMarkup(element, unsupported);
                    break;
                }
                case "gap":
                {
                    if (element.Attribute("extent") is null)
                        Report("gap-without-extent", PageXml.IssueSeverity.Note,
                            "gap has no @extent; imported as unknown extent", unsupported);
                    var extent = ((string?)element.Attribute("extent")) switch
                    {
                        "known" => GapExtent.Known,
                        "estimated" => GapExtent.Estimated,
                        _ => GapExtent.Unknown,
                    };
                    int? quantity = (int?)element.Attribute("quantity");
                    Add(AnnotationKind.Lost, TextLayer.L1Diplomatic, l1Start, l1Start, a => a with
                    {
                        GapExtent = extent,
                        EstimatedScalarCount = quantity,
                        Description = (string?)element.Attribute("reason"),
                    });
                    break;
                }
                case "app":
                {
                    var lem = element.Element(Tei + "lem");
                    var readings = element.Elements(Tei + "rdg").ToList();
                    if (lem is null && readings.Count == 0)
                        Report("app-without-reading", PageXml.IssueSeverity.Loss,
                            "app has neither a lem nor an rdg reading", unsupported);
                    var orig = readings.FirstOrDefault(r => (string?)r.Attribute("type") == "orig");
                    var corr = readings.FirstOrDefault(r => (string?)r.Attribute("type") == "corr");
                    if (orig is not null && corr is not null)
                    {
                        // NTVMR/correction apparatus: the witness's original reading is L1;
                        // the corrector's reading is the derived L2 reading.
                        var originalText = VisibleText(orig);
                        var correctedText = VisibleText(corr);
                        Append(originalText, l1: true, l2: false);
                        Append(correctedText, l1: false, l2: true);
                        _hasL2 = true;
                        Add(AnnotationKind.AlternativeReading, TextLayer.L1Diplomatic, l1Start,
                            ScalarText.Count(_l1.ToString()), a => a with
                            {
                                AlternativeText = correctedText,
                                Description = "app/orig-corr",
                                HandLabel = (string?)corr.Attribute("hand"),
                            });
                        foreach (var extra in readings.Where(r => !ReferenceEquals(r, orig)
                                     && !ReferenceEquals(r, corr)))
                        {
                            Add(AnnotationKind.AlternativeReading, TextLayer.L1Diplomatic, l1Start,
                                ScalarText.Count(_l1.ToString()), a => a with
                                {
                                    AlternativeText = VisibleText(extra),
                                    Description = ReadingDescription(extra),
                                    HandLabel = (string?)extra.Attribute("hand"),
                                });
                        }
                    }
                    else
                    {
                        var baseReading = lem ?? readings.FirstOrDefault();
                        var baseText = baseReading is null ? "" : VisibleText(baseReading);
                        Append(baseText);
                        foreach (var reading in readings)
                        {
                            if (ReferenceEquals(reading, baseReading)) continue;
                            var alternative = VisibleText(reading);
                            Add(AnnotationKind.AlternativeReading, TextLayer.L1Diplomatic, l1Start,
                                ScalarText.Count(_l1.ToString()), a => a with
                                {
                                    AlternativeText = alternative,
                                    Description = ReadingDescription(reading),
                                });
                        }
                        if (lem is null && readings.Count > 0)
                            Report("app-without-lem", PageXml.IssueSeverity.Note,
                                "app had no lem; the first rdg was used as the base reading and the rest were kept as alternatives",
                                unsupported);
                    }
                    ReportNestedMarkup(element, unsupported);
                    break;
                }
                case "abbr":
                {
                    // Standalone abbreviation (NTVMR): visible text only; an expansion is
                    // not fabricated. A nested <choice><abbr><expan> is handled above.
                    Report("abbr-without-expan", PageXml.IssueSeverity.Note,
                        "standalone abbr kept as visible text; no expansion was supplied", unsupported);
                    WalkChildren(element, unsupported);
                    break;
                }
                case "hi":
                {
                    var rend = (string?)element.Attribute("rend") ?? "";
                    if (rend.Contains("overline", StringComparison.OrdinalIgnoreCase))
                    {
                        int start = ScalarText.Count(_l1.ToString());
                        WalkChildren(element, unsupported);
                        int end = ScalarText.Count(_l1.ToString());
                        Add(AnnotationKind.Sign, TextLayer.L1Diplomatic, start, end,
                            a => a with { Description = "overline" });
                    }
                    else
                    {
                        WalkChildren(element, unsupported);
                    }
                    break;
                }
                case "choice":
                {
                    var abbr = element.Element(Tei + "abbr");
                    var expan = element.Element(Tei + "expan");
                    if (abbr is not null || expan is not null)
                    {
                        var visible = abbr is not null ? VisibleText(abbr) : expan is null ? "" : VisibleText(expan);
                        Append(visible, l1: true, l2: false);
                        Append(expan is null ? visible : VisibleText(expan), l1: false, l2: true);
                        _hasL2 = true;
                        Add(AnnotationKind.Abbreviation, TextLayer.L1Diplomatic, l1Start,
                            ScalarText.Count(_l1.ToString()), a => a with
                            {
                                ExpansionText = expan is null ? null
                                    : ScalarText.Slice(_l2.ToString(), l2Start, ScalarText.Count(_l2.ToString())),
                                ExpansionRange = expan is null
                                    ? null
                                    : new ScalarRange(l2Start, ScalarText.Count(_l2.ToString())),
                                Description = "tei:choice/abbr",
                            });
                    }
                    else
                    {
                        var orig = element.Element(Tei + "orig");
                        var reg = element.Element(Tei + "reg");
                        var sic = element.Element(Tei + "sic");
                        var corr = element.Element(Tei + "corr");
                        var visible = orig ?? sic;
                        var editorial = reg ?? corr;
                        if (visible is not null || editorial is not null)
                        {
                            var visibleText = visible is null ? VisibleText(editorial!) : VisibleText(visible);
                            var editorialText = editorial is null ? visibleText : VisibleText(editorial);
                            Append(visibleText, l1: true, l2: false);
                            Append(editorialText, l1: false, l2: true);
                            _hasL2 = true;
                            Add(AnnotationKind.AlternativeReading, TextLayer.L1Diplomatic, l1Start,
                                ScalarText.Count(_l1.ToString()), a => a with
                                {
                                    AlternativeText = editorialText,
                                    Description = reg is not null ? "choice/orig-reg" : "choice/sic-corr",
                                });
                        }
                        else
                        {
                            Report("choice-without-reading", PageXml.IssueSeverity.Loss,
                                "choice has no supported abbr/expan/orig/reg/sic/corr child", unsupported);
                            WalkChildren(element, unsupported);
                        }
                    }
                    break;
                }
                case "add":
                {
                    var inner = VisibleText(element);
                    Append(inner);
                    Add(AnnotationKind.Added, TextLayer.L1Diplomatic, l1Start,
                        ScalarText.Count(_l1.ToString()), a => a with
                        {
                            Description = (string?)element.Attribute("place"),
                        });
                    break;
                }
                case "del":
                {
                    var inner = VisibleText(element);
                    // Erased text is not visible in L1; it remains as an annotated L2 span.
                    Append(inner, l1: false, l2: true);
                    _hasL2 = true;
                    Add(AnnotationKind.Erased, TextLayer.L2Editorial, l2Start,
                        ScalarText.Count(_l2.ToString()), a => a with
                        {
                            Description = (string?)element.Attribute("reason"),
                        });
                    break;
                }
                case "addSpan":
                    Add(AnnotationKind.Added, TextLayer.L2Editorial, l2Start, l2Start, a => a with
                    {
                        Description = (string?)element.Attribute("spanTo"),
                    });
                    break;
                case "delSpan":
                    Add(AnnotationKind.Erased, TextLayer.L2Editorial, l2Start, l2Start, a => a with
                    {
                        Description = (string?)element.Attribute("spanTo"),
                    });
                    break;
                case "handShift":
                    // A milestone has an exact position in the complete reading, including
                    // prior supplied/erased text. Build binds this to L1 when no L2 exists.
                    Add(AnnotationKind.DifferentHand, TextLayer.L2Editorial, l2Start, l2Start, a => a with
                    {
                        HandLabel = (string?)element.Attribute("new") ?? (string?)element.Attribute("hand") ?? "unattributed",
                    });
                    break;
                case "note":
                case "bibl":
                case "ref":
                    Report(element.Name.LocalName, PageXml.IssueSeverity.Note,
                        "editorial note/reference skipped (not visible text)", unsupported);
                    break;
                case "w":
                case "seg":
                case "p":
                case "div":
                case "subst":
                    WalkChildren(element, unsupported);
                    break;
                default:
                {
                    var inner = element.Value;
                    Append(inner);
                    Report(element.Name.LocalName, PageXml.IssueSeverity.Loss,
                        "inline element outside the bounded profile; text preserved, markup preserved only in the source attachment",
                        unsupported);
                    break;
                }
            }
        }

        private void WalkChildren(XElement element,
            Dictionary<string, (int Count, PageXml.IssueSeverity Severity, string Detail)> unsupported)
        {
            foreach (var node in element.Nodes())
            {
                if (node is XText text) Append(text.Value);
                else if (node is XElement child) Walk(child, unsupported);
            }
        }

        private void ReportNestedMarkup(XElement element,
            Dictionary<string, (int Count, PageXml.IssueSeverity Severity, string Detail)> unsupported)
        {
            foreach (var nested in element.Descendants())
            {
                if (nested.Name.LocalName is "lem" or "rdg" or "abbr" or "expan" or "orig" or "reg"
                    or "sic" or "corr" or "lb") continue;
                Report(nested.Name.LocalName, PageXml.IssueSeverity.Note,
                    "nested markup flattened inside a supported construct", unsupported);
            }
        }

        private void Add(AnnotationKind kind, TextLayer layer, int start, int end, Func<Annotation, Annotation>? configure = null)
        {
            var annotation = new Annotation
            {
                // Deterministic per line: re-importing the same record produces the same ids.
                Id = new EntityId(EntityKind.Annotation, AnnotationSuffix(_lineId, _annotations.Count)),
                Kind = kind,
                AnchoredRevisionId = default,
                Range = new ScalarRange(start, end),
            };
            if (configure is not null) annotation = configure(annotation);
            _annotations.Add(new PendingAnnotation(annotation, layer));
        }

        private static string AnnotationSuffix(EntityId lineId, int index)
        {
            var suffix = $"tei-{lineId.Suffix}-{index}";
            return suffix.Length <= 63 ? suffix : suffix[..63];
        }

        private static void Report(string element, PageXml.IssueSeverity severity, string detail,
            Dictionary<string, (int Count, PageXml.IssueSeverity Severity, string Detail)> unsupported)
        {
            if (unsupported.TryGetValue(element, out var existing))
                unsupported[element] = (existing.Count + 1, existing.Severity, existing.Detail);
            else
                unsupported[element] = (1, severity, detail);
        }

        private static string ReadingDescription(XElement reading)
        {
            var type = (string?)reading.Attribute("type");
            var wit = (string?)reading.Attribute("wit");
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(type)) parts.Add($"type={type}");
            if (!string.IsNullOrWhiteSpace(wit)) parts.Add($"wit={wit}");
            return parts.Count == 0 ? "rdg" : string.Join(';', parts);
        }

        private static string VisibleText(XElement element)
        {
            var builder = new System.Text.StringBuilder();
            foreach (var node in element.Nodes())
            {
                if (node is XText text) builder.Append(text.Value);
                else if (node is XElement child)
                {
                    if (child.Name.LocalName is "note" or "bibl" or "ref") continue;
                    builder.Append(VisibleText(child));
                }
            }
            return builder.ToString();
        }

        private static string Normalize(string value) =>
            Collapse(value).TrimEnd();
    }

    private sealed record Provenance(
        string? Title,
        string? SurfaceId,
        IReadOnlyList<TeiGraphic> Graphics,
        IReadOnlyList<TeiIdno> Idnos,
        string? Licence,
        IReadOnlyList<TeiResponsibility> Responsibilities,
        IReadOnlyList<string> Languages,
        IReadOnlyList<TeiPageBreak> PageBreaks);

    private static Provenance ReadProvenance(XDocument doc, XElement body, XElement edition)
    {
        var title = doc.Descendants(Tei + "titleStmt").Elements(Tei + "title")
            .Select(t => t.Value.Trim()).FirstOrDefault(v => v.Length > 0);
        var idnos = doc.Descendants(Tei + "idno")
            .Select(i => new TeiIdno((string?)i.Attribute("type"), i.Value.Trim()))
            .Where(i => i.Value.Length > 0)
            .ToList();
        var licence = CleanProvenance(doc.Descendants(Tei + "licence").Select(l => l.Value).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)))
            ?? CleanProvenance(doc.Descendants(Tei + "availability").Elements(Tei + "p")
                .Select(p => p.Value).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)));
        var responsibilities = new List<TeiResponsibility>();
        foreach (var respStmt in doc.Descendants(Tei + "respStmt"))
        {
            var name = respStmt.Elements(Tei + "name").Select(n => n.Value.Trim()).FirstOrDefault();
            foreach (var resp in respStmt.Elements(Tei + "resp"))
                responsibilities.Add(new TeiResponsibility(resp.Value.Trim(), name, (string?)resp.Attribute("when")));
        }
        foreach (var change in doc.Descendants(Tei + "change"))
            responsibilities.Add(new TeiResponsibility(
                change.Value.Trim(),
                (string?)change.Attribute("who"),
                (string?)change.Attribute("when")));

        // Real EpiDoc may declare the image under <facsimile>, <sourceDesc> or a body
        // <surface>; accept any graphic and de-duplicate by URL without inventing dimensions.
        var graphics = doc.Descendants(Tei + "graphic")
            .OrderByDescending(g => g.Ancestors(Tei + "facsimile").Any() || (string?)g.Attribute("n") == "screen")
            .DistinctBy(g => (string?)g.Attribute("url"))
            .Select(g => new TeiGraphic(
                (string?)g.Attribute("url") ?? "",
                (string?)g.Attribute("n"),
                ParsePixels(g.Attribute("width")),
                ParsePixels(g.Attribute("height"))))
            .Where(g => g.Url.Length > 0)
            .ToList();
        var surfaceId = doc.Descendants(Tei + "surface").Select(s => (string?)s.Attribute(Xml + "id"))
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        var languages = new List<string>();
        foreach (var attribute in new[] { body.Attribute(Xml + "lang"), edition.Attribute(Xml + "lang") })
            if (attribute is not null && !languages.Contains(attribute.Value)) languages.Add(attribute.Value);

        var pageBreaks = body.DescendantsAndSelf(Tei + "pb")
            .Select(pb => new TeiPageBreak((string?)pb.Attribute("n"), (string?)pb.Attribute("type")))
            .ToList();

        return new Provenance(title, surfaceId, graphics, idnos, licence, responsibilities, languages, pageBreaks);
    }

    /// <summary>Real EpiDoc graphics use values such as "5520px"; missing units are also valid.</summary>
    private static int? ParsePixels(XAttribute? attribute)
    {
        if (attribute is null) return null;
        var value = attribute.Value.Trim();
        int end = 0;
        while (end < value.Length && char.IsDigit(value[end])) end++;
        return end > 0 && int.TryParse(value[..end], out var pixels) && pixels > 0 ? pixels : null;
    }

    /// <summary>NTVMR availability blocks embed HTML in CDATA; keep readable text only.</summary>
    private static string? CleanProvenance(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = System.Text.RegularExpressions.Regex.Replace(value, "<[^>]+>", " ");
        text = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+([.,;:])", "$1");
        return text.Length == 0 ? null : text;
    }

    private static void ReportUnsupported(
        Dictionary<string, (int Count, PageXml.IssueSeverity Severity, string Detail)> unsupported,
        string element, PageXml.IssueSeverity severity, string detail)
    {
        if (unsupported.TryGetValue(element, out var existing))
            unsupported[element] = (existing.Count + 1, existing.Severity, existing.Detail);
        else
            unsupported[element] = (1, severity, detail);
    }

    private static int? ParseLineNumber(XElement element) =>
        int.TryParse((string?)element.Attribute("n"), out var number) ? number : null;

    private static TextDirection? ParseDirection(XElement ab)
    {
        foreach (var part in ((string?)ab.Attribute("rend") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split(':', 2, StringSplitOptions.TrimEntries);
            if (pair.Length != 2 || pair[0] != "direction") continue;
            return pair[1] switch
            {
                "left-to-right" or "ltr" => TextDirection.Ltr,
                "right-to-left" or "rtl" => TextDirection.Rtl,
                "top-to-bottom" or "ttb" => TextDirection.VerticalTopToBottom,
                "bottom-to-top" or "btt" => TextDirection.VerticalBottomToTop,
                _ => null,
            };
        }
        return null;
    }

    private static string? ParseScript(XElement ab)
    {
        foreach (var part in ((string?)ab.Attribute("rend") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split(':', 2, StringSplitOptions.TrimEntries);
            if (pair.Length == 2 && pair[0] == "script") return pair[1];
        }
        return null;
    }

    private static IReadOnlyList<string> ParseLanguages(XElement ab) =>
        ab.Attribute(Xml + "lang") is { } lang ? [lang.Value] : [];

    private static IReadOnlyList<string> ParseTags(XElement ab)
    {
        var tags = ab.Attributes()
            .Where(a => a.Name.Namespace == XNamespace.None && a.Name.LocalName.StartsWith("data-", StringComparison.Ordinal))
            .Select(a => $"{a.Name.LocalName[5..]}={a.Value}")
            .ToList();
        // Source verse/line labels are not always integers (NTVMR uses B09K1V10).
        if ((string?)ab.Attribute("n") is { Length: > 0 } n && !int.TryParse(n, out _))
            tags.Add($"n={n}");
        if ((string?)ab.Attribute("part") is { Length: > 0 } part) tags.Add($"part={part}");
        return tags;
    }

    private static string? TagValue(TeiLineInfo? info, string key)
    {
        if (info is null) return null;
        var prefix = key + "=";
        return info.Tags.FirstOrDefault(t => t.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..];
    }

    private static string StableSuffix(string? xmlId, string token, int index, HashSet<string> used)
    {
        var candidate = string.IsNullOrWhiteSpace(xmlId)
            ? $"tei-{token}-{index}"
            : xmlId.Trim();
        var suffix = new string(candidate.Select(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z'
            or >= '0' and <= '9' or '_' or '.' or ':' or '-' ? c : '-').ToArray());
        suffix = suffix.Trim('-', '.', ':');
        if (suffix.Length == 0) suffix = $"tei-{token}-{index}";
        var unique = suffix;
        int counter = 1;
        while (!used.Add(unique)) unique = $"{suffix}-{++counter}";
        return unique;
    }

    private static bool Overlaps(ScalarRange mapped, ScalarRange occupied) =>
        mapped.IsEmpty
            ? occupied.Start <= mapped.Start && mapped.Start < occupied.End
            : occupied.Start < mapped.End && mapped.Start < occupied.End;

    private static int? FindUniqueScalarIndex(string haystack, string needle)
    {
        if (needle.Length == 0) return null;
        int found = haystack.IndexOf(needle, StringComparison.Ordinal);
        if (found < 0 || haystack.IndexOf(needle, found + 1, StringComparison.Ordinal) >= 0) return null;
        return ScalarText.Count(haystack[..found]);
    }

    /// <summary>
    /// Locate a source span in another layer. Repeated text is resolved by surrounding context;
    /// when two occurrences have the same context score the mapping is ambiguous and refused.
    /// </summary>
    private static int? FindContextualScalarIndex(string target, string needle,
        string source, int sourceStart, int sourceEnd)
    {
        if (needle.Length == 0) return null;
        int targetScalars = ScalarText.Count(target);
        int needleScalars = ScalarText.Count(needle);
        var candidates = new List<int>();
        for (int i = 0; i + needleScalars <= targetScalars; i++)
            if (ScalarText.Slice(target, i, i + needleScalars) == needle) candidates.Add(i);
        if (candidates.Count == 0) return null;
        if (candidates.Count == 1) return candidates[0];

        int bestScore = -1;
        int? best = null;
        bool tied = false;
        foreach (var candidate in candidates)
        {
            int score = ContextScore(source, sourceStart, sourceEnd,
                target, candidate, candidate + needleScalars);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
                tied = false;
            }
            else if (score == bestScore)
            {
                tied = true;
            }
        }
        return tied ? null : best;
    }

    private static int ContextScore(string source, int sourceStart, int sourceEnd,
        string target, int targetStart, int targetEnd)
    {
        int before = 0;
        while (sourceStart - before > 0 && targetStart - before > 0
               && ScalarText.Slice(source, sourceStart - before - 1, sourceStart - before)
                   == ScalarText.Slice(target, targetStart - before - 1, targetStart - before))
            before++;
        int after = 0;
        int sourceCount = ScalarText.Count(source);
        int targetCount = ScalarText.Count(target);
        while (sourceEnd + after < sourceCount && targetEnd + after < targetCount
               && ScalarText.Slice(source, sourceEnd + after, sourceEnd + after + 1)
                   == ScalarText.Slice(target, targetEnd + after, targetEnd + after + 1))
            after++;
        return before + after;
    }
}
