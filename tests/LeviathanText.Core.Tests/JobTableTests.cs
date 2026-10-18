using LeviathanText.Contracts;
using LeviathanText.Core.Jobs;

namespace LeviathanText.Core.Tests;

public class JobTableTests
{
    private static readonly Checksum Sum = new() { Algorithm = "sha256", Digest = new string('a', 64) };
    private static readonly AgentRef Owner = new() { Id = "worker-1", IsModel = false };

    private static StageArtifact Artifact(string entitySuffix) =>
        new(new EntityId(EntityKind.Export, entitySuffix), Sum);

    [Fact]
    public void Idempotent_creation_returns_the_same_job()
    {
        var table = new JobTable();
        var first = table.Create("pipeline.run", Owner, idempotencyKey: "client-key-1");
        var second = table.Create("pipeline.run", Owner, idempotencyKey: "client-key-1");
        Assert.Equal(first.Id, second.Id);

        // Distinct key → distinct job.
        Assert.NotEqual(first.Id, table.Create("pipeline.run", Owner, idempotencyKey: "client-key-2").Id);
    }

    [Fact]
    public void Publishing_outputs_succeeds_the_job_and_records_them()
    {
        var table = new JobTable();
        var job = table.Create("export.build", Owner, idempotencyKey: "k");
        var artifact = Artifact("exp1");

        var published = table.PublishOutput(job.Id, artifact);
        Assert.Equal(JobStatus.Succeeded, published.Status);
        Assert.Contains(artifact, published.Outputs);
    }

    [Fact]
    public void Re_publishing_identical_output_after_success_is_a_noop()
    {
        var table = new JobTable();
        var job = table.Create("export.build", Owner, idempotencyKey: "k");
        var artifact = Artifact("exp1");
        table.PublishOutput(job.Id, artifact);

        var republished = table.PublishOutput(job.Id, artifact);
        Assert.Equal(JobStatus.Succeeded, republished.Status);
        Assert.Single(republished.Outputs); // no duplicate
    }

    [Fact]
    public void Divergent_publication_after_finalization_is_a_hard_error()
    {
        var table = new JobTable();
        var job = table.Create("export.build", Owner, idempotencyKey: "k");
        table.PublishOutput(job.Id, Artifact("exp1"));

        var ex = Assert.Throws<LeviathanException>(() => table.PublishOutput(job.Id, Artifact("exp2")));
        Assert.Equal(ErrorCode.StageContractViolation, ex.Error.Code);
        Assert.Contains("divergent output", ex.Error.Details!["reason"]);
    }

    [Fact]
    public void Retry_is_bounded_and_recorded()
    {
        var table = new JobTable();
        var job = table.Create("pipeline.run", Owner, idempotencyKey: "k");

        var retried = table.Retry(job.Id, maxRetries: 2);
        Assert.Equal(1, retried.RetriesUsed);
        Assert.Equal(JobStatus.Pending, retried.Status);

        table.PublishOutput(job.Id, Artifact("exp1"));
        // retrying a successful job is invalid
        Assert.Throws<LeviathanException>(() => table.Retry(job.Id, maxRetries: 2));
    }

    [Fact]
    public void Retry_budget_exhaustion_fails_loudly()
    {
        var table = new JobTable();
        var job = table.Create("pipeline.run", Owner, idempotencyKey: "k");
        table.Retry(job.Id, maxRetries: 1);
        var ex = Assert.Throws<LeviathanException>(() => table.Retry(job.Id, maxRetries: 1));
        Assert.Contains("budget", ex.Error.Message);
    }

    [Fact]
    public void Cancel_is_idempotent_and_finalizes()
    {
        var table = new JobTable();
        var job = table.Create("pipeline.run", Owner, idempotencyKey: "k");
        table.Cancel(job.Id);
        Assert.Equal(JobStatus.Cancelled, table.Cancel(job.Id).Status);

        // cancelled job rejects output publication as divergent
        Assert.Throws<LeviathanException>(() => table.PublishOutput(job.Id, Artifact("exp1")));
    }
}
