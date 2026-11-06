using System.IO.Compression;
using System.Text;
using System.Text.Json;
using LeviathanText.Contracts;
using LeviathanText.Contracts.Serialization;
using LeviathanText.Formats;
using LeviathanText.Imaging;

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

    public static Result Export(NativeProject project, string targetZipPath, bool includeImages = false)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetZipPath);

        var directory = Path.GetDirectoryName(Path.GetFullPath(targetZipPath));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        var destination = Path.GetFullPath(targetZipPath);
        var pathComparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (File.Exists(destination))
        {
            var canonicalDestination = CanonicalPath(destination);
            if (project.Objects.SelectMany(obj => obj.Surfaces).SelectMany(surface => surface.Captures)
                .Any(capture => File.Exists(capture.ImageLocation)
                    && string.Equals(CanonicalPath(capture.ImageLocation), canonicalDestination, pathComparison)))
                throw new IOException("Bundle destination is a source capture; choose a different path.");
        }

        var staged = Path.Combine(directory!, $".lt-bundle-{Guid.NewGuid():N}.tmp");
        try
        {
            var count = WriteArchive(project, staged, includeImages);
            var size = new FileInfo(staged).Length;
            File.Move(staged, destination, overwrite: true);
            return new Result(targetZipPath, count, size);
        }
        finally
        {
            if (File.Exists(staged)) File.Delete(staged);
        }
    }

    private static string CanonicalPath(string path)
    {
        var full = Path.GetFullPath(path);
        FileSystemInfo entry = Directory.Exists(full) ? new DirectoryInfo(full) : new FileInfo(full);
        if (entry.LinkTarget is not null)
            return CanonicalPath(entry.ResolveLinkTarget(returnFinalTarget: true)!.FullName);
        var parent = Path.GetDirectoryName(full);
        return parent is null ? full : Path.Combine(CanonicalPath(parent), Path.GetFileName(full));
    }

    private static int WriteArchive(NativeProject project, string targetZipPath, bool includeImages)
    {
        var interchange = new InterchangeService(new ProjectService(ProjectStoreFree.Instance));

        using var stream = File.Create(targetZipPath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        var files = new List<string>();
        var surfaces = project.Objects.SelectMany(o => o.Surfaces).ToList();

        void Add(string name, string content)
        {
            var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write(content);
            files.Add(name);
        }

        void AddBytes(string name, byte[] content)
        {
            var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
            using var stream = entry.Open();
            stream.Write(content);
            files.Add(name);
        }

        // Persisted legacy identity ambiguity must not select a first image/surface
        // or create duplicate ZIP paths. Keep native recovery possible without
        // invoking any convenience exporter or opening image payloads.
        var ambiguities = surfaces.GroupBy(surface => surface.Id).Where(group => group.Count() > 1)
            .Select(group => new { kind = "surface-id", id = group.Key.ToString(), count = group.Count() })
            .Concat(surfaces.SelectMany(surface => surface.Captures).GroupBy(capture => capture.Id)
                .Where(group => group.Count() > 1)
                .Select(group => new { kind = "capture-id", id = group.Key.ToString(), count = group.Count() }))
            .OrderBy(reason => reason.kind, StringComparer.Ordinal).ThenBy(reason => reason.id, StringComparer.Ordinal)
            .ToArray();
        if (ambiguities.Length > 0)
        {
            Add("project.json", NativeJson.Save(project));
            Add("integrity.json", ProjectIntegrity.ToJson(ProjectIntegrity.Audit(project)));
            Add("exports-omitted.json", JsonSerializer.Serialize(new
            {
                schema = "leviathan.bundle-omissions.v1", reasons = ambiguities,
                omitted = "All convenience exports and image payloads are omitted because surface/capture identity is ambiguous.",
                native = "project.json retains the complete supplied native project; resolve ambiguity explicitly on a copy.",
            }, new JsonSerializerOptions { WriteIndented = true }));
            Add("manifest.json", JsonSerializer.Serialize(new
            {
                format = "leviathantext-bundle", contractVersion = ContractVersion.Current,
                createdAt = DateTimeOffset.UtcNow,
                project = new { id = project.Id.ToString(), name = project.Name },
                surfaces = surfaces.Select(surface => new
                {
                    id = surface.Id.ToString(), kind = surface.Kind, captures = surface.Captures.Count,
                    regions = surface.Regions.Count, lines = surface.Lines.Count,
                    revisions = surface.Revisions.Count, annotations = surface.Annotations.Count,
                }),
                files, imageFilesRequested = includeImages, imageFilesIncluded = 0,
                transcriptionProfile = new { id = Core.Unicode.GreekTextProfile.ProfileId, version = Core.Unicode.GreekTextProfile.Version },
                layers = new
                {
                    l0 = "raw model output; never an edition", l1 = "diplomatic transcription (visible text)",
                    l2 = "editorial reading/accepted proposals",
                },
                exportMode = "native-rescue", exportReasons = ambiguities,
                note = "Only native project, integrity and omission records are included. No image payloads were opened; " +
                    "derived exports require explicit identity repair on a copy. This ZIP is not a SQLite restore snapshot.",
            }, new JsonSerializerOptions { WriteIndented = true }));
            return files.Count;
        }

        var renderedPdfs = new HashSet<EntityId>();
        void AddPdf(NativeSurface surface, byte[]? imageBytes)
        {
            (byte[] Pdf, LossReport Report) rendered;
            try
            {
                rendered = interchange.ExportSearchablePdf(project, surface.Id,
                    includeCaptureImage: imageBytes is not null, captureBytes: imageBytes);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A failed convenience export must not prevent the native and TEI records.
                return;
            }
            AddBytes($"surfaces/{surface.Id}/searchable.pdf", rendered.Pdf);
            Add($"surfaces/{surface.Id}/searchable-pdf-loss.txt", rendered.Report.ToString());
            renderedPdfs.Add(surface.Id);
        }

        var imageEntries = new Dictionary<EntityId, string>();
        if (includeImages)
        {
            var omitted = new List<string>();
            long totalImageBytes = 0;
            var primarySurfaces = surfaces.Where(s => s.Captures.Count > 0)
                .ToLookup(s => s.Captures[0].Id);
            foreach (var capture in surfaces.SelectMany(s => s.Captures).GroupBy(c => c.Id).Select(g => g.First()))
            {
                var imagePath = capture.ImageLocation;
                if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                {
                    omitted.Add($"{capture.Id}: source not local/readable");
                    continue;
                }
                byte[] imageBytes;
                try
                {
                    imageBytes = ReadValidatedImage(imagePath, 250L * 1024 * 1024 - totalImageBytes);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException
                    or SixLabors.ImageSharp.UnknownImageFormatException
                    or SixLabors.ImageSharp.InvalidImageContentException or NotSupportedException)
                {
                    omitted.Add($"{capture.Id}: unreadable/invalid image or size limit");
                    continue;
                }
                var checksum = capture.Frame.ImageChecksum;
                if (string.Equals(checksum.Algorithm, "sha256", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(checksum.Digest, ImageDecoder.ChecksumOf(imageBytes).Digest, StringComparison.OrdinalIgnoreCase))
                {
                    omitted.Add($"{capture.Id}: source image checksum mismatch");
                    continue;
                }
                totalImageBytes += imageBytes.LongLength;
                var entry = $"images/{capture.Id}{Path.GetExtension(imagePath)}";
                AddBytes(entry, imageBytes);
                imageEntries[capture.Id] = entry;
                foreach (var surface in primarySurfaces[capture.Id])
                    AddPdf(surface, imageBytes);
            }
            if (omitted.Count > 0)
                Add("images-omitted.txt", string.Join('\n', omitted) + "\n");
        }

        Add("project.json", NativeJson.Save(project));
        Add("training-export.json", TrainingDataExport.ToJson(TrainingDataExport.Export(project)));
        Add("profile-report.json", ContractJson.ToJson(ProfileReport.Build(project)));
        Add("lines.csv", LineCsv.Export(project));
        Add("annotations.csv", AnnotationCsv.Export(project));
        Add("word-boundaries.csv", WordBoundaryCsv.Export(project));
        Add("crosswalk.csv", ProvenanceCrosswalk.ToCsv(ProvenanceCrosswalk.Build(project)));
        Add("line-crosswalk.csv", ProvenanceCrosswalk.ToLineCsv(ProvenanceCrosswalk.BuildLines(project)));
        Add("fragment-crosswalk.csv", ProvenanceCrosswalk.ToFragmentCsv(ProvenanceCrosswalk.BuildFragments(project)));
        Add("integrity.json", ProjectIntegrity.ToJson(ProjectIntegrity.Audit(project)));
        Add("overlap.json", CorpusOverlap.ToJson(CorpusOverlap.Audit(project)));
        var projectEdition = EditionHtml.ExportProject(project, project.Name, surface =>
        {
            var capture = surface.Captures.FirstOrDefault();
            return capture is not null && imageEntries.TryGetValue(capture.Id, out var entry) ? entry : "";
        });
        Add("edition.html", projectEdition.Html);
        Add("edition-loss.txt", projectEdition.Report.ToString());
        var reviews = new ReviewService(ProjectStoreFree.Instance);
        Add("review-summary.json", ContractJson.ToJson(reviews.Summarize(project)));
        Add("adjudication.csv", reviews.ExportAdjudicationCsv(project));

        foreach (var surface in surfaces)
        {
            var prefix = $"surfaces/{surface.Id}";
            Add($"{prefix}/diplomatic.txt", interchange.ExportDiplomatic(project, surface.Id).Text);
            Add($"{prefix}/tei.xml", interchange.ExportTei(project, surface.Id, $"LeviathanText export {surface.Id}"));
            if (!renderedPdfs.Contains(surface.Id))
                AddPdf(surface, imageBytes: null);
            var editionCapture = surface.Captures.FirstOrDefault();
            var editionImage = editionCapture is not null
                && imageEntries.TryGetValue(editionCapture.Id, out var editionEntry)
                ? $"../../{editionEntry}" : "";
            var edition = EditionHtml.Export(project, surface.Id, $"{project.Name} — {surface.Id}", editionImage);
            Add($"{prefix}/edition.html", edition.Html);
            Add($"{prefix}/edition-loss.txt", edition.Report.ToString());
            if (surface.Captures.Count > 0)
            {
                Add($"{prefix}/alto.xml", AltoXml.Export(surface, surface.Captures[0], surface.Revisions));
                Add($"{prefix}/hocr.html", HocrFormat.Export(surface, surface.Captures[0], surface.Revisions));
                Add($"{prefix}/abbyy.xml", AbbyyXml.Export(surface, surface.Captures[0], surface.Revisions));
            }
        }

        var manifest = new
        {
            format = "leviathantext-bundle",
            contractVersion = ContractVersion.Current,
            createdAt = DateTimeOffset.UtcNow,
            project = new { id = project.Id.ToString(), name = project.Name },
            surfaces = surfaces.Select(s => new
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
            imageFilesRequested = includeImages,
            imageFilesIncluded = imageEntries.Count,
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
                   "the native project.json is authoritative. " + (includeImages
                       ? "Validated source images are included under images/ and as PDF page backgrounds when available; images-omitted.txt records any omissions."
                       : "Searchable PDFs contain text only. Source image paths and checksums remain in project.json."),
        };
        Add("manifest.json", JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

        return files.Count;
    }

    private static byte[] ReadValidatedImage(string path, long remainingBytes)
    {
        long limit = Math.Min(25L * 1024 * 1024, remainingBytes);
        using var source = File.OpenRead(path);
        if (source.Length > limit) throw new InvalidDataException("Image size limit exceeded.");
        using var content = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = source.Read(buffer)) > 0)
        {
            if (content.Length + read > limit) throw new InvalidDataException("Image size limit exceeded.");
            content.Write(buffer, 0, read);
        }
        var bytes = content.ToArray();
        // Validate the exact bounded byte snapshot that is copied, rather than reopening
        // a path after validation. A renamed text file must never be a raw export channel.
        var options = new SixLabors.ImageSharp.Formats.DecoderOptions
        {
            MaxFrames = 1,
            SkipMetadata = true,
            TargetSize = new SixLabors.ImageSharp.Size(1, 1),
        };
        var info = SixLabors.ImageSharp.Image.Identify(options, bytes);
        if (info.Width <= 0 || info.Height <= 0
            || info.Width > ImageDecoder.MaxDimension || info.Height > ImageDecoder.MaxDimension)
            throw new InvalidDataException("Image dimensions exceed the supported limit.");
        using var decoded = SixLabors.ImageSharp.Image.Load(options, bytes);
        return bytes;
    }

    /// <summary>ProjectStore adapter for read-only exporters (never saves).</summary>
    private sealed class ProjectStoreFree : IProjectStore
    {
        public static readonly ProjectStoreFree Instance = new();
        public long SaveProject(NativeProject project) => project.Version + 1;
        public NativeProject? LoadProject(EntityId id) => null;
        public IReadOnlyList<(EntityId Id, string Name)> ListProjects() => [];
    }
}
