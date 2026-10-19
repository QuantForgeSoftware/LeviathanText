using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Training;

namespace LeviathanText.Worker;

/// <summary>
/// Durable recognition job: the first input artifact names the target surface; its digest
/// carries the model package path as <c>pkg:&lt;directory&gt;</c>. The package is verified by
/// <see cref="RecognitionModelLoader"/> (labels, architecture, digest), the pipeline
/// persists L0 revisions with provenance, and the job output records the line count.
/// </summary>
public sealed class RecognizeSurfaceHandler(IProjectStore store, ImageResolver? imageResolver = null) : IJobHandler
{
    public const string JobType = "recognize.surface";
    public const string PackagePrefix = "pkg:";

    public string Type => JobType;

    public JobRecord Execute(JobRecord job, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var input = job.Inputs.FirstOrDefault()
            ?? throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = job.Id.ToString(),
                Message = "recognize.surface needs the target surface as its first input artifact.",
            });

        if (!input.Artifact.Digest.StartsWith(PackagePrefix, StringComparison.Ordinal))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = job.Id.ToString(),
                Message = $"recognize.surface input digest must be '{PackagePrefix}<model-package-dir>'.",
            });

        var packageDirectory = input.Artifact.Digest[PackagePrefix.Length..];
        if (!Directory.Exists(packageDirectory))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = packageDirectory,
                RemedyHint = RemedyHint.ReRunStage,
                Message = "Model package directory does not exist on this worker host.",
            });

        var project = FindProjectForSurface(input.Entity)
            ?? throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = input.Entity.ToString(),
                Message = "Surface not found in any project.",
            });

        var loaded = RecognitionModelLoader.Load(packageDirectory);
        var pipeline = new RecognitionPipeline(store, imageResolver);
        var outcome = pipeline.Run(project, input.Entity, new RecognitionPipeline.Options
        {
            ModelDirectory = packageDirectory,
            ModelName = ModelNameOf(packageDirectory, loaded),
            ModelVersion = loaded.Manifest?.CheckpointDigest is { Length: >= 12 } digest ? digest[..12] : "package",
            ModelFactory = () => loaded.Model,
            LabelSetFactory = () => loaded.Labels,
            RunId = job.Id.Suffix,
        }, cancellationToken);

        return job with
        {
            Outputs = [new StageArtifact(input.Entity, new Checksum
            {
                Algorithm = "recognized-lines",
                Digest = outcome.Lines.Count.ToString(),
            })],
        };
    }

    /// <summary>Manifest name when present, else the single checkpoint's file stem (loader convention).</summary>
    private static string ModelNameOf(string packageDirectory, LoadedRecognitionModel loaded)
    {
        if (loaded.Manifest is not null) return loaded.Manifest.ModelName;
        var metadataFiles = Directory.GetFiles(packageDirectory, "*.best.json");
        return metadataFiles.Length == 1
            ? Path.GetFileName(metadataFiles[0]).Replace(".best.json", string.Empty, StringComparison.Ordinal)
            : Path.GetFileName(Path.TrimEndingDirectorySeparator(packageDirectory));
    }

    private NativeProject? FindProjectForSurface(EntityId surfaceId)
    {
        foreach (var (id, _) in store.ListProjects())
        {
            var project = store.LoadProject(id);
            if (project is not null && project.Objects.SelectMany(o => o.Surfaces).Any(s => s.Id == surfaceId))
                return project;
        }
        return null;
    }
}
