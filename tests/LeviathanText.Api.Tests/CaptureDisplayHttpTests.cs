using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SkiaSharp;
using Xunit;

namespace LeviathanText.Api.Tests;

public sealed partial class CaptureDisplayHttpTests : IDisposable
{
    private sealed class ControlledResponse(Func<ActionContext, Task> execute) : IActionResult
    {
        public Task ExecuteResultAsync(ActionContext context) => execute(context);
    }

    [Theory]
    [InlineData("success")] [InlineData("throw")] [InlineData("abort")] [InlineData("timeout")]
    public async Task Response_execution_retains_admission_and_always_releases_it(string mode)
    {
        using var factory = NewFactory(); using var client = factory.CreateClient(); var project = Seed(factory);
        var surface = project.Objects[0].Surfaces[0]; var service = factory.Services.GetRequiredService<ICaptureDisplayService>();
        using var first = await service.RenderAsync(project, surface.Id, surface.Captures[0].Id);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var aborted = new CancellationTokenSource();
        var http = new DefaultHttpContext { RequestAborted = aborted.Token };
        var context = new ActionContext(http, new RouteData(), new ActionDescriptor());
        var output = new CaptureDisplayActionResult(new ControlledResponse(async action =>
        {
            entered.TrySetResult(); await release.Task.WaitAsync(action.HttpContext.RequestAborted);
            if (mode == "throw") throw new IOException("generated failed output");
        }), first, mode == "timeout" ? TimeSpan.FromMilliseconds(50) : TimeSpan.FromSeconds(10));
        var execution = output.ExecuteResultAsync(context); await entered.Task;
        var queued = service.RenderAsync(project, surface.Id, surface.Captures[0].Id);
        if (mode != "timeout")
            await Assert.ThrowsAsync<TimeoutException>(() => queued.WaitAsync(TimeSpan.FromMilliseconds(100)));
        if (mode == "abort") aborted.Cancel();
        else if (mode != "timeout") release.TrySetResult();
        if (mode == "success") await execution;
        else if (mode == "throw") await Assert.ThrowsAsync<IOException>(() => execution);
        else await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
        using var next = await queued.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(aborted.Token, http.RequestAborted);
    }

    private const string AdminKey = "generated-capture-bootstrap";
    private const string MemberKey = "generated-capture-member";
    private readonly string _root = Directory.CreateTempSubdirectory("lt-capture-http-").FullName;

    private sealed class Gate
    {
        public TaskCompletionSource Rendered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private sealed class HeldDisplay(Gate gate) : ICaptureDisplayService, IDisposable
    {
        private readonly CaptureDisplayService _inner = new();
        public async Task<CaptureDisplayResult> RenderAsync(NativeProject project, EntityId surface, EntityId capture, CancellationToken cancellationToken)
        {
            var rendered = await _inner.RenderAsync(project, surface, capture, cancellationToken);
            try { gate.Rendered.TrySetResult(); await gate.Resume.Task.WaitAsync(cancellationToken); return rendered; }
            catch { rendered.Dispose(); throw; }
        }
        public void Dispose() => _inner.Dispose();
    }
    private sealed class Factory(string storePath, bool authenticated = false, bool membership = false, Gate? gate = null)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("LeviathanText:StorePath", storePath);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LeviathanText:Auth:ApiKey"] = authenticated ? AdminKey : null,
                ["LeviathanText:Auth:ProjectMembership"] = membership ? "true" : "false",
            }));
            if (gate is not null) builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICaptureDisplayService>(); services.AddSingleton<ICaptureDisplayService>(new HeldDisplay(gate));
            });
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Actual_HTTP_image_import_with_zero_lines_delivers_descriptor_and_pinned_PNG(bool jpeg)
    {
        using var factory = NewFactory(); using var client = factory.CreateClient(); var image = WriteImage(jpeg);
        var created = await (await client.PostAsJsonAsync("/api/v1/projects", new { name = "generated capture" })).Content.ReadFromJsonAsync<Dictionary<string, string>>();
        var id = created!["id"]; var imported = await client.PostAsJsonAsync($"/api/v1/projects/{id}/import-image", new { imagePath = image }); imported.EnsureSuccessStatusCode();
        var target = await imported.Content.ReadFromJsonAsync<Dictionary<string, string>>(); var route = Route(id, target!["surfaceId"], target["captureId"]);
        var before = Snapshot(factory, id);
        using var descriptorResponse = await client.GetAsync(route + "?path=/private/not-selected.png"); descriptorResponse.EnsureSuccessStatusCode();
        var descriptor = (await descriptorResponse.Content.ReadFromJsonAsync<CaptureDisplayDescriptor>())!;
        Assert.Equal("unavailable", descriptor.RecordedChecksumStatus); Assert.Equal((40, 24), (descriptor.SourceWidth, descriptor.SourceHeight));
        Assert.Equal('"' + descriptor.DerivativeId + '"', descriptorResponse.Headers.ETag!.ToString()); Assert.True(descriptorResponse.Headers.CacheControl!.NoStore);
        using var request = ImageRequest(route, descriptor.DerivativeId); using var response = await client.SendAsync(request); response.EnsureSuccessStatusCode();
        var png = await response.Content.ReadAsByteArrayAsync(); Assert.Equal("image/png", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(Hash(png), descriptor.DisplayChecksum.Digest); Assert.Equal(Hash(File.ReadAllBytes(image)), descriptor.ObservedSourceChecksum.Digest);
        using var bitmap = SKBitmap.Decode(png); Assert.Equal((descriptor.DisplayWidth, descriptor.DisplayHeight), (bitmap.Width, bitmap.Height));
        Assert.Equal(before, Snapshot(factory, id));
        using var scope = factory.Services.CreateScope(); var project = scope.ServiceProvider.GetRequiredService<LocalStore>().LoadProject(EntityId.Parse(id, EntityKind.Collection))!;
        Assert.Empty(Assert.Single(Assert.Single(project.Objects).Surfaces).Lines);
        Assert.DoesNotContain(image, await descriptorResponse.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(UserRole.Reader)] [InlineData(UserRole.Annotator)] [InlineData(UserRole.Reviewer)] [InlineData(UserRole.Admin)]
    public async Task Current_member_roles_can_read_but_outsiders_and_wrong_project_targets_cannot(UserRole role)
    {
        using var factory = NewFactory(true, true); using var client = factory.CreateClient(); var project = Seed(factory); var target = project.Objects[0].Surfaces[0];
        string actor;
        using (var scope = factory.Services.CreateScope()) { var store = scope.ServiceProvider.GetRequiredService<LocalStore>(); actor = store.CreateUser("member", role, UserKeys.Hash(MemberKey)).Id; if (role != UserRole.Admin) store.AddProjectMember(project.Id, actor); }
        client.DefaultRequestHeaders.Add("X-Api-Key", MemberKey); var route = Route(project.Id.ToString(), target.Id.ToString(), target.Captures[0].Id.ToString());
        using var response = await client.GetAsync(route); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        if (role != UserRole.Admin)
        {
            using var scope = factory.Services.CreateScope(); scope.ServiceProvider.GetRequiredService<LocalStore>().RemoveProjectMember(project.Id, actor);
            using var denied = await client.GetAsync(route); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        client.DefaultRequestHeaders.Clear(); client.DefaultRequestHeaders.Add("X-Api-Key", AdminKey);
        using var missing = await client.GetAsync(Route(project.Id.ToString(), "surf-other", target.Captures[0].Id.ToString())); Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Theory]
    [InlineData("membership")] [InlineData("disabled")] [InlineData("rotated-key")]
    [InlineData("bootstrap-rotated")] [InlineData("bootstrap-cleared")]
    [InlineData("local-activated")] [InlineData("capture-replaced")]
    public async Task Buffered_output_rechecks_current_credential_membership_local_mode_and_native_record(string change)
    {
        var gate = new Gate(); bool local = change == "local-activated";
        using var factory = NewFactory(!local, !local, gate); using var client = factory.CreateClient(); var project = Seed(factory); var surface = project.Objects[0].Surfaces[0];
        string? actor = null;
        if (change.StartsWith("bootstrap", StringComparison.Ordinal)) client.DefaultRequestHeaders.Add("X-Api-Key", AdminKey);
        else if (local) client.DefaultRequestHeaders.Add("X-Api-Key", MemberKey); // Must not upgrade an anonymous admission later.
        else
        {
            using var scope = factory.Services.CreateScope(); var store = scope.ServiceProvider.GetRequiredService<LocalStore>(); actor = store.CreateUser("reader", UserRole.Reader, UserKeys.Hash(MemberKey)).Id; store.AddProjectMember(project.Id, actor);
            client.DefaultRequestHeaders.Add("X-Api-Key", MemberKey);
        }
        var pending = client.GetAsync(Route(project.Id.ToString(), surface.Id.ToString(), surface.Captures[0].Id.ToString()));
        await gate.Rendered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        try
        {
            using var scope = factory.Services.CreateScope(); var store = scope.ServiceProvider.GetRequiredService<LocalStore>();
            if (change == "membership") store.RemoveProjectMember(project.Id, actor!);
            if (change == "disabled") store.DisableUser(actor!);
            if (change == "rotated-key") store.UpdateUserKey(actor!, UserKeys.Hash("new-key"));
            if (change == "local-activated") store.CreateUser("first account", UserRole.Reader, UserKeys.Hash(MemberKey));
            if (change.StartsWith("bootstrap", StringComparison.Ordinal)) factory.Services.GetRequiredService<IConfiguration>()["LeviathanText:Auth:ApiKey"] = change == "bootstrap-cleared" ? null : "new-bootstrap";
            if (change == "capture-replaced")
            {
                var current = store.LoadProject(project.Id)!; var old = current.Objects[0].Surfaces[0];
                store.SaveProject(current with { Objects = [current.Objects[0] with { Surfaces = [old with { Captures = [old.Captures[0] with { ImageLocation = WriteImage(true) }] }] }] });
            }
            var before = Snapshot(factory, project.Id.ToString()); gate.Resume.TrySetResult(); using var result = await pending;
            Assert.Equal(change == "capture-replaced" ? HttpStatusCode.PreconditionFailed : HttpStatusCode.Forbidden, result.StatusCode);
            Assert.DoesNotContain(_root, await result.Content.ReadAsStringAsync()); Assert.Equal(before, Snapshot(factory, project.Id.ToString()));
            using var next = await factory.Services.GetRequiredService<ICaptureDisplayService>()
                .RenderAsync(project, surface.Id, surface.Captures[0].Id).WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally { gate.Resume.TrySetResult(); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Missing_or_stale_IfMatch_and_replaced_source_never_send_raster_bytes(bool unavailable)
    {
        using var factory = NewFactory(); using var client = factory.CreateClient(); var project = Seed(factory, unavailable); var s = project.Objects[0].Surfaces[0]; var route = Route(project.Id.ToString(), s.Id.ToString(), s.Captures[0].Id.ToString());
        var descriptor = (await client.GetFromJsonAsync<CaptureDisplayDescriptor>(route))!;
        using var missing = await client.GetAsync(route + "/image"); Assert.Equal(HttpStatusCode.PreconditionFailed, missing.StatusCode);
        using var wrongRequest = ImageRequest(route, new string('0', 64)); using var wrong = await client.SendAsync(wrongRequest); Assert.Equal(HttpStatusCode.PreconditionFailed, wrong.StatusCode);
        File.WriteAllBytes(s.Captures[0].ImageLocation, Bytes(true));
        using var staleRequest = ImageRequest(route, descriptor.DerivativeId); using var stale = await client.SendAsync(staleRequest); Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.NotEqual("image/png", stale.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Exact_nested_identity_and_malformed_or_duplicate_targets_fail_closed()
    {
        using var factory = NewFactory(); using var client = factory.CreateClient(); var project = Seed(factory); var s = project.Objects[0].Surfaces[0];
        using var cross = await client.GetAsync(Route("col-other", s.Id.ToString(), s.Captures[0].Id.ToString())); Assert.Equal(HttpStatusCode.NotFound, cross.StatusCode);
        using var malformed = await client.GetAsync(Route(project.Id.ToString(), s.Id.ToString(), "not-an-id")); Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        using (var scope = factory.Services.CreateScope()) { var store = scope.ServiceProvider.GetRequiredService<LocalStore>(); var current = store.LoadProject(project.Id)!; store.SaveProject(current with { Objects = [current.Objects[0] with { Surfaces = [s with { Captures = [s.Captures[0], s.Captures[0]] }] }] }); }
        using var duplicate = await client.GetAsync(Route(project.Id.ToString(), s.Id.ToString(), s.Captures[0].Id.ToString())); Assert.Equal(HttpStatusCode.UnprocessableEntity, duplicate.StatusCode);
    }

    private Factory NewFactory(bool authenticated = false, bool membership = false, Gate? gate = null) => new(Path.Combine(_root, Guid.NewGuid().ToString("N") + ".db"), authenticated, membership, gate);
    private NativeProject Seed(Factory factory, bool unavailable = false)
    {
        using var scope = factory.Services.CreateScope(); var store = scope.ServiceProvider.GetRequiredService<LocalStore>(); var projects = new ProjectService(store); var project = projects.Create("generated display");
        var image = WriteImage(false); var imported = new InterchangeService(projects).ImportImage(project, image, "page"); var loaded = projects.Load(project.Id);
        var s = imported.Surfaces[0];
        store.SaveProject(loaded with { Objects = [loaded.Objects[0] with { Surfaces = [s with { Captures = [s.Captures[0] with { WidthPx = 40, HeightPx = 24, Frame = s.Captures[0].Frame with { ImageChecksum = new Checksum { Algorithm = unavailable ? "unavailable" : "sha256", Digest = unavailable ? "pending" : Hash(File.ReadAllBytes(image)) } } }] }] }] });
        return projects.Load(project.Id);
    }
    private string WriteImage(bool jpeg) { var p = Path.Combine(_root, Guid.NewGuid().ToString("N") + (jpeg ? ".jpg" : ".png")); File.WriteAllBytes(p, Bytes(jpeg)); return p; }
    private static byte[] Bytes(bool jpeg) { using var bitmap = new SKBitmap(40, 24); using (var canvas = new SKCanvas(bitmap)) canvas.Clear(jpeg ? SKColors.Blue : SKColors.Red); using var data = bitmap.Encode(jpeg ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png, 100); return data.ToArray(); }
    private static string Route(string p, string s, string c) => $"/api/v1/projects/{p}/surfaces/{s}/captures/{c}/display";
    private static HttpRequestMessage ImageRequest(string route, string id) { var request = new HttpRequestMessage(HttpMethod.Get, route + "/image"); request.Headers.TryAddWithoutValidation("If-Match", '"' + id + '"'); return request; }
    private static string Snapshot(Factory factory, string id) { using var scope = factory.Services.CreateScope(); var store = scope.ServiceProvider.GetRequiredService<LocalStore>(); return NativeJson.Save(store.LoadProject(EntityId.Parse(id, EntityKind.Collection))!); }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    public void Dispose() => Directory.Delete(_root, true);
}
