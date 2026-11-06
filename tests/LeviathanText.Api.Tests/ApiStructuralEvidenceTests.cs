using System.Net;
using System.Net.Http.Json;
using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using Microsoft.Extensions.DependencyInjection;

namespace LeviathanText.Api.Tests;

public sealed class ApiStructuralEvidenceTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "delete", "split", "merge-first", "merge-second" }
        from layer in Enum.GetValues<TextLayer>()
        select new object[] { operation, layer };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Structural_routes_reject_any_layer_evidence_without_mutating_the_project(string operation, TextLayer layer)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var initial = ApiOrderSnapshotTests.Seed(factory);
        var obj = initial.Objects[0];
        var surface = obj.Surfaces[0];
        var evidenceLine = surface.Lines[operation == "merge-second" ? 1 : 0];
        var revisionId = new EntityId(EntityKind.TranscriptionRevision, "revision-" + Guid.NewGuid().ToString("N"));
        var project = initial with { Objects = [obj with { Surfaces = [surface with
        {
            Revisions = [new TextRevision
            {
                Id = revisionId, ScopeId = evidenceLine.Id, Layer = layer, Text = "αβ",
                Author = new AgentRef { Id = "generated-fixture", IsModel = false }, CreatedAt = DateTimeOffset.UnixEpoch,
            }],
            Annotations = [new Annotation
            {
                Id = new(EntityKind.Annotation, Guid.NewGuid().ToString("N")), Kind = AnnotationKind.Unclear,
                AnchoredRevisionId = revisionId, Range = new(0, 1),
            }],
        }] }] };
        using (var scope = factory.Services.CreateScope()) scope.ServiceProvider.GetRequiredService<IProjectStore>().SaveProject(project);
        Assert.Empty(ProjectIntegrity.Audit(project));
        var before = await client.GetStringAsync($"/api/v1/projects/{project.Id}");
        var firstId = surface.Lines[0].Id;
        var response = operation == "delete" ? await client.DeleteAsync($"/api/v1/lines/{firstId}")
            : operation == "split" ? await client.PostAsJsonAsync($"/api/v1/lines/{firstId}/split", new { y = 4 })
            : await client.PostAsJsonAsync($"/api/v1/lines/{firstId}/merge", new { otherLineId = surface.Lines[1].Id.ToString() });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(before, await client.GetStringAsync($"/api/v1/projects/{project.Id}"));
        using var check = factory.Services.CreateScope();
        Assert.Empty(ProjectIntegrity.Audit(check.ServiceProvider.GetRequiredService<IProjectStore>().LoadProject(project.Id)!));
    }
}
