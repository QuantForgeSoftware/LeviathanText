using System.Net;
using System.Net.Http.Json;
using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Contracts.Serialization;
using LeviathanText.Core.Revisions;
using LeviathanText.Formats;
using LeviathanText.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace LeviathanText.Api.Tests;

public sealed partial class CaptureDisplayHttpTests
{
    private static readonly PointD[] ScopedOriginalPolygon = [new(1, 1), new(15, 1), new(15, 8), new(1, 8)];
    private static readonly PointD[] ScopedOriginalBaseline = [new(1, 7), new(15, 7)];
    private static readonly PointD[] ScopedUpdatedPolygon = [new(2, 2), new(16, 2), new(16, 10), new(2, 10)];
    private static readonly PointD[] ScopedUpdatedBaseline = [new(2, 9), new(16, 9)];

    private NativeProject SeedExistingGeometry(Factory factory, bool unavailable = false)
    {
        var project = Seed(factory, unavailable); var surface = project.Objects[0].Surfaces[0];
        var capture = surface.Captures[0] with { Frame = surface.Captures[0].Frame with { ImageRef = new EntityId(EntityKind.Derivative, "scoped-edit-raw-frame") } };
        var region = new NativeRegion { Id = new(EntityKind.Region, "scoped-edit-region"), FrameRef = capture.Frame.ImageRef,
            Polygon = [new(0, 0), new(40, 0), new(40, 24), new(0, 24)], ReadingOrderIndex = 0 };
        var lineId = new EntityId(EntityKind.Line, "scoped-edit-line");
        var source = new RevisionChain().Append(lineId, TextLayer.L1Diplomatic, "GENERATED-SOURCE",
            new AgentRef { Id = "generated-human", IsModel = false });
        var line = new NativeLine { Id = lineId, RegionId = region.Id, FrameRef = capture.Frame.ImageRef,
            Polygon = ScopedOriginalPolygon, Baseline = ScopedOriginalBaseline, DiplomaticRevisionId = source.Id,
            Direction = TextDirection.Rtl, Script = "generated", Languages = ["und"], Split = "generated" };
        surface = surface with { Captures = [capture], Regions = [region], Lines = [line], Revisions = [source] };
        using var scope = factory.Services.CreateScope(); var store = scope.ServiceProvider.GetRequiredService<LocalStore>();
        store.SaveProject(project with { Objects = [project.Objects[0] with { Surfaces = [surface] }] });
        return store.LoadProject(project.Id)!;
    }

    private static HttpRequestMessage ExistingGeometryMessage(NativeProject project, string? derivativeId,
        object? expectedGeometry = null)
    {
        var surface = project.Objects[0].Surfaces[0]; var line = surface.Lines[0];
        var route = Route(project.Id.ToString(), surface.Id.ToString(), surface.Captures[0].Id.ToString())
            .Replace("/display", $"/lines/{line.Id}/geometry");
        var request = new HttpRequestMessage(HttpMethod.Put, route)
        {
            Content = JsonContent.Create(new { polygon = ScopedUpdatedPolygon, baseline = ScopedUpdatedBaseline,
                expectedGeometry = expectedGeometry ?? new LineGeometrySnapshot(ScopedOriginalPolygon, ScopedOriginalBaseline) }, options: ContractJson.Options),
        };
        if (derivativeId is not null) request.Headers.TryAddWithoutValidation("If-Match", '"' + derivativeId + '"');
        return request;
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Existing_line_geometry_is_bound_to_current_capture_and_preserves_source_evidence(bool unavailable)
    {
        using var factory = NewFactory(); using var client = factory.CreateClient();
        var project = SeedExistingGeometry(factory, unavailable); var original = project.Objects[0].Surfaces[0];
        var descriptor = await DirectDisplay(project);
        using var request = ExistingGeometryMessage(project, descriptor.DerivativeId);
        using var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected capture-bound update, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        using var scope = factory.Services.CreateScope(); var saved = scope.ServiceProvider.GetRequiredService<LocalStore>().LoadProject(project.Id)!;
        var surface = saved.Objects[0].Surfaces[0]; var line = Assert.Single(surface.Lines);
        Assert.Equal(ContractJson.ToJson(original.Lines[0] with { Polygon = ScopedUpdatedPolygon, Baseline = ScopedUpdatedBaseline }), ContractJson.ToJson(line));
        Assert.Equal(ContractJson.ToJson(original.Revisions), ContractJson.ToJson(surface.Revisions));
        Assert.Equal(ContractJson.ToJson(original.Captures), ContractJson.ToJson(surface.Captures));
        Assert.Equal(ContractJson.ToJson(original.Regions), ContractJson.ToJson(surface.Regions));
        Assert.Equal(project.Version + 1, saved.Version);
        var audit = Assert.Single(surface.GeometryEdits); Assert.Equal(GeometryEditKind.LineGeometry, audit.Kind);
        Assert.Contains(descriptor.DerivativeId, audit.Note); Assert.Contains(descriptor.ObservedSourceChecksum.Digest, audit.Note);
    }

    [Theory]
    [InlineData("missing-header")] [InlineData("stale-header")] [InlineData("stale-geometry")]
    [InlineData("outside-polygon")] [InlineData("outside-baseline")] [InlineData("null-expected")]
    [InlineData("wrong-frame")] [InlineData("wrong-surface")] [InlineData("region-frame")]
    [InlineData("duplicate-local")] [InlineData("duplicate-global")]
    public async Task Scoped_existing_geometry_refuses_invalid_identity_structure_or_bounds_without_mutation(string mode)
    {
        using var factory = NewFactory(); using var client = factory.CreateClient(); var project = SeedExistingGeometry(factory);
        var surface = project.Objects[0].Surfaces[0]; var line = surface.Lines[0]; NativeSurface? other = null;
        if (mode == "wrong-frame") surface = surface with { Lines = [line with { FrameRef = new EntityId(EntityKind.Derivative, "wrong") }] };
        if (mode == "region-frame") surface = surface with { Regions = [surface.Regions[0] with { FrameRef = new EntityId(EntityKind.Derivative, "wrong") }] };
        if (mode == "duplicate-local") surface = surface with { Lines = [line, line] };
        if (mode is "wrong-surface" or "duplicate-global")
        {
            var capture = surface.Captures[0] with { Id = new EntityId(EntityKind.Capture, "other"),
                Frame = surface.Captures[0].Frame with { ImageRef = new EntityId(EntityKind.Derivative, "other") } };
            other = surface with { Id = new EntityId(EntityKind.Surface, "other"), Captures = [capture],
                Lines = mode == "duplicate-global" ? [line] : [] };
        }
        if (mode is "wrong-frame" or "region-frame" or "duplicate-local" or "wrong-surface" or "duplicate-global")
        {
            using var update = factory.Services.CreateScope(); var store = update.ServiceProvider.GetRequiredService<LocalStore>();
            store.SaveProject(project with { Objects = [project.Objects[0] with { Surfaces = other is null ? [surface] : [surface, other] }] });
            project = store.LoadProject(project.Id)!;
        }
        var descriptor = await DirectDisplay(project, 0);
        if (mode == "wrong-surface")
        {
            using var display = new CaptureDisplayService(); using var image = await display.RenderAsync(project, other!.Id, other.Captures[0].Id);
            descriptor = image.Descriptor;
        }
        using var request = ExistingGeometryMessage(project, mode == "missing-header" ? null : mode == "stale-header" ? new string('0', 64) : descriptor.DerivativeId);
        if (mode == "wrong-surface") request.RequestUri = new Uri(request.RequestUri!.ToString().Replace(surface.Id.ToString(), other!.Id.ToString()).Replace(surface.Captures[0].Id.ToString(), other.Captures[0].Id.ToString()), UriKind.Relative);
        if (mode is "outside-polygon" or "outside-baseline" or "null-expected" or "stale-geometry")
        {
            object? expected = mode == "null-expected" ? null : new LineGeometrySnapshot(mode == "stale-geometry" ? ScopedUpdatedPolygon : ScopedOriginalPolygon, ScopedOriginalBaseline);
            var polygon = mode == "outside-polygon" ? new PointD[] { new(-1, 1), new(15, 1), new(15, 8), new(-1, 8) } : ScopedUpdatedPolygon;
            var baseline = mode == "outside-baseline" ? new PointD[] { new(1, 25), new(15, 25) } : ScopedUpdatedBaseline;
            request.Content = JsonContent.Create(new { polygon, baseline, expectedGeometry = expected }, options: ContractJson.Options);
        }
        var before = Snapshot(factory, project.Id.ToString()); using var response = await client.SendAsync(request);
        var expectedStatus = mode is "missing-header" or "stale-header" ? HttpStatusCode.PreconditionFailed
            : mode == "stale-geometry" ? HttpStatusCode.Conflict : mode == "null-expected" ? HttpStatusCode.BadRequest : HttpStatusCode.UnprocessableEntity;
        Assert.True(expectedStatus == response.StatusCode, $"{mode}: expected {expectedStatus}, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        Assert.Equal(before, Snapshot(factory, project.Id.ToString()));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Existing_geometry_does_not_follow_replaced_source_bytes_for_known_or_unavailable_checksums(bool unavailable)
    {
        using var factory = NewFactory(); using var client = factory.CreateClient(); var project = SeedExistingGeometry(factory, unavailable);
        var descriptor = await DirectDisplay(project); var before = Snapshot(factory, project.Id.ToString());
        File.WriteAllBytes(project.Objects[0].Surfaces[0].Captures[0].ImageLocation, Bytes(true));
        using var request = ExistingGeometryMessage(project, descriptor.DerivativeId); using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode); Assert.Equal(before, Snapshot(factory, project.Id.ToString()));
    }

    [Theory]
    [InlineData("membership")] [InlineData("disabled")] [InlineData("key-rotation")] [InlineData("readonly")]
    public async Task Existing_geometry_reauthorizes_after_the_awaited_render_before_saving(string mode)
    {
        var gate = new Gate(); using var factory = NewFactory(true, true, gate); using var client = factory.CreateClient();
        var project = SeedExistingGeometry(factory); var descriptor = await DirectDisplay(project); string actor;
        using (var setup = factory.Services.CreateScope())
        {
            var store = setup.ServiceProvider.GetRequiredService<LocalStore>(); actor = store.CreateUser("generated-editor", UserRole.Annotator, UserKeys.Hash(MemberKey)).Id;
            store.AddProjectMember(project.Id, actor);
        }
        client.DefaultRequestHeaders.Add("X-Api-Key", MemberKey);
        using var request = ExistingGeometryMessage(project, descriptor.DerivativeId); var pending = client.SendAsync(request);
        await gate.Rendered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            using var revoke = factory.Services.CreateScope(); var store = revoke.ServiceProvider.GetRequiredService<LocalStore>();
            if (mode == "membership") store.RemoveProjectMember(project.Id, actor);
            if (mode == "disabled") store.DisableUser(actor);
            if (mode == "key-rotation") store.UpdateUserKey(actor, UserKeys.Hash("generated-replacement"));
            if (mode == "readonly") factory.Services.GetRequiredService<IConfiguration>()["LeviathanText:Auth:ReadOnly"] = "true";
            var before = Snapshot(factory, project.Id.ToString()); gate.Resume.TrySetResult(); using var response = await pending;
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); Assert.Equal(before, Snapshot(factory, project.Id.ToString()));
        }
        finally { gate.Resume.TrySetResult(); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Existing_geometry_uses_post_render_native_state_and_preserves_unrelated_concurrent_fields(bool changedGeometry)
    {
        var gate = new Gate(); using var factory = NewFactory(gate: gate); using var client = factory.CreateClient();
        var project = SeedExistingGeometry(factory); var descriptor = await DirectDisplay(project);
        using var request = ExistingGeometryMessage(project, descriptor.DerivativeId); var pending = client.SendAsync(request);
        await gate.Rendered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            using var update = factory.Services.CreateScope(); var store = update.ServiceProvider.GetRequiredService<LocalStore>();
            var current = store.LoadProject(project.Id)!; var surface = current.Objects[0].Surfaces[0];
            if (changedGeometry) surface = surface with { Lines = [surface.Lines[0] with { Polygon = [new(3, 3), new(17, 3), new(17, 11), new(3, 11)] }] };
            store.SaveProject(current with { Name = "concurrent metadata preserved", Objects = [current.Objects[0] with { Surfaces = [surface] }] });
            var before = Snapshot(factory, project.Id.ToString()); gate.Resume.TrySetResult(); using var response = await pending;
            Assert.Equal(changedGeometry ? HttpStatusCode.Conflict : HttpStatusCode.OK, response.StatusCode);
            if (changedGeometry) Assert.Equal(before, Snapshot(factory, project.Id.ToString()));
            else Assert.Equal("concurrent metadata preserved", store.LoadProject(project.Id)!.Name);
        }
        finally { gate.Resume.TrySetResult(); }
    }

    [Fact]
    public async Task Existing_geometry_cancelled_during_admission_never_saves_and_releases_the_render_lease()
    {
        var gate = new Gate(); using var factory = NewFactory(gate: gate); using var client = factory.CreateClient();
        var project = SeedExistingGeometry(factory); var descriptor = await DirectDisplay(project); var before = Snapshot(factory, project.Id.ToString());
        using var cancellation = new CancellationTokenSource(); using var request = ExistingGeometryMessage(project, descriptor.DerivativeId);
        var pending = client.SendAsync(request, cancellation.Token); await gate.Rendered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        gate.Resume.TrySetResult(); Assert.Equal(before, Snapshot(factory, project.Id.ToString()));
        var surface = project.Objects[0].Surfaces[0];
        using var next = await factory.Services.GetRequiredService<ICaptureDisplayService>().RenderAsync(project, surface.Id, surface.Captures[0].Id).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(40, next.Descriptor.SourceWidth);
    }

}
