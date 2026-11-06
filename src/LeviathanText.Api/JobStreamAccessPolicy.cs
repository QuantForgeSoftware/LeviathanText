using System.Security.Cryptography;
using System.Text;
using LeviathanText.Contracts;
using LeviathanText.Infrastructure;

namespace LeviathanText.Api;

internal static class JobStreamAccessPolicy
{
    // Refresh the credential itself, not only the ID admitted at connection start.
    public static bool TryRefresh(HttpContext http, LocalStore store, IConfiguration configuration,
        out UserAccount? current)
    {
        current = null;
        var configured = configuration["LeviathanText:Auth:ApiKey"];
        if (http.Items["user"] is not UserAccount admitted)
            return string.IsNullOrEmpty(configured) && !store.HasUsers();
        var provided = ProvidedCredential(http);
        bool bootstrap = !string.IsNullOrEmpty(provided) && !string.IsNullOrEmpty(configured)
            && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(configured));
        if (bootstrap != (admitted.Id == "usr-bootstrap" && admitted.Role == UserRole.Admin)) return false;
        current = bootstrap
            ? new UserAccount { Id = "usr-bootstrap", Name = "bootstrap-admin", Role = UserRole.Admin, CreatedAt = DateTimeOffset.MinValue }
            : string.IsNullOrEmpty(provided) ? null : store.FindUserByKeyHash(UserKeys.Hash(provided));
        return current is { Disabled: false } && current.Id == admitted.Id
            && current.Role is UserRole.Reader or UserRole.Annotator or UserRole.Reviewer or UserRole.Admin;
    }
    internal static string? CapturedAccountKeyHash(HttpContext http)
    {
        if (http.Items["user"] is not UserAccount admitted
            || admitted is { Id: "usr-bootstrap", Role: UserRole.Admin }) return null;
        var provided = ProvidedCredential(http);
        if (string.IsNullOrEmpty(provided)) throw new NativeAiAccessDeniedException();
        return UserKeys.Hash(provided);
    }

    private static string ProvidedCredential(HttpContext http)
    {
        var provided = http.Request.Headers.Authorization.ToString();
        if (provided.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) provided = provided[7..].Trim();
        if (string.IsNullOrEmpty(provided)) provided = http.Request.Headers["X-Api-Key"].ToString();
        if (string.IsNullOrEmpty(provided)) provided = http.Request.Query["api_key"].ToString();
        return provided;
    }

}
