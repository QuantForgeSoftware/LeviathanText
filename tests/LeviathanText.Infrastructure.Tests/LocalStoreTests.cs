using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Infrastructure;

namespace LeviathanText.Infrastructure.Tests;

public class LocalStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"leviathan-test-{Guid.NewGuid():N}.db");

    [Fact]
    public void Project_survives_store_reopen()
    {
        NativeProject saved;
        using (var store = new LocalStore(_path))
            store.SaveProject(saved = MinimalProject());

        using (var store = new LocalStore(_path))
        {
            var loaded = store.LoadProject(saved.Id);
            Assert.NotNull(loaded);
            Assert.Equal(saved.Name, loaded!.Name);
            Assert.Equal(saved.Objects.Count, loaded.Objects.Count);
        }
    }

    [Fact]
    public void ListProjects_returns_saved()
    {
        using var store = new LocalStore(_path);
        store.SaveProject(MinimalProject());
        var list = store.ListProjects();
        Assert.Single(list);
        Assert.Equal("infra-fixture", list[0].Name);
    }

    [Fact]
    public void Schema_downgrade_is_refused()
    {
        using (var store = new LocalStore(_path)) { } // create at v1

        // Pretend a future version wrote the file: user_version bumped ahead.
        using (var raw = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_path}"))
        {
            raw.Open();
            var cmd = raw.CreateCommand();
            cmd.CommandText = "PRAGMA user_version=99";
            cmd.ExecuteNonQuery();
        }

        var ex = Assert.Throws<LeviathanException>(() => new LocalStore(_path));
        Assert.Equal(ErrorCode.SchemaVersionUnsupported, ex.Error.Code);
    }

    [Fact]
    public void Job_record_roundtrips_through_the_store()
    {
        using var store = new LocalStore(_path);
        var job = new JobRecord
        {
            Id = new EntityId(EntityKind.Job, "j1"),
            Type = "pipeline.run",
            Inputs = [],
            Owner = new AgentRef { Id = "cli", IsModel = false },
            IdempotencyKey = "key-1",
            Status = JobStatus.Succeeded,
        };
        store.SaveJob(job);
        var loaded = store.LoadJob(job.Id);
        Assert.NotNull(loaded);
        Assert.Equal(job.Type, loaded!.Type);
        Assert.Equal(job.Status, loaded.Status);
        Assert.Equal(job.IdempotencyKey, loaded.IdempotencyKey);
    }

    [Fact]
    public void DeleteProject_removes_the_document_and_reports_missing_ids()
    {
        using var store = new LocalStore(_path);
        var project = MinimalProject();
        store.SaveProject(project);

        store.DeleteProject(project.Id);
        Assert.Null(store.LoadProject(project.Id));
        Assert.Empty(store.ListProjects());

        var ex = Assert.Throws<LeviathanException>(() => store.DeleteProject(project.Id));
        Assert.Equal(ErrorCode.InputMissing, ex.Error.Code);
    }

    [Fact]
    public void BackupTo_produces_an_independently_readable_database()
    {
        var backupPath = _path + ".backup";
        using (var store = new LocalStore(_path))
        {
            store.SaveProject(MinimalProject());
            store.BackupTo(backupPath);
        }

        using var restored = new LocalStore(backupPath);
        var list = restored.ListProjects();
        Assert.Single(list);
        Assert.Equal("infra-fixture", list[0].Name);
        File.Delete(backupPath);
    }

    public void Dispose() => File.Delete(_path);

    [Fact]
    public void Auth_audit_entries_are_appended_and_read_newest_first()
    {
        using var store = new LocalStore(_path);
        store.AppendAuthAudit(new AuthAuditEntry
        {
            At = DateTimeOffset.UtcNow.AddSeconds(-2), UserName = "reader", Role = UserRole.Reader,
            Action = "denied", Method = "POST", Path = "/api/v1/projects", Status = 403,
        });
        store.AppendAuthAudit(new AuthAuditEntry
        {
            At = DateTimeOffset.UtcNow, Action = "unauthorized", Method = "GET", Path = "/api/v1/projects", Status = 401,
        });

        var entries = store.LoadAuthAudit();
        Assert.Equal(2, entries.Count);
        Assert.Equal("unauthorized", entries[0].Action);
        Assert.Null(entries[0].UserName);
        Assert.Equal(UserRole.Reader, entries[1].Role);
        Assert.Equal(403, entries[1].Status);
    }

    private static NativeProject MinimalProject() => new()
    {
        Id = new EntityId(EntityKind.Collection, "c1"),
        Name = "infra-fixture",
        Objects =
        [
            new NativeObject
            {
                Id = new EntityId(EntityKind.PhysicalObject, "o1"),
                Surfaces =
                [
                    new NativeSurface
                    {
                        Id = new EntityId(EntityKind.Surface, "s1"),
                        Kind = "papyrus-side",
                        Captures = [],
                        Regions = [],
                        Lines = [],
                    },
                ],
            },
        ],
    };
}

public sealed class LocalStoreUserTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"lt-users-{Guid.NewGuid():N}.db");

    [Fact]
    public void Users_are_created_authenticated_rotated_and_disabled()
    {
        using var store = new LocalStore(_path);
        var key = LeviathanText.Infrastructure.UserKeys.NewKey();
        var hash = LeviathanText.Infrastructure.UserKeys.Hash(key);

        var account = store.CreateUser("reviewer-a", UserRole.Reviewer, hash);
        Assert.Equal(UserRole.Reviewer, account.Role);
        Assert.Equal(account.Id, store.FindUserByKeyHash(hash)?.Id);
        Assert.Null(store.FindUserByKeyHash(LeviathanText.Infrastructure.UserKeys.Hash("wrong")));
        Assert.Single(store.LoadUsers());

        var rotated = LeviathanText.Infrastructure.UserKeys.NewKey();
        store.UpdateUserKey(account.Id, LeviathanText.Infrastructure.UserKeys.Hash(rotated));
        Assert.Null(store.FindUserByKeyHash(hash));
        Assert.NotNull(store.FindUserByKeyHash(LeviathanText.Infrastructure.UserKeys.Hash(rotated)));

        store.DisableUser(account.Id);
        Assert.Null(store.FindUserByKeyHash(LeviathanText.Infrastructure.UserKeys.Hash(rotated)));
        Assert.True(store.LoadUsers().Single().Disabled);
    }

    [Fact]
    public void Duplicate_user_names_are_rejected()
    {
        using var store = new LocalStore(_path);
        store.CreateUser("same", UserRole.Reader, LeviathanText.Infrastructure.UserKeys.Hash("a"));
        var ex = Assert.Throws<LeviathanException>(() =>
            store.CreateUser("same", UserRole.Reader, LeviathanText.Infrastructure.UserKeys.Hash("b")));
        Assert.Equal(ErrorCode.StageContractViolation, ex.Error.Code);
    }

    [Fact]
    public void Newer_store_schema_is_refused_and_v1_is_migrated()
    {
        using (var store = new LocalStore(_path))
        {
            store.SaveProject(MinimalProject());
        }

        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_path}"))
        {
            connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "DROP TABLE users; PRAGMA user_version=1;";
            cmd.ExecuteNonQuery();
        }

        using (var migrated = new LocalStore(_path)) // v1 → v2 recreates users
        {
            migrated.CreateUser("migrated", UserRole.Reader, "hash");
            Assert.Single(migrated.LoadUsers());
            Assert.Single(migrated.ListProjects());
        }

        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_path}"))
        {
            connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA user_version=99;";
            cmd.ExecuteNonQuery();
        }

        var ex = Assert.Throws<LeviathanException>(() => new LocalStore(_path));
        Assert.Equal(ErrorCode.SchemaVersionUnsupported, ex.Error.Code);
    }

    [Fact]
    public void Auth_audit_entries_are_appended_and_read_newest_first()
    {
        using var store = new LocalStore(_path);
        store.AppendAuthAudit(new AuthAuditEntry
        {
            At = DateTimeOffset.UtcNow.AddSeconds(-2), UserName = "reader", Role = UserRole.Reader,
            Action = "denied", Method = "POST", Path = "/api/v1/projects", Status = 403,
        });
        store.AppendAuthAudit(new AuthAuditEntry
        {
            At = DateTimeOffset.UtcNow, Action = "unauthorized", Method = "GET", Path = "/api/v1/projects", Status = 401,
        });

        var entries = store.LoadAuthAudit();
        Assert.Equal(2, entries.Count);
        Assert.Equal("unauthorized", entries[0].Action);
        Assert.Null(entries[0].UserName);
        Assert.Equal(UserRole.Reader, entries[1].Role);
        Assert.Equal(403, entries[1].Status);
    }

    private static NativeProject MinimalProject() => new()
    {
        Id = new EntityId(EntityKind.Collection, "col-users"),
        Name = "users-fixture",
        Objects = [],
    };

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }
}
