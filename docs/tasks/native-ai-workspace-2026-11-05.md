# Native project AI workflow — two parallel instances

Activated by the owner's specialist-independent work assignment through13:00 CST
on2026-11-05. This is engineering against generated text and HTTP doubles. No live
provider/model request, training, reviewed Greek label or model promotion is involved.

## Problem and result

The browser's single-proposal acceptance URL is absent. The existing controller
uses a demonstration-only in-memory `RevisionChain`, disconnected from persisted
native project revisions, and wraps serialized proposal JSON in `Ok(string)`.
A generated native-project end-to-end test must expose these gaps before correction.
Deliver real source-linked proposals and explicit L2 acceptance, surviving restart,
with authoritative server records, preserved L1 and unchanged external source evidence.

## Instance A — native service and atomic persistence

Own a NEW `Application/NativeAiProposalService.cs`, its focused application tests,
and the Application project reference to AI if needed. Leave API/controller/UI files
to B. Coordinate exact public signatures with B before implementation.

- Resolve the supplied revision and scalar range within the explicitly passed
  NativeProject; derive source text on the server. Never trust incoming SourceText.
- Persist issued proposals as bounded checksummed native object attachments with a
  distinct versioned `ai-proposal` role; include project/object/surface/line/source
  identities. Preserve original records, then append acceptance receipts separately.
  No new SQLite schema or global in-memory proposal dependency.
- Load/validate authoritative issued proposals by ID and compare any supplied graph;
  tampering, duplicate IDs, unsupported advisory tasks and superseded targets fail.
- Accept at most one proposal per line per batch, within one project and one optimistic
  SaveProject. Validate all before mutation. Compose the scalar replacement into the
  full source line; conservatively merge against existing L2 so unrelated editorial
  edits survive. Record L2 parent and complete proposal/source/acceptor provenance.
  Never change L1. A conflicting editorial change must fail without partial writes.
- A repeated acceptance returns its existing receipt/revision without another save.
  Source/issued-record checks remain explicit. All receipts obey attachment size caps.
- Prove generated source injection refusal, scalar/astral/combining spans, stale source,
  edited L2 conflicts, unrelated L2 preservation, tampering, duplicates, atomic batch,
  store conflict, replay/idempotence and serialize/reload continuity.

## Instance B — HTTP/project authorization/browser contract

Own ProposalsController, NEW project-resolution/authorization helper and HTTP tests,
plus minimal DI/contract/client changes agreed with A. Preserve ApiActor binding.

- Resolve the target in native persisted projects. Explicit project IDs may disambiguate;
  otherwise reject ambiguous revision/proposal IDs. Before provider dispatch and before
  persistence/response, enforce current enabled principal and configured membership.
  Local/bootstrap/admin semantics remain explicit; unknown data is never read from the
  global demonstration chain as fallback.
- Route proposals, single accept, bulk preview and bulk accept through A's service.
  Return actual JSON objects matching browser expectations, not double-encoded strings.
  Require route/body proposal identity agreement and use authenticated acceptance actor.
- Membership checks apply to all involved records. Cross-project batches are refused;
  no global/no-target ownership policy is inferred. Keep original provider configuration
  lookup errors and advisory-result acceptance refusal.
- Prove a real native project create/import→request→accept→reload/export flow using an
  in-process provider double; source-only prompt, durable receipt/L2, unchanged L1,
  browser JSON shape, repeated acceptance, stale/tampered/advisory/cross-project/outsider
  rejection, and revocation while provider is pending. No real hosted call.

## Handoff and integration

Agree method/DTO/error shapes before edits; no edits in the other's files. Keep unit
RevisionChain services as independent pure fixtures, but production native routes must
use persisted projects. Record baseline failures, final commands and bounded limits in
separate evaluation handoffs. Root owns todo/plan/archive, independent review and final
normal-output regression/package verification. New source deltas do not rewrite the
already-frozen resource study binaries or their claims.
