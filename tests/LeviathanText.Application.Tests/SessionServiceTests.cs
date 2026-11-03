using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Infrastructure;

namespace LeviathanText.Application.Tests;

public sealed class SessionServiceTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"lt-session-{Guid.NewGuid():N}.db");

    private static UserAccount User(string id, string name, UserRole role = UserRole.Annotator) => new()
    {
        Id = id,
        Name = name,
        Role = role,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public void Sessions_are_created_heartbeated_listed_and_revoked_through_the_shared_store()
    {
        using var store = new LocalStore(_path);
        var firstNode = new SessionService(store, "node-a");
        var secondNode = new SessionService(store, "node-b");
        var user = User("usr-one", "one");

        var created = firstNode.Create(user, null, 15);
        Assert.Equal("node-a", created.NodeId);
        Assert.Equal(user.Id, created.UserId);

        // A second process over the same store sees the same registry.
        var seen = Assert.Single(secondNode.List(user));
        Assert.Equal(created.Id, seen.Id);

        var beaten = firstNode.Heartbeat(user, created.Id, 30);
        Assert.True(beaten.ExpiresAt > created.ExpiresAt);
        Assert.True(beaten.LastSeenAt >= created.LastSeenAt);

        Assert.True(firstNode.Revoke(user, created.Id));
        Assert.Empty(secondNode.List(user));
    }

    [Fact]
    public void Another_user_cannot_heartbeat_or_revoke_a_session()
    {
        using var store = new LocalStore(_path);
        var service = new SessionService(store, "node-a");
        var owner = User("usr-owner", "owner");
        var other = User("usr-other", "other");
        var session = service.Create(owner, null, 15);

        var heartbeat = Assert.Throws<LeviathanException>(() => service.Heartbeat(other, session.Id, 15));
        Assert.Equal(ErrorCode.StageContractViolation, heartbeat.Error.Code);
        var revoke = Assert.Throws<LeviathanException>(() => service.Revoke(other, session.Id));
        Assert.Equal(ErrorCode.StageContractViolation, revoke.Error.Code);
    }

    [Fact]
    public void Active_sessions_are_capped_per_user()
    {
        using var store = new LocalStore(_path);
        var service = new SessionService(store, "node-a");
        var user = User("usr-cap", "cap");
        for (int i = 0; i < SessionService.MaxSessionsPerUser; i++)
            _ = service.Create(user, null, 15);

        var ex = Assert.Throws<LeviathanException>(() => service.Create(user, null, 15));
        Assert.Equal(ErrorCode.StageContractViolation, ex.Error.Code);
    }

    [Fact]
    public void Expired_session_cannot_be_heartbeated()
    {
        using var store = new LocalStore(_path);
        var service = new SessionService(store, "node-a");
        var user = User("usr-expired", "expired");
        var session = store.CreateSession(user.Id, user.Name, "node-a", null,
            DateTimeOffset.UtcNow.AddMinutes(-30), TimeSpan.FromMinutes(1));

        var ex = Assert.Throws<LeviathanException>(() => service.Heartbeat(user, session.Id, 15));
        Assert.Equal(ErrorCode.RevisionConflict, ex.Error.Code);
        Assert.Empty(service.List(user));
        Assert.Single(service.List(user, includeExpired: true));
    }

    public void Dispose() => File.Delete(_path);
}
