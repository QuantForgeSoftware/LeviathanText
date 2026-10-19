using System.IO.Compression;
using System.Text;
using System.Text.Json;
using LeviathanText.Contracts;
using LeviathanText.Contracts.Serialization;
using LeviathanText.Formats;

namespace LeviathanText.Application;

/// <summary>
/// Export one project as a self-describing bundle: native project JSON, training export,
/// and per-surface diplomatic/TEI/ALTO/hOCR exports, plus a manifest that states what the
/// bundle contains and which layers mean what. Lossy exports keep their loss reports
/// (a bundle that went through a lossy conversion says so).
/// </summary>
public static class ProjectBundle
{
    public sealed record Result(string Path, int FileCount, long SizeBytes);

    public static Result Export(NativeProject project, string targetZipPath)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetZipPath);

        var directory = Path.GetDirectoryName(Path.GetFullPath(targetZipPath));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        var interchange = new InterchangeService(new ProjectService(ProjectStoreFree.Instance));

        using var stream = File.Create(targetZipPath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        var files = new List<string>();

        void Add(string name, string content)
        {
            var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write(content);
            files.Add(name);
        }

        Add("project.json", NativeJson.Save(project));
        Add("training-export.json", TrainingDataExport.ToJson(TrainingDataExport.Export(project)));

        foreach (var surface in project.Objects.SelectMany(o => o.Surfaces))
        {
            var prefix = $"surfaces/{surface.Id}";
            Add($"{prefix}/diplomatic.txt", interchange.ExportDiplomatic(project, surface.Id).Text);
            Add($"{prefix}/tei.xml", interchange.ExportTei(project, surface.Id, $"LeviathanText export {surface.Id}"));
            if (surface.Captures.Count > 0)
            {
                Add($"{prefix}/alto.xml", AltoXml.Export(surface, surface.Captures[0], surface.Revisions));
                Add($"{prefix}/hocr.html", HocrFormat.Export(surface, surface.Captures[0], surface.Revisions));
            }
        }

        var manifest = new
        {
            format = "leviathantext-bundle",
            contractVersion = ContractVersion.Current,
            createdAt = DateTimeOffset.UtcNow,
            project = new { id = project.Id.ToString(), name = project.Name },
            surfaces = project.Objects.SelectMany(o => o.Surfaces).Select(s => new
            {
                id = s.Id.ToString(),
                kind = s.Kind,
                captures = s.Captures.Count,
                regions = s.Regions.Count,
                lines = s.Lines.Count,
                revisions = s.Revisions.Count,
                annotations = s.Annotations.Count,
            }),
            files,
            transcriptionProfile = new
            {
                id = Core.Unicode.GreekTextProfile.ProfileId,
                version = Core.Unicode.GreekTextProfile.Version,
            },
            layers = new
            {
                l0 = "raw model output; never an edition",
                l1 = "diplomatic transcription (visible text)",
                l2 = "editorial reading/accepted proposals",
            },
            note = "Lossy conversions (plain text, bounded TEI/ALTO/hOCR) report what they drop; " +
                   "the native project.json is authoritative. Image files are NOT bundled — " +
                   "capture paths and checksums in project.json locate them.",
        };
        Add("manifest.json", JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

        return new Result(targetZipPath, files.Count, new FileInfo(targetZipPath).Length);
    }

    /// <summary>ProjectStore adapter for read-only exporters (never saves).</summary>
    private sealed class ProjectStoreFree : IProjectStore
    {
        public static readonly ProjectStoreFree Instance = new();
        public void SaveProject(NativeProject project) { }
        public NativeProject? LoadProject(EntityId id) => null;
        public IReadOnlyList<(EntityId Id, string Name)> ListProjects() => [];
    }
}
