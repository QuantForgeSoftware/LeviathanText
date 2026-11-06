# Cache v3 and workspace source audit

This independent review ran no build, test, browser, decoder, model, or dataset operation during the long API measurement quiet window. Source inventory hashes and existing receipt hashes are recorded in `.artifacts/experiments/cache-workspace-source-review-v3/source-review.json`. Existing logs report 24 focused and 218 frontend passes; they were read, not rerun, and are not asserted to cover every source hash reviewed here.

## Cache v3 conclusion

No new high-confidence blocker was found in the narrow cache boundary. The immediate authoritative-denial fence now precedes digest awaiting. Subscription captures key, local generation and shared authority revision; success and fallback recheck all three. Persistent entries require both owner SHA and authority revision. All three actual cached ApiClient reads pass the captured key through HttpContext; the interceptor strips contradictory authorization headers, including explicitly captured anonymous mode. Legacy namespaces cannot overwrite v3 entries. Source drafts and stored API keys are outside cache removal. Cryptographic-binding failure still permits current network data but disables persistent fallback.

The real cached-read fixture wraps public ApiClient methods and waits for their eventual emission/error. It does not replace cachedRequest or fake a completed owner digest; resolving its Promise schedules the test continuation after the synchronous subscriber callback. The separate auth tests prove captured-context header behavior. A future composed test should exercise listProjects/loadProject/line with the real auth interceptor and assert each actual HTTP header, providing stronger integration coverage than the present separate source/context checks.

Remaining limits are explicit: old tabs that do not rotate the shared ticket cannot establish full before-event ABA fencing; status-zero same-key offline policy cannot discover remote membership/account revocation without a server response; non-HTTP errors retain the documented broad compatibility fallback. Conservative delayed storage events can discard a newer in-flight read, requiring retry; this favors safety over guaranteed availability. Same-origin storage is not protection from same-origin script compromise. None of this reauthorizes already mounted workspace state or draft ownership.

## Narrow ProjectsPage correction

The pre-change source admits selection/credential/destroy races outside the cache. Stats and review are raw reads; old replies can populate another selection. Payloads are retained by project ID. Search has neither latest-request ordering nor immediate hit invalidation on query/mode changes. File.text reads current project/layer only after resolution, allowing an earlier file intent to become a later selection's mutation. Export callbacks trigger downloads without checking their originating context. The constructor's list response can restore a deep link after destruction, and create acknowledgements likewise have no lifetime guard.

A bounded correction should capture immutable intent and a selection revision, latest-search revision, credential guard and component lifetime. Credential scope includes shared authority revision, not only key and local generation, so updated-tab ABA before delivered events remains fenced. Deferred mutation dispatch should pin the captured credential on its actual request context. Layer changes require an explicit snapshot-or-cancel policy; they must never reinterpret a pending file using the new layer. A sent mutation may commit even when its acknowledgement becomes stale: suppressing that acknowledgement is not rollback.

Proposed generated regressions, to execute only after quiet releases:

| Ordering | Required result |
|---|---|
| Select A, select B, release A stats/review/payload | B state is unchanged by A callbacks. |
| Select A, B, A; release first A | First-A epoch cannot override current A. |
| Search first then second; release second then first | Only second results/status survive. |
| Search pending; edit query or mode without another click | Old hits disappear and pending result cannot return. |
| Deferred file text; switch project/layer/key or destroy | No reinterpretation or unauthorized new import; explicit layer policy holds. |
| Updated-tab A→B→A with queued events | Shared authority scope fences old intent. |
| Export pending; switch project/key or destroy; release | No old Blob URL/download/status side effect. |
| CSV already sent; switch before success/error | No new-view overwrite or silent draft acknowledgement; committed server result is not described as rolled back. |
| List/create callback after destroy | No state update, deep-link navigation or child creation. |

## Loaded workspace and draft ownership remain separate

The narrow correction must not reset or remount draft-bearing editors merely to hide stale read state. ProjectsPage currently retains prior collection names/payloads; SurfaceView refreshes preview on credential change but preserves other callbacks and presence activity. LineEditor recovery keys contain only line ID, so another account can claim an earlier account's draft. Geometry readOnly prevents many edits but still exposes discard paths; hiding or locking alone is not a complete suspension contract.

Fresh identity/membership confirmation must bypass offline fallback: a cached loadProject success is not proof of current project access. `/me` currently exposes name/role without stable account ID or distinct local/bootstrap modes. Names and roles cannot establish continuity. A later coordinated change needs authoritative identity, network-only access refresh, synchronous suspension and callback invalidation, preserved unconfirmed-write state, and owner-scoped atomic text/base plus geometry/order/undo recovery. If stable ownership of local/bootstrap mode is undefined, quarantine transitions rather than assign legacy ownerless drafts automatically.

The existing workspace review remains the implementation boundary brief: `docs/evaluation/workspace-credential-review-2026-11-05.md`. These are source-derived risks and proposed tests, not executed failures or a claim the cache correction fixes the entire workspace.


## Evolving narrow patch review and composed test preparation

Root prepared the separate ProjectsPage/API correction while this audit was being recorded. The initial hash inventory therefore overlaps edits; historical findings above are anchored to the preserved originals under `.artifacts/experiments/workspace-intent-v1/before`, and the subsequent patch has a separate read-only hash snapshot in the audit JSON. This is not a claim of an immutable accepted source freeze.

The reviewed patch captures selection/key/generation/shared authority and lifetime, initializes authority inside captureIntent, fences stats/review/payload callbacks, versions search and CSV/layer intent, and pins CSV/export credentials. No high-confidence blocker was found in those guarded paths by source reading. Proposed final consistency controls cover captured createProject credentials, explicitly bounded raw-read dispatch identity, and openSurface lifetime. Root retains implementation and regression ownership.

The new independent test-only `web/src/app/api-client-cache-credentials.spec.ts` prepares 15 generated composed controls: each of listProjects/loadProject/line checks actual captured headers despite a before-auth storage change, subscription-time capture, captured anonymous dispatch, real successful HTTP followed by same-key offline fallback, and cross-owner fallback refusal before queued storage events. They use the real ApiClient, cache, auth interceptor and HTTP testing backend. No cache/crypto replacement, build or test execution occurred.


The latest source-only follow-up pins createProject, stats, reviewSummary and search as well as CSV/PDF/HTML, and guards openSurface after destruction. The earlier dispatch/lifetime consistency findings above are therefore historical, not remaining blockers in this reviewed snapshot. A further independent new file, `projects-request-credentials.spec.ts`, prepares21 real ApiClient/interceptor/backend controls across those seven methods: captured A, captured anonymous, and omitted-credential legacy behavior, with exact HTTP method/body/query/response type/output assertions. All36 independent composed tests remain unexecuted during quiet.

Root's separate controller tests now include a literal shared-ticket/key A→B→A transition, plus a real cached collection bootstrap test without an initial authority ticket. The proposed before/after experiment restores only the preserved original ProjectsPage while retaining candidate optional ApiClient signatures, so its baseline claims are limited to the controller/bootstrap tests and preserve legacy omitted-argument behavior. It is not a before-proof for the new API overload implementation or all36 composed cases. No new high-confidence blocker was found in the latest guarded-path source snapshot; final compile/test/browser acceptance remains open.
