using System.Reflection;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LeviathanText.Api.Tests;

public sealed class RequestWritePolicyRefreshTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Resolver_installed_prewrite_policy_refreshes_membership_without_changing_actor_or_key(bool initiallyEnabled, bool enabledAtSave)
    {
        var directory = Directory.CreateTempSubdirectory("lt-request-write-policy-").FullName;
        try
        {
            using var store = new LocalStore(Path.Combine(directory, "store.db"));
            var hash = UserKeys.Hash("generated-policy-key");
            var user = store.CreateUser("generated-policy", UserRole.Annotator, hash);
            var project = new NativeProject { Id = new(EntityKind.Collection, "generated-policy"), Name = "unchanged", Objects = [] };
            store.SaveProject(project); project = store.LoadProject(project.Id)!; store.AddProjectMember(project.Id, user.Id);
            var before = NativeJson.Save(project);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LeviathanText:Auth:ApiKey"] = "generated-bootstrap",
                ["LeviathanText:Auth:ProjectMembership"] = initiallyEnabled.ToString(),
            }).Build();
            using var services = new ServiceCollection().AddScoped(_ => new RequestProjectStore(store)).BuildServiceProvider();
            using var scope = services.CreateScope();
            var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
            http.Items["user"] = user; http.Request.Headers["X-Api-Key"] = "generated-policy-key";
            new NativeAiProjectResolver(store, configuration).ReloadAuthorized(http, project.Id);
            var requestStore = scope.ServiceProvider.GetRequiredService<RequestProjectStore>();
            var fields = BindingFlags.NonPublic | BindingFlags.Instance;
            object Authorization() => typeof(RequestProjectStore).GetField("_writeAuthorization", fields)!.GetValue(requestStore)!;
            object? Value(object authorization, string name) => authorization.GetType().GetProperty(name)!.GetValue(authorization);
            Assert.Equal(initiallyEnabled, Value(Authorization(), "RequireMembership"));
            configuration["LeviathanText:Auth:ProjectMembership"] = enabledAtSave.ToString();
            // Exercise the real resolver-installed callback directly so membership
            // can be removed deterministically after this admission and before SQL.
            var policy = (Action)typeof(RequestProjectStore).GetField("_beforeWrite", fields)!.GetValue(requestStore)!;
            policy();
            var refreshed = Authorization();
            Assert.Equal(enabledAtSave, Value(refreshed, "RequireMembership"));
            Assert.Equal(user.Id, Value(refreshed, "ActorId"));
            Assert.Equal(hash, Value(refreshed, "ExpectedKeyHash"));
            store.RemoveProjectMember(project.Id, user.Id);
            // Composition: actual installed callback state plus the actual SQL guard;
            // this is not a new end-to-end scheduler race or atomic config guarantee.
            if (enabledAtSave)
            {
                Assert.Throws<ProjectWriteAccessDeniedException>(() => store.SaveProjectAuthorized(project with { Name = "must-not-save" },
                    user.Id, (bool)Value(refreshed, "RequireMembership")!, (string?)Value(refreshed, "ExpectedKeyHash")));
                Assert.Equal(before, NativeJson.Save(store.LoadProject(project.Id)!));
            }
            else
            {
                Assert.Equal(project.Version + 1, store.SaveProjectAuthorized(project with { Name = "opt-out-save" }, user.Id,
                    (bool)Value(refreshed, "RequireMembership")!, (string?)Value(refreshed, "ExpectedKeyHash")));
                Assert.Equal("opt-out-save", store.LoadProject(project.Id)!.Name);
            }
        }
        finally { Directory.Delete(directory, true); }
    }
}
