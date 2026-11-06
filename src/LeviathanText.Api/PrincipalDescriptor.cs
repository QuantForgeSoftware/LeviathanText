using System.Security.Cryptography;
using System.Text;
using LeviathanText.Contracts;
using LeviathanText.Infrastructure;

namespace LeviathanText.Api;

internal enum PrincipalAdmissionMode { Local, Bootstrap, Account }

// Additive fields preserve the existing /me reply. Only persisted account mode
// has a stable accountId; local/bootstrap are deliberately not account identities.
internal sealed record PrincipalDescriptorView(bool Authenticated, string Name, string Role,
    string Mode, string? AccountId, string? WorkspaceNamespace);

internal static class PrincipalDescriptor
{
    private const string AdmissionModeKey = "leviathan.principal-admission-mode";

    internal static void MarkAdmission(HttpContext http, PrincipalAdmissionMode mode) =>
        http.Items[AdmissionModeKey] = mode;

    internal static bool IsMePath(PathString path) =>
        string.Equals(path.Value?.TrimEnd('/'), "/api/v1/me", StringComparison.OrdinalIgnoreCase);

    internal static void ApplyNoStore(HttpResponse response)
    {
        response.Headers["Cache-Control"] = "no-store, private, max-age=0";
        response.Headers["Pragma"] = "no-cache";
        response.Headers["Expires"] = "Thu, 01 Jan 1970 00:00:00 GMT";
        response.Headers.Remove("ETag");
        response.Headers.Remove("Last-Modified");
        var vary = response.Headers["Vary"].ToString().Split(',',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
        if (vary.Contains("*", StringComparer.Ordinal))
        {
            response.Headers["Vary"] = "*"; // A wildcard must remain standalone.
        }
        else
        {
            foreach (string field in new[] { "Authorization", "X-Api-Key" })
                if (!vary.Contains(field, StringComparer.OrdinalIgnoreCase)) vary.Add(field);
            response.Headers["Vary"] = string.Join(", ", vary);
        }
    }

    internal static IResult Read(HttpContext http, LocalStore store, IConfiguration configuration)
    {
        ApplyNoStore(http.Response);
        return TryRead(http, store, configuration, out var view)
            ? Results.Ok(view)
            : Results.Json(new { code = "unauthorized", message = "Missing or invalid API key." },
                statusCode: StatusCodes.Status401Unauthorized);
    }

    // No await or cached account/principal lookup. This validates admission again
    // against the current configuration/store; it does not grant project access.
    // Explicit nonsecret operator namespace. Never infer durable ownership from
    // account names, credentials, paths, or an automatically regenerated value.
    private static string? ReadWorkspaceNamespace(IConfiguration configuration)
    {
        string? configured = configuration["LeviathanText:Auth:WorkspaceNamespace"];
        return configured?.Length == 36 && Guid.TryParseExact(configured, "D", out var value)
            && value != Guid.Empty ? value.ToString("D") : null;
    }

    internal static bool TryRead(HttpContext http, LocalStore store, IConfiguration configuration,
        out PrincipalDescriptorView? view)
    {
        view = null;
        if (http.Items[AdmissionModeKey] is not PrincipalAdmissionMode mode) return false;
        var admitted = http.Items["user"] as UserAccount;
        string? configured = configuration["LeviathanText:Auth:ApiKey"];
        if (mode == PrincipalAdmissionMode.Local)
        {
            if (admitted is not null || !string.IsNullOrEmpty(configured) || store.HasUsers()) return false;
            view = new(false, "local", "admin", "local", null, null);
            return true;
        }
        if (admitted is null) return false;
        string provided = http.Request.Headers.Authorization.ToString();
        if (provided.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) provided = provided[7..].Trim();
        if (string.IsNullOrEmpty(provided)) provided = http.Request.Headers["X-Api-Key"].ToString();
        if (string.IsNullOrEmpty(provided)) provided = http.Request.Query["api_key"].ToString();
        if (string.IsNullOrEmpty(provided)) return false;
        bool bootstrap = !string.IsNullOrEmpty(configured) && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(configured));
        if (mode == PrincipalAdmissionMode.Bootstrap)
        {
            if (!bootstrap || admitted.Id != "usr-bootstrap" || admitted.Role != UserRole.Admin) return false;
            view = new(true, "bootstrap-admin", UserRole.Admin.ToString(), "bootstrap", null, null);
            return true;
        }
        if (mode != PrincipalAdmissionMode.Account || bootstrap) return false;
        var current = store.FindUserByKeyHash(UserKeys.Hash(provided));
        if (current is null || current.Disabled || current.Id != admitted.Id
            || current.Role is not (UserRole.Reader or UserRole.Annotator or UserRole.Reviewer or UserRole.Admin))
            return false;
        view = new(true, current.Name, current.Role.ToString(), "account", current.Id, ReadWorkspaceNamespace(configuration));
        return true;
    }
}
