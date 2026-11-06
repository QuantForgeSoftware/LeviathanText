# AUTH-RECOVERY-03: manual annotation and L2 uncertain outcomes

Review implementation follow-up (2026-11-06): the bounded manual-workflow/common-admission portion is now implemented with versioned ancillary recovery under the true L1 editor base, explicit L2 ancestry/parent/merge preconditions and per-leg partial state. A later source review also fixes current L2 replacement and older direction/script acknowledgements overwriting newer mounted values. Actual before failures and 116/116 focused controls (29 new/87 existing) are preserved in the [manual editor review](../evaluation/manual-editor-review-2026-11-06.md). No receipt, replay, atomic combined backend operation, general form journal or full ProposalPanel restart recovery is implemented. Storage failure remains explicitly mounted-only. The chronological proposal/findings below describe the original inspected state and remain historical evidence; they are not a claim that every proposed stage is complete.

Dated narrow follow-up (2026-11-06 10:41 UTC): the historical newer-form-clearing finding below now has an independently executed before result (2 failures/2 ordinary-clear controls), a 4/4 isolated correction and reviewed main integration with 643/643 frontend tests plus production build. An older acknowledgement now preserves newer **different raw field values** and clears unchanged submitted values. This is not form-generation/ABA identity or an uncertainty, partial-commit, receipt or mutation-mutex solution. See [form-ack preservation evidence](../evaluation/manual-form-ack-preservation-2026-11-06.md). The original failure sources/receipts remain preserved; the broader proposal below remains unimplemented.

Original proposal status (historical): source-reviewed proposal only. No production change, regression execution or new receipt/replay capability is authorized by this brief. Root must review the exact delivery contract before implementation. Work is generated-data-only; no provider, model, corpus, training or specialist decision. No new runtime after 2026-11-06 10:45 UTC; the current work deadline is 11:00 UTC.

## Original inspected source scope

Current LineEditor protects L1 saves/correcting reviews, and ProposalPanel protects AI acceptance/advisory submission. Those locks do not cover the following manual routes:

| UI operation | Current path | Risk after a lost response or scope cancellation |
| --- | --- | --- |
| General annotation Add | `line-editor.ts:765–793`; POST `lines/{lineId}/annotations` | A committed annotation may already exist; retry mints another ID because the UI omits `Id`. Form retention alone does not identify the commit. |
| Mark unclear/lost/word boundary | `line-editor.ts:933–978`; same annotation POST | No pending operation/navigation lock; repeat can duplicate an annotation or position marker. |
| Retract annotation | `line-editor.ts:727–733`; POST `annotations/{annotationId}/retract` | Retraction may have committed. A later “already retracted” refusal is not proof of this request's identity, actor or receipt. |
| Accept manual alternative as L2 | `line-editor.ts:748–762`; POST `lines/{lineId}/revisions` | L2 may have committed. The retained original parent can produce a conflict on retry; a stale/missing parent is not an operation receipt. |
| Abbreviation expansion | `line-editor.ts:796–826`; L2 revision POST, then annotation POST | Two independent saves permit partial commit, described below. The annotation is the explicit L1-to-L2 link. |

Error branches currently retain form/state or do nothing; they do not add these manual operations to `pendingWrite`/`unconfirmedWrite` or `hasNavigationWork()`. A clean L1 editor therefore need not block navigation while a manual annotation/L2 write is unresolved. These are source findings, not newly executed before-failure tests.

### Current admission matrix, including another unknown L1 write

An existing unknown L1 save/correcting review currently retains `savingRevision=true`. That fact blocks some handlers, not every mutation. `workspaceWritable()` at 357–360 checks only current workspace access and Annotator/Reviewer/Admin role; it is not a common pending/unknown-write gate. The template's generic “new writes remain blocked” status at 30 must not be treated as evidence of broader action guards.

| Operation | Tracks its own unknown result now? | Can dispatch while an existing L1 write is unknown? | Actual admission condition |
| --- | --- | --- | --- |
| L1 save/correcting review | Yes, bounded existing text-save scope | No | `savingRevision`/conflict and operation-specific checks |
| General annotation Add | No | No | `canAnnotate` includes not dirty/conflicted/saving; Abbreviation delegates from this checked handler |
| Mark unclear/word boundary/lost | No | No | Explicit not dirty/conflicted/saving guard |
| Retract annotation | No | **Yes** | Only writable workspace and loaded line |
| Accept manual alternative L2 | No | No | `isCurrentAnnotation` includes not conflicted/saving and exact current L1 text/anchor |
| Abbreviation L2 then link | No | No new initial dispatch | Initial Add checks `canAnnotate`; an already started chain has no manual-operation lock or shared admission interlock |
| Direction/script PUT | No | **Yes** | `applyOrientation` at 617–627 checks writable workspace/loaded line, reports a status on error only |
| Review accept/dispute | No | No | `recordReview` at 831–853 rejects saving/conflict/dirty; only `correct` calls `beginWrite` |
| Proposal acceptance/advisory save | Separate ProposalPanel lock | **Yes at client admission**, if its existing proposal remains current | Parent passes line/revision/range, not L1 saving/unknown state; `canSave` checks only its own busy/current source and workspace/role. Backend may still refuse a stale L1 source. |

For retraction/orientation, dispatch during unknown L1 does not itself prove L1 text loss; it demonstrates that the present blanket new-writes-blocked claim is false and multiple untracked effects can coexist. A general annotation or review accept/dispute may also remain unknown after its *own* request while L1 stays clean and `savingRevision=false`, so its guard can permit repeat dispatch. Direction/script retries can additionally overwrite a newer remote metadata choice because their request has no operation receipt/expected-value identity. Do not infer idempotency merely from PUT or an “already retracted” response.

Existing L1/proposal locks are not a universal mutex in the reverse direction either: LineEditor `save`/`recordReview` do not inspect `proposalMutationPending`, and the child has no parent write-block input. That signal currently contributes to navigation protection. A common admission design needs explicit parent/child coordination and bidirectional tests; do not claim mutual exclusion from the presence of two separate busy flags.

Read-only/local text editing and Undo are different from dispatching a server mutation: preserving newer mounted edits while a frozen submission is unknown is intentional. Do not disable or discard those drafts as a shortcut. Direction/script and accept/dispute tracking must either join an explicitly reviewed manual-operation scope or remain named open gaps; they are not silently covered by an annotation/L2-only delivery.

API/service evidence: `ApiClient.appendRevision/addAnnotation/retractAnnotation`; `Program.cs` revision route near 1372 and annotation/retraction routes near 1454–1507; `TranscriptionService.AddAnnotation/RetractAnnotation/AppendLineRevision`. Each service saves independently. The annotation API accepts an optional ID, but current manual callers omit it; supplying a client ID alone would not make AddAnnotation idempotent, since the service appends and has no exact-payload receipt check. Revision append and retraction requests have no operation ID. No existing server contract permits blind replay of an uncertain request.

## Abbreviation state machine and partial commits

Freeze the exact initial owner/project/surface/line, L1 anchor/range, L2 requested parent, composed L2 text, expansion text/range and description before the first request. Keep runtime credentials/current predicates separate from the persistable packet.

1. **L2 request sent, no acknowledged result:** L2 may be absent or committed with an unknown revision ID. Do not send the linking annotation, resubmit L2, clear the operation, or infer success from equal current text.
2. **L2 acknowledged, annotation not dispatched:** retain the exact returned L2 ID. Boundary loss, destruction, missing scoped API, or failure before dispatch leaves a known unlinked L2 result. Do not recreate the revision on resume.
3. **L2 acknowledged, annotation sent, result unknown:** both records may exist, or only L2. Retain the known L2 ID and exact annotation payload. Do not repeat either request merely because a fresh list has no matching row.
4. **L2 acknowledged, annotation definitively refused:** the L2 commit still exists. Even a documented precommit 400/409/412/422 refusal of the annotation cannot roll back the earlier revision. Preserve a distinct partial-commit state; do not return the whole operation to an ordinary retryable Add button.
5. **Both acknowledged, refresh interrupted:** both write acknowledgements are known. A later metadata/annotation-read failure must not relabel either write as uncommitted or submit again. Keep the authoritative IDs until a current read can show the result.
6. **Both acknowledged and refreshed:** clear only this operation and its submitted form values. Do not clear a newer form draft, change a newer L1 base, or settle another pending write.

Current expansion calls append L2 without an explicit parent. This brief does not quietly change that legacy concurrency policy. Record the exact requested null parent separately from any actual observed L2 ancestor; any stricter expected-parent change needs its own reviewed before/after policy. Do not invent L2 ancestry from the L1 anchor.

The original inspected source contained a second independent form risk: success cleared form fields unconditionally, so a newer description/expansion typed during the request could be cleared by an older acknowledgement. The narrow equality fix is now integrated as recorded in the dated follow-up above; preserve this original finding as historical before evidence. A delivery may hold the submitted annotation form read-only while pending and/or clear only fields still equal to its frozen submission. Preserve later edits either way; do not reset mounted text/undo to make tests pass.

## Small common lock, without false L1 reconciliation

A mounted manual-operation lock is feasible using existing primitives, but it must be **separate** from L1 `pendingWrite`, `pendingSubmission` and `endWrite/reconcile`. Those methods use an L1 text merge pivot and may release a text-save lock after a fresh read; they cannot prove annotation identity or settle a two-leg L2 operation.

Use one discriminated manual-operation state with a local monotonic token, frozen target/payload, per-leg dispatch/acknowledgement state, known result IDs and a retained unresolved/partial reason. Capture one existing workspace intent/scoped ApiClient at admission; the second leg must not recapture a different owner's credential or route to a newly selected line. Reuse synchronous workspace suspension, `forCurrentLine`, role checks and generation guards. Every mutation dispatch must check the current intent immediately; stale callbacks cannot mutate or settle the new owner.

Include this lock in `hasNavigationWork()` and a common editor write-admission check, covering manual annotation/L2, L1 save/correction and proposal mutation interleaving. A matching old acknowledgement may settle only its own token/leg. Keep text editing/undo available if it already is, with the operation's submitted payload immutable; later text edits must not rewrite the pending packet. Disable/guard new mutations, focus/capture navigation and destructive discard while unresolved. Scope cancellation, status0/5xx/parse errors and destroy are not rollback. Ordinary fresh reads may display current persisted data, but must not clear an unresolved operation or advance its captured ancestor.

A current documented precommit refusal can release an otherwise single-leg operation; the same refusal on expansion's second leg leaves a known partial commit. Do not apply one blanket status-based unlock to the two-leg workflow. Existing 401/403/404 shared-boundary suppression can prevent an error callback from running, so the suspension listener must retain the in-flight operation even without that callback.

## Recovery and receipt limits

`WorkspaceDraftStore` already supplies bounded versioned opaque records, exact owner/scope keys and append-only recovery. Reuse its infrastructure only with a validated new operation payload; no prototype/credential fields or arbitrary response objects. Never delete legacy records, compact away uncertain operations, increase limits silently, or claim persistence succeeded when save fails.

A durable L1 annotation packet must have its actual loaded L1 base. An L2 packet needs an exact L2 scope and an actual known L2 ancestor (including verified empty ancestry), plus the separate L1 source anchor when relevant. Do not store an L2 packet as an L1 operation merely because today's editor loads only the L1 recovery scope. Recovery admission must load exact relevant layers and retain ambiguous/branch results for explicit adjudication. Unknown owner, namespace or base cannot be invented to obtain persistence. If an initial delivery only preserves mounted local/bootstrap/unowned work, show that limitation explicitly and do not claim restart recovery. Root must choose whether a failed pre-dispatch durable save blocks submission or permits an explicitly mounted-only flow; retain bytes and never silently downgrade the guarantee.

The [authoritative mutation-receipt proposal](authoritative-mutation-receipts-2026-11-06.md) remains unimplemented. Absence from an annotation list, an already-retracted response, matching L2 text, same author/display name, cancellation and missing receipts do not prove rollback or authorship. A future positive operation/owner/payload-bound receipt can settle only its exact pending leg. Atomic expansion-plus-link publication could remove the partial-save window, but requires a separate server transaction/receipt design; a UI lock alone does not make two HTTP saves atomic.

## Concrete generated regressions before implementation

Preserve actual current source and first demonstrate the intended observable failures; do not use missing helper/API compilation as before evidence.

- Commit annotation, then simulate status0 and500 separately: preserve exact form/anchor, block repeat dispatch/navigation, retain the unknown operation across a boundary; stale acknowledgement cannot clear a newer packet.
- Hold each shortcut/retraction/alternative-L2 request: repeated clicks and keyboard actions cannot dispatch again; current 422 on a single uncommitted leg can release only that leg under the documented contract.
- Expansion leg1 commit/lost response: no leg2 and no repeated L2 append, even if a fresh current L2 has equal text.
- Expansion leg1 acknowledgement followed by boundary **before** leg2 dispatch: retain known L2 ID, perform no B-key/new-line request, and report known partial state.
- Expansion leg2 commit/lost response; leg2 definite422 after known L2; stale L1 anchor409; and leg2 acknowledgement followed by refresh500: preserve each distinct state and both exact IDs where known.
- While an L1 save is held/unknown, exercise every matrix handler: retraction/orientation current dispatch before, reviewed exclusion after; retain existing canAnnotate/shortcut/review guards. Separately simulate accept/dispute and orientation own-request status0/500 if included, without pretending their current handlers track uncertainty.
- Retain later form edits and L1 dirty text/undo through an older success. No automatic clearing/base advance from a current read, return to the same account, or a matching displayed annotation.
- Test same-account fresh resumption, different-account/key/namespace quarantine, Reader downgrade, silent storage mutation, ABA and destroy while either leg is pending; legacy bytes and exact owner/layer scope stay untouched.
- Inject bounded storage failure, full journal, stale handle/branch and reload of unresolved packets: warn/block according to the reviewed delivery policy, preserve mounted work and all old bytes, and never report verified recovery if it failed.
- Preserve ordinary successful manual annotation/alternative/expansion pixels-independent text/ranges/linkage and authoritative IDs. Backend generated stores verify no accidental duplicate revision/annotation or invented rollback.

These controls do not evaluate scholarly correctness. Any actual API/browser process proof needs a separately sealed finite protocol, owned cleanup and root CPU-window approval. No metrics, training or real providers are needed.

## Parallel ownership and decision gates

Root owns policy, current root boards and integration. Before code, freeze the discriminated packet, status/leg transitions, form-admission policy, exact durable-layer rules and the no-replay limitation.

One implementation owner may own LineEditor/manual-op state, explicitly chosen adjacent metadata/review handlers and new focused tests. A separate evaluator owns new controlled before/after tests and any generated HTTP/browser proof, with no production edits. If typed recovery validation needs a new module, give it a third disjoint owner or sequence it after the shared DTO freezes; do not concurrently edit WorkspaceDraftStore's lineage algorithm. Backend atomic publication/receipts are separate work under AUTH-RECOVERY-02, not a hidden dependency/schema change in this UI lock patch. Leave ProposalPanel and geometry lock semantics intact unless a concrete shared-admission seam is reviewed.

Do not mark the gap closed from source alone, a local busy flag, or a UUID. Require the applicable before/after unknown/partial-commit controls, current-source integration gate, and honest mounted-only versus durable receipt scope.

## Prior verification incident reference

The precise incident report is [TEI language metadata — Verification scope mistake](../evaluation/tei-language-metadata-2026-11-05.md#verification-scope-mistake), with [Nov5 handoff disclosure](../handoffs/project-state-2026-11-05.md). Three unfiltered Application invocations ran 40 epochs each on four generated 260×320 pages; three API invocations ran 30 epochs each on three generated 240×300 pages, plus generated detection. This violated the earlier no-fitting assignment. It was not real manuscript/P46/provider work; no accepted labels or promoted checkpoint resulted. Image/model fixture roots had cleanup, but full cleanup of each random API SQLite fixture is not claimed. The accepted filtered gate and later default-off capability guards are distinct from those earlier executions; do not describe the entire multi-day conversation as training-free. The authorized overnight work described by this new brief executes no fitting.
