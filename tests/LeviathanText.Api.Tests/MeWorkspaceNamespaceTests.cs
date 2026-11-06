using System.Text.Json;
using LeviathanText.Contracts;
using LeviathanText.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace LeviathanText.Api.Tests;

public sealed class MeWorkspaceNamespaceTests
{
    private const string Key = "generated-namespace-key";
    private const string NamespaceId = "9da6ed1b-167c-4ecf-982b-39e45dd0629d";

    private static string? Read(string? configuredNamespace, PrincipalAdmissionMode mode = PrincipalAdmissionMode.Account)
    {
        using var store = new LocalStore(":memory:");
        var context = new DefaultHttpContext();
        var settings = new Dictionary<string, string?>
        {
            ["LeviathanText:Auth:WorkspaceNamespace"] = configuredNamespace
        };
        if (mode == PrincipalAdmissionMode.Account)
        {
            context.Items["user"] = store.CreateUser("generated-account", UserRole.Reader, UserKeys.Hash(Key));
            context.Request.Headers["X-Api-Key"] = Key;
        }
        else if (mode == PrincipalAdmissionMode.Bootstrap)
        {
            context.Items["user"] = new UserAccount { Id = "usr-bootstrap", Name = "bootstrap-admin", Role = UserRole.Admin,
                CreatedAt = DateTimeOffset.MinValue };
            context.Request.Headers["X-Api-Key"] = Key;
            settings["LeviathanText:Auth:ApiKey"] = Key;
        }
        PrincipalDescriptor.MarkAdmission(context, mode);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        Assert.True(PrincipalDescriptor.TryRead(context, store, configuration, out var descriptor));
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(descriptor, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        return document.RootElement.GetProperty("workspaceNamespace").GetString();
    }

    [Theory]
    [InlineData(NamespaceId)]
    [InlineData("9DA6ED1B-167C-4ECF-982B-39E45DD0629D")]
    public void Explicit_uuid_is_canonical_and_stable_for_persisted_accounts(string value)
    {
        Assert.Equal(NamespaceId, Read(value));
        Assert.Equal(Read(value), Read(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("generated-secret-not-a-namespace")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("9da6ed1b167c4ecf982b39e45dd0629d")]
    [InlineData(" 9da6ed1b-167c-4ecf-982b-39e45dd0629d ")]
    public void Undefined_or_invalid_namespace_never_manufactures_durable_ownership(string? value) => Assert.Null(Read(value));

    [Theory]
    [InlineData("Local")]
    [InlineData("Bootstrap")]
    public void Unpersisted_principals_do_not_receive_a_durable_namespace(string mode) => Assert.Null(Read(NamespaceId, Enum.Parse<PrincipalAdmissionMode>(mode)));

    [Fact]
    public void Unrelated_explicit_namespaces_remain_distinct() =>
        Assert.NotEqual(Read(NamespaceId), Read("601f6a24-564e-477e-a470-be20e7c498a0"));
}
