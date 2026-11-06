# GEO-02: capture-bound existing-line edits

Instance B owns GeometryService's new scoped update method, CaptureGeometryController's
new PUT operation, GeometryEditor, its capture API-client method and generated tests.
Root owns JOB-01 files and the API client's unrelated jobSnapshot method. Coordinate
before shared edits. No fitting, corpus/labels, real providers or LM Studio actions.

Add PUT `.../projects/{p}/surfaces/{s}/captures/{c}/lines/{line}/geometry` using the
existing capture mutation boundary. Require current quoted display If-Match, exact
surface/line/frame identity, observed raw-source bounds and nonnull expected geometry.
Reject ambiguous duplicate line IDs before any global FindLine fallback. Preserve
the line, source image, region, text/review evidence and unrelated native fields.
Use authorized optimistic save and audit current capture/observed checksum/derivative
as separate edit evidence; never upgrade historical unavailable checksums.

Keep existing line-only service/API signatures for compatibility, documenting that
they do not gain the new source-binding guarantee. The Angular editor uses the new
route for move, resize and existing unplaced-line placement. Preserve the original
source derivative when a draft starts; do not silently rebind a draft after a same-
frame source/display change. Same-identity authorization refresh preserves drafts.
Changed source identity blocks saving/adoption until explicit discard/reconciliation;
unauthorized, stale and failed replies retain drafts. Navigation/pending-request
guards and raw coordinate mapping remain intact.

Freeze exact source/draft state semantics and add generated fail-before controls
before implementation. Verify wrong frame/surface/line, duplicate identities,
out-of-bounds geometry, stale expected geometry, known/unavailable checksum changes,
same-/changed-source refresh, auth races, cancellation and complete native-state
nonmutation on refusal. Reuse the shared mutation boundary without weakening its
current credential, frame alias or admission cleanup checks. Run focused generated
tests and Angular regression/build checks; no mixed ML/Pilot or fitting execution.
An actual owned-browser pointer save/reload plus stale-source retained move-draft
proof and root independent review are required before completion.
