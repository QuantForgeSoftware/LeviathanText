using System.Net;
using System.Net.Http.Json;
using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LeviathanText.Api.Tests;

public sealed class ApiOrderSnapshotTests
{
    internal static NativeProject Seed(WebApplicationFactory<Program> factory)
    {
        var token = Guid.NewGuid().ToString("N");
        var region = new EntityId(EntityKind.Region, "a-" + token);
        var other = new EntityId(EntityKind.Region, "b-" + token);
        var frame = new EntityId(EntityKind.Capture, token);
        var project = new NativeProject
        {
            Id = new(EntityKind.Collection, token), Name = "generated order fixture", Objects = [new NativeObject
            {
                Id = new(EntityKind.PhysicalObject, token), Surfaces = [new NativeSurface
                {
                    Id = new(EntityKind.Surface, token), Kind = "page", Captures = [new NativeCapture
                    {
                        Id = frame, ImageLocation = "generated-fixture.png", WidthPx = 40, HeightPx = 64,
                        Frame = new CoordinateFrame { ImageRef = frame, ImageChecksum = new Checksum { Algorithm = "unavailable", Digest = "fixture" } },
                    }],
                    Regions = [new NativeRegion { Id = region, FrameRef = frame, ReadingOrderIndex = 0 },
                        new NativeRegion { Id = other, FrameRef = frame, ReadingOrderIndex = 1 }],
                    Lines = Enumerable.Range(0, 3).Select(i => new NativeLine
                    {
                        Id = new(EntityKind.Line, $"{token}-{i}"), RegionId = region, FrameRef = frame,
                        Polygon = [new(0, i * 10), new(20, i * 10), new(20, i * 10 + 8), new(0, i * 10 + 8)],
                    }).ToArray(),
                }],
            }],
        };
        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IProjectStore>();
        store.SaveProject(project);
        return store.LoadProject(project.Id)!;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Committed_remote_reorder_rejects_a_stale_ui_snapshot(bool regions)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var project = Seed(factory);
        var surface = project.Objects[0].Surfaces[0];
        var region = surface.Regions[0].Id;
        var initial = (regions ? surface.Regions.Select(r => r.Id) : surface.Lines.Select(l => l.Id)).Select(id => id.ToString()).ToArray();
        var remote = initial.Reverse().ToArray();
        var url = regions ? $"/api/v1/surfaces/{surface.Id}/reading-order" : $"/api/v1/regions/{region}/line-order";
        object Body(string[] desired, string[]? expected) => regions
            ? new { regionIds = desired, expectedRegionIds = expected }
            : new { lineIds = desired, expectedLineIds = expected };
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(url, Body(remote, initial))).StatusCode);
        var before = await client.GetStringAsync($"/api/v1/projects/{project.Id}");

        var stale = await client.PutAsJsonAsync(url, Body(initial, initial));

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Contains("revisionConflict", await stale.Content.ReadAsStringAsync());
        Assert.Equal(before, await client.GetStringAsync($"/api/v1/projects/{project.Id}"));
        // Omission remains the legacy unguarded behavior; optimistic concurrent-save checks still apply.
        object legacy = regions ? new { regionIds = initial } : new { lineIds = initial };
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(url, legacy)).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Duplicate_desired_order_is_422_and_retains_the_entire_project(bool regions)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var project = Seed(factory);
        var surface = project.Objects[0].Surfaces[0];
        var ids = (regions ? surface.Regions.Select(r => r.Id) : surface.Lines.Select(l => l.Id)).Select(id => id.ToString()).ToArray();
        string[] duplicate = [ids[0], ids[1], ids[0], .. ids.Skip(2)];
        var before = await client.GetStringAsync($"/api/v1/projects/{project.Id}");
        var url = regions ? $"/api/v1/surfaces/{surface.Id}/reading-order" : $"/api/v1/regions/{surface.Regions[0].Id}/line-order";
        object body = regions ? new { regionIds = duplicate } : new { lineIds = duplicate };

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PutAsJsonAsync(url, body)).StatusCode);

        Assert.Equal(before, await client.GetStringAsync($"/api/v1/projects/{project.Id}"));
    }
}
