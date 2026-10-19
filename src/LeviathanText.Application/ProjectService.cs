using LeviathanText.Contracts;
using LeviathanText.Formats;

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
        var result = PageXml.Import(xml, captureId, UnknownChecksum());

        // Resolve the referenced image next to the PAGE file (name from the document,
        // falling back to the XML basename with common extensions). Unresolved names are
        // kept as-is so the manifest still records what the document declared.
        var directory = Path.GetDirectoryName(Path.GetFullPath(pageXmlPath))!;
        var imageName = result.ImageFilename ?? Path.GetFileNameWithoutExtension(pageXmlPath) + ".jpg";
        var imagePath = new[] { imageName, Path.GetFileNameWithoutExtension(pageXmlPath) + ".png",
                                Path.GetFileNameWithoutExtension(pageXmlPath) + ".jpg",
                                Path.GetFileNameWithoutExtension(pageXmlPath) + ".tif" }
            .Select(name => Path.Combine(directory, name))
            .FirstOrDefault(File.Exists) ?? imageName;

        var surface = new NativeSurface
        {
            Id = NewId(EntityKind.Surface, project, "surf"),
            Kind = surfaceKind,
            Captures = [new NativeCapture
            {
                Id = captureId,
                Frame = new CoordinateFrame { ImageRef = captureId, ImageChecksum = UnknownChecksum() },
                ImageLocation = imagePath,
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

    public string ExportTei(NativeProject project, EntityId surfaceId, string title)
    {
        var surface = FindSurface(project, surfaceId);
        return TeiXml.Export(
            surface.Lines.Select(l => (l.Id, l.DiplomaticRevisionId is { } rid ? surface.Revisions.FirstOrDefault(r => r.Id == rid) : null,
                (IReadOnlyList<Annotation>)surface.Annotations.Where(a => a.AnchoredRevisionId == l.DiplomaticRevisionId).ToList())),
            title);
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

    private static Checksum UnknownChecksum() => new() { Algorithm = "unavailable", Digest = "pending" };
}
