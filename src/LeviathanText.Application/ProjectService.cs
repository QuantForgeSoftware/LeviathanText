using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Imaging;

namespace LeviathanText.Application;

/// <summary>
/// Project use cases over the store (todo: Application module — use cases,
/// validation and permissions; thin CLI/API entry points call these).
/// </summary>
public sealed class ProjectService(IProjectStore store)
{
    public NativeProject Create(string name, MaterialTrack? track = null, IReadOnlyDictionary<string, string>? externalRefs = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Project name is required.", nameof(name));

        var project = new NativeProject
        {
            Id = NewProjectId(),
            Name = name,
            Objects = [],
        };
        project = project with { Version = store.SaveProject(project) };
        return project;
    }

    private static EntityId NewProjectId() =>
        new(EntityKind.Collection, Guid.NewGuid().ToString("N")[..12]);

    public void Delete(EntityId id)
    {
        _ = Load(id); // explicit InputMissing when absent
        store.DeleteProject(id);
    }

    public NativeProject Load(EntityId id) =>
        store.LoadProject(id)
            ?? throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = id.ToString(),
                Message = "No project with this id exists in the store.",
            });

    public IReadOnlyList<(EntityId Id, string Name)> List() => store.ListProjects();

    /// <summary>Scan all projects for one containing a line (editor endpoints address lines directly).</summary>
    public NativeProject? LoadProjectForLine(EntityId lineId) =>
        List().Select(entry => Load(entry.Id)).FirstOrDefault(p =>
            p.Objects.SelectMany(o => o.Surfaces).Any(s => s.Lines.Any(l => l.Id == lineId)));

    /// <summary>Scan all projects for one containing a surface.</summary>
    public NativeProject? LoadProjectForSurface(EntityId surfaceId) =>
        List().Select(entry => Load(entry.Id)).FirstOrDefault(p =>
            p.Objects.SelectMany(o => o.Surfaces).Any(s => s.Id == surfaceId));

    /// <summary>Scan all projects for one containing an annotation.</summary>
    public NativeProject? LoadProjectForAnnotation(EntityId annotationId) =>
        List().Select(entry => Load(entry.Id)).FirstOrDefault(p =>
            p.Objects.SelectMany(o => o.Surfaces).Any(s => s.Annotations.Any(a => a.Id == annotationId)));

    /// <summary>Scan all projects for one containing a region.</summary>
    public NativeProject? LoadProjectForRegion(EntityId regionId) =>
        List().Select(entry => Load(entry.Id)).FirstOrDefault(p =>
            p.Objects.SelectMany(o => o.Surfaces).Any(s => s.Regions.Any(r => r.Id == regionId)));

    public long Save(NativeProject project) => store.SaveProject(project);

    /// <summary>
    /// Add a physical-fragment grouping to an object. Surfaces must belong to that object;
    /// fragment ids are native identity, while source identifiers stay in <paramref name="externalRefs"/>.
    /// </summary>
    public (NativeProject Project, NativeFragment Fragment) AddFragment(
        NativeProject project, EntityId objectId, IReadOnlyDictionary<string, string>? externalRefs = null,
        IReadOnlyDictionary<string, string>? metadata = null, IReadOnlyList<EntityId>? surfaceIds = null)
    {
        var obj = project.Objects.FirstOrDefault(o => o.Id == objectId)
            ?? throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = objectId.ToString(),
                Message = "No such object in the project.",
            });
        var ids = (surfaceIds ?? []).Distinct().ToList();
        if (ids.Count == 0)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = objectId.ToString(),
                Message = "A fragment must reference at least one surface.",
            });
        var objectSurfaces = obj.Surfaces.Select(s => s.Id).ToHashSet();
        var unknown = ids.FirstOrDefault(id => !objectSurfaces.Contains(id));
        if (unknown != default)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = unknown.ToString(),
                Message = "Fragment surface does not belong to the target object.",
            });

        var fragment = new NativeFragment
        {
            Id = new EntityId(EntityKind.Fragment, $"frag-{Guid.NewGuid().ToString("N")[..12]}"),
            ExternalRefs = externalRefs ?? new Dictionary<string, string>(),
            Metadata = metadata ?? new Dictionary<string, string>(),
            SurfaceIds = ids,
        };
        var updated = project with
        {
            Objects = [.. project.Objects.Select(o => o.Id == objectId
                ? o with { Fragments = [.. o.Fragments, fragment] }
                : o)],
        };
        updated = updated with { Version = store.SaveProject(updated) };
        return (updated, fragment);
    }
}

/// <summary>
/// Import/Export use cases: bounded PAGE/TEI ingestion into the native model and
/// lossy exports with mandatory loss reports.
/// </summary>
public sealed class InterchangeService(ProjectService projects)
{
    /// <summary>Import a PAGE XML file as a new object in an existing project.</summary>
    public (NativeObject Imported, IReadOnlyList<PageXml.ImportIssue> Unsupported) ImportPage(
        NativeProject project, string pageXmlPath, string surfaceKind = "page")
    {
        var xml = File.ReadAllText(pageXmlPath);
        var captureId = NewCaptureId(project);
        var parsed = PageXml.Import(xml, captureId, UnknownChecksum());
        var scopeToken = captureId.Suffix.Length >= 8 ? captureId.Suffix[^8..] : captureId.Suffix;
        var (scopedRegions, scopedLines, scopedRevisions) =
            ScopeImportedIds(parsed.Regions, parsed.Lines, parsed.Revisions, scopeToken);
        var result = parsed with { Regions = scopedRegions, Lines = scopedLines, Revisions = scopedRevisions };

        // Resolve the referenced image next to the PAGE file (name from the document,
        // falling back to the XML basename with common extensions). Unresolved names are
        // kept as-is so the manifest still records what the document declared.
        var fullXmlPath = Path.GetFullPath(pageXmlPath);
        var directory = Path.GetDirectoryName(fullXmlPath)!;
        var imageName = result.ImageFilename ?? Path.GetFileNameWithoutExtension(pageXmlPath) + ".jpg";
        var stem = Path.GetFileNameWithoutExtension(pageXmlPath);
        var imageDirectories = new[]
        {
            directory,
            Path.Combine(directory, "images"),
            Path.Combine(directory, "image"),
            Path.Combine(Path.GetDirectoryName(directory)!, "images"),
            Path.Combine(Path.GetDirectoryName(directory)!, "image"),
        };
        var imagePath = new[] { imageName, stem + ".png", stem + ".jpg", stem + ".tif" }
            .SelectMany(name => imageDirectories.Select(dir => Path.Combine(dir, name)))
            .FirstOrDefault(File.Exists) ?? imageName;
        var imageDimensions = TryImageDimensions(imagePath)
            ?? (result.WidthPx is { } declaredWidth && result.HeightPx is { } declaredHeight
                ? (declaredWidth, declaredHeight)
                : ((int Width, int Height)?)null);

        var surface = new NativeSurface
        {
            Id = NewId(EntityKind.Surface, project, "surf"),
            Kind = surfaceKind,
            Captures = [new NativeCapture
            {
                Id = captureId,
                Frame = new CoordinateFrame { ImageRef = captureId, ImageChecksum = UnknownChecksum() },
                ImageLocation = imagePath,
                WidthPx = imageDimensions?.Width,
                HeightPx = imageDimensions?.Height,
            }],
            Regions = result.Regions,
            Lines = result.Lines,
            Revisions = result.Revisions,
        };

        var sourceHash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(xml)));
        var attachments = new List<NativeAttachment>();
        if (System.Text.Encoding.UTF8.GetByteCount(xml) <= TeiXml.MaxAttachmentBytes)
        {
            attachments.Add(new NativeAttachment
            {
                Id = $"att-{Guid.NewGuid().ToString("N")[..12]}",
                Name = Path.GetFileName(pageXmlPath),
                MediaType = "application/xml",
                Sha256 = sourceHash,
                Content = xml,
                Role = "original-page-markup",
            });
        }

        var imported = new NativeObject
        {
            Id = NewId(EntityKind.PhysicalObject, project, "obj"),
            Metadata = new Dictionary<string, string> { ["page:sourceSha256"] = sourceHash },
            Attachments = attachments,
            Surfaces = [surface],
        };

        var updated = project with { Objects = [.. project.Objects.Append(imported)] };
        projects.Save(updated);
        return (imported, result.Unsupported);
    }

    /// <summary>
    /// Import a bare image as a capture-only surface (no layout, no text). Used for
    /// material where no PAGE/ALTO/hOCR exists — e.g. inscription photographs — so layout
    /// is drawn manually or detected later.
    /// </summary>
    public NativeObject ImportImage(NativeProject project, string imagePath, string surfaceKind = "page")
    {
        if (!File.Exists(imagePath))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = imagePath,
                Message = "Image is not readable on this host.",
            });

        var captureId = NewCaptureId(project);
        var surface = new NativeSurface
        {
            Id = NewId(EntityKind.Surface, project, "surf"),
            Kind = surfaceKind,
            Captures = [new NativeCapture
            {
                Id = captureId,
                Frame = new CoordinateFrame { ImageRef = captureId, ImageChecksum = UnknownChecksum() },
                ImageLocation = Path.GetFullPath(imagePath),
                WidthPx = TryImageDimensions(imagePath)?.Width,
                HeightPx = TryImageDimensions(imagePath)?.Height,
            }],
            Regions = [],
            Lines = [],
        };
        var imported = new NativeObject
        {
            Id = NewId(EntityKind.PhysicalObject, project, "obj"),
            Surfaces = [surface],
        };
        projects.Save(project with { Objects = [.. project.Objects.Append(imported)] });
        return imported;
    }

    /// <summary>
    /// Import a PDF as one physical object with one capture-only surface per rendered page.
    /// Rendering is bounded (DPI/page count/size) and produces traceable PNG derivatives;
    /// no text or layout is guessed from the PDF. Callers must provide a persistent output
    /// directory so captures remain readable after the import.
    /// </summary>
    public (NativeObject Imported, IReadOnlyList<PdfRenderer.RenderedPage> Pages, IReadOnlyList<string> Warnings)
        ImportPdf(NativeProject project, string pdfPath, string outputDirectory,
            string surfaceKind = "page", int dpi = PdfRenderer.DefaultDpi, int maxPages = PdfRenderer.MaxPages,
            bool extractText = false)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = "pdf-output",
                Message = "A persistent output directory is required for rendered PDF pages.",
            });

        // Validate optional reference extraction before writing any page derivatives.
        // Unsupported text orientation must not leave a seemingly successful image cache.
        var textPages = extractText ? PdfTextExtractor.Extract(pdfPath, maxPages) : null;
        var pages = PdfRenderer.Render(pdfPath, outputDirectory, dpi, maxPages);
        var surfaces = new List<NativeSurface>(pages.Count);
        var warnings = new List<string> { $"rendered {pages.Count} PDF page(s)" };
        foreach (var page in pages)
        {
            var captureId = NewCaptureId(project);
            surfaces.Add(new NativeSurface
            {
                Id = NewId(EntityKind.Surface, project, "surf"),
                Kind = surfaceKind,
                Captures = [new NativeCapture
                {
                    Id = captureId,
                    Frame = new CoordinateFrame { ImageRef = captureId, ImageChecksum = page.Checksum },
                    ImageLocation = page.ImagePath,
                    WidthPx = page.WidthPx,
                    HeightPx = page.HeightPx,
                }],
                Regions = [],
                Lines = [],
            });
        }

        int l2Lines = 0;
        if (textPages is not null)
        {
            for (int i = 0; i < Math.Min(surfaces.Count, textPages.Count); i++)
            {
                var textPage = textPages[i];
                var surface = surfaces[i];
                var capture = surface.Captures[0];
                if (textPage.Lines.Count == 0 || capture.WidthPx is not { } width || capture.HeightPx is not { } height)
                    continue;

                double scaleX = width / Math.Max(1, textPage.Width);
                double scaleY = height / Math.Max(1, textPage.Height);
                var regionId = NewId(EntityKind.Region, project, "reg");
                var lines = new List<NativeLine>();
                var revisions = new List<TextRevision>();
                foreach (var ordered in PdfReadingOrder.Order(textPage.Lines, textPage.Width))
                {
                    var textLine = ordered.Line;
                    double left = Math.Clamp(textLine.Left * scaleX, 0, width);
                    double right = Math.Clamp(textLine.Right * scaleX, 0, width);
                    double top = Math.Clamp((textPage.Height - textLine.Top) * scaleY, 0, height);
                    double bottom = Math.Clamp((textPage.Height - textLine.Bottom) * scaleY, 0, height);
                    if (right - left < 1 || bottom - top < 1) continue;

                    var lineId = NewId(EntityKind.Line, project, "ln");
                    var revision = new TextRevision
                    {
                        Id = NewId(EntityKind.TranscriptionRevision, project, "rev-pdf"),
                        ScopeId = lineId,
                        Layer = TextLayer.L2Editorial,
                        Author = new AgentRef { Id = "pdf-text-import", IsModel = false },
                        CreatedAt = DateTimeOffset.UtcNow,
                        Text = textLine.Text,
                    };
                    revisions.Add(revision);
                    lines.Add(new NativeLine
                    {
                        Id = lineId,
                        RegionId = regionId,
                        FrameRef = capture.Id,
                        Polygon = [new(left, top), new(right, top), new(right, bottom), new(left, bottom)],
                        Baseline = [new(left, bottom), new(right, bottom)],
                    });
                }

                if (lines.Count == 0) continue;
                l2Lines += lines.Count;
                surfaces[i] = surface with
                {
                    Regions = [new NativeRegion
                    {
                        Id = regionId,
                        FrameRef = capture.Id,
                        Polygon = [new(0, 0), new(width, 0), new(width, height), new(0, height)],
                        LayoutSource = LayoutSource.Imported,
                        ReadingOrderIndex = 0,
                    }],
                    Lines = lines,
                    Revisions = revisions,
                };
            }

            warnings.Add(l2Lines > 0
                ? $"extracted {l2Lines} PDF text line(s) as L2 reference; PDF text is not visible-text evidence"
                : "no extractable PDF text was found; page captures only");
        }
        else
        {
            warnings.Add("no text/layout was inferred");
        }

        var imported = new NativeObject
        {
            Id = NewId(EntityKind.PhysicalObject, project, "obj"),
            Metadata = new Dictionary<string, string>
            {
                ["source"] = "pdf",
                ["pdf:file"] = Path.GetFileName(pdfPath),
                ["pdf:pages"] = pages.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["pdf:l2Lines"] = l2Lines.ToString(System.Globalization.CultureInfo.InvariantCulture),
            },
            Surfaces = surfaces,
        };
        projects.Save(project with { Objects = [.. project.Objects.Append(imported)] });
        return (imported, pages, warnings);
    }

    /// <summary>
    /// Import the bounded TEI/EpiDoc profile as a text-first surface: L1 visible text and an
    /// optional linked L2 editorial reading, annotations, line numbers, orientation and image
    /// link. The exact source XML is retained as a bounded attachment when it fits, and every
    /// unsupported construct is reported. Lines start without geometry; placement is manual.
    /// </summary>
    /// <summary>
    /// Find an existing object whose retained TEI attachment has exactly this source hash.
    /// Used for an explicit repeat-import/dedup report; detection does not mutate the project.
    /// </summary>
    public NativeObject? FindTeiDuplicate(NativeProject project, string xml, string? pageNumber = null)
    {
        var hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(xml)));
        return project.Objects.FirstOrDefault(o =>
            string.Equals(o.Metadata.GetValueOrDefault("tei:importPage"), pageNumber, StringComparison.Ordinal)
            && (o.Attachments.Any(a => string.Equals(a.Sha256, hash, StringComparison.OrdinalIgnoreCase))
                || (o.Metadata.TryGetValue("tei:sourceSha256", out var stored)
                    && string.Equals(stored, hash, StringComparison.OrdinalIgnoreCase))));
    }

    public (NativeObject Imported, IReadOnlyList<PageXml.ImportIssue> Notes, string? ImageUrl)
        ImportTei(NativeProject project, string teiPath, string imagePath, string surfaceKind = "page",
            string? pageNumber = null)
    {
        if (!File.Exists(teiPath))
            throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = teiPath, Message = "TEI file is not readable." });
        if (!File.Exists(imagePath))
            throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = imagePath, Message = "Surface image is not readable." });

        var xml = File.ReadAllText(teiPath);
        var sourceHash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(xml)));
        TeiXml.TeiImportResult parsed;
        try
        {
            parsed = TeiXml.Import(xml);
        }
        catch (Exception error) when (error is InvalidDataException or System.Xml.XmlException or FormatException)
        {
            // Only source parsing/profile refusal is a client contract error. File IO,
            // decoding and persistence retain their own failure semantics outside here.
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = "tei-import",
                Message = "TEI cannot be imported safely. Supply well-formed TEI with unambiguous physical line boundaries and valid bounded source-context metadata; retain the original source for manual review.",
            });
        }
        using var decoded = ImageDecoder.Decode(imagePath);
        var captureId = NewCaptureId(project);
        var regionId = NewId(EntityKind.Region, project, "reg");
        var lines = new List<NativeLine>();
        var info = (parsed.Document?.Lines ?? []).ToList();
        if (!string.IsNullOrWhiteSpace(pageNumber))
        {
            var selected = info.Where(line => string.Equals(line.PageNumber, pageNumber, StringComparison.Ordinal)).ToList();
            if (selected.Count == 0)
                throw new LeviathanException(new LeviathanError
                {
                    Code = ErrorCode.InputMissing,
                    Scope = pageNumber,
                    Message = "No lines belong to the requested TEI page/folio.",
                });
            info = selected;
        }
        var selectedLineIds = info.Select(line => line.LineId).ToHashSet();
        var selectedRevisions = parsed.Revisions
            .Where(revision => selectedLineIds.Contains(revision.ScopeId)).ToList();
        var selectedRevisionIds = selectedRevisions.Select(revision => revision.Id).ToHashSet();
        var selectedAnnotations = parsed.Annotations
            .Where(annotation => selectedRevisionIds.Contains(annotation.AnchoredRevisionId)).ToList();
        var revisionGroups = selectedRevisions.GroupBy(r => r.ScopeId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var lineInfo in info)
        {
            var revisions = revisionGroups.GetValueOrDefault(lineInfo.LineId) ?? [];
            var l1 = revisions.FirstOrDefault(r => r.Layer == TextLayer.L1Diplomatic);
            var tags = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            foreach (var tag in lineInfo.Tags)
            {
                var pair = tag.Split('=', 2);
                tags[pair[0]] = [pair.Length > 1 ? pair[1] : ""];
            }
            if (!string.IsNullOrWhiteSpace(lineInfo.XmlId)) tags["source-id"] = [lineInfo.XmlId!];
            lines.Add(new NativeLine
            {
                Id = lineInfo.LineId,
                RegionId = regionId,
                FrameRef = captureId,
                Polygon = [],
                Baseline = [],
                LineNumber = lineInfo.Number,
                DiplomaticRevisionId = l1?.Id,
                Direction = lineInfo.Direction,
                Script = lineInfo.Script,
                Languages = lineInfo.Languages,
                Tags = tags,
            });
        }

        // TEI xml:id values are local to a source document, just like PAGE line IDs.
        // Keep them in source-id tags, but namespace every native link for this import.
        var scopeToken = Guid.NewGuid().ToString("N");
        var (_, scopedLines, scopedRevisions) = ScopeImportedIds([], lines, selectedRevisions, scopeToken);
        var revisionMap = selectedRevisions.Zip(scopedRevisions)
            .ToDictionary(pair => pair.First.Id, pair => pair.Second.Id);
        var scopedAnnotations = selectedAnnotations.Select(annotation => annotation with
        {
            Id = ScopeImportedId(annotation.Id, scopeToken),
            AnchoredRevisionId = revisionMap[annotation.AnchoredRevisionId],
            ExpansionRevisionId = annotation.ExpansionRevisionId is { } expansion
                ? revisionMap.GetValueOrDefault(expansion, expansion)
                : null,
        }).ToList();

        var surfaceRefs = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(parsed.Document?.SurfaceId))
            surfaceRefs["tei:surfaceId"] = parsed.Document!.SurfaceId!;
        var surface = new NativeSurface
        {
            Id = NewId(EntityKind.Surface, project, "surf"),
            Kind = surfaceKind,
            ExternalRefs = surfaceRefs,
            Captures = [new NativeCapture
            {
                Id = captureId,
                Frame = new CoordinateFrame { ImageRef = captureId, ImageChecksum = decoded.SourceChecksum },
                ImageLocation = Path.GetFullPath(imagePath),
                WidthPx = decoded.Bitmap.Width,
                HeightPx = decoded.Bitmap.Height,
            }],
            Regions = [new NativeRegion
            {
                Id = regionId,
                FrameRef = captureId,
                Polygon =
                [
                    new PointD(0, 0),
                    new PointD(decoded.Bitmap.Width, 0),
                    new PointD(decoded.Bitmap.Width, decoded.Bitmap.Height),
                    new PointD(0, decoded.Bitmap.Height),
                ],
                LayoutSource = LayoutSource.Manual,
                ReadingOrderIndex = 0,
            }],
            Lines = scopedLines,
            Revisions = scopedRevisions,
            Annotations = scopedAnnotations,
        };

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(parsed.Document?.Title)) metadata["tei:title"] = parsed.Document!.Title!;
        if (!string.IsNullOrWhiteSpace(parsed.Document?.SurfaceId)) metadata["tei:surfaceId"] = parsed.Document!.SurfaceId!;
        if (!string.IsNullOrWhiteSpace(parsed.Document?.Licence)) metadata["tei:licence"] = parsed.Document!.Licence!;
        if (parsed.Document?.LicenceTarget is { } licenceTarget) metadata["tei:licenceTarget"] = licenceTarget;
        metadata["tei:declaredLanguages"] = System.Text.Json.JsonSerializer.Serialize(parsed.Document?.DeclaredLanguages ?? []);
        if (parsed.Document?.PageBreaks is { Count: > 0 } breaks)
        {
            metadata["tei:pageBreaks"] = (pageNumber is null ? breaks.Count : 1)
                .ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(pageNumber)) metadata["tei:folio"] = pageNumber!;
            else if (!string.IsNullOrWhiteSpace(breaks[0].Number)) metadata["tei:folio"] = breaks[0].Number!;
            var selectedBreak = pageNumber is null ? breaks[0] : breaks.FirstOrDefault(pb => pb.Number == pageNumber);
            if (selectedBreak?.Break is { Length: > 0 } folioBreak) metadata["tei:folioBreak"] = folioBreak;
        }
        else if (!string.IsNullOrWhiteSpace(pageNumber)) metadata["tei:folio"] = pageNumber!;
        if (parsed.Unsupported.Count > 0) metadata["tei:unsupported"] = parsed.Unsupported.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        metadata["tei:sourceSha256"] = sourceHash;
        if (!string.IsNullOrWhiteSpace(pageNumber)) metadata["tei:importPage"] = pageNumber!;

        var externalRefs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var idno in parsed.Document?.Idnos ?? [])
        {
            var baseKey = string.IsNullOrWhiteSpace(idno.Type) ? "tei:idno" : $"tei:{idno.Type}";
            var key = baseKey;
            int duplicate = 1;
            while (externalRefs.ContainsKey(key)) key = $"{baseKey}:{++duplicate}";
            externalRefs[key] = idno.Value;
        }
        foreach (var graphic in parsed.Document?.Graphics ?? [])
            externalRefs[$"tei:graphic:{graphic.N ?? "view"}"] = graphic.Url;

        var attachments = new List<NativeAttachment>();
        if (parsed.OriginalMarkup is { } original)
        {
            attachments.Add(new NativeAttachment
            {
                Id = $"att-{Guid.NewGuid().ToString("N")[..12]}",
                Name = Path.GetFileName(teiPath),
                MediaType = "application/tei+xml",
                Sha256 = sourceHash,
                Content = original,
                Role = "original-tei-markup",
            });
        }

        var imported = new NativeObject
        {
            Id = NewId(EntityKind.PhysicalObject, project, "obj"),
            ExternalRefs = externalRefs,
            Track = surfaceKind.Contains("inscription", StringComparison.OrdinalIgnoreCase) ? MaterialTrack.Inscription
                : surfaceKind.Contains("papyrus", StringComparison.OrdinalIgnoreCase) ? MaterialTrack.Papyrus
                : MaterialTrack.Manuscript,
            Metadata = metadata,
            Attachments = attachments,
            Surfaces = [surface],
        };
        projects.Save(project with { Objects = [.. project.Objects.Append(imported)] });
        return (imported, parsed.Unsupported, parsed.Document?.Graphics.FirstOrDefault()?.Url);
    }

    /// <summary>
    /// Bootstrap an inscription record from EpiDoc: the published edition is imported as L2
    /// reference lines (no L1!), the image becomes the capture, and every line starts without
    /// geometry. A human draws the boxes and transcribes the visible text; the edition must
    /// never be presented as the diplomatic reading. Line ids are namespaced per import.
    /// </summary>
    public (NativeObject Imported, IReadOnlyList<PageXml.ImportIssue> Notes, string? ImageUrl)
        ImportEpiDoc(NativeProject project, string epdocPath, string imagePath, string surfaceKind = "inscription-face")
    {
        if (!File.Exists(epdocPath))
            throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = epdocPath, Message = "EpiDoc file is not readable." });
        if (!File.Exists(imagePath))
            throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = imagePath, Message = "Inscription image is not readable." });

        var sourceXml = File.ReadAllText(epdocPath);
        var sourceHash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(sourceXml)));
        var parsed = EpiDocImport.Parse(sourceXml);
        using var decoded = ImageDecoder.Decode(imagePath);
        var token = Guid.NewGuid().ToString("N")[..8];
        var captureId = NewCaptureId(project);
        var regionId = NewId(EntityKind.Region, project, "reg");
        var revisions = new List<TextRevision>();
        var lines = new List<NativeLine>();

        for (int i = 0; i < parsed.Lines.Count; i++)
        {
            var lineId = new EntityId(EntityKind.Line, $"epi-{token}-{i + 1}");
            var revision = new TextRevision
            {
                Id = new EntityId(EntityKind.TranscriptionRevision, $"rev-epi-{token}-{i + 1}"),
                ScopeId = lineId,
                Layer = TextLayer.L2Editorial,
                Author = new AgentRef { Id = "epidoc-import", IsModel = false },
                CreatedAt = DateTimeOffset.UtcNow,
                Text = parsed.Lines[i].Text,
            };
            revisions.Add(revision);
            lines.Add(new NativeLine
            {
                Id = lineId,
                RegionId = regionId,
                FrameRef = captureId,
                Polygon = [],
                Baseline = [],
                LineNumber = parsed.Lines[i].Number,
            });
        }

        var surface = new NativeSurface
        {
            Id = NewId(EntityKind.Surface, project, "surf"),
            Kind = surfaceKind,
            Captures = [new NativeCapture
            {
                Id = captureId,
                Frame = new CoordinateFrame { ImageRef = captureId, ImageChecksum = decoded.SourceChecksum },
                ImageLocation = Path.GetFullPath(imagePath),
                WidthPx = decoded.Bitmap.Width,
                HeightPx = decoded.Bitmap.Height,
            }],
            Regions = [new NativeRegion
            {
                Id = regionId,
                FrameRef = captureId,
                Polygon =
                [
                    new PointD(0, 0),
                    new PointD(decoded.Bitmap.Width, 0),
                    new PointD(decoded.Bitmap.Width, decoded.Bitmap.Height),
                    new PointD(0, decoded.Bitmap.Height),
                ],
                LayoutSource = LayoutSource.Manual,
                ReadingOrderIndex = 0,
            }],
            Lines = lines,
            Revisions = revisions,
        };

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(parsed.Title)) metadata["tei:title"] = parsed.Title!;
        if (!string.IsNullOrWhiteSpace(parsed.Licence)) metadata["tei:licence"] = parsed.Licence!;
        metadata["tei:sourceSha256"] = sourceHash;
        var externalRefs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var idno in parsed.Idnos ?? [])
            externalRefs[string.IsNullOrWhiteSpace(idno.Type) ? "tei:idno" : $"tei:{idno.Type}"] = idno.Value;
        if (!string.IsNullOrWhiteSpace(parsed.ImageUrl)) externalRefs["tei:graphic:screen"] = parsed.ImageUrl!;
        var attachments = new List<NativeAttachment>();
        if (parsed.OriginalMarkup is { } original)
        {
            attachments.Add(new NativeAttachment
            {
                Id = $"att-{Guid.NewGuid().ToString("N")[..12]}",
                Name = Path.GetFileName(epdocPath),
                MediaType = "application/tei+xml",
                Sha256 = sourceHash,
                Content = original,
                Role = "original-epidoc-markup",
            });
        }

        var imported = new NativeObject
        {
            Id = NewId(EntityKind.PhysicalObject, project, "obj"),
            ExternalRefs = externalRefs,
            Track = MaterialTrack.Inscription,
            Metadata = metadata,
            Attachments = attachments,
            Surfaces = [surface],
        };
        projects.Save(project with { Objects = [.. project.Objects.Append(imported)] });
        return (imported, parsed.Notes, parsed.ImageUrl);
    }

    /// <summary>
    /// Batch bootstrap from an EpiDoc record directory: imports records whose image can be
    /// resolved locally and reports the rest (missing images are never guessed). Intended for
    /// building an inscription pilot after images are fetched under their declared names.
    /// </summary>
    public (int Imported, int Skipped, IReadOnlyList<string> MissingImages, IReadOnlyList<string> Notes)
        ImportEpiDocDirectory(NativeProject project, string recordDirectory, string imageDirectory,
            string surfaceKind = "inscription-face", int limit = 0)
    {
        if (!Directory.Exists(recordDirectory))
            throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = recordDirectory, Message = "Record directory not found." });
        if (!Directory.Exists(imageDirectory))
            throw new LeviathanException(new LeviathanError { Code = ErrorCode.InputMissing, Scope = imageDirectory, Message = "Image directory not found." });

        var records = Directory.GetFiles(recordDirectory, "*.xml").OrderBy(f => f, StringComparer.Ordinal).ToList();
        if (limit > 0) records = [.. records.Take(limit)];

        int imported = 0, skipped = 0;
        var missing = new List<string>();
        var notes = new List<string>();
        foreach (var record in records)
        {
            string? image = null;
            try
            {
                var parsed = EpiDocImport.Parse(File.ReadAllText(record));
                var stem = Path.GetFileNameWithoutExtension(record);
                var candidates = new List<string>();
                if (!string.IsNullOrWhiteSpace(parsed.ImageUrl))
                    candidates.Add(Path.Combine(imageDirectory, Path.GetFileName(parsed.ImageUrl)));
                candidates.AddRange(new[]
                {
                    Path.Combine(imageDirectory, stem + ".jpg"),
                    Path.Combine(imageDirectory, stem + ".png"),
                    Path.Combine(imageDirectory, stem + "_w800.jpg"),
                });
                image = candidates.FirstOrDefault(File.Exists);
                if (image is null)
                {
                    skipped++;
                    if (missing.Count < 20) missing.Add(stem);
                    continue;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                skipped++;
                if (notes.Count < 20) notes.Add($"{Path.GetFileName(record)}: {ex.Message}");
                continue;
            }

            ImportEpiDoc(project, record, image, surfaceKind);
            project = projects.Load(project.Id); // ImportEpiDoc saves; keep appending to the reloaded document
            imported++;
        }

        return (imported, skipped, missing, notes);
    }

    /// <summary>
    /// Lines with no geometry yet (text-first imports such as EpiDoc), with their latest L2
    /// reference text so the editor can offer a placement list.
    /// </summary>
    public IReadOnlyList<(NativeLine Line, string ReferenceText)> UnplacedLines(NativeProject project, EntityId surfaceId)
    {
        var surface = FindSurface(project, surfaceId);
        var latestL2 = surface.Revisions
            .Where(r => r.Layer == TextLayer.L2Editorial)
            .GroupBy(r => r.ScopeId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.CreatedAt).First().Text);
        return
        [
            .. surface.Lines
                .Where(l => l.Polygon.Count < 3)
                .Select(l => (l, latestL2.GetValueOrDefault(l.Id, ""))),
        ];
    }

    /// <summary>
    /// Import a legacy OCR export (ALTO or hOCR) plus its page image as a new surface.
    /// Bounded subset only; unsupported elements are reported to the caller (never silently
    /// dropped). Text is imported as L1 diplomatic with an explicit importer author.
    /// </summary>
    public (NativeObject Imported, IReadOnlyList<PageXml.ImportIssue> Unsupported) ImportLegacyLayout(
        NativeProject project, string documentPath, string imagePath, string format, string surfaceKind = "page")
    {
        if (!File.Exists(documentPath))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = documentPath,
                Message = "Import document is not readable on this host.",
            });
        if (!File.Exists(imagePath))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = imagePath,
                Message = "Page image is not readable on this host.",
            });

        var captureId = NewCaptureId(project);
        var content = File.ReadAllText(documentPath);
        var scopeToken = captureId.Suffix.Length >= 8 ? captureId.Suffix[^8..] : captureId.Suffix;

        (IReadOnlyList<NativeRegion> Regions, IReadOnlyList<NativeLine> Lines,
            IReadOnlyList<TextRevision> Revisions, IReadOnlyList<PageXml.ImportIssue> Unsupported) parsed;
        int? declaredWidth = null, declaredHeight = null;
        switch (ResolveLegacyFormat(format, content))
        {
            case LegacyFormat.Alto:
                var alto = AltoXml.Import(content, captureId, UnknownChecksum());
                parsed = FromAlto(alto);
                declaredWidth = alto.WidthPx;
                declaredHeight = alto.HeightPx;
                break;
            case LegacyFormat.Hocr:
                parsed = FromHocr(HocrFormat.Import(content, captureId, UnknownChecksum()));
                break;
            case LegacyFormat.Abbyy:
                parsed = FromAbbyy(AbbyyXml.Import(content, captureId, UnknownChecksum()));
                break;
            default:
                throw new LeviathanException(new LeviathanError
                {
                    Code = ErrorCode.SchemaVersionUnsupported,
                    Scope = format,
                    Message = "Legacy-layout import supports 'auto', 'alto' and 'hocr'.",
                });
        }

        var (scopedRegions, scopedLines, scopedRevisions) =
            ScopeImportedIds(parsed.Regions, parsed.Lines, parsed.Revisions, scopeToken);
        var legacyDimensions = TryImageDimensions(imagePath)
            ?? (declaredWidth is { } width && declaredHeight is { } height ? (width, height) : ((int Width, int Height)?)null);
        var surface = new NativeSurface
        {
            Id = NewId(EntityKind.Surface, project, "surf"),
            Kind = surfaceKind,
            Captures = [new NativeCapture
            {
                Id = captureId,
                Frame = new CoordinateFrame { ImageRef = captureId, ImageChecksum = UnknownChecksum() },
                ImageLocation = Path.GetFullPath(imagePath),
                WidthPx = legacyDimensions?.Width,
                HeightPx = legacyDimensions?.Height,
            }],
            Regions = scopedRegions,
            Lines = scopedLines,
            Revisions = scopedRevisions,
        };

        var sourceHash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(content)));
        var attachments = new List<NativeAttachment>();
        if (System.Text.Encoding.UTF8.GetByteCount(content) <= TeiXml.MaxAttachmentBytes)
        {
            attachments.Add(new NativeAttachment
            {
                Id = $"att-{Guid.NewGuid().ToString("N")[..12]}",
                Name = Path.GetFileName(documentPath),
                MediaType = format.Equals("hocr", StringComparison.OrdinalIgnoreCase)
                    ? "text/html" : "application/xml",
                Sha256 = sourceHash,
                Content = content,
                Role = $"original-{format}-markup",
            });
        }

        var imported = new NativeObject
        {
            Id = NewId(EntityKind.PhysicalObject, project, "obj"),
            Metadata = new Dictionary<string, string> { ["legacy:sourceSha256"] = sourceHash },
            Attachments = attachments,
            Surfaces = [surface],
        };
        projects.Save(project with { Objects = [.. project.Objects.Append(imported)] });
        return (imported, parsed.Unsupported);
    }


    /// <summary>
    /// Namespace document-local IDs (PAGE/ALTO/hOCR carry IDs like "ln1") with a token
    /// unique to the import. Without this, two projects imported from similar documents
    /// share IDs and globally addressed endpoints can act on the wrong project.
    /// </summary>
    private static (IReadOnlyList<NativeRegion> Regions, IReadOnlyList<NativeLine> Lines,
        IReadOnlyList<TextRevision> Revisions) ScopeImportedIds(
        IReadOnlyList<NativeRegion> regions, IReadOnlyList<NativeLine> lines,
        IReadOnlyList<TextRevision> revisions, string token)
    {
        var regionMap = regions.ToDictionary(r => r.Id, r => ScopeImportedId(r.Id, token));
        var lineMap = lines.ToDictionary(l => l.Id, l => ScopeImportedId(l.Id, token));
        var revisionMap = revisions.ToDictionary(r => r.Id, r => ScopeImportedId(r.Id, token));

        var scopedRegions = regions.Select(r => r with { Id = regionMap[r.Id] }).ToList();
        var scopedLines = lines.Select(l => l with
        {
            Id = lineMap[l.Id],
            RegionId = regionMap.GetValueOrDefault(l.RegionId, l.RegionId),
            DiplomaticRevisionId = l.DiplomaticRevisionId is { } rid
                ? revisionMap.GetValueOrDefault(rid, rid)
                : null,
        }).ToList();
        var scopedRevisions = revisions.Select(r => r with
        {
            Id = revisionMap[r.Id],
            ScopeId = lineMap.GetValueOrDefault(r.ScopeId, r.ScopeId),
            ParentRevisionId = r.ParentRevisionId is { } parent
                ? revisionMap.GetValueOrDefault(parent, parent)
                : null,
        }).ToList();
        return (scopedRegions, scopedLines, scopedRevisions);
    }

    private static EntityId ScopeImportedId(EntityId id, string token)
    {
        var suffix = ExternalIds.Sanitize(id.Suffix);
        int maxBase = 63 - token.Length - 1;
        if (suffix.Length > maxBase)
        {
            // Truncation alone can erase the only distinguishing part (e.g. -l1/-l2).
            var digest = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(id.Suffix)))[..12];
            suffix = suffix[..(maxBase - digest.Length - 1)] + "-" + digest;
        }
        return new EntityId(id.Kind, suffix + "-" + token);
    }

    private static LegacyFormat ResolveLegacyFormat(string format, string content) =>
        format.ToLowerInvariant() switch
        {
            "auto" => LegacyFormats.Detect(content),
            "alto" => LegacyFormat.Alto,
            "hocr" => LegacyFormat.Hocr,
            "abbyy" => LegacyFormat.Abbyy,
            _ => throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.SchemaVersionUnsupported,
                Scope = format,
                Message = "Legacy-layout import supports 'auto', 'alto' and 'hocr'.",
            }),
        };

    private static (IReadOnlyList<NativeRegion>, IReadOnlyList<NativeLine>,
        IReadOnlyList<TextRevision>, IReadOnlyList<PageXml.ImportIssue>) FromAlto(AltoXml.AltoImportResult result) =>
        (result.Regions, result.Lines, result.Revisions, result.Unsupported);

    private static (IReadOnlyList<NativeRegion>, IReadOnlyList<NativeLine>,
        IReadOnlyList<TextRevision>, IReadOnlyList<PageXml.ImportIssue>) FromHocr(HocrFormat.HocrImportResult result) =>
        (result.Regions, result.Lines, result.Revisions, result.Unsupported);

    private static (IReadOnlyList<NativeRegion>, IReadOnlyList<NativeLine>,
        IReadOnlyList<TextRevision>, IReadOnlyList<PageXml.ImportIssue>) FromAbbyy(AbbyyXml.ImportResult result) =>
        (result.Regions, result.Lines, result.Revisions, result.Unsupported);

    /// <summary>
    /// Import a IIIF Presentation 3 manifest as one physical object whose surface carries
    /// every canvas as a SEPARATE capture (views are never coordinate-merged). Attribution
    /// from the manifest's requiredStatement is retained per capture; image bytes are not
    /// fetched here (offline-safe: the manifest is the record, images load on demand).
    /// </summary>
    public (NativeObject Imported, IiifImport.ManifestResult Manifest, IReadOnlyList<string> Warnings) ImportIiif(
        NativeProject project, string manifestPath)
    {
        var manifest = IiifImport.ParseManifest(File.ReadAllText(manifestPath));
        var warnings = new List<string>();

        var canvases = manifest.Canvases.Where(c => c.Images.Count > 0).ToList();
        if (canvases.Count == 0)
            warnings.Add("manifest yielded no canvases with image bodies; nothing imported");

        var captures = new List<NativeCapture>();
        for (int i = 0; i < canvases.Count; i++)
        {
            var canvas = canvases[i];
            var captureId = new EntityId(EntityKind.Capture, $"{Guid.NewGuid().ToString("N")[..12]}");
            captures.Add(new NativeCapture
            {
                Id = captureId,
                Frame = new CoordinateFrame { ImageRef = captureId, ImageChecksum = UnknownChecksum() },
                ImageLocation = canvas.Images[0].ImageUrl,
                RightsEvidenceRef = manifest.RightsStatement,
            });
            if (canvas.Images.Count > 1)
                warnings.Add($"canvas {canvas.CanvasId} exposes {canvas.Images.Count} image bodies; first used");
        }

        var surface = new NativeSurface
        {
            Id = NewId(EntityKind.Surface, project, "surf"),
            Kind = "inscription-face",
            Captures = captures,
            Regions = [],
            Lines = [],
        };

        var imported = new NativeObject
        {
            Id = NewId(EntityKind.PhysicalObject, project, "obj"),
            ExternalRefs = manifest.ManifestId is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string> { ["iiif-manifest"] = manifest.ManifestId },
            Surfaces = [surface],
        };

        if (captures.Count > 0)
        {
            var updated = project with { Objects = [.. project.Objects.Append(imported)] };
            projects.Save(updated);
        }
        return (imported, manifest, warnings);
    }

    public (string Text, LossReport Report) ExportDiplomatic(NativeProject project, EntityId surfaceId)
    {
        var surface = FindSurface(project, surfaceId);
        return DiplomaticExport.Export(surface, surface.Revisions);
    }

    public string ExportPage(NativeProject project, EntityId surfaceId)
    {
        var surface = FindSurface(project, surfaceId);
        return PageXml.Export(surface, surface.Captures[0], surface.Revisions);
    }

    public string ExportAlto(NativeProject project, EntityId surfaceId)
    {
        var surface = FindSurface(project, surfaceId);
        return AltoXml.Export(surface, surface.Captures[0], surface.Revisions);
    }

    public string ExportHocr(NativeProject project, EntityId surfaceId)
    {
        var surface = FindSurface(project, surfaceId);
        return HocrFormat.Export(surface, surface.Captures[0], surface.Revisions);
    }

    public string ExportAbbyy(NativeProject project, EntityId surfaceId)
    {
        var surface = FindSurface(project, surfaceId);
        return AbbyyXml.Export(surface, surface.Captures[0], surface.Revisions);
    }

    public string ExportTei(NativeProject project, EntityId surfaceId, string title) =>
        ExportTeiWithReport(project, surfaceId, title).Xml;

    /// <summary>TEI export with the mandatory machine-readable loss report.</summary>
    public TeiXml.TeiExportResult ExportTeiWithReport(NativeProject project, EntityId surfaceId, string title)
    {
        var surface = FindSurface(project, surfaceId);
        var owner = project.Objects.FirstOrDefault(o => o.Surfaces.Any(s => s.Id == surfaceId));
        var revisionsById = surface.Revisions.ToDictionary(r => r.Id);
        var latestL2 = surface.Revisions
            .Where(r => r.Layer == TextLayer.L2Editorial)
            .GroupBy(r => r.ScopeId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.CreatedAt).First());
        var lines = surface.Lines.Select(line =>
        {
            var l1 = line.DiplomaticRevisionId is { } rid ? revisionsById.GetValueOrDefault(rid) : null;
            var l2 = latestL2.GetValueOrDefault(line.Id);
            var tags = line.Tags.Select(t => $"{t.Key}={string.Join('|', t.Value)}").ToList();
            var info = new TeiXml.TeiLineInfo(line.Id, line.LineNumber,
                line.Tags.GetValueOrDefault("source-id")?.FirstOrDefault(), line.Direction, line.Script,
                line.Languages, tags);
            return new TeiXml.TeiExportLine(line.Id, l1, l2, surface.Annotations, info);
        }).ToList();

        var sourceGraphic = owner?.ExternalRefs.GetValueOrDefault("tei:graphic:screen");
        var graphics = surface.Captures
            .Where(c => !string.IsNullOrWhiteSpace(c.ImageLocation))
            .Select((c, index) => new TeiXml.TeiGraphic(
                index == 0 && !string.IsNullOrWhiteSpace(sourceGraphic) ? sourceGraphic! : c.ImageLocation,
                c.Id.Suffix, c.WidthPx, c.HeightPx))
            .ToList();
        var idnos = (owner?.ExternalRefs ?? new Dictionary<string, string>())
            .Where(pair => pair.Key.StartsWith("tei:", StringComparison.Ordinal) && !pair.Key.StartsWith("tei:graphic:", StringComparison.Ordinal))
            .Select(pair => new TeiXml.TeiIdno(
                pair.Key.StartsWith("tei:idno", StringComparison.Ordinal) ? "idno" : pair.Key["tei:".Length..],
                pair.Value))
            .ToList();
        var responsibilities = surface.Revisions
            .Where(r => r.Layer == TextLayer.L1Diplomatic)
            .GroupBy(r => r.Author.Id, StringComparer.Ordinal)
            .Select(g => new TeiXml.TeiResponsibility("transcriber", g.Key,
                g.Min(r => r.CreatedAt).ToString("O")))
            .ToList();
        var folio = owner?.Metadata.GetValueOrDefault("tei:folio");
        var folioBreak = owner?.Metadata.GetValueOrDefault("tei:folioBreak");
        var licenceTarget = owner?.Metadata.GetValueOrDefault("tei:licenceTarget");
        var sourceHash = owner?.Metadata.GetValueOrDefault("tei:sourceSha256");
        IReadOnlyList<string>? declaredLanguages = null;
        if (owner?.Metadata.GetValueOrDefault("tei:declaredLanguages") is { } languageMetadata)
        {
            try
            {
                // A valid 128×256-code JSON serialization fits comfortably inside
                // this field bound, even with escaped Unicode. Refuse oversized
                // stored payloads before allocating a deserialized array.
                if (languageMetadata.Length > 256 * 1024)
                    throw new InvalidDataException("Declared language metadata exceeds the bounded JSON field size.");
                var values = System.Text.Json.JsonSerializer.Deserialize<string[]>(languageMetadata)
                    ?? throw new InvalidDataException("Declared language metadata must be a JSON array.");
                declaredLanguages = TeiXml.CheckedDeclaredLanguages(values);
            }
            catch (Exception error) when (error is System.Text.Json.JsonException or InvalidDataException)
            {
                throw new LeviathanException(new LeviathanError
                {
                    Code = ErrorCode.StageContractViolation,
                    Scope = "tei-export",
                    Message = "Stored TEI declared-language metadata is invalid or exceeds the bounded profile. Retain the native project and repair the metadata explicitly before exporting.",
                });
            }
        }
        if (folioBreak is null && licenceTarget is null && sourceHash is not null
            && owner?.Attachments.FirstOrDefault(a => a.Role == "original-tei-markup"
                && string.Equals(a.Sha256, sourceHash, StringComparison.OrdinalIgnoreCase)) is { } source)
            (licenceTarget, folioBreak) = TeiXml.ReadRetainedSourceLinks(source.Content, folio);

        return TeiXml.ExportDocument(lines, title, new TeiXml.TeiExportContext(
            SurfaceId: owner?.Metadata.GetValueOrDefault("tei:surfaceId") ?? surface.Id.Suffix,
            Graphics: graphics,
            Idnos: idnos,
            Licence: owner?.Metadata.GetValueOrDefault("tei:licence"),
            Responsibilities: responsibilities,
            GeometryCount: surface.Lines.Count(l => l.Baseline.Count > 0 || l.Polygon.Count > 0),
            Orientations: surface.Lines.ToDictionary(l => l.Id,
                l => new TeiXml.TeiLineOrientation(l.Id, l.Direction, l.Script)),
            Folio: folio,
            PageBreakCount: int.TryParse(owner?.Metadata.GetValueOrDefault("tei:pageBreaks"),
                out var pageBreaks) ? pageBreaks : 0,
            FolioBreak: folioBreak,
            LicenceTarget: licenceTarget,
            DeclaredLanguages: declaredLanguages));
    }

    /// <summary>
    /// Searchable-PDF convenience export: the original capture is drawn as the page and each
    /// line's L1 text is placed invisibly at its source rectangle. The returned loss report
    /// keeps the scholarly layers and annotations distinct from this visual/search artifact.
    /// </summary>
    public (byte[] Pdf, LossReport Report) ExportSearchablePdf(NativeProject project, EntityId surfaceId)
        => ExportSearchablePdf(project, surfaceId, includeCaptureImage: true);

    internal (byte[] Pdf, LossReport Report) ExportSearchablePdf(NativeProject project, EntityId surfaceId,
        bool includeCaptureImage, byte[]? captureBytes = null)
    {
        var surface = FindSurface(project, surfaceId);
        var capture = surface.Captures.FirstOrDefault()
            ?? throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = surfaceId.ToString(),
                Message = "Surface has no capture for PDF export.",
            });
        int width = capture.WidthPx ?? 0;
        int height = capture.HeightPx ?? 0;

        var revisions = surface.Revisions.ToDictionary(r => r.Id);
        var lines = new List<SearchablePdf.TextLine>();
        int unplaced = 0;
        foreach (var line in surface.Lines)
        {
            if (line.DiplomaticRevisionId is not { } revisionId
                || !revisions.TryGetValue(revisionId, out var revision)
                || string.IsNullOrWhiteSpace(revision.Text))
                continue;
            IReadOnlyList<PointD> geometry = line.Polygon.Count >= 3 ? line.Polygon : line.Baseline;
            if (geometry.Count == 0)
            {
                unplaced++;
                continue;
            }
            double left = geometry.Min(p => p.X), right = geometry.Max(p => p.X);
            double top = geometry.Min(p => p.Y), bottom = geometry.Max(p => p.Y);
            lines.Add(new SearchablePdf.TextLine(revision.Text, left, top, right, bottom));
        }

        if (width <= 0 || height <= 0)
        {
            var points = surface.Lines.SelectMany(line => line.Polygon.Count > 0 ? line.Polygon : line.Baseline).ToList();
            if (points.Count == 0)
                throw new LeviathanException(new LeviathanError
                {
                    Code = ErrorCode.StageContractViolation,
                    Scope = surfaceId.ToString(),
                    Message = "Capture dimensions or line geometry are required for PDF export.",
                });
            width = Math.Max(1, (int)Math.Ceiling(points.Max(p => p.X)));
            height = Math.Max(1, (int)Math.Ceiling(points.Max(p => p.Y)));
        }

        var active = surface.Annotations
            .Where(a => !surface.AnnotationRetractions.Any(r => r.AnnotationId == a.Id))
            .ToList();
        var entries = new List<LossEntry>
        {
            new("capture-image", includeCaptureImage ? 0 : 1,
                "the capture raster was excluded from the PDF"),
            new("editorial-layer", surface.Revisions.Count(r => r.Layer == TextLayer.L2Editorial),
                "L2 editorial readings and their links are not represented in the PDF"),
            new("uncertainty", active.Count(a => a.Kind is AnnotationKind.Unclear or AnnotationKind.Illegible or AnnotationKind.Lost),
                "unclear/illegible/lost annotations are not searchable text in the PDF"),
            new("alternatives", active.Count(a => a.Kind == AnnotationKind.AlternativeReading),
                "alternative readings are not represented in the PDF"),
            new("provenance", surface.Revisions.Count,
                "revision history and authorship remain in the native project"),
            new("unplaced-lines", unplaced,
                "lines without geometry were omitted from the PDF text layer"),
        };
        var report = new LossReport
        {
            Format = "searchable-pdf",
            ContractVersion = ContractVersion.Current,
            Entries = [.. entries.Where(entry => entry.Count > 0)],
        };
        var page = new SearchablePdf.Page(includeCaptureImage ? capture.ImageLocation : null, width, height, lines)
        {
            ImageBytes = captureBytes,
        };
        var pdf = SearchablePdf.ExportPages([page], $"LeviathanText {surface.Id}");
        return (pdf, report);
    }

    /// <summary>Searchable PDF for every surface in a project, one PDF page per surface.</summary>
    public (byte[] Pdf, LossReport Report) ExportSearchablePdfProject(NativeProject project, string title)
    {
        var pages = new List<SearchablePdf.Page>();
        int unplaced = 0, omitted = 0, editorial = 0, uncertainty = 0, alternatives = 0, revisions = 0;
        foreach (var surface in project.Objects.SelectMany(o => o.Surfaces))
        {
            var capture = surface.Captures.FirstOrDefault();
            var revisionMap = surface.Revisions.GroupBy(r => r.Id).ToDictionary(g => g.Key, g => g.First());
            var lines = new List<SearchablePdf.TextLine>();
            foreach (var line in surface.Lines)
            {
                if (line.DiplomaticRevisionId is not { } revisionId
                    || !revisionMap.TryGetValue(revisionId, out var revision)
                    || string.IsNullOrWhiteSpace(revision.Text))
                    continue;
                IReadOnlyList<PointD> geometry = line.Polygon.Count >= 3 ? line.Polygon : line.Baseline;
                if (geometry.Count == 0) { unplaced++; continue; }
                lines.Add(new SearchablePdf.TextLine(revision.Text,
                    geometry.Min(p => p.X), geometry.Min(p => p.Y),
                    geometry.Max(p => p.X), geometry.Max(p => p.Y)));
            }
            int width = capture?.WidthPx ?? 0, height = capture?.HeightPx ?? 0;
            if (width <= 0 || height <= 0)
            {
                var points = surface.Lines.SelectMany(line => line.Polygon.Count > 0 ? line.Polygon : line.Baseline).ToList();
                if (points.Count == 0) { omitted++; continue; }
                width = Math.Max(1, (int)Math.Ceiling(points.Max(p => p.X)));
                height = Math.Max(1, (int)Math.Ceiling(points.Max(p => p.Y)));
            }
            pages.Add(new SearchablePdf.Page(capture?.ImageLocation, width, height, lines));
            editorial += surface.Revisions.Count(r => r.Layer == TextLayer.L2Editorial);
            revisions += surface.Revisions.Count;
            var active = surface.Annotations
                .Where(a => !surface.AnnotationRetractions.Any(r => r.AnnotationId == a.Id)).ToList();
            uncertainty += active.Count(a => a.Kind is AnnotationKind.Unclear or AnnotationKind.Illegible or AnnotationKind.Lost);
            alternatives += active.Count(a => a.Kind == AnnotationKind.AlternativeReading);
        }
        if (pages.Count == 0)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = project.Id.ToString(),
                Message = "No surface has capture dimensions or line geometry for PDF export.",
            });
        var entries = new List<LossEntry>
        {
            new("editorial-layer", editorial, "L2 readings are not represented in the PDF"),
            new("uncertainty", uncertainty, "uncertainty annotations are not searchable text"),
            new("alternatives", alternatives, "alternative readings are not represented"),
            new("provenance", revisions, "revision history remains in the native project"),
            new("unplaced-lines", unplaced, "lines without geometry were omitted"),
            new("omitted-surfaces", omitted, "surfaces without dimensions or geometry were omitted"),
        };
        var report = new LossReport
        {
            Format = "searchable-pdf",
            ContractVersion = ContractVersion.Current,
            Entries = [.. entries.Where(e => e.Count > 0)],
        };
        return (SearchablePdf.ExportPages(pages, title), report);
    }

    private static NativeSurface FindSurface(NativeProject project, EntityId surfaceId) =>
        project.Objects.SelectMany(o => o.Surfaces).FirstOrDefault(s => s.Id == surfaceId)
            ?? throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = surfaceId.ToString(),
                Message = "No such surface in the project.",
            });

    private static EntityId NewCaptureId(NativeProject project) =>
        NewId(EntityKind.Capture, project, "cap");

    private static EntityId NewId(EntityKind kind, NativeProject project, string prefix)
    {
        var n = project.Objects.Count + 1;
        return new EntityId(kind, $"{prefix}{n:x4}-{Guid.NewGuid().ToString("N")[..8]}");
    }

    private static (int Width, int Height)? TryImageDimensions(string path)
    {
        try { return File.Exists(path) ? ImageDecoder.Dimensions(path) : null; }
        catch (Exception) { return null; }
    }

    private static Checksum UnknownChecksum() => new() { Algorithm = "unavailable", Digest = "pending" };
}
