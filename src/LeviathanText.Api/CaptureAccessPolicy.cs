using System.Security.Cryptography;
using System.Text;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Infrastructure;

namespace LeviathanText.Api;

/// <summary>Refresh admission against the actual current credential, account and project membership.</summary>
internal static class CaptureAccessPolicy
{
    public static NativeProject LoadAuthorized(HttpContext http, LocalStore store, IConfiguration configuration,
        EntityId projectId, bool write = false)
    {
        Require(http, store, configuration, projectId, write);
        var scoped = http.RequestServices.GetRequiredService<RequestProjectStore>();
        void BeforeWrite()
        {
            Require(http, store, configuration, projectId, write: true);
            scoped.RefreshMembershipRequirement(configuration.GetValue<bool>("LeviathanText:Auth:ProjectMembership"));
        }
        if (!write) scoped.Bind(projectId);
        else if (http.Items["user"] is UserAccount { Id: "usr-bootstrap", Role: UserRole.Admin })
            scoped.Bind(projectId, BeforeWrite);
        else scoped.BindAuthorized(projectId, (http.Items["user"] as UserAccount)?.Id,
            configuration.GetValue<bool>("LeviathanText:Auth:ProjectMembership"),
            expectedKeyHash: JobStreamAccessPolicy.CapturedAccountKeyHash(http),
            beforeWrite: BeforeWrite);
        return scoped.LoadProject(projectId) ?? throw new LeviathanException(new LeviathanError
        {
            Code = ErrorCode.InputMissing, Scope = "capture", Message = "The selected capture is not available.",
        });
    }

    public static void Require(HttpContext http, LocalStore store, IConfiguration configuration,
        EntityId projectId, bool write = false)
    {
        if (write && configuration.GetValue<bool>("LeviathanText:Auth:ReadOnly")) throw new CaptureAccessDeniedException();
        var configured = configuration["LeviathanText:Auth:ApiKey"];
        if (http.Items["user"] is not UserAccount admitted)
        {
            if (!string.IsNullOrEmpty(configured) || store.HasUsers()) throw new CaptureAccessDeniedException();
            return;
        }
        var provided = http.Request.Headers.Authorization.ToString();
        if (provided.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) provided = provided[7..].Trim();
        if (string.IsNullOrEmpty(provided)) provided = http.Request.Headers["X-Api-Key"].ToString();
        if (string.IsNullOrEmpty(provided)) provided = http.Request.Query["api_key"].ToString();
        bool currentBootstrap = !string.IsNullOrEmpty(provided) && !string.IsNullOrEmpty(configured)
            && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(configured));
        bool admittedBootstrap = admitted.Id == "usr-bootstrap" && admitted.Role == UserRole.Admin;
        if (currentBootstrap != admittedBootstrap) throw new CaptureAccessDeniedException();
        var current = currentBootstrap
            ? new UserAccount { Id = "usr-bootstrap", Name = "bootstrap-admin", Role = UserRole.Admin, CreatedAt = DateTimeOffset.MinValue }
            : string.IsNullOrEmpty(provided) ? null : store.FindUserByKeyHash(UserKeys.Hash(provided));
        if (current is null || current.Id != admitted.Id || current.Disabled
            || current.Role is not (UserRole.Reader or UserRole.Annotator or UserRole.Reviewer or UserRole.Admin)
            || write && current.Role == UserRole.Reader)
            throw new CaptureAccessDeniedException();
        if (configuration.GetValue<bool>("LeviathanText:Auth:ProjectMembership")
            && current.Role != UserRole.Admin && !store.IsProjectMember(projectId, current.Id))
            throw new CaptureAccessDeniedException();
    }

    public static bool ValidMatchHeader(string value) => value.Length == 66 && value[0] == '"' && value[^1] == '"'
        && value.AsSpan(1, 64).IndexOfAnyExcept("0123456789abcdef") < 0;
}

internal sealed class CaptureAccessDeniedException : Exception;
