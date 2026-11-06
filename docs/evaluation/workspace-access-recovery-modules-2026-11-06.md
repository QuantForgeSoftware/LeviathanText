# Workspace access and recovery modules

The new coordinator/types/recovery-store modules pass90 focused generated browser tests. The separate mounted-component integration and actual workspace browser proof remain parent/InstanceB work; these module results alone do not establish complete workspace privacy. No fitting, corpus, model or provider workflow was used.

Owned source is `web/src/app/workspace-access.ts`, `workspace-access-types.ts`, `workspace-draft-store.ts` and their two new specs. No existing auth/cache/API/component implementation was edited by this instance. Final owned-source and point-in-time principal/API seam hashes are in `.artifacts/experiments/workspace-access-v1/module-preparation/final-module-handoff.json`. The root's namespace seam31 backend/8 frontend evidence remains a separate receipt; changing those API files later does not rewrite this point-in-time record.

## Component-facing contract

`WORKSPACE_ACCESS` is an injection token with **no default provider/trust**. Its interface exposes `state()`, `isActive()`, `capture()`, `subscribe(listener)`, `owner()`, `projectId()` and `retry()`. State is active/suspended/denied/quarantined with a monotonic generation. State reads and captures synchronously compare the live credential, local generation, shared authority revision, global boundary epoch and lifetime. `capture()` returnsnull while inactive. A captured intent is immutable transient credential/project/generation material plus `current()`; it is never draft ownership or persistent recovery data.

`notifyWorkspaceBoundary(reason)` is synchronous and must run before credential/cache mutation. It fences all live coordinators even if a presentation listener throws. It never automatically probes. Inline `open/retry` from boundary/state-notification callbacks is blocked; the owner must retry after the mutation/drain. Publication-time subscriber snapshots drain in FIFO order, including reentrant suspension. Imperative component actions must call `isActive()`/`capture()` directly; a cached Angular computed is insufficient to notice a direct storage change before notification. Templates need an appropriate direct check/DoCheck boundary as well.

`WorkspaceAccessCoordinator<TProject>` receives structural network-only `api.me(captured)` and `api.loadProjectFresh(projectId,captured)` methods, a live `readScope` function and optional `onConfirmed/onDenied` hooks. Production supplies `onDenied: clearCache` so a current fresh-probe401/403/404 also fences shared authority and other tabs; stale errors never call it. `onConfirmed` stages existing true-base reconciliation before active publication; it does not acknowledge a newer parent or reset editor inputs. Backend authorization remains authoritative.

| Prior witness | Event / fresh result | Result |
| --- | --- | --- |
|No admitted workspace | Explicit valid principal profile, then exact fresh project | Active; durable owner only for a canonical namespace plus persisted account ID. |
|Initial local/bootstrap/account-without-namespace | Ordinary fresh project navigation/retry; original key/generation/ticket/epoch and mode/account descriptor unchanged | Active session-only; no durable ownership or remount/reset. |
|Same initial ownerless witness | Any real notification/storage/key/generation/ticket boundary, fresh failure, invalid profile/project, mode/account change or namespace appearance | Immediately inactive; continuity permanently lost for that coordinator, then quarantined on attempted resume. |
|Known persisted namespace/account | Boundary, then exact same owner and fresh project membership | Active with fresh role; mounted draft/base/undo reconciliation stays caller-owned. |
|Known owner | Missing/changed namespace, different account or incompatible mode | Quarantined; prior work is not reassigned. |
|Any current probe | HTTP401/403/404 | Denied before the shared-authority callback; explicit fresh retry required. |
|Any current probe | Status0 or other fresh failure | Suspended; no cached success substitutes for membership. Ownerless continuity is lost. |
|Any pending probe | Credential/authority/lifetime no longer matches, invalid new project selection or destroy | Late results cannot reactivate/dispatch; old intents are unusable. |

The initial-session exception is deliberately ephemeral, not account-name/key-hash identity. Every durable resume after a real boundary still requires the exact configured namespace/account pair. A different identity needs a separately authorized fresh workspace while the old mounted work remains quarantined; destroying/recreating the coordinator is not an approved way to invent continuity for existing work.

## Recovery schema and guarantees

`new WorkspaceDraftStore(storage?, createRecordId?)` exposes `save(snapshot, previousHandle?)`, `load(exactScope)` and explicit ownerless `readLegacyLine(lineId)`. The snapshot includes all six exact scope values (namespace/account/project/surface/line/layer), actual known base text/revision, draft text, frame identity/checksum/dimensions, and versioned component-owned undo/geometry/order/unconfirmed-write JSON. Missing owner/surface/base does not produce a new durable record. Known empty text is distinct from an unknown ancestor.

Each write appends one authoritative atomic JSON envelope, verified by readback and a full unique-tip recovery lookup. It never overwrites/deletes prior bytes. An existing journal requires the exact current unique-tip handle; stale handles, missing handles, malformed/incomplete lineages or known branches refuse. If another branch appears during append, save reportsfailure and retains both copies. This is not a cross-tab CAS guarantee: a later concurrent append can change recoverability after the observed verification point.

Loading uses the exact encoded six-part namespace, never an account name, API-key digest, matching line ID or accessible project as owner inference. Multiple tips/duplicate-key ambiguity do not choose the newest timestamp. All previous `leviathan:draft:<line>` raw/recovery/base bytes remain untouched and are never auto-loaded as owned. Explicit legacy inspection returns an ownerless result without pairing/accepting its ancestor or assigning an owner.

Bounds are2MiB UTF-8 per record,4MiB aggregate per exact scope,512 records per scope and2048 total storage keys scanned (including malformed/unrelated keys). JSON depth32/nodes50,000 and plain own data descriptors are validated; arbitrary prototypes/accessors/functions/cycles/nonfinite values, credential-like fields and unsafe keys refuse. Opaque data is validated/copied rather than reinterpreted as an editor acknowledgement. Geometry coordinates are not converted or inferred. Unsupported/oversized records remain stored for explicit resolution.

Automatic compaction and an authorized recovery-resolution UI are open. Frequent autosaves can reach a limit; failures must remain visible and preserve the mounted copy. Cancellation is not rollback, and this storage module does not settle any unconfirmed write merely because a later read lacks it. Caller serialization and later authoritative receipt reconciliation remain component responsibilities.

## Generated evidence and corrections

Actual Chrome147 focused results are preserved under the artifact directory:

- Initial60 module controls passed. The first attempted launch had an isolated Karma-config path error before tests; its log is retained, then the corrected config ran.
- Five coherent behavior regressions reproduced60pass/5fail: three known-ambiguous/stale recovery false-success cases and two inline reauthorization/read-cancellation cases. The fixed implementation passed72 controls, including explicit storage budgets and FIFO/faulted teardown behavior. Intentional branch fixtures inject raw branches instead of asking a correct saver to create them successfully.
- The approved initial-session compatibility change and invalid-project stale-response guard reproduced72pass/4fail before correction. Final90/90 passed with all negative key/generation/ticket/mode/account/namespace/failure controls retained. Three compatibility failures are a newly approved policy expansion; the invalid-project case is a concrete stale-reactivation defect. No missing-method/type error is presented as a behavioral before proof.

Strict standalone TypeScript checks pass. The final browser run is `initial-session-fixed.log`; this is not the whole frontend suite or actual mounted-workspace/browser acceptance. Parent integration, full frontend/engineering checks and independent review remain separate gates.
