using LeviathanText.Application;
using LeviathanText.Contracts;

namespace LeviathanText.Application.Tests;

public class ProjectServiceTests
{
    private static InMemoryStore NewStore() => new();

    [Fact]
    public void Create_then_load_roundtrips()
    {
        var store = NewStore();
        var projects = new ProjectService(store);
        var project = projects.Create("pilot-corpus");
        var loaded = projects.Load(project.Id);
        Assert.Equal(project.Name, loaded.Name);
        Assert.Equal(project.Id, loaded.Id);
    }

    [Fact]
    public void List_returns_created_projects()
    {
        var projects = new ProjectService(NewStore());
        projects.Create("alpha");
        projects.Create("beta");
        Assert.Equal(["alpha", "beta"], projects.List().Select(p => p.Name).OrderBy(n => n).ToArray());
    }

    [Fact]
    public void Unknown_project_fails_with_input_missing()
    {
        var projects = new ProjectService(NewStore());
        var ex = Assert.Throws<LeviathanException>(() => projects.Load(new EntityId(EntityKind.Collection, "nope")));
        Assert.Equal(ErrorCode.InputMissing, ex.Error.Code);
    }

    [Fact]
    public void Empty_name_is_rejected()
    {
        var projects = new ProjectService(NewStore());
        Assert.Throws<ArgumentException>(() => projects.Create("  "));
    }
}
