using LeviathanText.Contracts;
using LeviathanText.Contracts.Serialization;
using LeviathanText.Infrastructure;
using Microsoft.Data.Sqlite;

namespace LeviathanText.Infrastructure.Tests;

public sealed class LeaseRenewalSessionTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("lt-lease-renewal-").FullName;
    private static readonly EntityId Id = new(EntityKind.Job, "generated-renewal");
    private static readonly EntityId Worker = new(EntityKind.Job, "generated-worker");
    private static readonly DateTimeOffset Start = new(2026, 11, 6, 0, 0, 0, TimeSpan.Zero);
    private string Database => Path.Combine(root, "store.db");
    private static JobRecord Job() => new()
    {
        Id = Id, Type = "generated", Inputs = [], Owner = new() { Id = "generated", IsModel = false },
        Status = JobStatus.Running, CreatedAt = Start,
        Lease = new() { Worker = Worker, ClaimId = "captured-a", LastHeartbeat = Start, ExpiresAt = Start.AddSeconds(3) },
    };
    private string Raw()
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Database, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        connection.Open(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT json FROM jobs WHERE id=$id"; command.Parameters.AddWithValue("$id", Id.ToString());
        return (string)command.ExecuteScalar()!;
    }
    [Fact]
    public void Exact_nonce_renews_from_independent_peer_without_changing_other_job_fields()
    {
        using var anchor = new LocalStore(Database); var job = Job(); anchor.SaveJob(job);
        using var peer = anchor.OpenLeaseRenewalPeer();
        Assert.Equal(JobLeaseRenewalResult.Renewed, peer.RenewJobLeaseOnce(Id, Worker, "captured-a", Start.AddSeconds(2), TimeSpan.FromSeconds(3)));
        var actual = anchor.LoadJob(Id)!;
        Assert.Equal(Start.AddSeconds(2), actual.Lease!.LastHeartbeat); Assert.Equal(Start.AddSeconds(5), actual.Lease.ExpiresAt);
        Assert.Equal(ContractJson.ToJson(job with { Lease = actual.Lease }), Raw());
        Assert.Equal(JobLeaseRenewalResult.Renewed, peer.RenewJobLeaseOnce(Id, Worker, "captured-a", Start.AddSeconds(1), TimeSpan.FromSeconds(3)));
        Assert.Equal(actual.Lease, anchor.LoadJob(Id)!.Lease);
    }
    [Theory]
    [InlineData("old-nonce")] [InlineData("wrong-worker")] [InlineData("missing-job")]
    [InlineData("cancelled")] [InlineData("succeeded")] [InlineData("failed")] [InlineData("pending")]
    [InlineData("missing-lease")] [InlineData("legacy-null-nonce")]
    public void Invalid_or_terminal_claim_never_renews_or_rewrites_current_json(string mutation)
    {
        using var anchor = new LocalStore(Database); var job = Job();
        job = mutation switch
        {
            "cancelled" => job with { Status = JobStatus.Cancelled, Lease = null },
            "succeeded" => job with { Status = JobStatus.Succeeded, Lease = null },
            "failed" => job with { Status = JobStatus.Failed, Lease = null },
            "pending" => job with { Status = JobStatus.Pending },
            "missing-lease" => job with { Lease = null },
            "legacy-null-nonce" => job with { Lease = job.Lease! with { ClaimId = null } },
            _ => job,
        };
        anchor.SaveJob(job); string raw = Raw(); using var peer = anchor.OpenLeaseRenewalPeer();
        var result = peer.RenewJobLeaseOnce(mutation == "missing-job" ? new(EntityKind.Job, "missing") : Id,
            mutation == "wrong-worker" ? new(EntityKind.Job, "other-worker") : Worker,
            mutation == "old-nonce" ? "captured-old" : "captured-a", Start.AddSeconds(2), TimeSpan.FromSeconds(3));
        Assert.Equal(JobLeaseRenewalResult.OwnershipLost, result); Assert.Equal(raw, Raw());
    }
    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData(" ")]
    public void Missing_nonce_refuses_before_any_write(string? claim)
    {
        using var anchor = new LocalStore(Database); anchor.SaveJob(Job()); string raw = Raw();
        using var peer = anchor.OpenLeaseRenewalPeer();
        Assert.Throws<ArgumentException>(() => peer.RenewJobLeaseOnce(Id, Worker, claim!, Start, TimeSpan.FromSeconds(3)));
        Assert.Equal(raw, Raw());
    }
    [Fact]
    public void Old_same_worker_nonce_cannot_touch_replacement_and_current_nonce_still_renews()
    {
        using var anchor = new LocalStore(Database); anchor.SaveJob(Job()); using var oldPeer = anchor.OpenLeaseRenewalPeer();
        var replacement = Job() with { Lease = Job().Lease! with { ClaimId = "captured-b", ExpiresAt = Start.AddSeconds(8) } };
        anchor.SaveJob(replacement); string raw = Raw();
        Assert.Equal(JobLeaseRenewalResult.OwnershipLost, oldPeer.RenewJobLeaseOnce(Id, Worker, "captured-a", Start.AddSeconds(7), TimeSpan.FromSeconds(3)));
        Assert.Equal(raw, Raw());
        using var currentPeer = anchor.OpenLeaseRenewalPeer();
        Assert.Equal(JobLeaseRenewalResult.Renewed, currentPeer.RenewJobLeaseOnce(Id, Worker, "captured-b", Start.AddSeconds(7), TimeSpan.FromSeconds(3)));
        Assert.Equal("captured-b", anchor.LoadJob(Id)!.Lease!.ClaimId);
    }
    [Fact]
    public async Task Independent_sqlite_write_contention_is_observed_without_retry_loop_or_json_mutation()
    {
        using var anchor = new LocalStore(Database); anchor.SaveJob(Job()); string raw = Raw();
        using var blocker = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Database, Pooling = false }.ToString());
        blocker.Open(); using var transaction = blocker.BeginTransaction(deferred: false);
        var attempt = Task.Run(() =>
        {
            using var peer = anchor.OpenLeaseRenewalPeer();
            return peer.RenewJobLeaseOnce(Id, Worker, "captured-a", Start.AddSeconds(2), TimeSpan.FromSeconds(3));
        });
        Assert.Equal(JobLeaseRenewalResult.Contended, await attempt.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(raw, Raw()); transaction.Rollback();
        using var released = anchor.OpenLeaseRenewalPeer();
        Assert.Equal(JobLeaseRenewalResult.Renewed, released.RenewJobLeaseOnce(Id, Worker, "captured-a", Start.AddSeconds(2), TimeSpan.FromSeconds(3)));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Racing_independent_peer_cancel_or_replacement_is_never_resurrected(bool replacement)
    {
        using var anchor = new LocalStore(Database);
        for (int iteration = 0; iteration < 8; iteration++)
        {
            anchor.SaveJob(Job());
            using var start = new ManualResetEventSlim();
            var renew = Task.Run(() =>
            {
                using var peer = anchor.OpenLeaseRenewalPeer();
                start.Wait();
                return peer.RenewJobLeaseOnce(Id, Worker, "captured-a", Start.AddSeconds(2), TimeSpan.FromSeconds(3));
            });
            var mutate = Task.Run(() =>
            {
                using var peer = anchor.OpenPeer(); start.Wait();
                return peer.UpdateJob(Id, current => replacement
                    ? current with { Lease = Job().Lease! with { ClaimId = "captured-b", ExpiresAt = Start.AddSeconds(20) } }
                    : current with { Status = JobStatus.Cancelled, Lease = null });
            });
            start.Set(); await Task.WhenAll(renew, mutate).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.NotNull(await mutate);
            var current = anchor.LoadJob(Id)!;
            if (replacement)
            {
                Assert.Equal(JobStatus.Running, current.Status); Assert.Equal("captured-b", current.Lease!.ClaimId);
                Assert.Equal(Start.AddSeconds(20), current.Lease.ExpiresAt); Assert.Equal(Start, current.Lease.LastHeartbeat);
            }
            else { Assert.Equal(JobStatus.Cancelled, current.Status); Assert.Null(current.Lease); }
        }
    }
    public void Dispose() => Directory.Delete(root, recursive: true);
}
