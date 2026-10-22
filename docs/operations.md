# Operating LeviathanText (pilot)

Date: 2026-10-19. Local-first, single-user by default. This is a pilot operations guide,
not a production runbook.

## Components

| Component | Command | Notes |
| --- | --- | --- |
| CLI | `dotnet run --project src/LeviathanText.Cli -- --store pilot.db …` | All ingestion/export/training flows |
| API | `dotnet run --project src/LeviathanText.Api` | Binds localhost; contract JSON + SSE |
| Worker | `dotnet run --project src/LeviathanText.Worker -- --store pilot.db [--once]` | Executes durable jobs from the same store |
| Web | `cd web && npx ng serve --proxy-config proxy.conf.json` | http://localhost:4200 |

The API and worker must point at the **same** store database to share jobs.

## First run

```bash
dotnet run --project src/LeviathanText.Cli -- --store pilot.db project new "Pilot corpus"
# returns a project id (col-…)
dotnet run --project src/LeviathanText.Cli -- --store pilot.db import page <projectId> page.xml
# no PAGE? import the image alone and draw/detect lines
dotnet run --project src/LeviathanText.Cli -- --store pilot.db import image <projectId> photo.jpg --kind inscription-face
# find text later (literal, or --base to ignore marks/case)
dotnet run --project src/LeviathanText.Cli -- --store pilot.db search <projectId> νθρωπ --base
dotnet run --project src/LeviathanText.Cli -- --store pilot.db export diplomatic <projectId> <surfaceId>
```

## Image-only commands

```bash
# deterministic Otsu/binary conversion (never overwrites the source)
leviathan image binarize page.jpg page-bw.png
# bi-level bbox segmentation (color pages must be binarized first)
leviathan image segment page-bw.png lines.json --format json --direction horizontal-lr
# alias: segment then OCR in one command
leviathan image segment-ocr page-bw.png ocr.json --package ./packages/model-a --format json
# one line or a segmented page; PAGE/ALTO XML input uses the XML's own line geometry
leviathan image ocr page-bw.png ocr.txt --package ./packages/model-a --mode segment
leviathan image ocr page.xml ocr.json --package ./packages/model-a --input-format xml --linetype bbox
# bounded column-centroid dewarp (B/W or grayscale only)
leviathan image dewarp line.png line-dewarped.png --height 64
```

Image OCR writes L0-style text/records; it does not create L1 scholarly transcription. PAGE/ALTO
XML OCR accepts `auto`, `baselines` or `bbox` line types. `image segment` rejects color input;
`image dewarp` rejects true color and does not claim full center-normalizer parity.

Image-only paths (IIIF or scanned pages without PAGE layout):

```bash
dotnet run --project src/LeviathanText.Cli -- --store pilot.db import iiif <projectId> manifest.json
dotnet run --project src/LeviathanText.Cli -- --store pilot.db import pdf <projectId> document.pdf --out ./pdf-pages --dpi 200 --text
dotnet run --project src/LeviathanText.Cli -- --store pilot.db layout detect <projectId> <surfaceId>
# remote captures are fetched once into <store-dir>/images with a sha256 record
# PDF pages become PNG captures; --text adds coordinate-bearing L2 reference lines (never L1)
```

Batch flows (durable jobs):

```bash
# Bulk import (API); then run one worker iteration (or keep the worker running)
curl -X POST http://127.0.0.1:5000/api/v1/jobs -H 'Content-Type: application/json' -d \
  '{"type":"import.dir","ownerId":"ops","projectId":"<col-id>","sourceDirectory":"/abs/pages","idempotencyKey":"import-1"}'
# then detect layout on every surface that has none
curl -X POST http://127.0.0.1:5000/api/v1/jobs -H 'Content-Type: application/json' -d \
  '{"type":"layout.project","ownerId":"ops","projectId":"<col-id>","idempotencyKey":"layout-1"}'
dotnet run --project src/LeviathanText.Worker -- --store pilot.db --once

# Recognition of a whole project needs a verified model package
dotnet run --project src/LeviathanText.Cli -- --store pilot.db train lines /abs/page-xml-dir --out ./packages/model-a --epochs 120
curl -X POST http://127.0.0.1:5000/api/v1/jobs -H 'Content-Type: application/json' -d \
  '{"type":"recognize.project","ownerId":"ops","projectId":"<col-id>","modelPackagePath":"/abs/packages/model-a","idempotencyKey":"rec-1"}'
```

Check jobs with `leviathan jobs list` or `GET /api/v1/jobs`; watch with
`GET /api/v1/jobs/{id}/events` (SSE; events are advisory, the job resource is authoritative).

Job creation accepts `priority` (0–100; higher claimed first, ties by creation time). Authenticated
callers own the job regardless of a client-supplied `ownerId`, so quota/audit cannot be evaded by
changing that field. Set
`LeviathanText:Jobs:MaxActivePerOwner` to cap pending+running jobs per owner; `0` is
unlimited. Idempotent duplicate creation still returns the existing job.

## Data-ledger audit

```bash
leviathan validate manifests data/manifests --strict
```

Checks the declared schema, contract version, change log and the required per-source fields
(rights identifiers/evidence, attribution, allowed use, checksums, units evidence). It does
not verify rights — it verifies the ledger says what it must. Warnings flag `unverified`
license values.

## Experiment runs

Training commands record a comparable run in the store:

```bash
leviathan --store pilot.db runs list [--kind recognition|layout] [--limit N]
leviathan --store pilot.db runs show <runId>
leviathan --store pilot.db runs compare bestDevCer            # ascending; --higher for metrics like devF1
leviathan --store pilot.db runs regress cer <baselineRunId> <candidateRunId> --max-regression 0.01
```

For a profile-specific gate, write criteria JSON and evaluate the whole suite:

```json
[
  {"metric":"cer","maxRegression":0.01},
  {"metric":"markErrorRate","maxRegression":0.005},
  {"metric":"devF1","maxRegression":0.01,"higherIsBetter":true}
]
```

```bash
leviathan --store pilot.db runs regress-suite <baselineRunId> <candidateRunId> --criteria criteria.json
```

Criteria may carry `"profile"` to apply only when the run's `profile` config matches. Missing
metrics fail; the command exits nonzero if any criterion regresses.

Each run stores kind/name/created/by, the **dataset digest** (split hash or layout data digest),
config map, metrics map, learning-curve series and artifact paths. The workspace lists runs and
renders the curve; `GET /runs` / `GET /runs/{id}` expose the same. This is an experiment index,
not an acceptance claim: runs retain their own labels/caveats.

## Evaluation and error drilldown

```bash
leviathan --store pilot.db evaluate lines /abs/page-xml-dir --package ./packages/model-a \
  --split dev --out dev-errors.csv --json dev-errors.json
```

This scores a packaged model on the **development or training** split only (the locked test
split is deliberately unavailable here), writes per-line CER/base/mark errors and top
confusions (CSV/JSON plus a Markdown report), and records an `evaluation` run with the split
digest. Empty hypotheses count as full-line deletions. It is an analysis aid, not an acceptance
claim: labels may not be specialist-adjudicated.

To apply frozen thresholds, pass an acceptance file; the command exits nonzero on failure:

```json
{"maxCer": 0.5, "maxMarkErrorRate": 0.1, "minLines": 100}
```

`--acceptance criteria.json` records PASS/FAIL in the JSON/Markdown report and run notes. The
tool never invents threshold values; the project owner/Greek reviewer must freeze them.

## Active-learning queue

```bash
leviathan --store pilot.db review active <projectId> [--limit 100] [--out queue.csv]
```

Ranks lines by explicit reasons: missing/empty L0, low mean confidence, never reviewed and
reviewer disagreement. Scores are a transparent review heuristic, not calibrated risk; the CSV
contains the reasons for audit.

## Review queue

`GET /api/v1/projects/{id}/review-queue[?surfaceId=…]` orders lines by stored L0 confidence
(empty outputs first, then lowest mean confidence, then no-confidence) — reproducible across
restarts. The workspace shows the queue after recognition and on reload.

## Layout models

```bash
# train a per-material layout package (page-level split; threshold tuned on dev pages)
leviathan layout train /abs/page-xml-dir --out ./packages/layout-codex --image-ext .png --epochs 100   # augmentation on; --no-augment to disable
# detect with the learned model (otherwise projection is used)
leviathan --store pilot.db layout detect <projectId> <surfaceId> --model ./packages/layout-codex
leviathan --store pilot.db layout inspect ./packages/layout-codex   # metadata, digests, threshold
```

The API selects learned models per surface kind from configuration:
`LeviathanText:Layout:Models:codex-page=/abs/packages/layout-codex` (and optionally `*` as a
default). Unknown kinds fall back to projection. Learned layout is opt-in and single-column
only; the P01-trained variant is measured but not enabled (per-page failures). Worker jobs
read the same kind-named directories from `LEVIATHANTEXT_LAYOUT_MODEL_DIR` (default
`<store-dir>/layout-models`).

## Models

`LeviathanText:Models:Directory` (default `~/.leviathantext/models`) is scanned by
`GET /api/v1/models`; clients name a package (never a path). `POST
/api/v1/surfaces/{id}/recognize {"model":"<name>"}` loads the verified package, persists L0
revisions with provenance and returns line/skip counts. Long batch work belongs in jobs.

Packages are data directories with a `run-manifest.json`, checkpoint metadata, weights and a
`model-card.md`. The card records task/format, architecture, tokenizer/preprocessing profile,
directions/scripts, runtime, provenance, license, hashes and pilot metrics. A package with an
executable payload, oversized artifact, broken label hash, future package format or
mismatched weights is rejected. A card's metrics are not an acceptance threshold.

## AI providers

Providers are configured under `LeviathanText:AiProviders:<name>` with
`ProviderId`, `Host` (`Local`/`Hosted`), `Protocol` (`OpenAiChatCompletions`/`OllamaChat`),
`Endpoint`, `ModelId`, optional `CredentialEnvironmentVariable`, timeout/token/response caps
and optional spend caps. Credentials are read from the named environment variable at call
time, never stored in proposal records. No hosted-provider accuracy or availability claim is
made; use the local scripted/Ollama smoke paths for verification.

## Exports

- `export diplomatic` — plain text with a mandatory loss report on stderr.
- `export tei` / `export alto` / `export hocr` / `export abbyy` — bounded profiles (unsupported fields reported).
- Legacy import also accepts `abbyy` FineReader XML alongside ALTO and hOCR.
- `export training` — L1-only labels, restorations excluded, uncertainty spans listed.
- `export bundle` — zip with native JSON, training export, profile report and all per-surface
  formats + manifest.
- `export lines` — line-level CSV (ids, layer, annotation count, text) for external review.
- HTTP equivalents: `/projects/{id}/surfaces/{sid}/{diplomatic|tei|alto|hocr}`,
  `/projects/{id}/training-export`, `/projects/{id}/bundle`.

## Source excerpts

`GET /api/v1/lines/{lineId}/crop` returns a bounded PNG crop for the line's own geometry.
The workspace shows it above the editor as a task-specific source excerpt; the full capture
link remains available. The crop is evidence review, not a new image or an L1 claim.

## Direction and script

Direction/script are declared metadata, not inferred. `PUT /lines/{id}/orientation` and
`PUT /regions/{id}/orientation` accept `{direction, script}` where `direction` is `ltr`,
`rtl`, `verticalTopToBottom`, `verticalBottomToTop` or null (inherit/undeclared); `script` is a
short ISO 15924 code such as `Grek`. The editor exposes line-level controls and the surface view
applies logical direction/vertical display. PAGE, hOCR and TEI imports/exports preserve
direction; native JSON preserves both. ALTO does not yet map direction, and recognition
remains LTR.

## Single-user local install

```bash
scripts/install-local.sh                 # ~/.leviathantext/app
scripts/install-local.sh /opt/leviathan --force
```

The installer publishes the Release CLI/API/worker and Angular assets into one prefix, then
creates `bin/leviathan`, `bin/leviathan-api` and `bin/leviathan-worker` wrappers. The API wrapper
serves the workspace at `/ui`; the CLI/worker use `$HOME/.leviathantext/store.db` unless
`LEVIATHAN_STORE` overrides it. Existing installs are moved to a timestamped backup when
`--force` is used. This is a local single-user install; it does not configure SSO or a shared
tenant.

## Packaged workspace hosting

A packaged release can serve the immutable Angular assets from the API instead of requiring a
second static server:

```bash
LeviathanText__WebRoot=/abs/release/web LeviathanText__WebPath=/ui \
  /abs/release/api/LeviathanText.Api --urls http://127.0.0.1:5000
```

The workspace is then at `/ui`; `/` remains the service/contract endpoint and `/api/v1` remains
the API. `WebRoot` is opt-in; no filesystem directory is exposed without it. The SPA fallback
returns `index.html` for non-file client routes and 404 for missing assets.

## Offline use

The workspace caches successful reads (project list/payloads, line payloads, diplomatic exports)
in browser localStorage and shows an **offline banner** when a read falls back to cache. Editing
still requires the server: drafts stay local and mutations are never replayed automatically.
Caches are cleared when the API key changes. Do not rely on cached data for decisions after
reconnecting.

## Bulk annotation import

```bash
leviathan --store pilot.db import annotations <projectId> annotations.csv
```

Header needs `line_id,kind,start,end`; optional `gap_extent`, `alternative_text`, `hand_label`,
`description` are honored. Rows pass through the normal annotation validation (ranges, lost-gap
extents, alternative text) and invalid rows are reported and skipped. The exporter's CSV can be
edited externally and re-imported; annotation ids are always minted by the server.

## Bulk line-text import

```bash
leviathan --store pilot.db import line-text <projectId> lines.csv [--layer L1Diplomatic|L2Editorial] [--author id]
```

CSV needs `line_id` and `text` columns (quoted commas/quotes/newlines supported). Each row is
applied through the normal revision service (parent chain, provenance, conflict rules), so an
external review/adjudication sheet can be merged without bypassing history. Unknown ids and
empty rows are reported and skipped, never silently dropped. CSV remains lossy; the project
document is authoritative.

## Inscription bootstrap (EpiDoc)

```bash
leviathan --store pilot.db import epidoc <projectId> <record.xml> <photo.jpg>
```

For a batch of records with locally fetched images:
`leviathan import epidoc-dir <projectId> <record-dir> <image-dir> [--limit N]`. Records whose
declared/stem image is absent are skipped and reported (never guessed). The ISicily-style
published edition is split at `<lb/>` and imported as **L2 editorial reference**, never as L1. Lines start without geometry; the editor lists them under
"Unplaced text lines", where a human selects a line and draws its box. Notes/gap markup is
reported and not presented as visible evidence. `GET /surfaces/{id}/unplaced-lines` exposes
the same list.

## Reviewer packets

For known-transcription corpora, generate a self-contained review packet:

```bash
leviathan --store review.db review packet <projectId> .artifacts/review/p01 --limit 50
```

Prepared known-transcription packets:
`.artifacts/review/p01/packet-50/`, `.artifacts/review/m02a/packet-50/` and
`.artifacts/review/m01/packet-50/`. Page imports resolve images next to the XML or in a sibling
`images/`/`image/` directory, and external IDs with spaces/parentheses are sanitized rather than
dropped.

`index.html` shows each line crop, current L1, and L2 reference; the reviewer marks
agree/correct/unclear/not-visible, edits corrected text, and downloads `decisions.csv`. The page
does not mutate the project. Apply returned decisions through the review ledger:

```bash
leviathan --store review.db review preview <projectId> decisions.csv
leviathan --store review.db review apply <projectId> decisions.csv --reviewer <id>
```

`preview` validates ids/decisions and reports counts without writing; `apply` performs the ledger
writes.

`agree` records accept, `correct` creates a new L1 correction revision, and `unclear` /
`not-visible` record disputes with notes. Unknown/uncorrected rows are reported and skipped.
`review summary` now reports accepted/corrected/disputed counts alongside adjudication needs.

## Double review and adjudication

```bash
leviathan --store pilot.db review line <projectId> <lineId> --reviewer <id> --outcome accept|correct|dispute [--note …] [--text …]
leviathan --store pilot.db review summary <projectId> [--surface <surfaceId>]
leviathan --store pilot.db export adjudication <projectId> adjudication.csv
```

Annotations can be retracted from the editor (or `POST /api/v1/annotations/{id}/retract`); the
original record stays in the ledger, active views and training exports exclude it, and the
annotation CSV marks it as retracted.

Decisions are append-only and anchored to the exact reviewed revision. A `correct` decision
creates a new L1 revision (the correction) and records both revision ids. Two reviewers with
different outcomes make the line an adjudication candidate; the tooling never forces agreement
and does not adjudicate automatically. The line editor exposes the same actions.

## Backup and restore

```bash
leviathan --store pilot.db backup ./snapshots/2026-10-19
leviathan --store pilot.db restore ./snapshots/2026-10-19 --into pilot-restored.db [--force]
```

Deleting a project is destructive and requires an explicit flag:
`leviathan --store pilot.db project delete <projectId> --force`, or
`DELETE /api/v1/projects/{id}`. Back up first.

Backup uses `VACUUM INTO` (consistent snapshot), copies the image cache and writes a
checksum manifest. Restore verifies the database **and every image** before writing; a
corrupt set leaves no partial target.

## Shared deployment (opt-in)

LeviathanText can run with local accounts and role-based authorization:

```bash
# bootstrap an admin (prints the key once; keys are stored only as SHA-256 hashes)
leviathan --store pilot.db user add alice --role admin
leviathan --store pilot.db user list
leviathan --store pilot.db user rotate <userId>
leviathan --store pilot.db user disable <userId>
```

Start the API with a bootstrap admin key (optional once accounts exist):

```bash
LeviathanText__Auth__ApiKey=… dotnet run --project src/LeviathanText.Api
```

Requests send `Authorization: Bearer <key>`, `X-Api-Key: <key>`, or (for SSE) `?api_key=…`.
Roles: **reader** (GET only), **annotator** (import/edit/jobs/exports), **reviewer**
(+ review decisions), **admin** (+ user management and project deletion). The configured key is
an admin bootstrap credential and never replaces account management. `Auth:ReadOnly=true`
blocks all mutations regardless of role. Without a bootstrap key and without accounts the API
runs local-only and logs a warning.

Admins can also manage accounts over HTTP (`GET/POST /api/v1/users`,
`POST /api/v1/users/{id}/disable|rotate`) or from the workspace **Accounts (admin)** panel;
new/rotated keys are shown once. The **Auth audit (admin)** panel shows recent authenticated/denied/unauthorized requests.

**Per-project membership (opt-in):** set `LeviathanText:Auth:ProjectMembership=true` to scope
non-admin access to projects they belong to. A project created through the API automatically
adds its creator; projects created by the CLI start with no members until an admin adds them.
Admins manage membership with `GET/POST /projects/{id}/members` and
`DELETE /projects/{id}/members/{userId}`, from the workspace **Project members** panel, or:

```bash
leviathan --store pilot.db project member list <projectId>
leviathan --store pilot.db project member add <projectId> <userId>
leviathan --store pilot.db project member remove <projectId> <userId>
```

Membership covers project routes and indirect entity routes (`/lines`, `/surfaces`, `/regions`,
`/annotations`); admins bypass it.

**Invitation tokens:** admins can issue one-time, expiring project invitations:

```bash
curl -X POST http://127.0.0.1:5000/api/v1/projects/<projectId>/invites \
  -H "X-Api-Key: $ADMIN_KEY" -H 'Content-Type: application/json' \
  -d '{"role":"annotator","expiresInHours":24}'
# returns a one-time token; the recipient accepts it
curl -X POST http://127.0.0.1:5000/api/v1/invites/accept -H 'Content-Type: application/json' \
  -d '{"token":"<token>","name":"invited-name"}'
```

Acceptance creates a normal account, returns its API key once, and adds the account to the
project. Tokens are stored as SHA-256 hashes, expire after 1–168 hours, are single-use, and only
admins can issue them. Admin invitations are refused. The workspace provides **Project members →
Create invitation** plus a public **Have an invitation?** acceptance panel. Successful and
rejected acceptances are written to the auth audit (`invite.accepted` / `invite.rejected`).

**Presence (advisory, shared store):** the surface view heartbeats
`POST /surfaces/{id}/presence {editorId}` every 20 seconds and displays active editors from
`GET /surfaces/{id}/presence`; `DELETE /surfaces/{id}/presence/{editorId}` clears it. Entries live
in the shared SQLite store (schema v6) and expire after 60 seconds, so processes sharing the store
see the same state. Presence is still advisory: it is not a lock, not a distributed session
registry, and never authorizes an edit. Entity→project resolution scans projects, which is acceptable
at pilot scale but not a high-throughput design. Authentication/authorization events are recorded:
`leviathan --store pilot.db audit list` or `GET /api/v1/audit?limit=` (admin only; mutations and
denials, never key material). The Angular workspace has an **API key panel**: paste
a key there and every request sends `X-Api-Key` (SSE appends `?api_key=`). Reload data after
changing keys. Opt-in per-project membership, invitation panels and advisory presence exist;
distributed session state, invitation email delivery and concurrent-edit merge are not
implemented.

## Known limits to communicate

- TIFF is not supported by the current imaging backend (clean `RejectedArchive`). PDF pages can
  be rasterized into captures (bounded DPI/page count/size). Optional PDF text is L2 reference;
  clean two-column gutter order is inferred mechanically, but complex/rotated layouts are not.
- Region/line direction and script metadata is stored and displayed; BiDi layout and vertical
  recognition are not implemented (recognition remains LTR).
- Automatic layout handles single-column pages; multi-column/curved/rotated pages are unsupported.
- Geometry edits are stored directly (no server-side geometry revision history); the editor
  has undo, and backups are the recovery path.
- Recognition L0 is not an edition; only reviewed L1/L2 is transcription evidence.
- The model packages shipped by `train lines` are pilot models trained on PAGE-derived
  labels, not specialist-adjudicated ground truth. `--profile-strict` excludes labels with
  scalars outside the Greek profile (markup leakage) and reports how many.
- Imported PAGE/ALTO/hOCR ids are namespaced per import (`ln-ln1-1a2b3c4d`), so globally
  addressed endpoints cannot act on another project's line with the same document-local id.
- `leviathan validate text <projectId>` (or `GET /projects/{id}/profile-report`) flags Latin
  letters, unknown scalars and unpaired combining marks without rewriting text.
- Lines can be split at a y position or merged with the next line **only while untranscribed**;
  once a line carries diplomatic text, both operations are refused (409) rather than detaching
  text from geometry.
- Annotation ranges are checked against the anchored revision: spans beyond the text, empty
  spans (except lost markers, which may be a position) and estimated gaps without a count are
  rejected. A lost marker without an extent defaults to `Unknown`.
