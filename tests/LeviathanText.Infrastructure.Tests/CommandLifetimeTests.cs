using System.Collections;
using System.Reflection;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using Microsoft.Data.Sqlite;

namespace LeviathanText.Infrastructure.Tests;

public sealed class CommandLifetimeTests
{
    [Fact]
    public void Fresh_store_releases_migration_commands()
    {
        using var store = new LocalStore(":memory:");
        Assert.Equal(0, RegisteredCommands(store));
    }

    [Fact]
    public void Materialized_user_reads_do_not_accumulate_provider_command_entries()
    {
        using var store = new LocalStore(":memory:");
        var user = store.CreateUser("reader", UserRole.Reader, "fixture-key");
        int before = RegisteredCommands(store);
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(user.Id, Assert.Single(store.LoadUsers()).Id);
            Assert.Equal(user.Id, store.FindUserByKeyHash("fixture-key")!.Id);
            Assert.True(store.HasUsers());
            Assert.Equal(user.Id, store.FindUserById(user.Id)!.Id);
        }
        Assert.Equal(before, RegisteredCommands(store));
    }

    [Fact]
    public void Reader_materialization_failure_releases_its_command()
    {
        using var store = new LocalStore(":memory:");
        store.CreateUser("broken", UserRole.Reader, "fixture-key");
        using (var corruption = Connection(store).CreateCommand())
        {
            corruption.CommandText = "UPDATE users SET role = 'invalid-role'";
            corruption.ExecuteNonQuery();
        }
        int before = RegisteredCommands(store);
        for (int i = 0; i < 10; i++)
            Assert.Throws<ArgumentException>(() => store.LoadUsers());
        Assert.Equal(before, RegisteredCommands(store));
    }

    [Fact]
    public void Rejected_insert_releases_command_and_preserves_existing_account()
    {
        using var store = new LocalStore(":memory:");
        var user = store.CreateUser("same-name", UserRole.Annotator, "first-key");
        int before = RegisteredCommands(store);
        Assert.Throws<LeviathanException>(() => store.CreateUser("same-name", UserRole.Admin, "second-key"));
        Assert.Equal(before, RegisteredCommands(store));
        Assert.Equal(user, store.FindUserById(user.Id));
    }

    [Fact]
    public void Committed_and_conflicting_project_transactions_release_all_commands()
    {
        using var store = new LocalStore(":memory:");
        var project = new NativeProject { Id = new(EntityKind.Collection, "commands"), Name = "first", Objects = [] };
        int before = RegisteredCommands(store);
        long version = store.SaveProject(project);
        Assert.Equal(before, RegisteredCommands(store));
        var loaded = store.LoadProject(project.Id)!;
        Assert.Equal(version, loaded.Version);
        Assert.Equal(before, RegisteredCommands(store));
        store.SaveProject(loaded with { Name = "updated" });
        Assert.Equal(before, RegisteredCommands(store));
        var conflict = Assert.Throws<LeviathanException>(() => store.SaveProject(loaded with { Name = "stale" }));
        Assert.Equal(ErrorCode.RevisionConflict, conflict.Error.Code);
        Assert.Equal(before, RegisteredCommands(store));
        Assert.Equal("updated", store.LoadProject(project.Id)!.Name);
        Assert.Equal(before, RegisteredCommands(store));
    }

    [Fact]
    public void Authorized_publication_and_denial_release_transaction_commands()
    {
        using var store = new LocalStore(":memory:");
        var project = new NativeProject { Id = new(EntityKind.Collection, "authorized-commands"), Name = "first", Objects = [] };
        store.SaveProject(project);
        project = store.LoadProject(project.Id)!;
        var user = store.CreateUser("member", UserRole.Annotator, "fixture-key");
        store.AddProjectMember(project.Id, user.Id);
        int before = RegisteredCommands(store);
        store.SaveProjectAuthorized(project with { Name = "saved" }, user.Id, true);
        Assert.Equal(before, RegisteredCommands(store));
        var saved = store.LoadProject(project.Id)!;
        store.DisableUser(user.Id);
        Assert.Throws<ProjectWriteAccessDeniedException>(() => store.SaveProjectAuthorized(saved with { Name = "denied" }, user.Id, true));
        Assert.Equal(before, RegisteredCommands(store));
        Assert.Equal("saved", store.LoadProject(project.Id)!.Name);
        Assert.Equal(before, RegisteredCommands(store));
    }

    // Microsoft.Data.Sqlite 10.0.13 retains weak command registrations until command
    // disposal or connection close. These pinned-provider tests inspect actual lifetime,
    // rather than treating GC or returned row equality as evidence of cleanup.
    private static SqliteConnection Connection(LocalStore store) =>
        (SqliteConnection)typeof(LocalStore).GetField("_connection", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(store)!;

    private static int RegisteredCommands(LocalStore store) =>
        ((ICollection)typeof(SqliteConnection).GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(Connection(store))!).Count;
}
