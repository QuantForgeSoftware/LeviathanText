using System.Net;
using System.Text.Json;
using LeviathanText.Contracts;
using LeviathanText.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LeviathanText.Api.Tests;

// Generated accounts only. These are proposed regressions, not executed evidence.
public sealed class MePrincipalDescriptorHttpTests
{
    private const string BootstrapKey = "generated-me-bootstrap-key";
    private const string AccountKey = "generated-me-account-key";
    private sealed class MeFactory(string bootstrap = BootstrapKey) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // ApiStoreLifetime gives each host a unique shared in-memory anchor.
            builder.UseSetting("LeviathanText:StorePath", ":memory:");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["LeviathanText:Auth:ApiKey"] = bootstrap }));
        }
    }
    private static UserAccount Seed(MeFactory factory, string name, UserRole role, string key)
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<LocalStore>().CreateUser(name, role, UserKeys.Hash(key));
    }
    private static void Change(MeFactory factory, Action<LocalStore> change)
    {
        using var scope = factory.Services.CreateScope();
        change(scope.ServiceProvider.GetRequiredService<LocalStore>());
    }
    private static async Task<HttpResponseMessage> Read(HttpClient client, string? key = null, string path = "/api/v1/me")
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.CacheControl = new() { NoCache = true, NoStore = true };
        if (key is not null) request.Headers.Add("X-Api-Key", key);
        return await client.SendAsync(request);
    }
    private static void NoStore(HttpResponseMessage response)
    {
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.True(response.Headers.CacheControl?.Private);
        Assert.Equal(TimeSpan.Zero, response.Headers.CacheControl?.MaxAge);
        Assert.Contains(response.Headers.Pragma, item => item.Name.Equals("no-cache", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(response.Headers.Vary, item => item.Equals("Authorization", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(response.Headers.Vary, item => item.Equals("X-Api-Key", StringComparison.OrdinalIgnoreCase));
        Assert.Null(response.Headers.ETag);
        Assert.Null(response.Content.Headers.LastModified);
    }
    private static async Task<JsonElement> View(HttpResponseMessage response, bool authenticated, string name,
        string role, string mode, string? accountId)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); NoStore(response);
        string body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(BootstrapKey, body, StringComparison.Ordinal);
        Assert.DoesNotContain(AccountKey, body, StringComparison.Ordinal);
        using var json = JsonDocument.Parse(body); var view = json.RootElement;
        Assert.Equal(authenticated, view.GetProperty("authenticated").GetBoolean());
        Assert.Equal(name, view.GetProperty("name").GetString());
        Assert.Equal(role, view.GetProperty("role").GetString());
        Assert.Equal(mode, view.GetProperty("mode").GetString());
        Assert.Equal(accountId, view.GetProperty("accountId").GetString());
        return view.Clone();
    }

    [Fact]
    public async Task Local_mode_preserves_legacy_fields_but_never_claims_an_account()
    {
        using var factory = new MeFactory(""); using var client = factory.CreateClient();
        using var response = await Read(client);
        await View(response, false, "local", "admin", "local", null);
    }

    [Fact]
    public async Task Bootstrap_preserves_legacy_fields_without_a_stable_synthetic_account_id()
    {
        using var factory = new MeFactory(); using var client = factory.CreateClient();
        using var response = await Read(client, BootstrapKey);
        var view = await View(response, true, "bootstrap-admin", "Admin", "bootstrap", null);
        Assert.DoesNotContain("usr-bootstrap", view.GetRawText(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(UserRole.Reader)]
    [InlineData(UserRole.Annotator)]
    [InlineData(UserRole.Reviewer)]
    [InlineData(UserRole.Admin)]
    public async Task Persisted_accounts_have_exact_stable_identity_for_every_read_role(UserRole role)
    {
        using var factory = new MeFactory(); using var client = factory.CreateClient();
        var account = Seed(factory, "generated-account", role, AccountKey);
        using var response = await Read(client, AccountKey);
        await View(response, true, account.Name, role.ToString(), "account", account.Id);
    }

    [Fact]
    public async Task A_real_account_named_bootstrap_admin_is_not_the_bootstrap_principal()
    {
        using var factory = new MeFactory(); using var client = factory.CreateClient();
        var account = Seed(factory, "bootstrap-admin", UserRole.Admin, AccountKey);
        using var bootstrap = await Read(client, BootstrapKey);
        using var persisted = await Read(client, AccountKey);
        await View(bootstrap, true, "bootstrap-admin", "Admin", "bootstrap", null);
        await View(persisted, true, "bootstrap-admin", "Admin", "account", account.Id);
    }

    [Fact]
    public async Task Same_name_and_role_in_distinct_stores_do_not_establish_account_continuity()
    {
        using var first = new MeFactory(); using var second = new MeFactory();
        using var firstClient = first.CreateClient(); using var secondClient = second.CreateClient();
        var a = Seed(first, "same-name", UserRole.Reader, AccountKey);
        var b = Seed(second, "same-name", UserRole.Reader, AccountKey);
        Assert.NotEqual(a.Id, b.Id);
        using var ar = await Read(firstClient, AccountKey); using var br = await Read(secondClient, AccountKey);
        await View(ar, true, a.Name, "Reader", "account", a.Id);
        await View(br, true, b.Name, "Reader", "account", b.Id);
    }

    [Fact]
    public async Task Rotated_account_key_keeps_identity_but_old_key_is_rejected_without_cache_authority()
    {
        const string rotated = "generated-me-rotated-key";
        using var factory = new MeFactory(); using var client = factory.CreateClient();
        var account = Seed(factory, "rotate", UserRole.Reader, AccountKey);
        using var original = await Read(client, AccountKey);
        await View(original, true, account.Name, "Reader", "account", account.Id);
        Change(factory, store => store.UpdateUserKey(account.Id, UserKeys.Hash(rotated)));
        using var rejected = await Read(client, AccountKey);
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode); NoStore(rejected);
        using var current = await Read(client, rotated);
        await View(current, true, account.Name, "Reader", "account", account.Id);
    }

    [Fact]
    public async Task Disabled_account_and_first_account_activation_do_not_fall_back_to_local()
    {
        using var factory = new MeFactory(""); using var client = factory.CreateClient();
        using var local = await Read(client); await View(local, false, "local", "admin", "local", null);
        var account = Seed(factory, "first", UserRole.Reader, AccountKey);
        Change(factory, store => store.DisableUser(account.Id));
        using var anonymous = await Read(client); using var disabled = await Read(client, AccountKey);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode); NoStore(anonymous);
        Assert.Equal(HttpStatusCode.Unauthorized, disabled.StatusCode); NoStore(disabled);
    }

    [Theory]
    [InlineData("/api/v1/me")]
    [InlineData("/api/v1/me/")]
    [InlineData("/API/V1/ME")]
    public async Task Authentication_denials_and_supported_aliases_receive_no_store_before_the_route(string path)
    {
        using var factory = new MeFactory(); using var client = factory.CreateClient();
        using var rejected = await Read(client, "generated-wrong-key", path);
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode); NoStore(rejected);
        using var accepted = await Read(client, BootstrapKey, path);
        await View(accepted, true, "bootstrap-admin", "Admin", "bootstrap", null);
    }
}
