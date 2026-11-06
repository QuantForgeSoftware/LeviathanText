using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Infrastructure;
using Microsoft.Data.Sqlite;

namespace LeviathanText.Infrastructure.Tests;

public sealed class ExistingStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lt-existing-store", Guid.NewGuid().ToString("N"));
    private string StorePath => Path.Combine(_root, "store.db");
    private static readonly EntityId ProjectId = new(EntityKind.Collection, "existing");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Missing_store_does_not_create_parent_or_file(bool readOnly)
    {
        Assert.Throws<SqliteException>(() => LocalStore.OpenExisting(StorePath, readOnly));
        Assert.False(Directory.Exists(_root));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Empty_store_is_refused_without_initialization(bool readOnly)
    {
        Directory.CreateDirectory(_root);
        File.WriteAllBytes(StorePath, []);

        var error = Assert.Throws<LeviathanException>(() => LocalStore.OpenExisting(StorePath, readOnly));

        Assert.Equal(ErrorCode.SchemaVersionUnsupported, error.Error.Code);
        Assert.Empty(File.ReadAllBytes(StorePath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Actual_old_schema_is_refused_without_migration(bool readOnly)
    {
        Directory.CreateDirectory(_root);
        using (var connection = RawConnection())
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE projects (id TEXT PRIMARY KEY, name TEXT NOT NULL, contract_version TEXT NOT NULL, json TEXT NOT NULL);
                PRAGMA user_version = 1;
                """;
            command.ExecuteNonQuery();
        }
        var before = File.ReadAllBytes(StorePath);

        var error = Assert.Throws<LeviathanException>(() => LocalStore.OpenExisting(StorePath, readOnly));

        Assert.Equal(ErrorCode.SchemaVersionUnsupported, error.Error.Code);
        Assert.Equal(before, File.ReadAllBytes(StorePath));
    }

    [Fact]
    public void Read_only_store_and_peer_preserve_zero_versions_and_reject_writes()
    {
        Seed();
        using (var raw = RawConnection())
        {
            raw.Open();
            using var command = raw.CreateCommand();
            command.CommandText = "UPDATE projects SET version = 0";
            command.ExecuteNonQuery();
        }
        var before = File.ReadAllBytes(StorePath);
        using (var store = LocalStore.OpenExisting(StorePath, readOnly: true))
        using (var peer = store.OpenPeer())
        {
            var original = store.LoadProject(ProjectId)!;
            Assert.Equal(0, original.Version);
            Assert.Equal(0, peer.LoadProject(ProjectId)!.Version);
            Assert.Equal(8, Assert.Throws<SqliteException>(() => store.SaveProject(original with { Name = "write rejected" })).SqliteErrorCode);
            Assert.Equal(8, Assert.Throws<SqliteException>(() => peer.SaveProject(original with { Name = "peer write rejected" })).SqliteErrorCode);
        }
        Assert.Equal(before, File.ReadAllBytes(StorePath));
    }

    [Fact]
    public void Existing_write_store_and_peer_save_with_optimistic_versions()
    {
        Seed();
        using var store = LocalStore.OpenExisting(StorePath);
        using var peer = store.OpenPeer();
        var snapshot = store.LoadProject(ProjectId)!;

        Assert.Equal(2, peer.SaveProject(snapshot with { Name = "existing writer" }));

        Assert.Equal("existing writer", store.LoadProject(ProjectId)!.Name);
        Assert.Equal(ErrorCode.RevisionConflict,
            Assert.Throws<LeviathanException>(() => store.SaveProject(snapshot with { Name = "stale" })).Error.Code);
    }

    private SqliteConnection RawConnection() => new(new SqliteConnectionStringBuilder
    {
        DataSource = StorePath, Pooling = false,
    }.ToString());

    private void Seed()
    {
        using var store = new LocalStore(StorePath);
        store.SaveProject(new NativeProject { Id = ProjectId, Name = "existing", Objects = [] });
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
