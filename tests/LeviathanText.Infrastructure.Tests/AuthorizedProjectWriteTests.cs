using LeviathanText.Contracts;
using LeviathanText.Formats;
using Microsoft.Data.Sqlite;

namespace LeviathanText.Infrastructure.Tests;

public sealed class AuthorizedProjectWriteTests
{
    [Theory]
    [InlineData(UserRole.Annotator, true, true, true)]
    [InlineData(UserRole.Reviewer, true, true, true)]
    [InlineData(UserRole.Admin, false, true, true)]
    [InlineData(UserRole.Annotator, false, false, true)]
    [InlineData(UserRole.Annotator, false, true, false)]
    [InlineData(UserRole.Reader, true, true, false)]
    [InlineData(UserRole.Reader, true, false, false)]
    public void Commit_checks_current_role_and_optional_membership(UserRole role, bool member,
        bool membershipEnabled, bool expected)
    {
        using var store = new LocalStore(":memory:");
        var project = Seed(store);
        var user = store.CreateUser("owner", role, "generated-key");
        if (member) store.AddProjectMember(project.Id, user.Id);
        var changed = project with { Name = "committed" };

        if (expected)
        {
            long version = store.SaveProjectAuthorized(changed, user.Id, membershipEnabled);
            Assert.Equal(project.Version + 1, version);
            Assert.Equal("committed", store.LoadProject(project.Id)!.Name);
        }
        else
        {
            Assert.Throws<ProjectWriteAccessDeniedException>(() => store.SaveProjectAuthorized(changed, user.Id, membershipEnabled));
            Assert.Equal(NativeJson.Save(project), NativeJson.Save(store.LoadProject(project.Id)!));
        }
    }

    [Theory]
    [InlineData("revoke")]
    [InlineData("disable")]
    [InlineData("demote")]
    [InlineData("delete")]
    public void Changes_after_admission_are_checked_again_at_the_write(string change)
    {
        var directory = Directory.CreateTempSubdirectory("lt-authorized-write-");
        try
        {
            string database = Path.Combine(directory.FullName, "store.db");
            using var store = new LocalStore(database);
            using var peer = new LocalStore(database);
            var project = Seed(store);
            var user = store.CreateUser("owner", UserRole.Annotator, "generated-key");
            store.AddProjectMember(project.Id, user.Id);
            Assert.True(store.IsProjectMember(project.Id, user.Id));
            if (change == "revoke") peer.RemoveProjectMember(project.Id, user.Id);
            else if (change == "disable") peer.DisableUser(user.Id);
            else
            {
                using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database, Pooling = false }.ToString());
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = change == "demote" ? "UPDATE users SET role = 'Reader' WHERE id = $id"
                    : "DELETE FROM users WHERE id = $id";
                command.Parameters.AddWithValue("$id", user.Id);
                command.ExecuteNonQuery();
            }

            Assert.Throws<ProjectWriteAccessDeniedException>(() =>
                store.SaveProjectAuthorized(project with { Name = "must-not-persist" }, user.Id, requireMembership: true));
            Assert.Equal(NativeJson.Save(project), NativeJson.Save(store.LoadProject(project.Id)!));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public void Known_admin_is_rechecked_and_bootstrap_id_is_not_a_storage_bypass()
    {
        using var store = new LocalStore(":memory:");
        var project = Seed(store);
        var admin = store.CreateUser("admin", UserRole.Admin, "generated-key");
        store.DisableUser(admin.Id);
        Assert.Throws<ProjectWriteAccessDeniedException>(() => store.SaveProjectAuthorized(project, admin.Id, true));
        Assert.Throws<ProjectWriteAccessDeniedException>(() => store.SaveProjectAuthorized(project, "usr-bootstrap", false));
        Assert.Throws<ProjectWriteAccessDeniedException>(() => store.SaveProjectAuthorized(project, "missing", false));
        Assert.Equal(NativeJson.Save(project), NativeJson.Save(store.LoadProject(project.Id)!));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Anonymous_local_admission_is_invalidated_by_even_a_disabled_first_account(bool disabled)
    {
        using var store = new LocalStore(":memory:");
        var project = Seed(store);
        Assert.False(store.HasUsers());
        var first = store.CreateUser("first", UserRole.Reader, "generated-key");
        if (disabled) store.DisableUser(first.Id);

        Assert.Throws<ProjectWriteAccessDeniedException>(() =>
            store.SaveProjectAuthorized(project with { Name = "must-not-persist" }, actorId: null, requireMembership: false));
        Assert.Equal(NativeJson.Save(project), NativeJson.Save(store.LoadProject(project.Id)!));
    }

    [Fact]
    public void No_account_local_save_and_trusted_legacy_save_keep_their_contract()
    {
        using var store = new LocalStore(":memory:");
        var project = Seed(store);
        long version = store.SaveProjectAuthorized(project with { Name = "local" }, null, true);
        Assert.Equal(project.Version + 1, version);
        var local = store.LoadProject(project.Id)!;
        store.CreateUser("reader", UserRole.Reader, "generated-key");
        long trusted = store.SaveProject(local with { Name = "legacy" });
        Assert.Equal(version + 1, trusted);
        Assert.Equal("legacy", store.LoadProject(project.Id)!.Name);
    }

    [Fact]
    public void Authorized_save_preserves_optimistic_conflict_without_publishing_partial_data()
    {
        using var store = new LocalStore(":memory:");
        var project = Seed(store);
        var admin = store.CreateUser("admin", UserRole.Admin, "generated-key");
        store.SaveProject(project with { Name = "concurrent winner" });

        var conflict = Assert.Throws<LeviathanException>(() =>
            store.SaveProjectAuthorized(project with { Name = "stale" }, admin.Id, true));
        Assert.Equal(ErrorCode.RevisionConflict, conflict.Error.Code);
        Assert.Equal("concurrent winner", store.LoadProject(project.Id)!.Name);
        Assert.Equal(project.Version + 1, store.LoadProject(project.Id)!.Version);
    }

    private static NativeProject Seed(LocalStore store)
    {
        var project = new NativeProject { Id = new(EntityKind.Collection, "authorized"), Name = "original", Objects = [] };
        store.SaveProject(project);
        return store.LoadProject(project.Id)!;
    }
}
