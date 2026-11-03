# Shared editing: optimistic concurrency, merge and sessions (2026-11-02)

Status: implemented and tested. This narrows the shared-editing gap in
`docs/release/known-issues.md`; it is not a multi-tenant security claim or a replacement for
SSO. The pilot store remains SQLite with a single writer.

## 1. Optimistic concurrency

`NativeProject.Version` is store-assigned. `LocalStore.SaveProject`:

- inserts new documents at version 1;
- for a loaded document (`Version > 0`) updates only `WHERE id = ? AND version = ?`;
- on a stale version raises `revision-conflict` with `expectedVersion`/`currentVersion` and
  `RemedyHint.RebaseRevision`;
- returns the new version so services can carry it forward.

`IProjectStore.SaveProject` now returns `long`; application services return the saved
version with their result. Version 0 remains an unversioned/legacy write for new-project and
external callers; all normal read-modify-write paths carry a non-zero version.

Evidence: `LocalStoreTests.Stale_project_saves_are_rejected_and_versions_increment`.

## 2. Three-way line merge

`LeviathanText.Core.Text.ThreeWayMerge` diffs the base revision against the local and remote
texts at Unicode-scalar granularity (LCS, capped at 4,000,000 cells; prefix/suffix fallback
above the cap) and:

- accepts non-overlapping edits and identical edits;
- refuses every other overlap with the base range and both replacements;
- returns the base text on refusal so the editor can show something stable.

`TranscriptionService.AppendLineRevisionMerged` applies it when the caller's parent is no
longer current, retries on a concurrent save, and raises `revision-conflict` with
`conflictStart`, `conflictEnd`, `localText` and `remoteText` when it refuses.

The API accepts `merge: true` on `POST /lines/{id}/revisions`; the Angular editor always asks
for a merge and shows the overlap message without discarding the draft.

Evidence: `ThreeWayMergeTests` (7 scenarios), `TranscriptionServiceTests.Automatic_merge_*`,
and `ApiContractTests.Revision_merge_endpoint_combines_disjoint_edits_and_refuses_overlap`.

## 3. Server-issued sessions

`ISessionStore`/`LocalStore` add a store-backed `edit_sessions` table (schema v9). A session
binds the authenticated principal to a server node, an optional surface, creation/last-seen
and expiry times; it expires unless heartbeat and can be revoked by its owner (or an admin).
`SessionService` enforces a 1–480 minute TTL and a 20-session per-user cap.

Endpoints:

- `POST /api/v1/sessions` -> create
- `POST /api/v1/sessions/{id}/heartbeat` -> extend/refresh
- `DELETE /api/v1/sessions/{id}` -> revoke
- `GET /api/v1/sessions?surfaceId=…` -> list own sessions (admins see all)
- `GET /api/v1/surfaces/{id}/sessions` -> sessions bound to a surface

Presence accepts an optional `sessionId`; the editor identity then comes from the session's
authenticated principal, not a client-chosen string. The legacy `editorId` path remains for
local/advisory use. Two processes over one store see the same registry, so a multi-node
deployment does not need a separate broker for this bounded feature.

Evidence: `SessionServiceTests` (create/heartbeat/list/revoke, ownership, expiry) and
`ApiContractTests.Server_issued_sessions_scope_presence_and_can_be_revoked`.

## Limits retained

- Sessions are coordination records, not tokens: each request is still authenticated.
- No WebSocket/SignalR push; clients poll or heartbeat.
- No edit locks. Annotation writes now send the revision they were drawn against and fail
  `stale-annotation-anchor` if it was superseded; review decisions accept an optional
  `revisionId` and fail `revision-conflict` if it is stale. Geometry/order/retraction writes use
  the optimistic project version and fail `revision-conflict` when stale; tests cover concurrent
  geometry and retraction. They are not field-level merged.
- The surface view creates/heartbeats/revokes its own session and shows the serving node; a
  surface panel lists/revokes sessions for that surface, and the workspace collection page has
  an admin-visible session console (non-admins see their own). There is still no cross-project
  presence feed or push channel.
- SQLite remains a single-writer local store; PostgreSQL/object storage for shared
  deployments is still future work.
