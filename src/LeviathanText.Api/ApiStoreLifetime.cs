using LeviathanText.Infrastructure;

namespace LeviathanText.Api;

/// <summary>Initialize the API database once and keep a configured in-memory database alive.</summary>
internal sealed class ApiStoreLifetime(string databasePath) : IDisposable
{
    private readonly LocalStore _anchor = new(databasePath);

    public LocalStore OpenRequestStore() => _anchor.OpenPeer();

    public void Dispose() => _anchor.Dispose();
}
