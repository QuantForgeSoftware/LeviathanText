using LeviathanText.Contracts;
using LeviathanText.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace LeviathanText.Api.Tests;

// Deterministic admission-to-response changes without a timing/race sleep.
public sealed class MePrincipalDescriptorFreshnessTests
{
    private const string Key = "generated-current-key";
    private static IConfiguration Config(string? bootstrap = null) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["LeviathanText:Auth:ApiKey"] = bootstrap }).Build();
    private static DefaultHttpContext Admitted(UserAccount? account, PrincipalAdmissionMode mode, string? key)
    {
        var http = new DefaultHttpContext();
        if (account is not null) http.Items["user"] = account;
        if (key is not null) http.Request.Headers["X-Api-Key"] = key;
        PrincipalDescriptor.MarkAdmission(http, mode);
        return http;
    }

    [Theory]
    [InlineData("disable")]
    [InlineData("rotate")]
    public void Current_store_denies_disabled_or_rotated_account_after_admission(string change)
    {
        using var store = new LocalStore(":memory:");
        var account = store.CreateUser("generated", UserRole.Reader, UserKeys.Hash(Key));
        var http = Admitted(account, PrincipalAdmissionMode.Account, Key);
        if (change == "disable") store.DisableUser(account.Id);
        else store.UpdateUserKey(account.Id, UserKeys.Hash("generated-new-key"));
        Assert.False(PrincipalDescriptor.TryRead(http, store, Config(), out var descriptor));
        Assert.Null(descriptor);
    }

    [Fact]
    public void Local_admission_cannot_be_upgraded_to_first_new_account_by_a_later_matching_header()
    {
        using var store = new LocalStore(":memory:");
        var http = Admitted(null, PrincipalAdmissionMode.Local, Key);
        store.CreateUser("first", UserRole.Reader, UserKeys.Hash(Key));
        Assert.False(PrincipalDescriptor.TryRead(http, store, Config(), out var descriptor));
        Assert.Null(descriptor);
    }

    [Fact]
    public void Bootstrap_current_configuration_is_rechecked_without_treating_synthetic_id_as_account()
    {
        using var store = new LocalStore(":memory:");
        var bootstrap = new UserAccount { Id = "usr-bootstrap", Name = "bootstrap-admin", Role = UserRole.Admin,
            CreatedAt = DateTimeOffset.MinValue };
        var http = Admitted(bootstrap, PrincipalAdmissionMode.Bootstrap, Key);
        Assert.True(PrincipalDescriptor.TryRead(http, store, Config(Key), out var admitted));
        Assert.Equal("bootstrap", admitted!.Mode); Assert.Null(admitted.AccountId);
        Assert.False(PrincipalDescriptor.TryRead(http, store, Config("generated-rotated-bootstrap"), out _));
        Assert.False(PrincipalDescriptor.TryRead(http, store, Config(), out _));
    }

    [Fact]
    public void A_different_current_account_or_unknown_admission_marker_never_supplies_continuity()
    {
        using var store = new LocalStore(":memory:");
        var a = store.CreateUser("a", UserRole.Reader, UserKeys.Hash(Key));
        store.CreateUser("b", UserRole.Reader, UserKeys.Hash("generated-b-key"));
        var changed = Admitted(a, PrincipalAdmissionMode.Account, "generated-b-key");
        Assert.False(PrincipalDescriptor.TryRead(changed, store, Config(), out _));
        var unknown = new DefaultHttpContext(); unknown.Items["user"] = a; unknown.Request.Headers["X-Api-Key"] = Key;
        Assert.False(PrincipalDescriptor.TryRead(unknown, store, Config(), out _));
    }
    [Theory]
    [InlineData("*", "*")]
    [InlineData("Accept-Encoding", "Accept-Encoding, Authorization, X-Api-Key")]
    public void No_store_is_idempotent_preserves_valid_vary_and_removes_cached_validators(string initial, string expected)
    {
        var http = new DefaultHttpContext();
        http.Response.Headers["Vary"] = initial;
        http.Response.Headers["ETag"] = "\"generated-validator\"";
        http.Response.Headers["Last-Modified"] = "Thu, 01 Jan 1970 00:00:00 GMT";
        PrincipalDescriptor.ApplyNoStore(http.Response);
        PrincipalDescriptor.ApplyNoStore(http.Response);
        Assert.Equal(expected, http.Response.Headers["Vary"].ToString());
        Assert.Equal("no-store, private, max-age=0", http.Response.Headers["Cache-Control"].ToString());
        Assert.False(http.Response.Headers.ContainsKey("ETag"));
        Assert.False(http.Response.Headers.ContainsKey("Last-Modified"));
    }
}
