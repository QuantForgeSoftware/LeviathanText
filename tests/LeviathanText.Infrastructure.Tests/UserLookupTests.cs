using LeviathanText.Contracts;

namespace LeviathanText.Infrastructure.Tests;

public sealed class UserLookupTests
{
    [Fact]
    public void Empty_store_has_no_accounts_and_missing_id_stays_absent()
    {
        using var store = new LocalStore(":memory:");
        Assert.False(store.HasUsers());
        Assert.Null(store.FindUserById("missing"));
    }

    [Theory]
    [InlineData(UserRole.Reader)]
    [InlineData(UserRole.Annotator)]
    [InlineData(UserRole.Reviewer)]
    [InlineData(UserRole.Admin)]
    public void Id_lookup_matches_list_including_role_name_timestamp_and_disabled_state(UserRole role)
    {
        using var store = new LocalStore(":memory:");
        var account = store.CreateUser("Ἰωάννης 'quoted'", role, "fixture-key-hash");
        Assert.True(store.HasUsers());
        Assert.Equal(store.LoadUsers().Single(), store.FindUserById(account.Id));
        store.DisableUser(account.Id);
        Assert.True(store.HasUsers()); // Disabled-only stores must never reopen anonymous mode.
        var disabled = store.FindUserById(account.Id);
        Assert.NotNull(disabled);
        Assert.True(disabled.Disabled);
        Assert.Equal(store.LoadUsers().Single(), disabled);
        Assert.Null(store.FindUserByKeyHash("fixture-key-hash"));
    }

    [Fact]
    public void Lookup_selects_only_exact_parameterized_id_and_observes_peer_updates()
    {
        var directory = Directory.CreateTempSubdirectory("lt-user-lookups-");
        try
        {
            string database = Path.Combine(directory.FullName, "store.db");
            using var first = new LocalStore(database);
            using var second = new LocalStore(database);
            var account = first.CreateUser("one", UserRole.Annotator, "fixture-one");
            first.CreateUser("two", UserRole.Admin, "fixture-two");
            Assert.True(second.HasUsers());
            Assert.Equal(first.LoadUsers().Single(user => user.Id == account.Id), second.FindUserById(account.Id));
            Assert.Null(second.FindUserById(account.Id.ToUpperInvariant()));
            Assert.Null(second.FindUserById("' OR 1=1 --"));
            first.DisableUser(account.Id);
            Assert.True(second.FindUserById(account.Id)!.Disabled);
        }
        finally { directory.Delete(recursive: true); }
    }
}
