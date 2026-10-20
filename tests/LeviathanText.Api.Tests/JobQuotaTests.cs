using LeviathanText.Api;
using LeviathanText.Contracts;
using LeviathanText.Infrastructure;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LeviathanText.Api.Tests;

public class JobQuotaTests
{
    [Fact]
    public void Per_owner_active_quota_rejects_excess_jobs_and_idempotency_still_wins()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lt-quota-{Guid.NewGuid():N}.db");
        try
        {
            using var store = new LocalStore(path);
            var config = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["LeviathanText:Jobs:MaxActivePerOwner"] = "1" }).Build();
            var enqueuer = new JobEnqueuer(store, new JobEventBus(), config);
            var owner = new AgentRef { Id = "quota-owner", IsModel = false };

            var first = enqueuer.Enqueue("noop", owner, idempotencyKey: "same");
            var duplicate = enqueuer.Enqueue("noop", owner, idempotencyKey: "same");
            Assert.Equal(first.Id, duplicate.Id);

            var ex = Assert.Throws<LeviathanException>(() => enqueuer.Enqueue("noop", owner));
            Assert.Equal(ErrorCode.StageContractViolation, ex.Error.Code);
            Assert.Contains("quota", ex.Error.Message);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
