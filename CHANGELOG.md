# Changelog

All dates are UTC-ish session dates. No stable release exists; this is a pilot record. Contract
set: C0-0.4-draft.

## Unreleased — 2026-10-19 / 2026-10-20 work block

### Changed

- **Plan split:** the plan narrative, requirements, source register, architecture decisions,
  delivery briefs, gates and estimates now live in `project-plan.md`; `todo.md` contains only
  open/in-progress checklist items. Section numbering is preserved for references.

### Added

- **Admin audit panel:** recent authenticated/denied/unauthorized events are visible in the workspace.
- **Admin accounts panel:** workspace UI creates accounts, shows new/rotated keys once, and disables accounts.
- **Source evidence link:** the line editor opens the selected capture in a new tab; task-specific evidence excerpts remain open.
- **Correction-sequence capture:** editor records `CorrectionSequence` and `DifferentHand` annotations with a hand/sequence label; replay/ordering visualization remains open.
- **Workspace deep links and surface navigation:** project/surface/line query parameters restore and update workspace state; previous/next surface controls move through the collection.
- **Member management UI:** admin-only project panel loads accounts/members and grants/revokes opt-in project access; no invitation tokens or presence.
- **Pilot profile:** reproducible single-page startup/import/layout/export/backup timing + peak RSS harness (`scripts/profile-pilot.sh`).
- **Migration rehearsal:** old-binary → current-store migration with explicit rollback refusal (`scripts/migration-rehearsal.sh`) PASS.
- **ABBYY XML:** bounded FineReader 10 export/import and CLI/API/bundle integration.
- **Local installer:** `scripts/install-local.sh` publishes the stack and creates CLI/API/worker wrappers; single-user/local only.
- **Reviewer preview:** decisions CSVs can be validated/count-checked without applying.
- **Reviewer summary:** accepted/corrected counts are available in review summaries and the workspace.
- **Known-transcription intake:** sibling `images/` resolution and external-ID sanitization make M01/M02a imports reliable; reviewer packets prepared for P01/M02a/M01.
- **Reviewer packets:** static HTML packets with line crops, current L1/L2 text, downloadable decisions CSV and `review apply` through the review ledger.
- **Revision-conflict prompt:** a 409 now surfaces a reload/rebase action in the editor while keeping the draft text.
- **Invite audit:** successful and rejected acceptances are recorded in the auth audit.
- **Invitation tokens/UI:** admin-issued one-time project invites with hashed storage, expiry and membership-on-accept; member panel creates invites and a public panel accepts them.
- **Job ownership binding:** authenticated job creation ignores client-supplied `ownerId` for ownership/quota.
- **Job priority/quota:** jobs persist priority (0–100) and creation time; workers claim highest priority first, and an optional per-owner active-job quota is enforced on creation.
- **Presence heartbeat:** per-surface editor presence with a 60-second TTL and workspace display; advisory, stored in the shared SQLite store (schema v6).
- **Acceptance gate:** `evaluate lines --acceptance criteria.json` applies caller-supplied thresholds and exits nonzero on failure.
- **Run regression gate:** `runs regress <metric> <baseline> <candidate>` compares stored metrics with an absolute tolerance and exits nonzero on regression.
- **Evaluation error drilldown:** `evaluate lines` scores a packaged model on dev/train,
  writes per-line CER/base/mark/confusion CSV + JSON + Markdown report and records an `evaluation` run with a
  split digest; locked test labels remain unavailable to routine evaluation.
- **Banded line extraction:** large JPEG pages (>25 MP) decode horizontal bands per line instead of a full frame; PNG/TIFF keep the full-frame fallback.
- **PDF line segmentation:** large horizontal gaps in PDF text are separated into candidate L2 lines; reading order/columns remain uninferred.
- **PDF text reference import:** optional `--text` extracts coordinate-bearing PDF text as L2 reference lines (never L1), with page-coordinate mapping and extraction caveats.
- **PDF page ingestion (Phase 4 partial):** bounded PDFtoImage/PDFium rendering to PNG
  captures (DPI/page/size limits; one surface per page; no text/layout inference), exposed by
  CLI `import pdf` and `POST /projects/{id}/import-pdf`.
- **Packaged workspace hosting:** optional `LeviathanText:WebRoot` serves the Angular build at
  `/ui` with SPA fallback; `/` and `/api/v1` remain separate.
- **Release rehearsal:** `scripts/package-release.sh` (target-specific framework-dependent
  package + SHA-256 manifest) and `scripts/backup-restore-rehearsal.sh`; v2/v3/v4→v5 migration
  tests.
- **Direction/script metadata (Phase 4 partial):** per-region and per-line logical direction
  (`ltr`, `rtl`, vertical top-to-bottom/bottom-to-top) and script code; PAGE
  `readingDirection`/`primaryScript`, hOCR `dir`/`ocr_vertical` and bounded TEI `rend`
  round-trips; API orientation endpoints; editor controls and direction-aware surface rendering.
  Recognition remains LTR.
- **Trainable layout (`patch-row-cnn-v2`)**: aspect-preserving preprocessing, line-core targets,
  dev-tuned threshold/expansion, augmentation, per-material opt-in (CLI `layout train/detect/
  inspect`, API `Layout:Models:<kind>`, worker `LEVIATHANTEXT_LAYOUT_MODEL_DIR/<kind>`), SHA-256
  weights digest, training-data digest and model card.
- **Double review / adjudication**: append-only accept/correct/dispute decisions anchored to
  revisions; correction revisions; project review dashboard; adjudication CSV; CLI/API/editor.
- **Annotation retraction**: append-only retraction ledger; active views/training exports exclude
  retracted annotations; CSV marks retractions.
- **Accounts and roles (schema v3)**: local users with hashed keys, reader/annotator/reviewer/
  admin, rotation/disable, admin API/CLI, auth audit (`audit_audit` table), workspace API-key
  panel + interceptor and `/me` identity.
- **Inscription bootstrap**: EpiDoc edition split at `<lb/>` imported as L2 reference, manual
  unplaced-line placement, batch import by record directory, inventory scanner with manifest
  count reconciliation.
- **CSV line-text import**: `import line-text` / HTTP endpoint / workspace upload; rows go through
  the normal revision service.
- **Offline read cache** for projects/lines/diplomatic exports with stale-data banner; cache
  cleared on credential change.
- **Release/ops**: fresh-install smoke script, architecture overview, parity matrix, upgrade/
  migration/rollback guidance, adjudication runbook, known-issues consolidation.
- Per-grapheme confidence persisted on L0 revisions; editor confidence rendering; persisted
  review queue with worst-span reporting.
- **Experiment registry (schema v5):** comparable recognition/layout runs with dataset digests,
  configs, metrics, learning curves and artifact paths; CLI/API/workspace panel.
- Local/codex layout model card, data digest and `layout inspect`.
- Bulk PAGE/EpiDoc import failure isolation; annotation/line CSVs in export bundles; review
  summary/adjudication artifacts in bundles.

### Changed

- **Layout/metric correctness**: layout comparison and page-CER matching are now one-to-one
  greedy by IoU. All layout and page reports were regenerated; earlier non-one-to-one numbers
  are superseded.
- Projection layout restricted to the bright page area (precision improvement); density
  post-filter retained; merge rule rejected after out-of-domain regression.
- PAGE/ALTO/hOCR imports namespace document-local ids per capture (cross-project collision fix).
- Test suite no longer re-scores the spent P01 locked split; the evaluator is opt-in
  (`LEVIATHANTEXT_RUN_LOCKED=1`).
- Bundles include review/QA artifacts; schema v2→v3 migration is automatic, newer schemas are
  refused.

### Fixed

- A non-one-to-one layout matcher that could report precision > 1 and inflate detection recall.
- Cross-project line id collisions in globally addressed endpoints.
- Flaky confidence assertions for empty decodes.
- Missing `provideHttpClient` in the Angular app config (workspace would fail at runtime).
- Jobs runner only reading its in-process cache (API-enqueued jobs were never picked up).
- Annotation ranges/payload validation (empty spans, estimated-gap counts, alternatives).
- TIFF decode crashes → clean `RejectedArchive`; unsupported formats reported.

### Security

- Hashed API keys; role-based authorization; bootstrap admin key; auth audit; SSE query-token
  support; caches cleared on key change.
- No SSO, refresh tokens, per-object workspace membership or structured session policy yet.

### Tests / evidence

- Test counts are recorded in [docs/STATUS.md](docs/STATUS.md) and the board in
  [project-plan.md](project-plan.md) and the actionable board in [todo.md](todo.md); evaluation
  records are indexed in [docs/evaluation/README.md](docs/evaluation/README.md).
- Smokes: `scripts/pilot-demo.sh`, `scripts/fresh-install-smoke.sh`.
