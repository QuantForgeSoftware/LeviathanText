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

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Store_migrates_forward_to_v6_from_older_schemas(int fromVersion)
    {
        var path = Path.Combine(Path.GetTempPath(), $"leviathan-migrate-{fromVersion}-{Guid.NewGuid():N}.db");
        NativeProject saved;
        using (var store = new LocalStore(path))
            store.SaveProject(saved = MinimalProject());

        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path}"))
        {
            connection.Open();
            var cmd = connection.CreateCommand();
            var drops = fromVersion switch
            {
                2 => "DROP TABLE IF EXISTS auth_audit; DROP TABLE IF EXISTS users; "
                     + "DROP TABLE IF EXISTS project_members; DROP TABLE IF EXISTS runs; DROP TABLE IF EXISTS presence;",
                3 => "DROP TABLE IF EXISTS project_members; DROP TABLE IF EXISTS runs; DROP TABLE IF EXISTS presence;",
                4 => "DROP TABLE IF EXISTS runs; DROP TABLE IF EXISTS presence;",
                _ => "DROP TABLE IF EXISTS presence;",
            };
            cmd.CommandText = drops + $" PRAGMA user_version={fromVersion};";
            cmd.ExecuteNonQuery();
        }

        using (var migrated = new LocalStore(path))
        {
            Assert.NotNull(migrated.LoadProject(saved.Id));
            migrated.CreateUser("migrated", UserRole.Reader, "hash");
            migrated.AddProjectMember(saved.Id, "usr-migrated");
            Assert.True(migrated.IsProjectMember(saved.Id, "usr-migrated"));
            migrated.SaveRun(new RunRecord
            {
                Id = "run-migrated", Kind = "recognition", Name = "migrated",
                CreatedAt = DateTimeOffset.UtcNow, Metrics = new Dictionary<string, double> { ["cer"] = 1 },
            });
            Assert.Single(migrated.LoadRuns());
            migrated.TouchPresence(saved.Id, "usr-migrated", DateTimeOffset.UtcNow);
            Assert.Contains("usr-migrated", migrated.ListPresence(saved.Id, DateTimeOffset.UtcNow.AddMinutes(-1)));
        }
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

    [Fact]
    public void Project_membership_roundtrips_and_resolves_entity_owner()
    {
        using var store = new LocalStore(_path);
        var project = MinimalProject() with
        {
            Objects =
            [
                new NativeObject
                {
                    Id = new EntityId(EntityKind.PhysicalObject, "obj-1"),
                    Surfaces =
                    [
                        new NativeSurface
                        {
                            Id = new EntityId(EntityKind.Surface, "surf-1"),
                            Kind = "page",
                            Captures = [],
                            Regions = [],
                            Lines = [new NativeLine
                            {
                                Id = new EntityId(EntityKind.Line, "ln-1"),
                                RegionId = new EntityId(EntityKind.Region, "reg-1"),
                                FrameRef = new EntityId(EntityKind.Capture, "cap-1"),
                            }],
                        },
                    ],
                },
            ],
        };
        store.SaveProject(project);

        store.AddProjectMember(project.Id, "usr-a");
        store.AddProjectMember(project.Id, "usr-b");
        store.AddProjectMember(project.Id, "usr-a"); // idempotent
        Assert.Equal(["usr-a", "usr-b"], store.LoadProjectMembers(project.Id));
        Assert.True(store.IsProjectMember(project.Id, "usr-a"));
        Assert.False(store.IsProjectMember(project.Id, "usr-c"));
        Assert.Equal(project.Id, store.FindProjectForEntity(new EntityId(EntityKind.Line, "ln-1")));
        Assert.Null(store.FindProjectForEntity(new EntityId(EntityKind.Line, "missing")));

        store.RemoveProjectMember(project.Id, "usr-a");
        Assert.False(store.IsProjectMember(project.Id, "usr-a"));
    }

    [Fact]
    public void Runs_roundtrip_filter_and_sort_newest_first()
    {
        using var store = new LocalStore(_path);
        store.SaveRun(new RunRecord
        {
            Id = "run-old", Kind = "recognition", Name = "old", CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            DatasetDigest = "hash-a",
            Config = new Dictionary<string, string> { ["epochs"] = "10" },
            Metrics = new Dictionary<string, double> { ["bestDevCer"] = 0.5 },
            History = [0.9, 0.7, 0.5],
            Artifacts = ["package-a"],
        });
        store.SaveRun(new RunRecord
        {
            Id = "run-new", Kind = "layout", Name = "new", CreatedAt = DateTimeOffset.UtcNow,
            Metrics = new Dictionary<string, double> { ["devF1"] = 0.8 },
        });

        var all = store.LoadRuns();
        Assert.Equal(["run-new", "run-old"], all.Select(r => r.Id));
        var recognition = store.LoadRun("run-old")!;
        Assert.Equal("recognition", recognition.Kind);
        Assert.Equal(0.5, recognition.Metrics["bestDevCer"]);
        Assert.Equal([0.9, 0.7, 0.5], recognition.History);
        Assert.Single(store.LoadRuns("layout"));
        Assert.Null(store.LoadRun("missing"));
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

    [Fact]
    public void Project_membership_roundtrips_and_resolves_entity_owner()
    {
        using var store = new LocalStore(_path);
        var project = MinimalProject() with
        {
            Objects =
            [
                new NativeObject
                {
                    Id = new EntityId(EntityKind.PhysicalObject, "obj-1"),
                    Surfaces =
                    [
                        new NativeSurface
                        {
                            Id = new EntityId(EntityKind.Surface, "surf-1"),
                            Kind = "page",
                            Captures = [],
                            Regions = [],
                            Lines = [new NativeLine
                            {
                                Id = new EntityId(EntityKind.Line, "ln-1"),
                                RegionId = new EntityId(EntityKind.Region, "reg-1"),
                                FrameRef = new EntityId(EntityKind.Capture, "cap-1"),
                            }],
                        },
                    ],
                },
            ],
        };
        store.SaveProject(project);

        store.AddProjectMember(project.Id, "usr-a");
        store.AddProjectMember(project.Id, "usr-b");
        store.AddProjectMember(project.Id, "usr-a"); // idempotent
        Assert.Equal(["usr-a", "usr-b"], store.LoadProjectMembers(project.Id));
        Assert.True(store.IsProjectMember(project.Id, "usr-a"));
        Assert.False(store.IsProjectMember(project.Id, "usr-c"));
        Assert.Equal(project.Id, store.FindProjectForEntity(new EntityId(EntityKind.Line, "ln-1")));
        Assert.Null(store.FindProjectForEntity(new EntityId(EntityKind.Line, "missing")));

        store.RemoveProjectMember(project.Id, "usr-a");
        Assert.False(store.IsProjectMember(project.Id, "usr-a"));
    }

    [Fact]
    public void Runs_roundtrip_filter_and_sort_newest_first()
    {
        using var store = new LocalStore(_path);
        store.SaveRun(new RunRecord
        {
            Id = "run-old", Kind = "recognition", Name = "old", CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            DatasetDigest = "hash-a",
            Config = new Dictionary<string, string> { ["epochs"] = "10" },
            Metrics = new Dictionary<string, double> { ["bestDevCer"] = 0.5 },
            History = [0.9, 0.7, 0.5],
            Artifacts = ["package-a"],
        });
        store.SaveRun(new RunRecord
        {
            Id = "run-new", Kind = "layout", Name = "new", CreatedAt = DateTimeOffset.UtcNow,
            Metrics = new Dictionary<string, double> { ["devF1"] = 0.8 },
        });

        var all = store.LoadRuns();
        Assert.Equal(["run-new", "run-old"], all.Select(r => r.Id));
        var recognition = store.LoadRun("run-old")!;
        Assert.Equal("recognition", recognition.Kind);
        Assert.Equal(0.5, recognition.Metrics["bestDevCer"]);
        Assert.Equal([0.9, 0.7, 0.5], recognition.History);
        Assert.Single(store.LoadRuns("layout"));
        Assert.Null(store.LoadRun("missing"));
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
