using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Worker;

/// <summary>
/// Durable batch layout: first input artifact entity = target project. Every surface with a
/// capture and no regions is detected; surfaces that already have layout are skipped (never
/// overwritten). Failures are isolated per surface and reported in the output counts.
/// </summary>
public sealed class LayoutProjectHandler(IProjectStore store, ImageResolver? imageResolver = null, IJobStore? jobs = null) : IJobHandler
{
    public const string JobType = "layout.project";

    public string Type => JobType;

    public JobRecord Execute(JobRecord job, CancellationToken cancellationToken)
    {
        var input = job.Inputs.FirstOrDefault()
            ?? throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = job.Id.ToString(),
                Message = "layout.project needs the target project as its first input artifact.",
            });

        if (input.Entity.Kind != EntityKind.Collection)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = input.Entity.ToString(),
                Message = "layout.project input entity must be the target project.",
            });

        var project = store.LoadProject(input.Entity)
            ?? throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = input.Entity.ToString(),
                Message = "Target project not found.",
            });

        var service = new LayoutService(store, imageResolver);
        int detected = 0, lines = 0, failed = 0, skipped = 0;
        var failures = new List<string>();

        foreach (var surface in project.Objects.SelectMany(o => o.Surfaces).ToList())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (jobs?.Find(job.Id)?.Status == JobStatus.Cancelled)
                throw new OperationCanceledException();

            if (surface.Regions.Count > 0 || surface.Captures.Count == 0)
            {
                skipped++;
                continue;
            }

            try
            {
                var current = store.LoadProject(input.Entity) ?? project;
                var outcome = service.Detect(current, surface.Id);
                detected++;
                lines += outcome.LineCount;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                if (failures.Count < 5) failures.Add($"{surface.Id}: {ex.Message}");
            }
        }

        if (detected == 0 && failed == 0)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = input.Entity.ToString(),
                Details = new Dictionary<string, string> { ["skipped"] = skipped.ToString() },
                Message = "No surface needed layout detection (all have layout or no capture).",
            });

        return job with
        {
            Outputs = [new StageArtifact(input.Entity, new Checksum
            {
                Algorithm = "layout-counts",
                Digest = $"surfaces={detected};lines={lines};failed={failed};skipped={skipped}",
            })],
        };
    }
}
