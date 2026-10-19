using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Worker;

/// <summary>
/// Durable bulk import: the first input artifact's entity is the target project and its
/// digest carries the source directory as <c>dir:&lt;path&gt;</c>. Files are processed with
/// per-file failure isolation (one bad PAGE file does not lose the batch); the output
/// artifact records imported/failed counts. Layout never overwrites: imported files are
/// appended as new objects, so re-running a batch duplicates rather than destroying work —
/// the manifest is the record, not the filesystem.
/// </summary>
public sealed class ImportDirectoryHandler(IProjectStore store, IJobStore? jobs = null) : IJobHandler
{
    public const string JobType = "import.dir";
    public const string DirectoryPrefix = "dir:";

    public string Type => JobType;

    public JobRecord Execute(JobRecord job, CancellationToken cancellationToken)
    {
        var input = job.Inputs.FirstOrDefault()
            ?? throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = job.Id.ToString(),
                Message = "import.dir needs the target project as its first input artifact.",
            });

        if (input.Entity.Kind != EntityKind.Collection)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = input.Entity.ToString(),
                Message = "import.dir input entity must be the target project.",
            });

        if (!input.Artifact.Digest.StartsWith(DirectoryPrefix, StringComparison.Ordinal))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = job.Id.ToString(),
                Message = $"import.dir input digest must be '{DirectoryPrefix}<page-xml-directory>'.",
            });

        var directory = input.Artifact.Digest[DirectoryPrefix.Length..];
        if (!Directory.Exists(directory))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = directory,
                Message = "Source directory does not exist on this worker host.",
            });

        var files = Directory.GetFiles(directory, "*.xml").OrderBy(f => f, StringComparer.Ordinal).ToList();
        if (files.Count == 0)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = directory,
                Message = "Source directory contains no PAGE XML files.",
            });

        var project = store.LoadProject(input.Entity)
            ?? throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = input.Entity.ToString(),
                Message = "Target project not found.",
            });

        var interchange = new InterchangeService(new ProjectService(store));
        int imported = 0, failed = 0;
        var failures = new List<string>();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Explicit API cancellation: check between files on this thread (no concurrent
            // connection use); a cancelled job stops without losing already imported files.
            if (jobs?.Find(job.Id)?.Status == JobStatus.Cancelled)
                throw new OperationCanceledException();
            try
            {
                // Reload each iteration: the import appends to the persisted project.
                project = store.LoadProject(input.Entity) ?? project;
                interchange.ImportPage(project, file);
                imported++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                if (failures.Count < 5) failures.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }
        }

        if (imported == 0)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.RejectedArchive,
                Scope = directory,
                Details = new Dictionary<string, string> { ["failed"] = failed.ToString(), ["first"] = failures.FirstOrDefault() ?? "" },
                Message = "No PAGE file could be imported.",
            });

        return job with
        {
            Outputs = [new StageArtifact(input.Entity, new Checksum
            {
                Algorithm = "import-counts",
                Digest = $"imported={imported};failed={failed}",
            })],
        };
    }
}
