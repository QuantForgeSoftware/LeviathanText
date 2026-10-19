using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Recognition;
using LeviathanText.Training;

namespace LeviathanText.Worker;

/// <summary>
/// Durable batch recognition: first input artifact entity = target project; digest carries
/// <c>pkg:&lt;model-package-dir&gt;</c>. Every surface with lines is recognized (a model
/// instance is loaded per surface because the pipeline owns/disposes it), L0 revisions are
/// persisted with provenance, and the output records surface/line counts. A surface that
/// fails is reported without stopping the batch.
/// </summary>
public sealed class RecognizeProjectHandler(IProjectStore store, ImageResolver? imageResolver = null) : IJobHandler
{
    public const string JobType = "recognize.project";

    public string Type => JobType;

    public JobRecord Execute(JobRecord job, CancellationToken cancellationToken)
    {
        var input = job.Inputs.FirstOrDefault()
            ?? throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = job.Id.ToString(),
                Message = "recognize.project needs the target project as its first input artifact.",
            });

        if (input.Entity.Kind != EntityKind.Collection)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = input.Entity.ToString(),
                Message = "recognize.project input entity must be the target project.",
            });

        if (!input.Artifact.Digest.StartsWith(RecognizeSurfaceHandler.PackagePrefix, StringComparison.Ordinal))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = job.Id.ToString(),
                Message = "recognize.project input digest must be 'pkg:<model-package-dir>'.",
            });

        var packageDirectory = input.Artifact.Digest[RecognizeSurfaceHandler.PackagePrefix.Length..];
        if (!Directory.Exists(packageDirectory))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = packageDirectory,
                RemedyHint = RemedyHint.ReRunStage,
                Message = "Model package directory does not exist on this worker host.",
            });

        var project = store.LoadProject(input.Entity)
            ?? throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = input.Entity.ToString(),
                Message = "Target project not found.",
            });

        var loaded = RecognitionModelLoader.Load(packageDirectory);
        var metadata = loaded.Metadata;
        var labels = loaded.Labels;
        var modelPath = CheckpointStore.ModelPath(packageDirectory,
            loaded.Manifest?.ModelName ?? FindModelName(packageDirectory), ".best");
        var modelName = loaded.Manifest?.ModelName ?? FindModelName(packageDirectory);
        var pipeline = new RecognitionPipeline(store, imageResolver);

        int surfacesDone = 0, linesRecognized = 0, surfacesFailed = 0;
        var failures = new List<string>();

        foreach (var surface in project.Objects.SelectMany(o => o.Surfaces).ToList())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (surface.Lines.Count == 0) continue;

            try
            {
                var current = store.LoadProject(input.Entity) ?? project;
                var outcome = pipeline.Run(current, surface.Id, new RecognitionPipeline.Options
                {
                    ModelDirectory = packageDirectory,
                    ModelName = modelName,
                    ModelVersion = loaded.Manifest?.CheckpointDigest is { Length: >= 12 } digest ? digest[..12] : "package",
                    ModelFactory = () =>
                    {
                        var model = new CrnnModel(labels.NumClasses, metadata.HiddenSize,
                            metadata.WidthDownsampleFactor, metadata.InputHeightPixels, metadata.Dropout);
                        model.load(modelPath);
                        return model;
                    },
                    LabelSetFactory = () => labels,
                }, cancellationToken, persistL0: true);

                surfacesDone++;
                linesRecognized += outcome.Lines.Count;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                surfacesFailed++;
                if (failures.Count < 5) failures.Add($"{surface.Id}: {ex.Message}");
            }
        }

        if (surfacesDone == 0)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = input.Entity.ToString(),
                Details = new Dictionary<string, string>
                {
                    ["surfaces"] = surfacesFailed.ToString(),
                    ["first"] = failures.FirstOrDefault() ?? "",
                },
                Message = "No surface could be recognized.",
            });

        return job with
        {
            Outputs = [new StageArtifact(input.Entity, new Checksum
            {
                Algorithm = "recognize-counts",
                Digest = $"surfaces={surfacesDone};lines={linesRecognized};failed={surfacesFailed}",
            })],
        };
    }

    private static string FindModelName(string packageDirectory)
    {
        var metadataFiles = Directory.GetFiles(packageDirectory, "*.best.json");
        return metadataFiles.Length == 1
            ? Path.GetFileName(metadataFiles[0]).Replace(".best.json", string.Empty, StringComparison.Ordinal)
            : Path.GetFileName(Path.TrimEndingDirectorySeparator(packageDirectory));
    }
}
