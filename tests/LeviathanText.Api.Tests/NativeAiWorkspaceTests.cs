using System.Net;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LeviathanText.AI;
using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Contracts.Serialization;
using LeviathanText.Formats;
using LeviathanText.Core.Revisions;
using LeviathanText.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SkiaSharp;

namespace LeviathanText.Api.Tests;

public sealed class NativeAiWorkspaceTests : IDisposable
{
    private const string AdminKey = "native-ai-generated-bootstrap";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lt-native-ai-http", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("preview")]
    [InlineData("accept")]
    public async Task Oversized_batch_is_refused_before_target_resolution(string action)
    {
        var provider = new ProviderDouble();
        using var factory = new Factory(Path.Combine(_root, "store.db"), provider);
        using var client = factory.CreateClient();
        var project = await Import(client, factory);
        var issued = await Issue(client, project);
        var before = NativeJson.Save(Load(factory, project.Id));
        var oversized = Enumerable.Range(0, NativeAiProposalService.MaxBatchSize + 1)
            .Select(index => issued with { Id = new(EntityKind.Proposal, $"oversized-{index}") }).ToArray();

        using var response = await Bulk(client, action, oversized);

        // The first ID is deliberately unissued. A lookup would return404 instead
        // of enforcing the batch bound before any project/record resolution.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("stageContractViolation", payload.RootElement.GetProperty("code").GetString());
        Assert.Contains(NativeAiProposalService.MaxBatchSize.ToString(), payload.RootElement.GetProperty("message").GetString());
        Assert.Single(provider.Bodies);
        Assert.Equal(before, NativeJson.Save(Load(factory, project.Id)));
    }

    private sealed class ProviderDouble : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Hold { get; init; }
        public string Text { get; init; } = "Ω";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            Entered.TrySetResult();
            if (Hold) await Resume.Task.WaitAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    choices = new[] { new { message = new { content = Text } } },
                    usage = new { prompt_tokens = 4, completion_tokens = 1 },
                }), Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class SaveGate
    {
        public Action<NativeProject>? BeforeSave { get; set; }
        public int Triggered { get; private set; }
        public void Run(NativeProject project)
        {
            var action = BeforeSave;
            if (action is null) return;
            BeforeSave = null;
            Triggered++;
            action(project);
        }
    }

    private sealed class BeforeSaveStore(IProjectStore inner, SaveGate gate) : IProjectStore
    {
        public NativeProject? LoadProject(EntityId id) => inner.LoadProject(id);
        public IReadOnlyList<(EntityId Id, string Name)> ListProjects() => inner.ListProjects();
        public long SaveProject(NativeProject project) { gate.Run(project); return inner.SaveProject(project); }
        public void DeleteProject(EntityId id) => inner.DeleteProject(id);
    }

    private sealed class Factory(string path, ProviderDouble provider, bool authenticated = false, bool membership = false, SaveGate? saveGate = null)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("LeviathanText:StorePath", path);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LeviathanText:Auth:ApiKey"] = authenticated ? AdminKey : null,
                ["LeviathanText:Auth:ProjectMembership"] = membership ? "true" : "false",
            }));
            builder.ConfigureTestServices(services =>
            {
                if (saveGate is not null)
                {
                    var original = services.Last(service => service.ServiceType == typeof(IProjectStore));
                    services.RemoveAll<IProjectStore>();
                    services.AddScoped<IProjectStore>(sp => new BeforeSaveStore((IProjectStore)original.ImplementationFactory!(sp), saveGate));
                }
                services.RemoveAll<AiProviderRegistry>();
                services.AddSingleton(new AiProviderRegistry([new AiProviderSettings
                {
                    ProviderId = "generated-provider", Host = AiProviderHost.Local, Protocol = AiWireProtocol.OpenAiChatCompletions,
                    Endpoint = new Uri("https://generated.invalid/v1"), ModelId = "generated-model", MaxAttempts = 1,
                    BaseRetryDelay = TimeSpan.Zero, Timeout = TimeSpan.FromSeconds(20),
                }], new HttpClient(provider)));
            });
        }
    }

    private async Task<NativeProject> Import(HttpClient client, Factory factory, string text = "αβγδε")
    {
        Directory.CreateDirectory(_root);
        var token = Guid.NewGuid().ToString("N");
        var xml = Path.Combine(_root, token + ".xml");
        var image = Path.Combine(_root, token + ".png");
        await File.WriteAllTextAsync(xml, $"""
            <TEI xmlns="http://www.tei-c.org/ns/1.0"><teiHeader><fileDesc><titleStmt><title>Generated fixture</title></titleStmt><publicationStmt><p>Generated test</p></publicationStmt><sourceDesc><p>Invented test text</p></sourceDesc></fileDesc></teiHeader><text><body><div type="edition"><ab><lb n="1"/>{text}</ab></div></body></text></TEI>
            """);
        using (var bitmap = new SKBitmap(64, 24))
        {
            bitmap.Erase(SKColors.White);
            using var data = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
            using var output = File.Create(image);
            data.SaveTo(output);
        }
        var created = await client.PostAsJsonAsync("/api/v1/projects", new { name = "generated AI " + token });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var info = await created.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        var projectId = info!["id"];
        var imported = await client.PostAsJsonAsync($"/api/v1/projects/{projectId}/import-tei", new { documentPath = xml, imagePath = image });
        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IProjectStore>().LoadProject(EntityId.Parse(projectId))!;
    }

    private static object Request(NativeProject project, string task = "expansion", string source = "UNTRUSTED OVERRIDE", int lineIndex = 0) => new
    {
        targetRevisionId = project.Objects[0].Surfaces[0].Lines[lineIndex].DiplomaticRevisionId.ToString(),
        range = new { start = 1, end = 3 }, task, sourceText = source,
        providerConfigRef = "generated-provider", promptVersion = "fixture-v1",
    };

    private static NativeProject Load(Factory factory, EntityId id)
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IProjectStore>().LoadProject(id)!;
    }

    private static HttpClient Admin(Factory factory)
    {
        var client = factory.CreateClient(); client.DefaultRequestHeaders.Add("X-Api-Key", AdminKey); return client;
    }

    private static async Task<(HttpClient Client, string Id)> Account(Factory factory, HttpClient admin, string role = "annotator")
    {
        var response = await admin.PostAsJsonAsync("/api/v1/users", new { name = "generated-" + Guid.NewGuid().ToString("N"), role });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var client = factory.CreateClient(); client.DefaultRequestHeaders.Add("X-Api-Key", json.RootElement.GetProperty("apiKey").GetString());
        return (client, json.RootElement.GetProperty("account").GetProperty("id").GetString()!);
    }

    private static async Task<AiProposal> Issue(HttpClient client, NativeProject project, string task = "expansion", int lineIndex = 0, string? explicitProject = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/proposals" + (explicitProject is null ? "" : "?projectId=" + explicitProject), Request(project, task, lineIndex: lineIndex));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(raw); Assert.Equal(JsonValueKind.Object, json.RootElement.ValueKind);
        return ContractJson.FromJson<AiProposal>(raw);
    }

    private static Task<HttpResponseMessage> Accept(HttpClient client, AiProposal proposal, string? routeId = null, string? projectId = null) =>
        client.PostAsync($"/api/v1/proposals/{routeId ?? proposal.Id.ToString()}/accept", new StringContent(
            ContractJson.ToJson(new { proposal, acceptorId = "submitted-editor-label", projectId }), Encoding.UTF8, "application/json"));

    private static Task<HttpResponseMessage> Bulk(HttpClient client, string action, params AiProposal[] proposals) =>
        client.PostAsync("/api/v1/proposals/bulk-" + action, new StringContent(
            ContractJson.ToJson(new { proposals, acceptorId = "submitted-editor-label" }), Encoding.UTF8, "application/json"));

    [Fact]
    public async Task Native_import_request_accept_restart_export_preserves_L1_and_source_with_durable_L2()
    {
        var storePath = Path.Combine(_root, "store.db");
        var provider = new ProviderDouble();
        NativeProject before;
        string issued;
        string proposalId;
        string saved;
        using (var factory = new Factory(storePath, provider))
        using (var client = factory.CreateClient())
        {
            before = await Import(client, factory);
            var response = await client.PostAsJsonAsync("/api/v1/proposals", Request(before));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            issued = await response.Content.ReadAsStringAsync();
            using var json = JsonDocument.Parse(issued);
            Assert.Equal(JsonValueKind.Object, json.RootElement.ValueKind);
            proposalId = json.RootElement.GetProperty("id").GetString()!;
            Assert.Equal("βγ", json.RootElement.GetProperty("request").GetProperty("sourceText").GetString());
            Assert.DoesNotContain("UNTRUSTED OVERRIDE", Assert.Single(provider.Bodies));
            var accepted = await client.PostAsync($"/api/v1/proposals/{proposalId}/accept", new StringContent(
                $"{{\"proposal\":{issued},\"acceptorId\":\"local-editor\"}}", Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            var after = Load(factory, before.Id);
            var surface = after.Objects[0].Surfaces[0];
            Assert.Equal("αβγδε", surface.Revisions.Single(r => r.Layer == TextLayer.L1Diplomatic).Text);
            var editorial = surface.Revisions.Single(r => r.Layer == TextLayer.L2Editorial);
            Assert.Equal("αΩδε", editorial.Text);
            Assert.Equal("local-editor", editorial.Author.Id);
            Assert.Contains(after.Objects[0].Attachments, a => a.Role == "ai-proposal.v1");
            Assert.Contains(after.Objects[0].Attachments, a => a.Role == "ai-proposal-acceptance.v1");
            var original = before.Objects[0].Attachments[0];
            Assert.Contains(after.Objects[0].Attachments, a => a.Id == original.Id && a.Content == original.Content && a.Sha256 == original.Sha256);
            saved = NativeJson.Save(after);
        }
        using var restarted = new Factory(storePath, new ProviderDouble());
        using var restartedClient = restarted.CreateClient();
        Assert.Equal(saved, NativeJson.Save(Load(restarted, before.Id)));
        Assert.Equal(HttpStatusCode.OK, (await restartedClient.PostAsync($"/api/v1/proposals/{proposalId}/accept", new StringContent(
            $"{{\"proposal\":{issued},\"acceptorId\":\"local-editor\"}}", Encoding.UTF8, "application/json"))).StatusCode);
        Assert.Equal(saved, NativeJson.Save(Load(restarted, before.Id)));
        var surfaceId = before.Objects[0].Surfaces[0].Id;
        var exported = await restartedClient.GetStringAsync($"/api/v1/projects/{before.Id}/surfaces/{surfaceId}/diplomatic");
        using var diplomatic = JsonDocument.Parse(exported);
        Assert.Contains("αβγδε", diplomatic.RootElement.GetProperty("text").GetString());
        var csv = await restartedClient.GetStringAsync($"/api/v1/projects/{before.Id}/lines.csv");
        Assert.Contains("αβγδε", csv);
        var bundleBytes = await restartedClient.GetByteArrayAsync($"/api/v1/projects/{before.Id}/bundle");
        using var bundle = new ZipArchive(new MemoryStream(bundleBytes));
        using var nativeReader = new StreamReader(bundle.GetEntry("project.json")!.Open());
        Assert.Equal(saved, NativeJson.Save(NativeJson.Load(await nativeReader.ReadToEndAsync())));
        var tei = await restartedClient.GetStringAsync($"/api/v1/projects/{before.Id}/surfaces/{surfaceId}/tei");
        var roundtrip = TeiXml.Import(tei);
        Assert.Contains(roundtrip.Revisions, r => r.Layer == TextLayer.L1Diplomatic && r.Text == "αβγδε");
        Assert.Contains(roundtrip.Revisions, r => r.Layer == TextLayer.L2Editorial && r.Text == "αΩδε");
    }

    [Fact]
    public async Task Tampered_graph_route_mismatch_unknown_issued_id_and_stale_L1_never_mutate_the_project()
    {
        using var factory = new Factory(Path.Combine(_root, "store.db"), new ProviderDouble());
        using var client = factory.CreateClient();
        var original = await Import(client, factory);
        var proposal = await Issue(client, original);
        var issued = NativeJson.Save(Load(factory, original.Id));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Accept(client, proposal with { ProposedText = "FORGED" })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Accept(client, proposal, "prop-route-mismatch")).StatusCode);
        var unknown = proposal with { Id = new(EntityKind.Proposal, "unknown") };
        Assert.Equal(HttpStatusCode.NotFound, (await Accept(client, unknown)).StatusCode);
        Assert.Equal(issued, NativeJson.Save(Load(factory, original.Id)));
        var line = original.Objects[0].Surfaces[0].Lines[0];
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/v1/lines/{line.Id}/revisions", new
        {
            layer = "l1Diplomatic", text = "αβγδεζ", authorId = "local-writer", parentRevisionId = line.DiplomaticRevisionId.ToString(),
        })).StatusCode);
        var updated = NativeJson.Save(Load(factory, original.Id));
        Assert.Equal(HttpStatusCode.Conflict, (await Accept(client, proposal)).StatusCode);
        Assert.Equal(updated, NativeJson.Save(Load(factory, original.Id)));
    }

    [Fact]
    public async Task Advisory_results_are_returned_as_objects_but_cannot_be_accepted_as_editorial_text()
    {
        using var factory = new Factory(Path.Combine(_root, "store.db"), new ProviderDouble());
        using var client = factory.CreateClient();
        var project = await Import(client, factory);
        var proposal = await Issue(client, project, "translation");
        var before = NativeJson.Save(Load(factory, project.Id));
        var preview = await Bulk(client, "preview", proposal);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        using var json = JsonDocument.Parse(await preview.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("isFullyApplicable").GetBoolean());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Accept(client, proposal)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Bulk(client, "accept", proposal)).StatusCode);
        Assert.Equal(before, NativeJson.Save(Load(factory, project.Id)));
    }

    [Fact]
    public async Task Bulk_accepts_one_project_atomically_and_refuses_cross_project_or_duplicate_batches()
    {
        using var factory = new Factory(Path.Combine(_root, "store.db"), new ProviderDouble());
        using var client = factory.CreateClient();
        var project = await Import(client, factory, "αβγδε<lb n=\"2\"/>ζηθικ");
        var first = await Issue(client, project);
        var second = await Issue(client, project, lineIndex: 1);
        var foreignProject = await Import(client, factory);
        var foreign = await Issue(client, foreignProject);
        var before = Load(factory, project.Id);
        var foreignBefore = NativeJson.Save(Load(factory, foreignProject.Id));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Bulk(client, "accept", first, foreign)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Bulk(client, "preview", first, first)).StatusCode);
        Assert.Equal(NativeJson.Save(before), NativeJson.Save(Load(factory, project.Id)));
        Assert.Equal(foreignBefore, NativeJson.Save(Load(factory, foreignProject.Id)));
        var preview = await Bulk(client, "preview", first, second);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        using var json = JsonDocument.Parse(await preview.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Object, json.RootElement.ValueKind);
        Assert.True(json.RootElement.GetProperty("isFullyApplicable").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await Bulk(client, "accept", first, second)).StatusCode);
        var saved = Load(factory, project.Id);
        Assert.Equal(before.Version + 1, saved.Version);
        Assert.Equal(2, saved.Objects[0].Surfaces[0].Revisions.Count(r => r.Layer == TextLayer.L2Editorial));
        Assert.Equal(project.Objects[0].Surfaces[0].Revisions.Select(r => r.Text),
            saved.Objects[0].Surfaces[0].Revisions.Where(r => r.Layer == TextLayer.L1Diplomatic).Select(r => r.Text));
    }

    [Fact]
    public async Task Ambiguous_revision_and_proposal_ids_require_explicit_project_selection()
    {
        var provider = new ProviderDouble();
        using var factory = new Factory(Path.Combine(_root, "store.db"), provider);
        using var client = factory.CreateClient();
        var project = await Import(client, factory);
        using (var scope = factory.Services.CreateScope())
            scope.ServiceProvider.GetRequiredService<IProjectStore>().SaveProject(project with
            {
                Id = new(EntityKind.Collection, "duplicate-project"), Version = 0, Name = "explicit ambiguity fixture",
            });
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/v1/proposals", Request(project))).StatusCode);
        Assert.Empty(provider.Bodies);
        var proposal = await Issue(client, project, explicitProject: project.Id.ToString());
        var withIssued = Load(factory, project.Id);
        using (var scope = factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IProjectStore>();
            var duplicate = store.LoadProject(new(EntityKind.Collection, "duplicate-project"))!;
            store.SaveProject(duplicate with { Objects = withIssued.Objects });
        }
        Assert.Equal(HttpStatusCode.Conflict, (await Accept(client, proposal)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Accept(client, proposal, projectId: project.Id.ToString())).StatusCode);
    }

    [Fact]
    public async Task Nonmembers_are_rejected_before_dispatch_preview_or_acceptance()
    {
        var provider = new ProviderDouble();
        using var factory = new Factory(Path.Combine(_root, "store.db"), provider, true, true);
        using var admin = Admin(factory);
        var project = await Import(admin, factory);
        var account = await Account(factory, admin);
        using var outsider = account.Client;
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.PostAsJsonAsync("/api/v1/proposals", Request(project))).StatusCode);
        Assert.Empty(provider.Bodies);
        var proposal = await Issue(admin, project);
        var before = NativeJson.Save(Load(factory, project.Id));
        Assert.Equal(HttpStatusCode.Forbidden, (await Bulk(outsider, "preview", proposal)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Accept(outsider, proposal)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Bulk(outsider, "accept", proposal)).StatusCode);
        Assert.Equal(before, NativeJson.Save(Load(factory, project.Id)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Revocation_or_disable_while_the_provider_is_pending_prevents_issuance_and_disclosure(bool disable)
    {
        var provider = new ProviderDouble { Hold = true };
        using var factory = new Factory(Path.Combine(_root, "store.db"), provider, true, true);
        using var admin = Admin(factory);
        var project = await Import(admin, factory);
        var account = await Account(factory, admin);
        using var member = account.Client;
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/projects/{project.Id}/members", new { userId = account.Id })).StatusCode);
        var before = NativeJson.Save(Load(factory, project.Id));
        var pending = member.PostAsJsonAsync("/api/v1/proposals", Request(project));
        try
        {
            await provider.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var revoked = disable ? await admin.PostAsync($"/api/v1/users/{account.Id}/disable", null)
                : await admin.DeleteAsync($"/api/v1/projects/{project.Id}/members/{account.Id}");
            Assert.True(revoked.IsSuccessStatusCode);
        }
        finally { provider.Resume.TrySetResult(); }
        var result = await pending;
        Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
        Assert.DoesNotContain("Ω", await result.Content.ReadAsStringAsync());
        Assert.Equal(before, NativeJson.Save(Load(factory, project.Id)));
    }

    [Fact]
    public async Task Authenticated_single_acceptance_records_stable_account_id_and_preserves_source()
    {
        using var factory = new Factory(Path.Combine(_root, "store.db"), new ProviderDouble(), true, true);
        using var admin = Admin(factory);
        var project = await Import(admin, factory);
        var account = await Account(factory, admin);
        using var member = account.Client;
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/projects/{project.Id}/members", new { userId = account.Id })).StatusCode);
        var proposal = await Issue(member, project);
        Assert.Equal(HttpStatusCode.OK, (await Accept(member, proposal)).StatusCode);
        var saved = Load(factory, project.Id);
        Assert.Equal(account.Id, saved.Objects[0].Surfaces[0].Revisions.Single(r => r.Layer == TextLayer.L2Editorial).Author.Id);
        Assert.Equal(project.Objects[0].Surfaces[0].Revisions[0].Author.Id, saved.Objects[0].Surfaces[0].Revisions[0].Author.Id);
    }

    [Fact]
    public async Task Zero_width_restoration_exports_as_supplied_L2_without_entering_diplomatic_L1()
    {
        using var factory = new Factory(Path.Combine(_root, "store.db"), new ProviderDouble());
        using var client = factory.CreateClient();
        var project = await Import(client, factory);
        var line = project.Objects[0].Surfaces[0].Lines[0];
        var response = await client.PostAsJsonAsync("/api/v1/proposals", new
        {
            targetRevisionId = line.DiplomaticRevisionId.ToString(), range = new { start = 2, end = 2 },
            task = "restoration", providerConfigRef = "generated-provider", promptVersion = "fixture-v1",
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var proposal = ContractJson.FromJson<AiProposal>(await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await Accept(client, proposal)).StatusCode);
        var surfaceId = project.Objects[0].Surfaces[0].Id;
        var tei = await client.GetStringAsync($"/api/v1/projects/{project.Id}/surfaces/{surfaceId}/tei");
        Assert.Contains("<supplied", tei);
        var roundtrip = TeiXml.Import(tei);
        Assert.Contains(roundtrip.Revisions, r => r.Layer == TextLayer.L1Diplomatic && r.Text == "αβγδε");
        Assert.Contains(roundtrip.Revisions, r => r.Layer == TextLayer.L2Editorial && r.Text == "αβΩγδε");
        Assert.Equal("αβγδε", Load(factory, project.Id).Objects[0].Surfaces[0].Revisions.Single(r => r.Layer == TextLayer.L1Diplomatic).Text);
    }

    [Fact]
    public async Task Unknown_provider_configuration_error_is_preserved_after_native_authorization()
    {
        var provider = new ProviderDouble();
        using var factory = new Factory(Path.Combine(_root, "store.db"), provider);
        using var client = factory.CreateClient();
        var project = await Import(client, factory);
        var line = project.Objects[0].Surfaces[0].Lines[0];
        var response = await client.PostAsJsonAsync("/api/v1/proposals", new
        {
            targetRevisionId = line.DiplomaticRevisionId.ToString(), range = new { start = 0, end = 1 },
            task = "candidateReading", providerConfigRef = "unknown-provider", promptVersion = "fixture-v1",
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("No configured provider", await response.Content.ReadAsStringAsync());
        Assert.Empty(provider.Bodies);
    }

    [Fact]
    public async Task A_source_changed_during_provider_work_prevents_issuing_the_stale_result()
    {
        var provider = new ProviderDouble { Hold = true };
        using var factory = new Factory(Path.Combine(_root, "store.db"), provider);
        using var client = factory.CreateClient();
        var project = await Import(client, factory);
        var line = project.Objects[0].Surfaces[0].Lines[0];
        var pending = client.PostAsJsonAsync("/api/v1/proposals", Request(project));
        try
        {
            await provider.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/v1/lines/{line.Id}/revisions", new
            {
                layer = "l1Diplomatic", text = "αβγδεζ", authorId = "local-writer", parentRevisionId = line.DiplomaticRevisionId.ToString(),
            })).StatusCode);
        }
        finally { provider.Resume.TrySetResult(); }
        Assert.Equal(HttpStatusCode.Conflict, (await pending).StatusCode);
        Assert.DoesNotContain(Load(factory, project.Id).Objects[0].Attachments, a => a.Role == "ai-proposal.v1");
    }

    [Fact]
    public async Task An_unlinked_existing_editorial_change_survives_acceptance_and_reports_TEI_layer_loss()
    {
        using var factory = new Factory(Path.Combine(_root, "store.db"), new ProviderDouble());
        using var client = factory.CreateClient();
        var project = await Import(client, factory);
        var line = project.Objects[0].Surfaces[0].Lines[0];
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/v1/lines/{line.Id}/revisions", new
        {
            layer = "l2Editorial", text = "δαβγδε", authorId = "other-editor", parentRevisionId = (string?)null,
        })).StatusCode);
        var existing = Load(factory, project.Id).Objects[0].Surfaces[0].Revisions.Single(r => r.Layer == TextLayer.L2Editorial);
        var proposal = await Issue(client, Load(factory, project.Id));
        Assert.Equal(HttpStatusCode.OK, (await Accept(client, proposal)).StatusCode);
        var saved = Load(factory, project.Id);
        var editorial = saved.Objects[0].Surfaces[0].Revisions.Last(r => r.Layer == TextLayer.L2Editorial);
        Assert.Equal("δαΩδε", editorial.Text); Assert.Equal(existing.Id, editorial.ParentRevisionId);
        Assert.Equal("αβγδε", saved.Objects[0].Surfaces[0].Revisions.Single(r => r.Layer == TextLayer.L1Diplomatic).Text);
        var report = await client.GetStringAsync($"/api/v1/projects/{project.Id}/surfaces/{saved.Objects[0].Surfaces[0].Id}/tei?report=true");
        using var json = JsonDocument.Parse(report);
        Assert.Contains(json.RootElement.GetProperty("lossReport").GetProperty("entries").EnumerateArray(),
            entry => entry.GetProperty("category").GetString() == "diplomatic-layer");
    }

    [Fact]
    public async Task Demonstration_chain_records_never_substitute_for_native_project_authority()
    {
        var provider = new ProviderDouble();
        using var factory = new Factory(Path.Combine(_root, "store.db"), provider);
        using var client = factory.CreateClient();
        var chain = factory.Services.GetRequiredService<RevisionChain>();
        var source = chain.Append(new(EntityKind.Line, "demo-only"), TextLayer.L1Diplomatic, "αβγδε",
            new AgentRef { Id = "demonstration", IsModel = false });
        var request = new AiProposalRequest
        {
            TargetRevisionId = source.Id, Range = new(1, 3), Task = AiTaskKind.Expansion,
            ProviderConfigRef = "generated-provider", PromptVersion = "fixture-v1",
        };
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/v1/proposals", new
        {
            targetRevisionId = source.Id.ToString(), range = new { start = 1, end = 3 }, task = "expansion",
            providerConfigRef = "generated-provider", promptVersion = "fixture-v1",
        })).StatusCode);
        var unissued = new AiProposal
        {
            Id = new(EntityKind.Proposal, "demo-only"), Request = request, ProposedText = "Ω", ProviderId = "generated-provider",
            ModelId = "generated-model", PromptVersion = "fixture-v1", Confidence = Confidence.None, CreatedAt = DateTimeOffset.UnixEpoch,
        };
        Assert.Equal(HttpStatusCode.NotFound, (await Accept(client, unissued)).StatusCode);
        Assert.Empty(provider.Bodies);
        Assert.Null(chain.Current(source.ScopeId, TextLayer.L2Editorial));
    }

    [Theory]
    [InlineData("issue")]
    [InlineData("single-accept")]
    [InlineData("bulk-accept")]
    public async Task Revocation_immediately_before_the_actual_save_refuses_native_state_changes(string operation)
    {
        var gate = new SaveGate();
        using var factory = new Factory(Path.Combine(_root, "store.db"), new ProviderDouble(), true, true, gate);
        using var admin = Admin(factory);
        var project = await Import(admin, factory);
        var account = await Account(factory, admin);
        using var member = account.Client;
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/projects/{project.Id}/members", new { userId = account.Id })).StatusCode);
        var proposal = operation == "issue" ? null : await Issue(member, project);
        var before = NativeJson.Save(Load(factory, project.Id));
        gate.BeforeSave = _ =>
        {
            using var scope = factory.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<LocalStore>().RemoveProjectMember(project.Id, account.Id);
        };

        var response = operation == "issue" ? await member.PostAsJsonAsync("/api/v1/proposals", Request(project))
            : operation == "single-accept" ? await Accept(member, proposal!) : await Bulk(member, "accept", proposal!);

        Assert.Equal(1, gate.Triggered);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("Ω", await response.Content.ReadAsStringAsync());
        Assert.Equal(before, NativeJson.Save(Load(factory, project.Id)));
    }

    [Fact]
    public async Task Persisted_admin_is_rechecked_at_commit_even_when_membership_is_bypassed()
    {
        var gate = new SaveGate();
        using var factory = new Factory(Path.Combine(_root, "store.db"), new ProviderDouble(), true, true, gate);
        using var bootstrap = Admin(factory);
        var project = await Import(bootstrap, factory);
        var account = await Account(factory, bootstrap, "admin");
        using var admin = account.Client;
        var before = NativeJson.Save(Load(factory, project.Id));
        gate.BeforeSave = _ =>
        {
            using var scope = factory.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<LocalStore>().DisableUser(account.Id);
        };

        var response = await admin.PostAsJsonAsync("/api/v1/proposals", Request(project));

        Assert.Equal(1, gate.Triggered);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(before, NativeJson.Save(Load(factory, project.Id)));
    }

    [Fact]
    public async Task Anonymous_local_mode_cannot_commit_after_a_user_activates_authentication()
    {
        var gate = new SaveGate();
        using var factory = new Factory(Path.Combine(_root, "store.db"), new ProviderDouble(), saveGate: gate);
        using var local = factory.CreateClient();
        var project = await Import(local, factory);
        var before = NativeJson.Save(Load(factory, project.Id));
        gate.BeforeSave = _ =>
        {
            using var scope = factory.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<LocalStore>().CreateUser("activated-admin", UserRole.Admin, UserKeys.Hash("generated-test-key"));
        };

        var response = await local.PostAsJsonAsync("/api/v1/proposals", Request(project));

        Assert.Equal(1, gate.Triggered);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(before, NativeJson.Save(Load(factory, project.Id)));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
