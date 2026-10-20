using LeviathanText.Contracts;
using Xunit;

namespace LeviathanText.Application.Tests;

public class PresenceServiceTests
{
    private readonly EntityId _surface = new(EntityKind.Surface, "surf-1");

    [Fact]
    public void Heartbeats_expire_after_the_ttl_and_leave_removes_an_editor()
    {
        var presence = new PresenceService();
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
    public void Invalid_editor_is_rejected()
    {
        var presence = new PresenceService();
        Assert.Throws<LeviathanException>(() => presence.Touch(_surface, " "));
    }
}
