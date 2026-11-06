using LeviathanText.Layout;

namespace LeviathanText.Api;

/// <summary>
/// Optional learned layout packages, selected by surface kind. Configuration:
/// <c>LeviathanText:Layout:Models:&lt;kind&gt; = &lt;package-directory&gt;</c> (or <c>*</c> as a
/// default). Missing/broken packages are logged and skipped so the API falls back to the
/// projection detector instead of failing.
/// </summary>
public sealed class LayoutModelRegistry : IDisposable
{
    private readonly object _gate = new();
    private bool _disposed;
    private readonly Dictionary<string, PatchLayoutModel> _byKind;

    public LayoutModelRegistry(IConfiguration configuration, ILogger<LayoutModelRegistry> logger)
    {
        _byKind = new Dictionary<string, PatchLayoutModel>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var child in configuration.GetSection("LeviathanText:Layout:Models").GetChildren())
            {
                if (string.IsNullOrWhiteSpace(child.Value)) continue;
                try
                {
                    var loaded = PatchLayoutModel.Load(child.Value);
                    if (_byKind.TryGetValue(child.Key, out var previous)) previous.Dispose();
                    _byKind[child.Key] = loaded;
                    logger.LogInformation("layout model loaded for kind {Kind}: {Path}", child.Key, child.Value);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "layout model for kind {Kind} could not be loaded from {Path}", child.Key, child.Value);
                }
            }
        }
        catch
        {
            foreach (var model in _byKind.Values) model.Dispose();
            throw;
        }
    }

    /// <summary>Borrowed wrapper owned by this singleton; callers must not dispose it.</summary>
    public PatchLayoutModel? Resolve(string surfaceKind)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _byKind.GetValueOrDefault(surfaceKind) ?? _byKind.GetValueOrDefault("*");
        }
    }

    public bool IsEmpty
    {
        get { lock (_gate) { ObjectDisposedException.ThrowIf(_disposed, this); return _byKind.Count == 0; } }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var model in _byKind.Values) model.Dispose();
        }
    }
}
