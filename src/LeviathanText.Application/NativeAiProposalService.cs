using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LeviathanText.AI;
using LeviathanText.Contracts;
using LeviathanText.Contracts.Serialization;
using LeviathanText.Core.Text;
using ScalarText = LeviathanText.Core.Unicode.ScalarText;
using LeviathanText.Formats;

namespace LeviathanText.Application;

public sealed record NativeAiPreparedRequest(EntityId ProjectId, EntityId ObjectId, EntityId SurfaceId,
    EntityId LineId, TextRevision SourceRevision, AiProposalRequest Request);

public sealed record NativeAiIssuedProposal
{
    public required int SchemaVersion { get; init; }
    public required EntityId ProjectId { get; init; }
    public required EntityId ObjectId { get; init; }
    public required EntityId SurfaceId { get; init; }
    public required EntityId LineId { get; init; }
    public required TextRevision SourceRevision { get; init; }
    public required AiProposal Proposal { get; init; }
}

public sealed record NativeAiIssueResult(NativeProject Project, NativeAiIssuedProposal Record);

public sealed record NativeAiAcceptanceReceipt
{
    public required int SchemaVersion { get; init; }
    public required string Id { get; init; }
    public required EntityId ProjectId { get; init; }
    public required EntityId ObjectId { get; init; }
    public required EntityId SurfaceId { get; init; }
    public required EntityId LineId { get; init; }
    public required EntityId ProposalId { get; init; }
    public required EntityId SourceRevisionId { get; init; }
    public required string SourceTextSha256 { get; init; }
    public required string IssuedRecordSha256 { get; init; }
    public required ScalarRange Range { get; init; }
    public required string ProviderId { get; init; }
    public required string ModelId { get; init; }
    public required string PromptVersion { get; init; }
    public required AiTaskKind Task { get; init; }
    public required string ProposedText { get; init; }
    public required EntityId EditorialRevisionId { get; init; }
    public required string EditorialRevisionSha256 { get; init; }
    public required EntityId AnnotationId { get; init; }
    public required string AnnotationSha256 { get; init; }
    public EntityId? ParentEditorialRevisionId { get; init; }
    public required AgentRef Acceptor { get; init; }
    public required DateTimeOffset AcceptedAt { get; init; }
}

public sealed record NativeAiAcceptance(AiProposal Accepted, TextRevision EditorialRevision,
    NativeAiAcceptanceReceipt Receipt, bool AlreadyAccepted);

public sealed record NativeAiAcceptanceResult(NativeProject Project, IReadOnlyList<NativeAiAcceptance> Acceptances)
{
    public IReadOnlyList<BulkAcceptanceOutcome> Outcomes => [.. Acceptances.Select(item => new BulkAcceptanceOutcome(
        item.Accepted.Id, ProposalStatus.Accepted, item.EditorialRevision.Id, BulkAcceptanceBlocker.None))];
    public int AcceptedCount => Acceptances.Count;
}

/// <summary>
/// Native project proposal issuance and explicit L2 span acceptance. Issued records and
/// receipts are immutable checksummed attachments; one optimistic save applies a batch.
/// Callers own authorization before provider dispatch, issue, acceptance and disclosure.
/// </summary>
public sealed class NativeAiProposalService(IProjectStore store)
{
    public const string IssuedRole = "ai-proposal.v1";
    public const string AcceptanceRole = "ai-proposal-acceptance.v1";
    public const int MaxBatchSize = 128;

    public NativeAiPreparedRequest Prepare(NativeProject project, AiProposalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var source = LocateSource(project, request.TargetRevisionId, requireCurrent: true);
        if (!Enum.IsDefined(request.Task) || string.IsNullOrWhiteSpace(request.ProviderConfigRef)
            || string.IsNullOrWhiteSpace(request.PromptVersion))
            throw Invalid(request.TargetRevisionId, "A supported task, provider configuration and prompt version are required.");
        ValidateRange(request.Range, source.Revision);
        var enriched = request with { SourceText = ScalarText.Slice(source.Revision.Text, request.Range.Start, request.Range.End) };
        return new(project.Id, source.Object.Id, source.Surface.Id, source.Line.Id, source.Revision, enriched);
    }

    public NativeAiIssueResult Issue(NativeProject project, NativeAiPreparedRequest prepared, AiProposal proposal)
    {
        RequireVersioned(project);
        var rebound = Prepare(project, prepared.Request);
        if (!Same(rebound, prepared))
            throw Stale(prepared.SourceRevision.Id, "The prepared source changed before proposal issuance.");
        ValidateProposal(proposal, prepared.SourceRevision);
        if (!Same(proposal.Request, prepared.Request))
            throw Invalid(proposal.Id, "The provider proposal does not match the authoritative prepared request.");
        var ledger = ReadLedger(project);
        if (ledger.Issued.ContainsKey(proposal.Id))
            throw Invalid(proposal.Id, "A proposal with this ID has already been issued.");
        var record = new NativeAiIssuedProposal
        {
            SchemaVersion = 1, ProjectId = project.Id, ObjectId = prepared.ObjectId,
            SurfaceId = prepared.SurfaceId, LineId = prepared.LineId,
            SourceRevision = prepared.SourceRevision, Proposal = proposal,
        };
        var attachment = Attachment("issued-" + proposal.Id, IssuedRole, record);
        var updated = project with
        {
            Objects = [.. project.Objects.Select(obj => obj.Id == record.ObjectId
                ? obj with { Attachments = [.. obj.Attachments, attachment] } : obj)],
        };
        updated = updated with { Version = store.SaveProject(updated) };
        return new(updated, record);
    }

    public NativeAiIssuedProposal GetIssued(NativeProject project, EntityId proposalId)
    {
        var ledger = ReadLedger(project);
        return ledger.Issued.TryGetValue(proposalId, out var found) ? found.Record
            : throw Missing(proposalId, "No authoritative issued proposal exists in this project.");
    }

    public BulkAcceptancePreview Preview(NativeProject project, IReadOnlyList<EntityId> proposalIds,
        IReadOnlyList<AiProposal>? supplied = null) =>
        new([.. Plan(project, proposalIds, supplied).Select(plan => plan.Item)]);

    public NativeAiAcceptanceResult Accept(NativeProject project, IReadOnlyList<EntityId> proposalIds,
        AgentRef acceptor, IReadOnlyList<AiProposal>? supplied = null, DateTimeOffset? at = null)
    {
        RequireVersioned(project);
        if (acceptor is null || string.IsNullOrWhiteSpace(acceptor.Id) || acceptor.IsModel)
            throw Invalid(project.Id, "Explicit human acceptance requires an acceptor identity.");
        var plans = Plan(project, proposalIds, supplied);
        var blocked = plans.FirstOrDefault(plan => !plan.Item.CanAccept);
        if (blocked is not null)
            throw new LeviathanException(new LeviathanError
            {
                Code = blocked.Item.Blocker switch
                {
                    BulkAcceptanceBlocker.TargetSuperseded => ErrorCode.StaleAnnotationAnchor,
                    BulkAcceptanceBlocker.EditorialConflict => ErrorCode.RevisionConflict,
                    BulkAcceptanceBlocker.UnknownIssuedProposal => ErrorCode.InputMissing,
                    _ => ErrorCode.StageContractViolation,
                },
                Scope = blocked.Item.ProposalId.ToString(), Message = blocked.Item.Detail,
                RemedyHint = RemedyHint.RebaseRevision,
            });

        var acceptedAt = at ?? DateTimeOffset.UtcNow;
        var outcomes = new List<NativeAiAcceptance>();
        var additions = new List<(NativeAiIssuedProposal Record, TextRevision Revision, Annotation Annotation, NativeAttachment Receipt)>();
        foreach (var plan in plans)
        {
            var record = plan.Issued!.Record;
            if (plan.Previous is { } previous)
            {
                outcomes.Add(new(record.Proposal with { Status = ProposalStatus.Accepted },
                    plan.PreviousRevision!, previous, AlreadyAccepted: true));
                continue;
            }
            // Native editorial head readers order by CreatedAt. Keep a new append
            // ahead of an imported future-dated head without altering acceptance time.
            var revisionAt = plan.CurrentEditorial is { } parent && acceptedAt <= parent.CreatedAt
                ? parent.CreatedAt.AddTicks(1) : acceptedAt;
            var revision = new TextRevision
            {
                Id = new(EntityKind.TranscriptionRevision, Guid.NewGuid().ToString("N")),
                ScopeId = record.LineId, Layer = TextLayer.L2Editorial,
                ParentRevisionId = plan.CurrentEditorial?.Id, Author = acceptor,
                CreatedAt = revisionAt, Text = plan.Text!,
            };
            var restoration = record.Proposal.Request.Task == AiTaskKind.Restoration;
            var annotation = new Annotation
            {
                Id = new(EntityKind.Annotation, Guid.NewGuid().ToString("N")),
                Kind = restoration ? AnnotationKind.SuppliedEditorial : AnnotationKind.AlternativeReading,
                AnchoredRevisionId = restoration ? revision.Id : record.SourceRevision.Id,
                Range = restoration ? new(record.Proposal.Request.Range.Start,
                    record.Proposal.Request.Range.Start + ScalarText.ScalarLength(record.Proposal.ProposedText)) : record.Proposal.Request.Range,
                AlternativeText = restoration ? null : record.Proposal.ProposedText,
                Description = restoration ? "ai-editorial-restoration" : "choice/orig-reg",
                Author = acceptor,
            };
            var receipt = new NativeAiAcceptanceReceipt
            {
                SchemaVersion = 1, Id = "aiaccept-" + Guid.NewGuid().ToString("N"),
                ProjectId = project.Id, ObjectId = record.ObjectId, SurfaceId = record.SurfaceId, LineId = record.LineId,
                ProposalId = record.Proposal.Id, SourceRevisionId = record.SourceRevision.Id,
                SourceTextSha256 = Digest(record.SourceRevision.Text), IssuedRecordSha256 = plan.Issued.Attachment.Sha256,
                Range = record.Proposal.Request.Range, ProviderId = record.Proposal.ProviderId,
                ModelId = record.Proposal.ModelId, PromptVersion = record.Proposal.PromptVersion,
                Task = record.Proposal.Request.Task, ProposedText = record.Proposal.ProposedText,
                EditorialRevisionId = revision.Id, EditorialRevisionSha256 = Digest(ContractJson.ToJson(revision)),
                AnnotationId = annotation.Id, AnnotationSha256 = Digest(ContractJson.ToJson(annotation)),
                ParentEditorialRevisionId = revision.ParentRevisionId, Acceptor = acceptor, AcceptedAt = acceptedAt,
            };
            additions.Add((record, revision, annotation, Attachment(receipt.Id, AcceptanceRole, receipt)));
            outcomes.Add(new(record.Proposal with { Status = ProposalStatus.Accepted }, revision, receipt, AlreadyAccepted: false));
        }
        if (additions.Count == 0) return new(project, outcomes);
        var updated = project with
        {
            Objects = [.. project.Objects.Select(obj => obj with
            {
                Attachments = [.. obj.Attachments, .. additions.Where(item => item.Record.ObjectId == obj.Id).Select(item => item.Receipt)],
                Surfaces = [.. obj.Surfaces.Select(surface => surface with
                {
                    Revisions = [.. surface.Revisions, .. additions.Where(item => item.Record.ObjectId == obj.Id && item.Record.SurfaceId == surface.Id)
                        .Select(item => item.Revision)],
                    Annotations = [.. surface.Annotations, .. additions.Where(item => item.Record.ObjectId == obj.Id && item.Record.SurfaceId == surface.Id)
                        .Select(item => item.Annotation)],
                })],
            })],
        };
        updated = updated with { Version = store.SaveProject(updated) };
        return new(updated, outcomes);
    }

    private sealed record Source(NativeObject Object, NativeSurface Surface, NativeLine Line, TextRevision Revision);
    private sealed record Issued(NativeAiIssuedProposal Record, NativeAttachment Attachment);
    private sealed record Ledger(Dictionary<EntityId, Issued> Issued, Dictionary<EntityId, NativeAiAcceptanceReceipt> Receipts);
    private sealed record AcceptancePlan(BulkAcceptanceItem Item, Issued? Issued = null,
        TextRevision? CurrentEditorial = null, string? Text = null,
        NativeAiAcceptanceReceipt? Previous = null, TextRevision? PreviousRevision = null);

    private static List<AcceptancePlan> Plan(NativeProject project, IReadOnlyList<EntityId> ids, IReadOnlyList<AiProposal>? supplied)
    {
        if (ids is null || ids.Count is < 1 or > MaxBatchSize)
            throw Invalid(project.Id, $"Acceptance requires between 1 and {MaxBatchSize} proposals.");
        if (ids.Distinct().Count() != ids.Count)
            throw Invalid(project.Id, "Duplicate proposal IDs are not allowed in a batch.");
        var ledger = ReadLedger(project);
        if (supplied is not null)
        {
            if (supplied.Count != ids.Count || supplied.Any(item => item is null)
                || supplied.Select(item => item.Id).Distinct().Count() != ids.Count
                || supplied.Any(item => !ids.Contains(item.Id)))
                throw Invalid(project.Id, "Supplied proposal graphs must match the requested proposal IDs exactly.");
            foreach (var graph in supplied)
                if (ledger.Issued.TryGetValue(graph.Id, out var issued) && !Same(graph, issued.Record.Proposal))
                    throw Invalid(graph.Id, "The supplied proposal graph differs from the authoritative issued record.");
        }
        var plans = new List<AcceptancePlan>();
        var lines = new HashSet<EntityId>();
        foreach (var id in ids)
        {
            AcceptancePlan Block(BulkAcceptanceBlocker blocker, string detail) => new(new(id, false, blocker, detail));
            if (!ledger.Issued.TryGetValue(id, out var issued))
            { plans.Add(Block(BulkAcceptanceBlocker.UnknownIssuedProposal, "No authoritative issued proposal exists in this project.")); continue; }
            var record = issued.Record;
            if (!lines.Add(record.LineId))
            { plans.Add(Block(BulkAcceptanceBlocker.ConflictsWithEarlierInBatch, "At most one proposal per line may be accepted in a batch.")); continue; }
            Source source;
            try { source = LocateSource(project, record.SourceRevision.Id, requireCurrent: true); }
            catch (LeviathanException ex) when (ex.Error.Code == ErrorCode.StaleAnnotationAnchor)
            { plans.Add(Block(BulkAcceptanceBlocker.TargetSuperseded, ex.Error.Message!)); continue; }
            if (record.Proposal.Request.Task is AiTaskKind.Translation or AiTaskKind.StructuredExtraction
                or AiTaskKind.LexicalAnnotation or AiTaskKind.MorphologicalAnnotation)
            { plans.Add(Block(BulkAcceptanceBlocker.UnsupportedTask, "Advisory results cannot replace native line text.")); continue; }
            if (record.Proposal.Request.Task != AiTaskKind.Restoration && record.Proposal.Request.Range.IsEmpty)
            { plans.Add(Block(BulkAcceptanceBlocker.EditorialConflict, "An empty non-restoration source has no reversible native layer mapping; request a nonempty source span.")); continue; }
            if (record.Proposal.Request.Task == AiTaskKind.Restoration && !record.Proposal.Request.Range.IsEmpty)
            { plans.Add(Block(BulkAcceptanceBlocker.EditorialConflict, "Restoration acceptance currently requires a zero-width L1 source position.")); continue; }
            if (ledger.Receipts.TryGetValue(id, out var previous))
            {
                plans.Add(new(new(id, true, BulkAcceptanceBlocker.None), issued, Previous: previous,
                    PreviousRevision: source.Surface.Revisions.Single(revision => revision.Id == previous.EditorialRevisionId)));
                continue;
            }
            var current = source.Surface.Revisions.Where(revision => revision.ScopeId == source.Line.Id && revision.Layer == TextLayer.L2Editorial)
                .OrderBy(revision => revision.CreatedAt).LastOrDefault();
            if (current?.CreatedAt == DateTimeOffset.MaxValue)
            { plans.Add(Block(BulkAcceptanceBlocker.EditorialConflict, "The editorial head has no representable later revision timestamp; repair its chronology before accepting.")); continue; }
            var range = record.Proposal.Request.Range;
            var composed = ScalarText.Slice(record.SourceRevision.Text, 0, range.Start) + record.Proposal.ProposedText +
                ScalarText.Slice(record.SourceRevision.Text, range.End, ScalarText.ScalarLength(record.SourceRevision.Text));
            var merge = current is null ? ThreeWayMerge.MergeResult.Clean(composed)
                : ThreeWayMerge.MergeAnchoredReplacement(record.SourceRevision.Text,
                    new(range.Start, range.End, record.Proposal.ProposedText), current.Text);
            if (!merge.Success)
            { plans.Add(Block(BulkAcceptanceBlocker.EditorialConflict, "Existing editorial changes conflict with or cannot be uniquely mapped against the selected source span; choose a reading before accepting.")); continue; }
            if (record.Proposal.Request.Task == AiTaskKind.Restoration && merge.Text != composed)
            { plans.Add(Block(BulkAcceptanceBlocker.EditorialConflict, "Restoration cannot infer an inserted span after other editorial changes; prepare a reversible layer mapping first.")); continue; }
            plans.Add(new(new(id, true, BulkAcceptanceBlocker.None), issued, current, merge.Text));
        }
        return plans;
    }

    private static Ledger ReadLedger(NativeProject project)
    {
        var issued = new Dictionary<EntityId, Issued>();
        var receipts = new Dictionary<EntityId, NativeAiAcceptanceReceipt>();
        var receiptIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var obj in project.Objects)
        foreach (var attachment in obj.Attachments.Where(item => item.Role == IssuedRole))
        {
            var record = ReadAttachment<NativeAiIssuedProposal>(attachment, project.Id);
            if (record.SchemaVersion != 1 || record.ProjectId != project.Id || record.ObjectId != obj.Id
                || record.SourceRevision is null || record.Proposal is null)
                throw Invalid(project.Id, "The issued proposal's version or native ownership is invalid.");
            var source = LocateSource(project, record.SourceRevision.Id, requireCurrent: false);
            if (source.Object.Id != record.ObjectId || source.Surface.Id != record.SurfaceId || source.Line.Id != record.LineId
                || !Same(source.Revision, record.SourceRevision))
                throw Invalid(record.Proposal.Id, "The issued proposal source does not match immutable native history.");
            ValidateProposal(record.Proposal, source.Revision);
            if (!issued.TryAdd(record.Proposal.Id, new(record, attachment)))
                throw Invalid(record.Proposal.Id, "Duplicate authoritative proposal IDs exist in this project.");
        }
        foreach (var obj in project.Objects)
        foreach (var attachment in obj.Attachments.Where(item => item.Role == AcceptanceRole))
        {
            var receipt = ReadAttachment<NativeAiAcceptanceReceipt>(attachment, project.Id);
            if (!issued.TryGetValue(receipt.ProposalId, out var source))
                throw Invalid(receipt.ProposalId, "An acceptance receipt has no authoritative issued proposal.");
            var record = source.Record;
            var surface = project.Objects.Single(item => item.Id == record.ObjectId).Surfaces.Single(item => item.Id == record.SurfaceId);
            var revisions = surface.Revisions.Where(item => item.Id == receipt.EditorialRevisionId).ToArray();
            var annotations = surface.Annotations.Where(item => item.Id == receipt.AnnotationId).ToArray();
            var restoration = record.Proposal.Request.Task == AiTaskKind.Restoration;
            if (receipt.SchemaVersion != 1 || string.IsNullOrWhiteSpace(receipt.Id) || receipt.ProjectId != project.Id
                || receipt.ObjectId != obj.Id || receipt.ObjectId != record.ObjectId || receipt.SurfaceId != record.SurfaceId
                || receipt.LineId != record.LineId || receipt.SourceRevisionId != record.SourceRevision.Id
                || receipt.SourceTextSha256 != Digest(record.SourceRevision.Text) || receipt.IssuedRecordSha256 != source.Attachment.Sha256
                || receipt.Range != record.Proposal.Request.Range || receipt.ProviderId != record.Proposal.ProviderId
                || receipt.ModelId != record.Proposal.ModelId || receipt.PromptVersion != record.Proposal.PromptVersion
                || receipt.Task != record.Proposal.Request.Task || receipt.ProposedText != record.Proposal.ProposedText
                || receipt.Acceptor is null || receipt.Acceptor.IsModel || string.IsNullOrWhiteSpace(receipt.Acceptor.Id)
                || revisions.Length != 1 || revisions[0].Layer != TextLayer.L2Editorial || revisions[0].ScopeId != record.LineId
                || revisions[0].ParentRevisionId != receipt.ParentEditorialRevisionId || revisions[0].Author != receipt.Acceptor
                || receipt.EditorialRevisionSha256 != Digest(ContractJson.ToJson(revisions[0]))
                || annotations.Length != 1 || annotations[0].Author != receipt.Acceptor
                || annotations[0].Kind != (restoration ? AnnotationKind.SuppliedEditorial : AnnotationKind.AlternativeReading)
                || annotations[0].AnchoredRevisionId != (restoration ? receipt.EditorialRevisionId : record.SourceRevision.Id)
                || annotations[0].Range != (restoration ? new ScalarRange(record.Proposal.Request.Range.Start,
                    record.Proposal.Request.Range.Start + ScalarText.ScalarLength(record.Proposal.ProposedText)) : record.Proposal.Request.Range)
                || annotations[0].AlternativeText != (restoration ? null : record.Proposal.ProposedText)
                || annotations[0].Description != (restoration ? "ai-editorial-restoration" : "choice/orig-reg")
                || receipt.AnnotationSha256 != Digest(ContractJson.ToJson(annotations[0])))
                throw Invalid(receipt.ProposalId, "The acceptance receipt does not match its issued proposal or editorial revision.");
            if (!receipts.TryAdd(receipt.ProposalId, receipt) || !receiptIds.Add(receipt.Id))
                throw Invalid(receipt.ProposalId, "Duplicate acceptance receipts exist for this proposal.");
        }
        return new(issued, receipts);
    }

    private static Source LocateSource(NativeProject project, EntityId revisionId, bool requireCurrent)
    {
        var matches = (from obj in project.Objects from surface in obj.Surfaces from revision in surface.Revisions
            where revision.Id == revisionId select (obj, surface, revision)).ToArray();
        if (matches.Length == 0) throw Stale(revisionId, "The native target revision is missing.");
        if (matches.Length != 1) throw Invalid(revisionId, "The native target revision ID is ambiguous.");
        var (owner, page, target) = matches[0];
        if (target.Text is null) throw Invalid(revisionId, "The native source revision has no readable text.");
        var lines = page.Lines.Where(line => line.Id == target.ScopeId).ToArray();
        if (lines.Length != 1 || project.Objects.Count(obj => obj.Id == owner.Id) != 1
            || owner.Surfaces.Count(surface => surface.Id == page.Id) != 1
            || project.Objects.SelectMany(obj => obj.Surfaces).SelectMany(surface => surface.Lines).Count(line => line.Id == target.ScopeId) != 1)
            throw Invalid(revisionId, "The native source ownership is missing or ambiguous.");
        if (target.Layer != TextLayer.L1Diplomatic)
            throw Invalid(revisionId, "Native AI requests currently require an L1 diplomatic source revision.");
        if (requireCurrent && lines[0].DiplomaticRevisionId != target.Id)
            throw Stale(revisionId, "The proposal targets a superseded L1 revision; request a new proposal for the current source.");
        return new(owner, page, lines[0], target);
    }

    private static void ValidateProposal(AiProposal proposal, TextRevision source)
    {
        if (proposal is null || proposal.Request is null || proposal.Id.Kind != EntityKind.Proposal
            || proposal.Status != ProposalStatus.Proposed || proposal.Request.TargetRevisionId != source.Id
            || !Enum.IsDefined(proposal.Request.Task) || string.IsNullOrWhiteSpace(proposal.ProviderId)
            || string.IsNullOrWhiteSpace(proposal.ModelId) || string.IsNullOrWhiteSpace(proposal.Request.ProviderConfigRef)
            || string.IsNullOrWhiteSpace(proposal.PromptVersion) || proposal.PromptVersion != proposal.Request.PromptVersion
            || string.IsNullOrWhiteSpace(proposal.ProposedText) || proposal.ProposedText.Length > 4096 || proposal.ProposedText.Any(char.IsControl))
            throw Invalid(source.Id, "The provider proposal is not a valid issued proposal graph.");
        ValidateRange(proposal.Request.Range, source);
        if (proposal.Request.SourceText != ScalarText.Slice(source.Text, proposal.Request.Range.Start, proposal.Request.Range.End))
            throw Invalid(proposal.Id, "The proposal's source span differs from the native revision.");
    }

    private static void ValidateRange(ScalarRange range, TextRevision revision)
    {
        if (range.Start < 0 || range.End < range.Start || range.End > ScalarText.ScalarLength(revision.Text))
            throw Invalid(revision.Id, "The selected scalar range is outside the native source revision.");
    }

    private static T ReadAttachment<T>(NativeAttachment attachment, EntityId scope)
    {
        if (attachment.Content is null || attachment.Sha256 is null || Encoding.UTF8.GetByteCount(attachment.Content) > TeiXml.MaxAttachmentBytes
            || !Digest(attachment.Content).Equals(attachment.Sha256, StringComparison.OrdinalIgnoreCase))
            throw Invalid(scope, "An AI record attachment is oversized or has an invalid checksum.");
        try { return ContractJson.FromJson<T>(attachment.Content); }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException or FormatException)
        { throw Invalid(scope, "An AI record attachment cannot be read using its versioned contract."); }
    }

    private static NativeAttachment Attachment<T>(string id, string role, T record)
    {
        var content = ContractJson.ToJson(record);
        if (Encoding.UTF8.GetByteCount(content) > TeiXml.MaxAttachmentBytes)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation, Scope = id,
                Message = "The AI record exceeds the native attachment byte limit.",
            });
        return new() { Id = id, Name = id + ".json", Role = role, MediaType = "application/json", Content = content, Sha256 = Digest(content) };
    }
    private static bool Same<T>(T left, T right) => ContractJson.ToJson(left) == ContractJson.ToJson(right);
    private static string Digest(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static void RequireVersioned(NativeProject project)
    {
        if (project.Version <= 0) throw Invalid(project.Id, "Native AI persistence requires a versioned project snapshot.");
    }
    private static LeviathanException Invalid(EntityId scope, string message) => new(new LeviathanError
        { Code = ErrorCode.StageContractViolation, Scope = scope.ToString(), Message = message });
    private static LeviathanException Stale(EntityId scope, string message) => new(new LeviathanError
        { Code = ErrorCode.StaleAnnotationAnchor, Scope = scope.ToString(), Message = message, RemedyHint = RemedyHint.RebaseRevision });
    private static LeviathanException Missing(EntityId scope, string message) => new(new LeviathanError
        { Code = ErrorCode.InputMissing, Scope = scope.ToString(), Message = message });
}
