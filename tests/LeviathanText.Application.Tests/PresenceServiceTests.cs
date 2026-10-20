using LeviathanText.Contracts;
using Xunit;

namespace LeviathanText.Application.Tests;

public class PresenceServiceTests
{
    private readonly EntityId _surface = new(EntityKind.Surface, "surf-1");
    private readonly string _db = Path.Combine(Path.GetTempPath(), $"lt-presence-{Guid.NewGuid():N}.db");

    [Fact]
    public void Heartbeats_expire_after_the_ttl_and_leave_removes_an_editor()
    {
        using var store = new Infrastructure.LocalStore(_db);
        var presence = new PresenceService(store);
        var t0 = DateTimeOffset.UtcNow;
        Assert.Equal(["a"], presence.Touch(_surface, "a", t0));
        Assert.Equal(["a", "b"], presence.Touch(_surface, "b", t0.AddSeconds(10)));
        Assert.Equal(["a", "b"], presence.List(_surface, t0.AddSeconds(59)));
        Assert.Equal(["b"], presence.List(_surface, t0.AddSeconds(61)));
        Assert.Equal(["b"], presence.Touch(_surface, "b", t0.AddSeconds(70)));
        Assert.Empty(presence.Leave(_surface, "b"));
        Assert.Empty(presence.List(_surface, t0.AddSeconds(71)));
    }

    [Fact]
    public void A_second_service_on_the_same_store_sees_presence()
    {
        using var first = new Infrastructure.LocalStore(_db);
        using var second = new Infrastructure.LocalStore(_db);
        var a = new PresenceService(first);
        var b = new PresenceService(second);
        a.Touch(_surface, "anna");
        Assert.Contains("anna", b.List(_surface));
    }

    [Fact]
    public void Invalid_editor_is_rejected()
    {
        using var store = new Infrastructure.LocalStore(_db);
        var presence = new PresenceService(store);
        Assert.Throws<LeviathanException>(() => presence.Touch(_surface, " "));
    }
}
