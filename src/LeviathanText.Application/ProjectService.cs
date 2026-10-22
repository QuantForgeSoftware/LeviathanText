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
        store.SaveProject(project);
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

    public void Save(NativeProject project) => store.SaveProject(project);
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

        var imported = new NativeObject
        {
            Id = NewId(EntityKind.PhysicalObject, project, "obj"),
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
        if (extractText)
        {
            var textPages = PdfTextExtractor.Extract(pdfPath, maxPages);
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
                foreach (var textLine in textPage.Lines)
                {
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

        var parsed = EpiDocImport.Parse(File.ReadAllText(epdocPath));
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

        var imported = new NativeObject
        {
            Id = NewId(EntityKind.PhysicalObject, project, "obj"),
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

        var imported = new NativeObject
        {
            Id = NewId(EntityKind.PhysicalObject, project, "obj"),
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
        static EntityId Scoped(EntityId id, string token)
        {
            var suffix = ExternalIds.Sanitize(id.Suffix);
            int maxBase = 63 - token.Length - 1;
            if (suffix.Length > maxBase) suffix = suffix[..Math.Max(1, maxBase)];
            if (suffix.Length == 0) suffix = "id";
            return new EntityId(id.Kind, suffix + "-" + token);
        }

        var regionMap = regions.ToDictionary(r => r.Id, r => Scoped(r.Id, token));
        var lineMap = lines.ToDictionary(l => l.Id, l => Scoped(l.Id, token));
        var revisionMap = revisions.ToDictionary(r => r.Id, r => Scoped(r.Id, token));

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
        }).ToList();
        return (scopedRegions, scopedLines, scopedRevisions);
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

    public string ExportTei(NativeProject project, EntityId surfaceId, string title)
    {
        var surface = FindSurface(project, surfaceId);
        return TeiXml.Export(
            surface.Lines.Select(l => (l.Id, l.DiplomaticRevisionId is { } rid ? surface.Revisions.FirstOrDefault(r => r.Id == rid) : null,
                (IReadOnlyList<Annotation>)surface.Annotations.Where(a => a.AnchoredRevisionId == l.DiplomaticRevisionId).ToList())),
            title,
            surface.Lines.ToDictionary(l => l.Id, l => new TeiXml.TeiLineOrientation(l.Id, l.Direction, l.Script)));
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
