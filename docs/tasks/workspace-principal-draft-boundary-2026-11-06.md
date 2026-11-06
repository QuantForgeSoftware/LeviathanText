# Parallel workspace identity and draft boundary

This is a future implementation brief, separate from the narrow cache and ProjectsPage asynchronous-intent corrections. It authorizes no execution during the long API measurement quiet window. Root coordinates implementation authorization, the shared contract, final integration and validation. No training, corpus loader, reserved labels, real provider, or LM Studio operation is needed.

## Required behavior

Credential changes synchronously suspend protected presentation and actions, invalidate callbacks, and stop presence/background requests. Preserve the mounted draft-bearing editor while identity is unresolved; do not clear inputs or remount merely to conceal it. Same-account resumption requires authoritative identity and selected-project access. Different identities, denied access, and undefined local/bootstrap ownership retain prior work quarantined rather than assigning it to the new principal.

Identity checks use stable persisted account ID and explicit authentication mode, never name/role or an API-key hash. The additive principal API proposal is a dependency to freeze and review first. Account IDs are store-scoped: decide a deployment/store namespace policy before persisting owner-scoped recovery. Local/bootstrap mode without a defined stable owner must not be treated as a persisted account. This is an ownership/presentation boundary; backend authorization remains authoritative for every request and commit.

Use network-only identity and selected-project reads for reauthorization. `loadProjectFresh` already supplies a non-cache project read, but its actual request also needs captured credential/lifetime guards. A status-zero cached success is not fresh membership confirmation. Reauthorization can fail offline without losing drafts.

## Shared contract before parallel edits

Agree a new common workspace types module before either instance modifies components. It should define:

- A nonsecret principal descriptor with explicit mode and persisted account ID only where meaningful; no credential-derived owner identity.
- Access state: active, suspended, denied, or quarantined, with a monotonic credential/access generation. Every accepted transition checks the originating request scope and current principal/access result. Same-tab cache-authority invalidation after a server denial also needs an explicit access notification or coordinator-owned denial path; listening only to apiKeyRevision misses it.
- A capture/check operation for callbacks and deferred intent that includes local generation, current key equality, shared authority revision and lifetime. The transient captured key is passed only to request context, never persisted as draft ownership.
- An editor draft snapshot that preserves text plus its actual base, undo state, geometry and reading-order drafts, frame identity, expected saved snapshots, and unconfirmed writes. Snapshot/restore is separate from accepting a newer parent.
- A recovery-store result that can report persistence failure. Failure must not trigger destruction of the only mounted copy.

No public interface should imply that cancelled HTTP means an already submitted mutation rolled back. There must be an explicit unconfirmed-write state and later authoritative reconciliation.

## Instance A: identity coordinator and recovery storage

Own only new `web/src/app/workspace-access.ts`, `workspace-access-types.ts`, `workspace-draft-store.ts` and their new tests, plus any root-assigned API principal helper/tests. Do not edit ProjectsPage, SurfaceView, LineEditor, GeometryEditor, ProposalPanel, existing auth/cache modules, or root's current intent tests without handoff.

Implement the reviewed access state machine and network-only identity/project confirmation. Preserve complete captured-request fencing, including storage-event ordering and ABA. Own recovery serialization, validation, principal/project/line namespace policy, atomic record writes and explicit legacy-ownerless handling. Keep old draft bytes intact until an authorized recovery resolution and successful replacement are verified. Do not infer an owner from a line ID, account name, role, matching project access or unpersisted-mode label.

Acceptance uses generated identities, controlled HTTP/Promises and recording stores. Cover same-account key rotation, distinct accounts with equal names and common project access, denied/missing project, role change, local/bootstrap transitions, stale identity/access responses, authoritative denial, status-zero refusal to resume, quota/storage exceptions, malformed records, atomic text/base recovery and legacy bytes preserved. No real credentials or source manuscript text are input.

Handoff an exact DTO/API source hash, state transition table, component-facing interface, store schema/namespace policy, tests and unresolved decisions. Notify B before changing any agreed shared type.

## Instance B: preserved editors and workspace orchestration

Own ProjectsPage/SurfaceView/LineEditor/GeometryEditor/ProposalPanel integration and new component/browser tests. Do not edit A's coordinator/storage files or API principal implementation; consume the agreed interface.

Suspend protected presentation and all action handlers immediately on access-generation change. Do not rely solely on CSS or `readOnly`: discard, keyboard, pointer, queued callbacks, downloads and background presence/session operations also need suspension guards. Preserve live text/base/undo, geometry/new-line/order snapshots and pending-write uncertainty. Avoid input changes that invoke existing reset-heavy scope-change paths.

Resume the same principal only after current network-only access succeeds, through existing reconciliation paths. A delayed save/proposal acknowledgement must not clear newer source drafts. If saved L1 changed, compare against the true base rather than treating the newest parent as already accepted. If frame identity changed, preserve old-frame geometry quarantined; never guess a coordinate conversion. A different principal must not see, recover or submit prior work merely because it can access the same line.

Acceptance uses generated projects, independent text/base/geometry sentinels and controlled callbacks. Cover credential change with dirty L1 plus undo and geometry/order drafts, active write with late acknowledgement, revoked membership, same account rotated key, equal-name different accounts, incomplete recovery persistence, stale presence session, frame changes and returning original owner. An actual fresh-profile browser proof should assert concealed old collection/snippet/editor data and retained drafts separately from JobsPanel. Respect existing dirty-source proposal guards; do not relax them to manufacture a passing browser demonstration.

Handoff component ownership changes, exact source hashes, snapshot/reconciliation examples, generated evidence and the browser's owned-process cleanup receipt. No corpus, model or provider calls are required; a local provider double may be used only if root separately includes that path.

## Root integration and stop conditions

Root freezes the principal contract and namespace/local-mode policy before parallel production edits. Integrate A's typed seam first, then B's component use; avoid concurrent writes to shared files. After the quiet window releases, run source-only/generated policy tests first, then focused/full frontend and safe backend gates and a fresh actual browser proof. Record before-failures against coherent compilable source, not missing API overloads or replaced cache implementations.

Stop on data loss, cross-principal draft visibility/submission, implicit owner reassignment, stale mutation dispatch, unresolved write acknowledgement, or destructive remount. Preserve the failing source and generated evidence before correction. Existing specialist decisions, provisional geometry, image rights and model gates remain unchanged. Passing this work would not erase the original spent P01 evaluation or establish new Greek accuracy.

## Frozen namespace/transport seam — root checkpoint

`PrincipalProfile.workspaceNamespace` is optional/null for compatibility. Only fresh persisted-account replies may carry a canonical nonempty UUID from explicit `LeviathanText:Auth:WorkspaceNamespace` configuration; malformed/missing values and local/bootstrap yield null. `persistedWorkspaceOwnerFromFreshProfile` returns `{ workspaceNamespace, accountId }` only for this complete account capability. An equal accountId with changed/missing namespace after a transition is not continuity. Do not inherit the previous namespace. The API never grants project membership through these fields.

Operators must assign a unique stable namespace for each ownership domain (store/deployment), retain it for intended account-key rotations and backups, and rotate it when an unrelated store/ownership domain replaces the backend. Generate an individual UUID; do not copy a shared example value. This setting is nonsecret, changes no schema and is not automatically configured in this work. Without it, preserve the mounted draft but provide no invented durable owner or cross-transition resumption. Exact session-only initial local/bootstrap behavior must be documented separately from account continuity.

New recovery scopes include namespace/account/project/surface/line/layer and the actual base; ambiguous duplicate graph identities must refuse automatic lookup. All previous `leviathan:draft:*` ownerless keys remain untouched and must not be imported or erased merely because a line ID matches. A source frame belongs in geometry snapshots; do not convert it implicitly.

Root owns `PrincipalDescriptor.cs`, `principal-profile.ts`, `api-client.ts` and their seam tests. `me(capturedCredential)` and `loadProjectFresh(projectId,capturedCredential)` request no-cache/no-store data with an actual pinned auth context, including empty credentials. The omitted-parameter old caller remains compatible. Root's31 API checks (11 new namespace/20 existing principal) and8 new client checks pass. The full current checkpoint passes2050 engineering tests/18 intentional skips,371 frontend and production build; it predates coordinator/editor implementation. This additive capability alone is not an AUTH-UI-01 closure.
