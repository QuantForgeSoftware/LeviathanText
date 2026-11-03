using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application.Tests;

public sealed class ProjectFragmentTests
{
    [Fact]
    public void Fragment_grouping_persists_and_crosswalks_without_changing_entity_ids()
    {
        var store = new InMemoryStore();
        var projects = new ProjectService(store);
        var project = SampleProjectBuilder.Build();
        store.SaveProject(project);
        var obj = project.Objects[0];
        var surface = obj.Surfaces[0];

        var (updated, fragment) = projects.AddFragment(project, obj.Id,
            new Dictionary<string, string> { ["TM"] = "12345" },
            new Dictionary<string, string> { ["label"] = "fragment A" },
            [surface.Id]);

        Assert.Equal(obj.Id, updated.Objects[0].Id);
        Assert.Equal(surface.Id, updated.Objects[0].Surfaces[0].Id);
        var reloaded = store.LoadProject(project.Id)!;
        Assert.Equal(fragment.Id, Assert.Single(reloaded.Objects[0].Fragments).Id);
        var row = Assert.Single(ProvenanceCrosswalk.BuildFragments(reloaded));
        Assert.Equal(obj.Id.ToString(), row.ObjectId);
        Assert.Contains("TM=12345", row.FragmentRefs);
        Assert.Equal(surface.Id.ToString(), row.SurfaceIds);
        Assert.Contains("fragmentId", ProvenanceCrosswalk.ToFragmentCsv([row]));
        Assert.Empty(ProjectIntegrity.Audit(reloaded));
    }

    [Fact]
    public void Fragment_requires_a_surface_on_the_target_object()
    {
        var store = new InMemoryStore();
        var projects = new ProjectService(store);
        var project = SampleProjectBuilder.Build();
        store.SaveProject(project);
        var obj = project.Objects[0];

        Assert.Throws<LeviathanException>(() => projects.AddFragment(project, obj.Id));
        Assert.Throws<LeviathanException>(() => projects.AddFragment(project, obj.Id,
            surfaceIds: [new EntityId(EntityKind.Surface, "surf-missing")]));
    }
}
