using LeviathanText.Layout;

namespace LeviathanText.Api;

/// <summary>
/// Optional learned layout packages, selected by surface kind. Configuration:
/// <c>LeviathanText:Layout:Models:&lt;kind&gt; = &lt;package-directory&gt;</c> (or <c>*</c> as a
/// default). Missing/broken packages are logged and skipped so the API falls back to the
/// projection detector instead of failing.
/// </summary>
public sealed class LayoutModelRegistry
{
    private readonly Dictionary<string, PatchLayoutModel> _byKind;

    public LayoutModelRegistry(IConfiguration configuration, ILogger<LayoutModelRegistry> logger)
    {
        _byKind = new Dictionary<string, PatchLayoutModel>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in configuration.GetSection("LeviathanText:Layout:Models").GetChildren())
        {
            if (string.IsNullOrWhiteSpace(child.Value)) continue;
            try
            {
                _byKind[child.Key] = PatchLayoutModel.Load(child.Value);
                logger.LogInformation("layout model loaded for kind {Kind}: {Path}", child.Key, child.Value);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "layout model for kind {Kind} could not be loaded from {Path}", child.Key, child.Value);
            }
        }
    }

    public PatchLayoutModel? Resolve(string surfaceKind) =>
        _byKind.GetValueOrDefault(surfaceKind) ?? _byKind.GetValueOrDefault("*");

    public bool IsEmpty => _byKind.Count == 0;
}
