# Manual editor review and preservation fixes

The bounded LineEditor/ProposalPanel review fixes manual uncertain-outcome replay, parent/child write interleaving, partial expansion commits and unsafe manual L2 replacement. It also preserves newer mounted direction/script values after an older acknowledgement. The final focused suite passes **116/116: 29 new cases and 87 existing selected controls**. Root owns the subsequent whole-frontend/AOT gate and final handoff.

Production changes are confined to `web/src/app/line-editor.ts` and `web/src/app/proposal-panel.ts`. New permanent coverage is `line-editor-manual-outcomes.spec.ts`. Authorized fixture/contract adaptations are limited to `line-editor.spec.ts` and `manual-form-ack.spec.ts`. No API, authentication, cache, recovery-store algorithm, package, database or provider implementation was changed by this reviewer. Root separately corrected the existing addAnnotation response DTO to match its server response.

## Actual before evidence

All fixtures use generated text, recording Subjects, actual component handlers, explicit workspace capabilities and owned browser test processes. No training, corpus/locked material, real provider, LM Studio or model execution occurred.

| Source state and control | Actual result | Interpretation |
| --- | --- | --- |
| Initial manual workflow/interleaving source |10 executed: 9 failures, 1 ordinary-success control | Repeated unconfirmed annotations, pending-write interleaving, lost partial-state/navigation/recovery protection |
| Later L2 replacement source |20 executed: 2 failures, 18 passing controls | Both shortcuts submitted `Yb` from L1 `ab` despite existing L2 `aXb`, dropping the unrelated current `X` |
| Later orientation acknowledgement source |29 executed: 1 failure, 28 passing controls | Held old response replaced newer mounted `ltr`/`Latn` fields with submitted `rtl`/`Grek` |
| Final focused source |116/116 pass |29 new cases plus 87 existing selected component controls |

The L2 failures record the exact actual arguments: the alternative used the current editorial parent; expansion omitted a parent. Both could replace the current editorial text. Earlier revisions remained in native history; the demonstrated loss concerns the current L2 reading, not deletion of all historical evidence. This pre-existing semantic gap was discovered separately from the workflow-lock patch.

Artifact root: `.artifacts/experiments/manual-editor-review-v1/`. Original production/test copies and all logs remain preserved. `before.log`, `overwrite-before.log`/`overwrite-before-v2.log` and `orientation-ack-before.log` are the relevant behavioral receipts. The second overwrite invocation changes diagnostic request printing only. Intermediate after-runs include two compile-only attempts around the existing response-DTO mismatch; they are not behavioral failures or passing-test evidence. Passing focused stages and their exact sources remain separately recorded.

## Workflow and admission contract

A separate manual-workflow state captures original line/surface, source L1 revision, target layer, exact requests and followup body, observed editorial ancestry, known result IDs and per-leg phase. It covers general annotations and shortcuts, retraction, manual alternative/expansion, direction/script and non-correcting review mutations. L1 save/correcting-review settlement continues through its existing text-specific reconciliation.

One synchronous admission policy blocks a new parent, manual or proposal mutation while another is pending/unconfirmed. Local text editing and Undo retain their existing policy. Navigation/input changes preserve and quarantine the original workflow instead of reassigning it. ProposalPanel receives a stable parent guard callback through a backward-compatible boolean-or-function input; imperative admission reads current parent state before change detection. Its mutationPending output is synchronous before dispatch. Both same-tick directions have regression coverage without relying on template rendering to apply a lock.

Each manual leg accepts one response under its own token/current-line scope. The extra duplicate-ack test uses a scripted Observable: an old callback cannot clear another operation's newer form or lock. It does not claim an actual network server sent duplicate responses. Workspace boundaries, disposal and fresh reads preserve uncertainty. A documented precommit 400/409/412/422 refusal can release a single uncommitted leg; transport/status 0/5xx/parse loss cannot prove rollback.

For abbreviation expansion, both payloads are frozen before dispatch. Acknowledged L2 plus refused/unknown annotation remains a partial workflow, including the exact L2 ID and frozen link. `linkAttempted` means a **client dispatch attempt**, without proving wire delivery or server commit; `linkAcknowledged` records a received acknowledgement. A deterministic boundary between L2 acknowledgement and link attempt verifies the false/false state; attempted/unknown and acknowledged-link states remain distinct. Acknowledged editorial work waits for a fresh response for the exact line containing its L2 revision before ordinary mutation admission resumes. Read failures or wrong-line payloads cannot settle it or trigger replay.

## Recovery and limits

Unresolved workflow evidence is a versioned ancillary record in the existing L1 editor snapshot. That snapshot retains its actual loaded L1 base/revision and exact original namespace/account/project/surface/line/layer scope. L2 target, requested parent and observed L2 ancestry are separate fields; an L1 ancestor is never presented as an L2 ancestor. Runtime intents/credentials are excluded from the serializable record.

The existing store validates and bounds the opaque JSON envelope; this change does not enlarge its limits or replace its lineage algorithm. Recovery treats unresolved entries conservatively: all mutations remain blocked, payloads/known partial IDs stay available in the record, and no request is reconstructed or replayed. Unknown or legacy payloads are not trusted as a server receipt. Captured original recovery scope is used across a boundary/destroy, preventing a new owner or line from receiving the old packet. Tests verify reload blocking, original-scope retention, absence of generated credentials and unchanged old recovery bytes.

A storage/quota/branch failure is explicit: mounted work/operation remains retained, while the last durable tip can stay behind. The warning instructs the operator to keep the editor open. Successful restart recovery is not claimed when persistence failed. Existing append-only generations and ownerless bytes are preserved; no deletion, compaction or ambiguous automatic resolution was added.

## Manual L2 publication preconditions

Different current L2 text is retained and the shortcut refuses replacement with an explanatory notice. Missing editorial history is separately unavailable; an explicit reload performs a fresh read without submitting a write. Loaded empty history and loaded L2 equal to L1 retain positive controls.

The server's legacy nonmerged null-parent append is unconditional, so a view-only guard would miss a competing first L2. Both shortcuts now send:

- Known empty L2: undefined/null requested parent with `merge=true`, using the existing server contract that atomically expects no current revision.
- Known current L2 equal to source L1: its exact observed parent with `merge=false`, preserving strict parent comparison.

Known-parent expansion never requests merging, preserving direct scalar-range/link interpretation. No guessed text merge or offset mapping is used. Parent/merge values are frozen in recovery evidence and asserted by client recording tests. Root separately strengthened pure backend expected-empty concurrency controls for L1/L2; those backend results are separate from this Angular proof. The old omitted-parent policy is intentionally corrected to prevent concurrent current-layer replacement.

## Mounted acknowledgement fields

Annotation success keeps the earlier per-field raw-value equality protection. Direction/script now capture raw values before script trimming; each returned normalized value applies only if that field still equals its submission. Different newer values survive independently. Status distinguishes the submitted orientation being saved from newer field edits remaining unsaved. The ordinary unchanged-field control still applies normalization.

Matching-value ABA follows the existing clear/update behavior: this is raw-value comparison, not form-generation identity. General unsubmitted annotation/metadata form drafts still lack universal navigation protection and durable recovery.

## Existing-control adaptations and final source

Two old scenarios permitted unsafe overlapping work. Their preserved originals expected navigation during a pending orientation save and L1 publication during a held non-correcting review. Updated assertions retain original target/packet, reject another dispatch and verify later authorized work after acknowledgement. Existing late-response, field, actor and source assertions remain.

Legitimate known-empty fixtures now declare editorial history explicitly. The parent-positive fixture uses L2 text equal to L1 while retaining its exact parent-ID assertion; separate new cases assert refusal for the old differing-text scenario. First-L2/current-parent calls assert their exact parent and merge arguments. Form-ack payload, scalar-range, linkage and unchanged-clear assertions remain intact.

Final focused freeze: `final-focused-source-freeze-v2.json`, SHA-256 `05fd533663d379304a0b6ef8dcfe0cec76fdb642bf02da1edb766135374a62eb`.

| Production file | SHA-256 |
| --- | --- |
| `line-editor.ts` | `c08759be26e835e3c8cde7f451cc0748d11b6ee8389a09c446d14cf3d6def8ce` |
| `proposal-panel.ts` | `00a0ea2fad528f3f081b8ebb89fbb400cda078e599b0e7fc00c4bea6d7cf3422` |

`after-focused-v8.log` records 116/116; `git diff --check` passes for the owned files. All owned Karma processes are closed. No commit or push was made.

Remaining scope: authoritative receipts/adjudication, combined backend expansion/link atomicity, general form durability and manual anchored L2 reconciliation remain open. ProposalPanel's pending acceptance/advisory graph remains mounted-only; the parent boolean does not create new child-packet durability. Unknown outcomes therefore cannot be advertised as universally recoverable across reload. No broader scientific, Greek quality, native-memory or provider-readiness claim follows from these fixes.
