using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LeviathanText.AI;
using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Contracts.Serialization;
using LeviathanText.Formats;
using LeviathanText.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LeviathanText.Api.Tests;

public sealed class NativeAiEventRetentionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_issue_and_failure_do_not_retain_revision_payloads_in_job_replay(bool providerFailure)
    {
        using var factory = new Factory(providerFailure);
        using var client = factory.CreateClient();
        var project = Seed(factory);
        var source = project.Objects[0].Surfaces[0].Revisions.Single();
        var before = NativeJson.Save(project);
        var request = new AiProposalRequest
        {
            TargetRevisionId = source.Id, Range = new(1, 2), Task = AiTaskKind.Expansion,
            SourceText = "forged client source", ProviderConfigRef = "generated-retention", PromptVersion = "retention-v1",
        };
        using var response = await client.PostAsync("/api/v1/proposals",
            new StringContent(ContractJson.ToJson(request), Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var scope = factory.Services.CreateScope();
        var persisted = scope.ServiceProvider.GetRequiredService<LocalStore>().LoadProject(project.Id)!;
        if (providerFailure)
        {
            using var json = JsonDocument.Parse(body);
            Assert.Equal("empty-response", json.RootElement.GetProperty("failure").GetProperty("reason").GetString());
            Assert.Equal(before, NativeJson.Save(persisted));
        }
        else
        {
            var proposal = ContractJson.FromJson<AiProposal>(body);
            var issued = scope.ServiceProvider.GetRequiredService<NativeAiProposalService>().GetIssued(persisted, proposal.Id);
            Assert.Equal("β", issued.Proposal.Request.SourceText);
            Assert.Equal("Ω", issued.Proposal.ProposedText);
            Assert.Single(persisted.Objects[0].Attachments, item => item.Role == NativeAiProposalService.IssuedRole);
            Assert.Equal(project.Version + 1, persisted.Version);
            Assert.Equal(ContractJson.ToJson(source), ContractJson.ToJson(Assert.Single(persisted.Objects[0].Surfaces[0].Revisions)));
        }
        var bus = factory.Services.GetRequiredService<JobEventBus>();
        Assert.Empty(bus.Replay(source.Id, 0));
        // Actual job replay remains available; only unused proposal publication is removed.
        var job = new EntityId(EntityKind.Job, "generated-retention-control");
        bus.Publish(job, "job.created", "generated-job-payload");
        Assert.Equal("generated-job-payload", Assert.Single(bus.Replay(job, 0)).Payload);
    }

    private static NativeProject Seed(Factory factory)
    {
        var capture = new EntityId(EntityKind.Capture, "retention-capture");
        var region = new EntityId(EntityKind.Region, "retention-region");
        var line = new EntityId(EntityKind.Line, "retention-line");
        var revision = new EntityId(EntityKind.TranscriptionRevision, "retention-source");
        var project = new NativeProject
        {
            Id = new(EntityKind.Collection, "retention-project"), Name = "generated retention fixture",
            Objects = [new NativeObject
            {
                Id = new(EntityKind.PhysicalObject, "retention-object"),
                Surfaces = [new NativeSurface
                {
                    Id = new(EntityKind.Surface, "retention-surface"), Kind = "page",
                    Captures = [new NativeCapture
                    {
                        Id = capture, ImageLocation = "generated-image-not-opened.png", WidthPx = 32, HeightPx = 16,
                        Frame = new CoordinateFrame { ImageRef = capture, ImageChecksum = new Checksum { Algorithm = "unavailable", Digest = "generated" } },
                    }],
                    Regions = [new NativeRegion { Id = region, FrameRef = capture, Polygon = [new(0,0), new(32,0), new(32,16), new(0,16)], LayoutSource = LayoutSource.Manual, ReadingOrderIndex = 0 }],
                    Lines = [new NativeLine { Id = line, RegionId = region, FrameRef = capture, Polygon = [new(1,1), new(30,1), new(30,14), new(1,14)], Baseline = [new(1,12),new(30,12)], DiplomaticRevisionId = revision }],
                    Revisions = [new TextRevision { Id = revision, ScopeId = line, Text = "αβγ", Layer = TextLayer.L1Diplomatic, Author = new() { Id = "generated-human", IsModel = false }, CreatedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z") }],
                }],
            }],
        };
        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<LocalStore>();
        return project with { Version = store.SaveProject(project) };
    }

    private sealed class ProviderDouble(bool failure) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = failure ? "" : "Ω" } } } }), Encoding.UTF8, "application/json"),
            });
    }

    private sealed class Factory(bool failure) : WebApplicationFactory<Program>
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "lt-native-ai-retention", Guid.NewGuid().ToString("N"));
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("LeviathanText:StorePath", Path.Combine(_directory, "store.db"));
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LeviathanText:Auth:ApiKey"] = null, ["LeviathanText:Auth:ProjectMembership"] = "false",
            }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<AiProviderRegistry>();
                services.AddSingleton(new AiProviderRegistry([new AiProviderSettings
                {
                    ProviderId = "generated-retention", ModelId = "generated-double", Host = AiProviderHost.Local,
                    Protocol = AiWireProtocol.OpenAiChatCompletions, Endpoint = new Uri("https://generated.invalid/v1"), MaxAttempts = 1,
                }], new HttpClient(new ProviderDouble(failure))));
            });
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        }
    }
}
