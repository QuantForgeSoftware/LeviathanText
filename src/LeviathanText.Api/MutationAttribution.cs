using LeviathanText.Contracts;

namespace LeviathanText.Api;

/// <summary>
/// Direct human mutations belong to the authenticated stable account, never a
/// caller-supplied display label. Local mode retains its existing caller labels.
/// Source imports and model/provider provenance do not use this boundary.
/// </summary>
internal static class MutationAttribution
{
    public static string Actor(HttpContext context, string? localLabel, string fallback = "api") =>
        context.Items["user"] is UserAccount account ? account.Id : localLabel ?? fallback;

    public static AgentRef? AnnotationAuthor(HttpContext context) =>
        context.Items["user"] is UserAccount account ? new AgentRef { Id = account.Id, IsModel = false } : null;
}
