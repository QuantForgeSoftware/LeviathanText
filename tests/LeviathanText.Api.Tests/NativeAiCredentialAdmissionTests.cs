using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using System.Text;
using System.Text.Json;
using LeviathanText.AI;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LeviathanText.Api.Tests;

public sealed class NativeAiCredentialAdmissionTests
{
    private const string Bootstrap = "generated-bootstrap-admission";
    private const string AccountKey = "generated-account-admission";

    [Theory]
    [InlineData("account-rotate", true)]
    [InlineData("bootstrap-replace", true)]
    [InlineData("bootstrap-remove", true)]
    [InlineData("account-readonly", true)]
    [InlineData("bootstrap-readonly", true)]
    [InlineData("account-unchanged", false)]
    [InlineData("bootstrap-unchanged", false)]
    [InlineData("account-unchanged-bearer", false)]
    [InlineData("account-unchanged-query", false)]
    [InlineData("account-rotate-bearer", true)]
    [InlineData("account-rotate-query", true)]
    public async Task Current_credential_and_write_policy_are_required_after_provider_await(string change, bool denied)
    {
        var directory = Directory.CreateTempSubdirectory("lt-ai-admission-").FullName;
        try
        {
            using var provider = new HeldProvider();
            using var factory = new Factory(Path.Combine(directory, "store.db"), provider);
            using var client = factory.CreateClient();
            var accountRequest = change.StartsWith("account", StringComparison.Ordinal);
            if (change.EndsWith("-bearer", StringComparison.Ordinal)) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccountKey);
            else if (!change.EndsWith("-query", StringComparison.Ordinal)) client.DefaultRequestHeaders.Add("X-Api-Key", accountRequest ? AccountKey : Bootstrap);
            EntityId projectId = new(EntityKind.Collection, "generated-admission");
            string accountId;
            string before;
            using (var scope = factory.Services.CreateScope())
            {
                var store = scope.ServiceProvider.GetRequiredService<LocalStore>();
                accountId = store.CreateUser("generated-admission", UserRole.Annotator, UserKeys.Hash(AccountKey)).Id;
                store.SaveProject(Project(projectId));
                store.AddProjectMember(projectId, accountId);
                before = NativeJson.Save(store.LoadProject(projectId)!);
            }
            var pending = client.PostAsJsonAsync(change.EndsWith("-query", StringComparison.Ordinal) ? "/api/v1/proposals?api_key=" + Uri.EscapeDataString(AccountKey) : "/api/v1/proposals", new
            {
                targetRevisionId = "rev-generated-admission", range = new { start = 1, end = 2 },
                task = "candidateReading", sourceText = "untrusted", providerConfigRef = "generated-admission", promptVersion = "generated-v1",
            });
            try
            {
                await provider.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var configuration = factory.Services.GetRequiredService<IConfiguration>();
                if (change.StartsWith("account-rotate", StringComparison.Ordinal))
                {
                    using var scope = factory.Services.CreateScope();
                    scope.ServiceProvider.GetRequiredService<LocalStore>().UpdateUserKey(accountId, UserKeys.Hash("generated-replacement-key"));
                }
                if (change == "bootstrap-replace") configuration["LeviathanText:Auth:ApiKey"] = "generated-replacement-bootstrap";
                if (change == "bootstrap-remove") configuration["LeviathanText:Auth:ApiKey"] = null;
                if (change.EndsWith("readonly", StringComparison.Ordinal)) configuration["LeviathanText:Auth:ReadOnly"] = "true";
            }
            finally { provider.Resume.TrySetResult(); }
            using var response = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            var body = await response.Content.ReadAsStringAsync();
            Assert.Equal(denied ? HttpStatusCode.Forbidden : HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(1, provider.Calls);
            using var finalScope = factory.Services.CreateScope();
            var after = finalScope.ServiceProvider.GetRequiredService<LocalStore>().LoadProject(projectId)!;
            if (denied)
            {
                Assert.Equal(before, NativeJson.Save(after));
                Assert.DoesNotContain("generated-provider-output", body);
            }
            else
            {
                Assert.Contains("generated-provider-output", body);
                Assert.Single(after.Objects[0].Attachments, a => a.Role == "ai-proposal.v1");
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void Account_binding_never_falls_back_to_a_null_captured_credential_hash()
    {
        var http = new DefaultHttpContext();
        http.Items["user"] = new UserAccount { Id = "usr-generated", Name = "generated", Role = UserRole.Annotator, CreatedAt = DateTimeOffset.UnixEpoch };
        Assert.Throws<NativeAiAccessDeniedException>(() => JobStreamAccessPolicy.CapturedAccountKeyHash(http));
    }

    [Theory]
    [InlineData("account-rotate")]
    [InlineData("bootstrap-replace")]
    [InlineData("bootstrap-readonly")]
    public async Task Revocation_immediately_before_native_save_cannot_commit_an_issued_record(string change)
    {
        var directory = Directory.CreateTempSubdirectory("lt-ai-commit-admission-").FullName;
        try
        {
            using var provider = new HeldProvider();
            provider.Resume.TrySetResult();
            using var factory = new Factory(Path.Combine(directory, "store.db"), provider);
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Api-Key", change == "account-rotate" ? AccountKey : Bootstrap);
            EntityId projectId = new(EntityKind.Collection, "generated-admission");
            string accountId;
            string before;
            using (var scope = factory.Services.CreateScope())
            {
                var store = scope.ServiceProvider.GetRequiredService<LocalStore>();
                accountId = store.CreateUser("generated-admission", UserRole.Annotator, UserKeys.Hash(AccountKey)).Id;
                store.SaveProject(Project(projectId)); store.AddProjectMember(projectId, accountId);
                before = NativeJson.Save(store.LoadProject(projectId)!);
            }
            int entered = 0;
            factory.BeforeSave = () =>
            {
                entered++;
                if (change == "account-rotate")
                {
                    using var scope = factory.Services.CreateScope();
                    scope.ServiceProvider.GetRequiredService<LocalStore>().UpdateUserKey(accountId, UserKeys.Hash("generated-new-key-before-save"));
                }
                else factory.Services.GetRequiredService<IConfiguration>()[change == "bootstrap-replace"
                    ? "LeviathanText:Auth:ApiKey" : "LeviathanText:Auth:ReadOnly"] = change == "bootstrap-replace" ? "generated-new-bootstrap" : "true";
            };
            using var response = await client.PostAsJsonAsync("/api/v1/proposals", new
            {
                targetRevisionId = "rev-generated-admission", range = new { start = 1, end = 2 },
                task = "candidateReading", sourceText = "untrusted", providerConfigRef = "generated-admission", promptVersion = "generated-v1",
            });
            Assert.Equal(1, entered);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            using var finalScope = factory.Services.CreateScope();
            Assert.Equal(before, NativeJson.Save(finalScope.ServiceProvider.GetRequiredService<LocalStore>().LoadProject(projectId)!));
        }
        finally { Directory.Delete(directory, true); }
    }

    private sealed class BeforeSaveStore(IProjectStore inner, Action beforeSave) : IProjectStore
    {
        public NativeProject? LoadProject(EntityId id) => inner.LoadProject(id);
        public IReadOnlyList<(EntityId Id, string Name)> ListProjects() => inner.ListProjects();
        public long SaveProject(NativeProject project) { beforeSave(); return inner.SaveProject(project); }
        public void DeleteProject(EntityId id) => inner.DeleteProject(id);
    }

    private static NativeProject Project(EntityId id)
    {
        EntityId lineId = new(EntityKind.Line, "generated-admission");
        EntityId revisionId = new(EntityKind.TranscriptionRevision, "generated-admission");
        return new NativeProject
        {
            Id = id, Name = "Generated admission fixture", Objects = [new NativeObject
            {
                Id = new(EntityKind.PhysicalObject, "generated-admission"), Surfaces = [new NativeSurface
                {
                    Id = new(EntityKind.Surface, "generated-admission"), Kind = "generated", Captures = [], Regions = [],
                    Lines = [new NativeLine { Id = lineId, RegionId = default, FrameRef = default, DiplomaticRevisionId = revisionId }],
                    Revisions = [new TextRevision { Id = revisionId, ScopeId = lineId, Layer = TextLayer.L1Diplomatic,
                        Text = "abcde", Author = new AgentRef { Id = "generated-source", IsModel = false }, CreatedAt = DateTimeOffset.UnixEpoch }],
                }],
            }],
        };
    }

    private sealed class HeldProvider : HttpMessageHandler
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++; Entered.TrySetResult(); await Resume.Task.WaitAsync(cancellationToken);
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new
                { choices = new[] { new { message = new { content = "generated-provider-output" } } } }), Encoding.UTF8, "application/json") };
        }
    }

    private sealed class Factory(string path, HeldProvider provider) : WebApplicationFactory<Program>
    {
        public Action? BeforeSave;
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("LeviathanText:StorePath", path);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LeviathanText:Auth:ApiKey"] = Bootstrap,
                ["LeviathanText:Auth:ProjectMembership"] = "true",
                ["LeviathanText:Auth:ReadOnly"] = "false",
            }));
            builder.ConfigureTestServices(services =>
            {
                var original = services.Last(service => service.ServiceType == typeof(IProjectStore));
                services.RemoveAll<IProjectStore>();
                services.AddScoped<IProjectStore>(sp => new BeforeSaveStore((IProjectStore)original.ImplementationFactory!(sp),
                    () => Interlocked.Exchange(ref BeforeSave, null)?.Invoke()));
                services.RemoveAll<AiProviderRegistry>();
                services.AddSingleton(new AiProviderRegistry([new AiProviderSettings
                {
                    ProviderId = "generated-admission", Host = AiProviderHost.Local, Protocol = AiWireProtocol.OpenAiChatCompletions,
                    Endpoint = new Uri("https://generated.invalid/v1"), ModelId = "generated", MaxAttempts = 1, Timeout = TimeSpan.FromSeconds(15),
                }], new HttpClient(provider)));
            });
        }
    }
}
