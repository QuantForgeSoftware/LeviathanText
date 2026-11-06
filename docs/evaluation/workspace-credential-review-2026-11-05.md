# Workspace credential transition review

This is a source-only case brief, recorded during the coordinated resource quiet window. No regression or browser command was run for this brief. Root owns the auth/cache fix; the proposals below are not implemented or accepted behavior.

The separate [JOB browser proof](job01-browser-api-proof-2026-11-05.md) verifies JobsPanel row/event disposal after switching credentials. Its credential-switch screenshot also retains the previous collection name in ProjectsPage. The sixteen JOB assertions do not establish whole-workspace reauthorization.

## Confirmed source boundaries

- `ProjectsPage` lists projects once in its constructor. It observes neither credential revisions nor component destruction, and holds project payloads, selected IDs, statistics, review text and search snippets indefinitely. Its subscriptions have no selection/credential generation checks. An old project's delayed statistics or search response can also update a newly selected project without any credential change.
- `offline-cache.ts` falls back on every HTTP failure, including401/403, and successful late reads write an unscoped cache. Clearing cache during a credential transition does not invalidate an already subscribed old request. This can deliver previously protected cached content as a new account's successful result after the server denies access. It is a distinct correctness/security defect from already loaded browser state.
- `onCsvFile` awaits `File.text()` then reads the current selection and credentials; an old user intent can therefore become a new account's mutation. Export responses create downloads without checking whether the initiating credential or selection remains current. Cancelling HTTP is not proof a mutation was rolled back.
- `/api/v1/me` exposes authenticated/name/role only. Names are not stable account identities. Bootstrap currently has authenticated=true/name=bootstrap-admin/role=Admin through a synthetic UserAccount; anonymous local mode has authenticated=false/name=local/role=admin. Neither shape explicitly establishes a persisted account identity, so clients must not infer continuity from a name or synthetic ID.
- `SurfaceView` refreshes preview on credential revisions. Its existing editor tree and other requests remain live. Hiding a parent alone is insufficient to suspend child callbacks or side effects.
- `LineEditor` atomically retains text/base recovery, but keys contain line ID only. Opening the same accessible line under another account can recover the previous account's local draft. Geometry drafts and undo state can exist only in mounted memory. Destroying the subtree to refresh credentials can lose that work.

The exact source-only snapshot is `.artifacts/experiments/workspace-credential-review-v1/source-review.json`. It is a historical anchor; root may independently update owned auth/cache files afterward.

## Proposed finite delivery sequence

1. Root's cache boundary should deny401/403 fallback, permit only explicitly selected transport failures, and bind both cache reads/writes to the request's captured credential generation. A late old request must neither emit a newly authorized value nor populate the new generation's cache. Never persist raw API keys in cache names or metadata. Legacy unscoped caches require an explicit discard policy.
2. Introduce an authoritative nonsecret principal discriminator, including stable account ID and explicit local/bootstrap modes. Do not infer same-account continuity from name, role, collection membership or equal project IDs. A new key for the same account still requires fresh membership/role checks.
3. On credential revision, synchronously suspend protected presentation/actions, invalidate page request generations and cancel active reads. Independently check captured credential, selected project and operation generation in every callback, including deferred file reads and download side effects. Stop background/presence requests that could otherwise silently execute under a new credential.
4. Re-fetch authorized collection membership and the selected project under the new generation. Retain the mounted draft-bearing editor in a concealed, interaction-disabled suspended state while identity is unresolved; do not change its project/surface inputs merely to clear the page. Suspension must also prevent editor mutation handlers and stale acknowledgements, not rely solely on CSS. A previously submitted write may have committed: retain an unconfirmed-write state and reconcile against the actual saved parent after access returns.
5. Resume the existing editor only after the same stable principal and fresh project access are confirmed. Preserve text, text base, undo, geometry/order polygons and their expected snapshots. Refresh saved state through the existing reconciliation paths, not unconditional input reset. If principal changes or access is denied, keep prior work quarantined and unpresented; do not silently assign it to the new principal. Expose recovery only after the original identity reauthenticates, or through an explicit owner-approved transfer workflow.
6. Scope future recovery records to stable principal/project/line identity while preserving the atomic text/base record. Legacy ownerless drafts require explicit comparison/confirmation under an authorized recovery workflow; never silently delete them, automatically claim them for a new account, or put credential secrets in the record. Decide the local/bootstrap transition policy explicitly.

This requires coordinated page/editor/API changes and must not be smuggled into a cache-only patch. Existing backend authorization remains authoritative; suspended client state is a presentation and stale-intent boundary.

## Proposed regressions, not yet executed

| Generated case | Acceptance |
| --- | --- |
| A read pending, change to B, clear cache, release A success, B403 | A value is neither newly rendered nor cached for B;403 remains an error. |
| Previously cached A payload, current B401/403 | No offline success or protected new content. Explicit same-generation transport-offline policy is tested separately. |
| A collection/project/stats/review/search responses released after B transition | Previous names/snippets remain concealed; only current-generation accessible data becomes visible. |
| Same account changes key while L1 and GEO02 drafts exist | Stable identity and membership are verified; drafts/base/undo/order snapshots survive and no automatic save occurs. |
| Different accounts have the same name and access the same project | Account identity still differs; prior drafts are not automatically restored, presented or submitted. |
| Account loses project membership during credential refresh | Editor stays suspended; no retained toolbar, hidden click or delayed callback writes under the new credential. |
| Deferred CSV text or PDF/HTML response resolves after switch | No import starts for the new account and no old protected download is triggered. |
| A write committed but acknowledgement arrives after transition | No blind acknowledgement or draft clearing; returning original identity reconciles committed state before retry. |
| Surface changes while old stats/review/search callbacks remain pending | Responses update only their exact originating view; no cross-selection overwrite. |
| Legacy line-only recovery under a different account | Recovery is not silently reassigned; retained bytes survive until explicit authorized resolution. |

A new actual browser proof should assert collection/project presentation and drafts independently of JobsPanel, and retain a denied server request as evidence. The existing JOB screenshot is the starting observation, not a passing workspace-auth acceptance.

## Existing editor seams for the later workspace change

A second static pass identifies reusable boundaries without implementing them. `GeometryEditor.readOnly` already contributes to `locked`; toggling it does not itself clear the working frame/drafts. It can disable geometry mutations while an identity check runs, but its locked-state discard controls still need suspension-specific protection. Do not turn a credential refresh into a project/surface input change: the existing scope-change path deliberately clears/reloads geometry.

`LineEditor.forCurrentLine` fences line/version changes, not credential transitions for the same line. A suspension path must invalidate old callbacks without calling its reset-heavy `ngOnChanges` or clearing atomic recovery. Pending writes require an explicit unconfirmed/reconciliation state: cancelling an old acknowledgement does not mean the server did not commit. Stable-identity reauthorization should resume via existing text/base reconciliation, never merely assign a newer parent.

`SurfaceView` needs to suppress its credential-triggered preview effect and presence/session heartbeat while the workspace is suspended. Otherwise a hidden existing component can continue requests under the new key for the old surface. Its child ProposalPanel callbacks also need the same access generation; visual concealment alone is insufficient. Resume/reauthorization must not revive an old session as the newly authenticated principal. Existing saved-source refresh and geometry frame-change safeguards should remain authoritative; they are not a reason to discard old-frame drafts automatically.

These are scoped implementation requirements for a later coordinated workspace change, not assertions that the separate read-only cache fix implements them.
