using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace LeviathanText.Api.Tests;

public sealed class NativeAiResolverScanTests(ITestOutputHelper output)
{
    [Fact]
    public void Registered_generated_resolver_benchmark()
    {
        string? destination = Environment.GetEnvironmentVariable("LEVIATHANTEXT_RESOLVER_BENCH_OUTPUT");
        if (string.IsNullOrWhiteSpace(destination)) { output.WriteLine("Opt-in resolver benchmark not requested."); return; }
        var cases = new List<object>();
        foreach (int projects in new[] { 1, 6 })
        {
            using var fixture = new Fixture();
            var ids = Enumerable.Range(0, 128).Select(i => Id("requested-" + i)).ToArray();
            var target = fixture.Add("target", ids);
            for (int n = 1; n < projects; n++) fixture.Add("other-" + n,
                Enumerable.Range(0, 128).Select(i => Id($"other-{n}-{i}")).ToArray());
            foreach (int size in new[] { 10, 64, 128 })
            {
                var requested = ids.Take(size).ToArray();
                Assert.Equal(target.Id, fixture.Resolve(requested).Id);
                var elapsed = new List<double>(); var allocated = new List<long>();
                for (int sample = 0; sample < 3; sample++)
                {
                    long bytes = GC.GetAllocatedBytesForCurrentThread(); var timer = Stopwatch.StartNew();
                    var resolved = fixture.Resolve(requested); timer.Stop();
                    allocated.Add(GC.GetAllocatedBytesForCurrentThread() - bytes); elapsed.Add(timer.Elapsed.TotalMilliseconds);
                    Assert.Equal(target.Id, resolved.Id);
                }
                cases.Add(new { projects, requested = size, elapsedMs = elapsed, allocatedBytes = allocated });
            }
        }
        File.WriteAllText(destination, JsonSerializer.Serialize(new { cases }, new JsonSerializerOptions { WriteIndented = true }));
        output.WriteLine("Recorded generated resolver benchmark: " + destination);
    }

    [Fact]
    public void Resolution_keeps_explicit_scope_missing_ambiguity_and_cross_project_contracts()
    {
        using var f = new Fixture(); var a = Id("a"); var b = Id("b");
        var first = f.Add("first", [a]); var second = f.Add("second", [b]);
        Assert.Equal(first.Id, f.Resolve([a]).Id);
        Assert.Equal(ErrorCode.InputMissing, Assert.Throws<LeviathanException>(() => f.Resolve([Id("missing")])).Error.Code);
        Assert.Equal(ErrorCode.InputMissing, Assert.Throws<LeviathanException>(() => f.Resolve([a], second.Id.ToString())).Error.Code);
        Assert.Equal(ErrorCode.StageContractViolation, Assert.Throws<LeviathanException>(() => f.Resolve([a, b])).Error.Code);
        f.Add("duplicate", [a]);
        Assert.Equal(ErrorCode.RevisionConflict, Assert.Throws<LeviathanException>(() => f.Resolve([a, Id("missing")])).Error.Code);
        Assert.Equal(ErrorCode.InputMissing, Assert.Throws<LeviathanException>(() => f.Resolve([Id("missing"), a])).Error.Code);
        Assert.Equal(first.Id, f.Resolve([a], first.Id.ToString()).Id);
    }

    [Fact]
    public void Size_and_duplicates_are_rejected_before_missing_targets()
    {
        using var f = new Fixture();
        foreach (var batch in new[] { Array.Empty<EntityId>(), Enumerable.Range(0, 129).Select(i => Id("missing-" + i)).ToArray(), new[] { Id("same"), Id("same") } })
            Assert.Equal(ErrorCode.StageContractViolation, Assert.Throws<LeviathanException>(() => f.Resolve(batch)).Error.Code);
    }

    [Fact]
    public void Multiple_attachments_in_one_project_do_not_create_project_ambiguity_and_bad_records_are_skipped()
    {
        using var f = new Fixture(); var id = Id("valid");
        var project = f.Add("records", [id, id], ["not json", "[]", "{\"proposal\":null}", "{\"proposal\":{\"id\":123}}", "{\"proposal\":{\"id\":true}}", "{\"proposal\":{\"id\":{}}}"]);
        Assert.Equal(project.Id, f.Resolve([id]).Id);
        Assert.Equal(ErrorCode.InputMissing, Assert.Throws<LeviathanException>(() => f.Resolve([Id("123")])).Error.Code);
    }

    [Fact]
    public void Denied_first_target_precedes_later_missing_target_and_auth_precedes_cross_project_refusal()
    {
        using var f = new Fixture(membership: true); var a = Id("a"); var b = Id("b");
        var first = f.Add("first", [a]); f.Add("second", [b]);
        f.User = f.Store.CreateUser("generated", UserRole.Annotator, UserKeys.Hash("generated-resolver-key"));
        Assert.Equal("NativeAiAccessDeniedException", Assert.ThrowsAny<Exception>(() => f.Resolve([a, Id("missing")])).GetType().Name);
        f.Store.AddProjectMember(first.Id, f.User.Id);
        Assert.Equal("NativeAiAccessDeniedException", Assert.ThrowsAny<Exception>(() => f.Resolve([a, b])).GetType().Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Unreadable_record_after_a_valid_match_does_not_break_target_discovery(bool nullEntry)
    {
        using var f = new Fixture(); var id = Id("valid"); var project = f.Add("valid-before-unreadable", [id]);
        var obj = project.Objects[0];
        var unreadable = nullEntry ? null! : obj.Attachments[0] with { Id = "unreadable", Content = null! };
        f.Store.SaveProject(project with { Objects = [obj with { Attachments = [.. obj.Attachments, unreadable] }] });
        Assert.Equal(project.Id, f.Resolve([id]).Id);
    }

    private static EntityId Id(string value) => new(EntityKind.Proposal, value);

    private sealed class Fixture : IDisposable
    {
        private readonly string _directory = Directory.CreateTempSubdirectory("lt-resolver-scan-").FullName;
        public LocalStore Store { get; }
        private readonly ServiceProvider _services;
        private readonly IConfiguration _configuration;
        public UserAccount? User { get; set; }
        public Fixture(bool membership = false)
        {
            Store = new LocalStore(Path.Combine(_directory, "store.db"));
            _configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["LeviathanText:Auth:ProjectMembership"] = membership.ToString() }).Build();
            var services = new ServiceCollection();
            var requestStore = typeof(NativeAiProjectResolver).Assembly.GetType("LeviathanText.Api.RequestProjectStore")!;
            services.AddScoped(requestStore, _ => Activator.CreateInstance(requestStore, Store)!);
            _services = services.BuildServiceProvider();
        }
        public NativeProject Add(string name, EntityId[] ids, string[]? bad = null)
        {
            var contents = ids.Select(id => JsonSerializer.Serialize(new { proposal = new { id = id.ToString() }, padding = new string('x', 384) })).Concat(bad ?? []).ToArray();
            var attachments = contents.Select((content, index) => Attachment("issued-" + index, content, NativeAiProposalService.IssuedRole))
                .Concat(Enumerable.Range(0, 16).Select(i => Attachment("irrelevant-" + i, "Generated original attachment " + new string('x', 384), "original-source"))).ToArray();
            var p = new NativeProject { Id = new(EntityKind.Collection, name), Name = name, Objects = [new NativeObject
                { Id = new(EntityKind.PhysicalObject, name), Attachments = attachments, Surfaces = [] }] };
            Store.SaveProject(p); return Store.LoadProject(p.Id)!;
        }
        public NativeProject Resolve(IReadOnlyList<EntityId> ids, string? project = null)
        {
            using var scope = _services.CreateScope(); var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
            if (User is not null)
            {
                http.Items["user"] = User;
                http.Request.Headers["X-Api-Key"] = "generated-resolver-key";
            }
            return new NativeAiProjectResolver(Store, _configuration).ForProposals(http, ids, project);
        }
        private static NativeAttachment Attachment(string id, string content, string role) => new()
        {
            Id = id, Name = id + ".json", MediaType = "application/json", Role = role, Content = content,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)))
        };
        public void Dispose() { _services.Dispose(); Store.Dispose(); Directory.Delete(_directory, true); }
    }
}
