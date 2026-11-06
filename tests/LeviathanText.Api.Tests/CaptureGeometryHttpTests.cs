using System.Net;
using System.Net.Http.Json;
using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Contracts.Serialization;
using LeviathanText.Formats;
using LeviathanText.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LeviathanText.Api.Tests;

public sealed partial class CaptureDisplayHttpTests
{
    private static CaptureGeometryController.CreateLineRequest NewLine(NativeSurface surface) => new(
        [new(2, 3), new(30, 3), new(30, 15), new(2, 15)], [new(2, 14), new(30, 14)],
        surface.Lines.Select(l => l.Id).ToArray(), surface.Regions.Select(r => r.Id).ToArray());

    private static HttpRequestMessage CreateLineMessage(NativeProject project, string? derivativeId, int captureIndex = 0)
    {
        var surface = project.Objects[0].Surfaces[0];
        var route = Route(project.Id.ToString(), surface.Id.ToString(), surface.Captures[captureIndex].Id.ToString());
        var request = new HttpRequestMessage(HttpMethod.Post, route.Replace("/display", "/lines"))
        { Content = JsonContent.Create(NewLine(surface), options: ContractJson.Options) };
        if (derivativeId is not null) request.Headers.TryAddWithoutValidation("If-Match", '"' + derivativeId + '"');
        return request;
    }

    private static async Task<CaptureDisplayDescriptor> DirectDisplay(NativeProject project, int captureIndex = 0)
    {
        var surface = project.Objects[0].Surfaces[0];
        using var service = new CaptureDisplayService();
        using var result = await service.RenderAsync(project, surface.Id, surface.Captures[captureIndex].Id);
        return result.Descriptor;
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Capture_geometry_HTTP_targets_secondary_raw_frame_and_refuses_duplicate_retry(bool unavailable)
    {
        using var factory = NewFactory(); using var client = factory.CreateClient(); var project = Seed(factory, unavailable);
        var old = project.Objects[0].Surfaces[0];
        var secondary = old.Captures[0] with
        {
            Id = new EntityId(EntityKind.Capture, "secondary"),
            Frame = old.Captures[0].Frame with { ImageRef = new EntityId(EntityKind.Derivative, "secondary-frame") },
        };
        using (var scope = factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<LocalStore>();
            store.SaveProject(project with { Objects = [project.Objects[0] with { Surfaces = [old with { Captures = [old.Captures[0], secondary] }] }] });
            project = store.LoadProject(project.Id)!;
        }
        var descriptor = await DirectDisplay(project, 1);
        using var request = CreateLineMessage(project, descriptor.DerivativeId, 1); using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        using var check = factory.Services.CreateScope(); var current = check.ServiceProvider.GetRequiredService<LocalStore>().LoadProject(project.Id)!;
        var surface = current.Objects[0].Surfaces[0]; var line = Assert.Single(surface.Lines);
        Assert.Equal(secondary.Frame.ImageRef, line.FrameRef); Assert.Equal(NewLine(old).Polygon, line.Polygon);
        Assert.Equal(secondary.Frame.ImageRef, Assert.Single(surface.Regions).FrameRef);
        Assert.Contains(descriptor.DerivativeId, Assert.Single(surface.GeometryEdits).Note);
        Assert.Equal(secondary.Frame.ImageChecksum, surface.Captures[1].Frame.ImageChecksum);
        using var retry = CreateLineMessage(project, descriptor.DerivativeId, 1); using var refused = await client.SendAsync(retry);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal(NativeJson.Save(current), Snapshot(factory, project.Id.ToString()));
    }

    [Theory]
    [InlineData("missing", false)] [InlineData("stale", false)] [InlineData("replaced-source", false)]
    [InlineData("missing", true)] [InlineData("stale", true)] [InlineData("replaced-source", true)]
    public async Task Capture_geometry_requires_current_display_identity_before_mutation(string mode, bool unavailable)
    {
        using var factory = NewFactory(); using var client = factory.CreateClient(); var project = Seed(factory, unavailable);
        var descriptor = await DirectDisplay(project);
        if (mode == "replaced-source") File.WriteAllBytes(project.Objects[0].Surfaces[0].Captures[0].ImageLocation, Bytes(true));
        var before = Snapshot(factory, project.Id.ToString());
        using var request = CreateLineMessage(project, mode == "missing" ? null : mode == "stale" ? new string('0', 64) : descriptor.DerivativeId);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        Assert.Equal(before, Snapshot(factory, project.Id.ToString()));
    }

    [Theory]
    [InlineData("membership")] [InlineData("disabled")] [InlineData("rotated-key")]
    [InlineData("bootstrap-rotated")] [InlineData("bootstrap-cleared")]
    [InlineData("local-activated")] [InlineData("capture-replaced")] [InlineData("readonly")]
    public async Task Capture_geometry_rechecks_write_authorization_after_awaited_render(string change)
    {
        var gate = new Gate(); bool local = change == "local-activated";
        using var factory = NewFactory(!local, !local, gate); using var client = factory.CreateClient(); var project = Seed(factory);
        var surface = project.Objects[0].Surfaces[0]; var descriptor = await DirectDisplay(project);
        string? actor = null;
        if (change.StartsWith("bootstrap", StringComparison.Ordinal)) client.DefaultRequestHeaders.Add("X-Api-Key", AdminKey);
        else if (local) client.DefaultRequestHeaders.Add("X-Api-Key", MemberKey);
        else
        {
            using var scope = factory.Services.CreateScope(); var store = scope.ServiceProvider.GetRequiredService<LocalStore>();
            actor = store.CreateUser("annotator", UserRole.Annotator, UserKeys.Hash(MemberKey)).Id;
            store.AddProjectMember(project.Id, actor); client.DefaultRequestHeaders.Add("X-Api-Key", MemberKey);
        }
        using var request = CreateLineMessage(project, descriptor.DerivativeId);
        var pending = client.SendAsync(request);
        await gate.Rendered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        try
        {
            using var scope = factory.Services.CreateScope(); var store = scope.ServiceProvider.GetRequiredService<LocalStore>();
            if (change == "membership") store.RemoveProjectMember(project.Id, actor!);
            if (change == "disabled") store.DisableUser(actor!);
            if (change == "rotated-key") store.UpdateUserKey(actor!, UserKeys.Hash("replacement-key"));
            if (change == "local-activated") store.CreateUser("first account", UserRole.Admin, UserKeys.Hash(MemberKey));
            if (change.StartsWith("bootstrap", StringComparison.Ordinal))
                factory.Services.GetRequiredService<IConfiguration>()["LeviathanText:Auth:ApiKey"] = change == "bootstrap-cleared" ? null : "replacement-bootstrap";
            if (change == "readonly") factory.Services.GetRequiredService<IConfiguration>()["LeviathanText:Auth:ReadOnly"] = "true";
            if (change == "capture-replaced") store.SaveProject(project with { Objects = [project.Objects[0] with
                { Surfaces = [surface with { Captures = [surface.Captures[0] with { ImageLocation = WriteImage(true) }] }] }] });
            var before = Snapshot(factory, project.Id.ToString()); gate.Resume.TrySetResult(); using var response = await pending;
            Assert.Equal(change == "capture-replaced" ? HttpStatusCode.PreconditionFailed : HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(before, Snapshot(factory, project.Id.ToString()));
            using var next = await factory.Services.GetRequiredService<ICaptureDisplayService>()
                .RenderAsync(project, surface.Id, surface.Captures[0].Id).WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally { gate.Resume.TrySetResult(); }
    }

    [Fact]
    public async Task Capture_geometry_reader_cannot_create_a_line()
    {
        using var factory = NewFactory(true, true); using var client = factory.CreateClient(); var project = Seed(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<LocalStore>();
            var actor = store.CreateUser("reader", UserRole.Reader, UserKeys.Hash(MemberKey)); store.AddProjectMember(project.Id, actor.Id);
        }
        client.DefaultRequestHeaders.Add("X-Api-Key", MemberKey); var descriptor = await DirectDisplay(project);
        var before = Snapshot(factory, project.Id.ToString()); using var request = CreateLineMessage(project, descriptor.DerivativeId);
        using var response = await client.SendAsync(request); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(before, Snapshot(factory, project.Id.ToString()));
    }

    private NativeProject SeedMixedFrames(Factory factory)
    {
        var project = Seed(factory); var original = project.Objects[0].Surfaces[0];
        var primary = original.Captures[0]; var secondary = primary with
        {
            Id = new EntityId(EntityKind.Capture, "secondary"),
            Frame = primary.Frame with { ImageRef = new EntityId(EntityKind.Derivative, "other-frame") },
        };
        var regions = Enumerable.Range(0, 4).Select(i => new NativeRegion
        {
            Id = new EntityId(EntityKind.Region, "r" + i), ReadingOrderIndex = i * 10,
            FrameRef = i % 2 == 0 ? primary.Frame.ImageRef : secondary.Frame.ImageRef,
            Polygon = [new(0, 0), new(40, 0), new(40, 24), new(0, 24)],
        }).ToArray();
        var a = new NativeLine { Id = new EntityId(EntityKind.Line, "a"), RegionId = regions[0].Id, FrameRef = primary.Frame.ImageRef };
        var b = a with { Id = new EntityId(EntityKind.Line, "b"), FrameRef = secondary.Frame.ImageRef };
        var c = a with { Id = new EntityId(EntityKind.Line, "c") };
        var outside = a with { Id = new EntityId(EntityKind.Line, "outside"), RegionId = regions[1].Id, FrameRef = secondary.Frame.ImageRef };
        using var scope = factory.Services.CreateScope(); var store = scope.ServiceProvider.GetRequiredService<LocalStore>();
        store.SaveProject(project with { Objects = [project.Objects[0] with
            { Surfaces = [original with { Captures = [primary, secondary], Regions = regions, Lines = [a, outside, b, c] }] }] });
        return store.LoadProject(project.Id)!;
    }

    [Theory]
    [InlineData("regions")] [InlineData("lines")]
    [InlineData("stale-regions")] [InlineData("stale-lines")]
    [InlineData("foreign-regions")] [InlineData("foreign-lines")]
    public async Task Capture_geometry_HTTP_order_preserves_other_frame_slots_and_refuses_stale_or_foreign_subsets(string mode)
    {
        using var factory = NewFactory(); using var client = factory.CreateClient(); var project = SeedMixedFrames(factory);
        var surface = project.Objects[0].Surfaces[0]; var descriptor = await DirectDisplay(project);
        var regionOrder = mode.EndsWith("regions", StringComparison.Ordinal);
        var route = Route(project.Id.ToString(), surface.Id.ToString(), surface.Captures[0].Id.ToString())
            .Replace("/display", regionOrder ? "/reading-order" : "/regions/reg-r0/line-order");
        using var request = new HttpRequestMessage(HttpMethod.Put, route);
        request.Headers.TryAddWithoutValidation("If-Match", '"' + descriptor.DerivativeId + '"');
        request.Content = regionOrder
            ? JsonContent.Create(new CaptureGeometryController.RegionOrderRequest(
                mode == "foreign-regions" ? [surface.Regions[1].Id, surface.Regions[0].Id] : [surface.Regions[2].Id, surface.Regions[0].Id],
                mode == "stale-regions" ? [] : surface.Regions.Select(r => r.Id).ToArray()), options: ContractJson.Options)
            : JsonContent.Create(new CaptureGeometryController.CaptureLineOrderRequest(
                mode == "foreign-lines" ? [surface.Lines[2].Id, surface.Lines[0].Id] : [surface.Lines[3].Id, surface.Lines[0].Id],
                mode == "stale-lines" ? [] : [surface.Lines[0].Id, surface.Lines[2].Id, surface.Lines[3].Id]), options: ContractJson.Options);
        var before = Snapshot(factory, project.Id.ToString()); using var response = await client.SendAsync(request);
        var expectedStatus = mode.StartsWith("stale", StringComparison.Ordinal) ? HttpStatusCode.Conflict
            : mode.StartsWith("foreign", StringComparison.Ordinal) ? HttpStatusCode.UnprocessableEntity : HttpStatusCode.OK;
        Assert.True(expectedStatus == response.StatusCode, $"Expected {expectedStatus}, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        if (expectedStatus != HttpStatusCode.OK) Assert.Equal(before, Snapshot(factory, project.Id.ToString()));
        else
        {
            using var scope = factory.Services.CreateScope(); var current = scope.ServiceProvider.GetRequiredService<LocalStore>().LoadProject(project.Id)!;
            var updated = current.Objects[0].Surfaces[0];
            if (regionOrder) Assert.Equal([surface.Regions[2].Id, surface.Regions[1].Id, surface.Regions[0].Id, surface.Regions[3].Id],
                updated.Regions.OrderBy(r => r.ReadingOrderIndex).Select(r => r.Id));
            else Assert.Equal([surface.Lines[3].Id, surface.Lines[1].Id, surface.Lines[2].Id, surface.Lines[0].Id], updated.Lines.Select(l => l.Id));
            Assert.Single(updated.GeometryEdits); Assert.Equal(surface.Captures, updated.Captures);
        }
    }
}
