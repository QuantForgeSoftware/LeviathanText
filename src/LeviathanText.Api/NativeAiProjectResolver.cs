using System.Text.Json;
using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Infrastructure;

namespace LeviathanText.Api;

/// <summary>
/// Resolve native AI targets without demonstration-chain fallback or a first-match
/// cross-project lookup. Authorization is refreshed again after an awaited provider call.
/// </summary>
public sealed class NativeAiProjectResolver(LocalStore store, IConfiguration configuration)
{
    public NativeProject ForRevision(HttpContext http, EntityId revisionId, string? projectId = null) =>
        Resolve(http, projectId, project => project.Objects.SelectMany(o => o.Surfaces)
            .Any(surface => surface.Revisions.Any(r => r.Id == revisionId)));

    public NativeProject ForProposals(HttpContext http, IReadOnlyList<EntityId> proposalIds, string? projectId = null)
    {
        if (proposalIds.Count is < 1 or > NativeAiProposalService.MaxBatchSize)
            throw Invalid($"A native proposal batch must contain between 1 and {NativeAiProposalService.MaxBatchSize} proposals.");
        if (proposalIds.Distinct().Count() != proposalIds.Count)
            throw Invalid("A native proposal batch must not contain duplicate proposal IDs.");
        var requestedIds = proposalIds.Select(id => id.ToString()).ToArray();
        var requested = requestedIds.ToHashSet(StringComparer.Ordinal);
        var matches = requested.ToDictionary(id => id, _ => new List<EntityId>(2), StringComparer.Ordinal);
        var candidates = string.IsNullOrWhiteSpace(projectId)
            ? store.ListProjects().Select(project => project.Id)
            : [EntityId.Parse(projectId, EntityKind.Collection)];
        // One project payload at a time; retain only bounded identity matches, not
        // a cache of native projects. Candidate discovery is not an atomic DB snapshot.
        foreach (var id in candidates)
        {
            var candidate = store.LoadProject(id);
            if (candidate is null) continue;
            foreach (var matched in RequestedProposalIds(candidate, requested))
            {
                var projects = matches[matched];
                if (projects.Count < 2) projects.Add(candidate.Id);
            }
        }

        EntityId? selected = null;
        foreach (var proposalId in requestedIds)
        {
            var projects = matches[proposalId];
            if (projects.Count == 0) throw Missing();
            if (projects.Count > 1) throw Ambiguous();
            // Keep original per-ID outcome/authorization order: an earlier denial
            // precedes a later missing target; denial also precedes cross-project refusal.
            RequireAccess(http, projects[0]);
            Bind(http, projects[0]);
            if (selected is not null && selected != projects[0])
                throw Invalid("A proposal batch must belong to one explicit native project.");
            selected = projects[0];
        }
        // Reload only the selected project, then refresh authorization/binding. Native
        // service validation remains authoritative; its eventual save is optimistic.
        return ReloadAuthorized(http, selected!.Value);
    }

    public NativeProject ReloadAuthorized(HttpContext http, EntityId projectId)
    {
        var project = store.LoadProject(projectId) ?? throw Missing();
        RequireAccess(http, project.Id);
        Bind(http, project);
        return project;
    }

    public void RequireAccess(HttpContext http, EntityId projectId)
    {
        // The provider await must not outlive a credential, bootstrap mode, or
        // deployment write-policy change. Reuse the same captured-request refresh
        // used by long-lived job streams rather than trusting the admitted ID alone.
        if (configuration.GetValue<bool>("LeviathanText:Auth:ReadOnly")
            || !JobStreamAccessPolicy.TryRefresh(http, store, configuration, out var current))
            throw new NativeAiAccessDeniedException();
        if (current is null) return;
        if (current.Role is not (UserRole.Annotator or UserRole.Reviewer or UserRole.Admin))
            throw new NativeAiAccessDeniedException();
        if (configuration.GetValue<bool>("LeviathanText:Auth:ProjectMembership")
            && current.Role != UserRole.Admin && !store.IsProjectMember(projectId, current.Id))
            throw new NativeAiAccessDeniedException();
    }

    private NativeProject Resolve(HttpContext http, string? explicitProject, Func<NativeProject, bool> matches)
    {
        NativeProject? selected = null;
        var candidates = string.IsNullOrWhiteSpace(explicitProject)
            ? store.ListProjects().Select(p => p.Id)
            : [EntityId.Parse(explicitProject, EntityKind.Collection)];
        foreach (var id in candidates)
        {
            var candidate = store.LoadProject(id);
            if (candidate is null || !matches(candidate)) continue;
            if (selected is not null)
                throw Ambiguous();
            selected = candidate;
        }
        if (selected is null) throw Missing();
        RequireAccess(http, selected.Id);
        Bind(http, selected);
        return selected;
    }

    private void Bind(HttpContext http, NativeProject project) => Bind(http, project.Id);

    private void Bind(HttpContext http, EntityId projectId)
    {
        // Resolve the concrete request binding even if the service's IProjectStore has
        // a decorator. The authorized save guard must not silently disappear in that case.
        var requestStore = http.RequestServices.GetRequiredService<RequestProjectStore>();
        void BeforeWrite()
        {
            RequireAccess(http, projectId);
            requestStore.RefreshMembershipRequirement(configuration.GetValue<bool>("LeviathanText:Auth:ProjectMembership"));
        }
        if (http.Items["user"] is UserAccount { Id: "usr-bootstrap", Role: UserRole.Admin })
        {
            requestStore.Bind(projectId, BeforeWrite);
            return;
        }
        requestStore.BindAuthorized(projectId, (http.Items["user"] as UserAccount)?.Id,
            configuration.GetValue<bool>("LeviathanText:Auth:ProjectMembership"),
            expectedKeyHash: JobStreamAccessPolicy.CapturedAccountKeyHash(http),
            beforeWrite: BeforeWrite);
    }

    private static HashSet<string> RequestedProposalIds(NativeProject project, HashSet<string> requested)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var attachment in project.Objects.SelectMany(o => o.Attachments)
                     .Where(a => a?.Role == NativeAiProposalService.IssuedRole))
        {
            if (attachment.Content is null) continue;
            try
            {
                // Candidate selection only; checksums/context/source/full graph are
                // validated by the native service after authorization.
                using var document = JsonDocument.Parse(attachment.Content);
                if (document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty("proposal", out var proposal) && proposal.ValueKind == JsonValueKind.Object
                    && proposal.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String
                    && id.GetString() is { } value && requested.Contains(value)) found.Add(value);
            }
            catch (JsonException) { /* An unreadable record cannot identify this target. */ }
        }
        return found;
    }

    private static LeviathanException Ambiguous() => new(new LeviathanError
    {
        Code = ErrorCode.RevisionConflict, Scope = "native-ai", RemedyHint = RemedyHint.RebaseRevision,
        Message = "The native target is ambiguous; select an explicit project before continuing.",
    });

    private static LeviathanException Missing() => new(new LeviathanError
    {
        Code = ErrorCode.InputMissing, Scope = "native-ai", Message = "No native target exists in the selected project scope.",
    });

    private static LeviathanException Invalid(string message) => new(new LeviathanError
    {
        Code = ErrorCode.StageContractViolation, Scope = "native-ai", Message = message,
    });
}

internal sealed class NativeAiAccessDeniedException(Exception? inner = null)
    : Exception("Current project write access was denied.", inner);
