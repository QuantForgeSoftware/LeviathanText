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

Image-only paths (IIIF or scanned pages without PAGE layout):

```bash
dotnet run --project src/LeviathanText.Cli -- --store pilot.db import iiif <projectId> manifest.json
dotnet run --project src/LeviathanText.Cli -- --store pilot.db layout detect <projectId> <surfaceId>
# remote captures are fetched once into <store-dir>/images with a sha256 record
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

## Data-ledger audit

```bash
leviathan validate manifests data/manifests --strict
```

Checks the declared schema, contract version, change log and the required per-source fields
(rights identifiers/evidence, attribution, allowed use, checksums, units evidence). It does
not verify rights — it verifies the ledger says what it must. Warnings flag `unverified`
license values.

## Review queue

`GET /api/v1/projects/{id}/review-queue[?surfaceId=…]` orders lines by stored L0 confidence
(empty outputs first, then lowest mean confidence, then no-confidence) — reproducible across
restarts. The workspace shows the queue after recognition and on reload.

## Layout models

```bash
# train a per-material layout package (page-level split; threshold tuned on dev pages)
leviathan layout train /abs/page-xml-dir --out ./packages/layout-codex --image-ext .png --epochs 100
# detect with the learned model (otherwise projection is used)
leviathan --store pilot.db layout detect <projectId> <surfaceId> --model ./packages/layout-codex
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

## Exports

- `export diplomatic` — plain text with a mandatory loss report on stderr.
- `export tei` / `export alto` / `export hocr` — bounded profiles (unsupported fields reported).
- `export training` — L1-only labels, restorations excluded, uncertainty spans listed.
- `export bundle` — zip with native JSON, training export, profile report and all per-surface
  formats + manifest.
- `export lines` — line-level CSV (ids, layer, annotation count, text) for external review.
- HTTP equivalents: `/projects/{id}/surfaces/{sid}/{diplomatic|tei|alto|hocr}`,
  `/projects/{id}/training-export`, `/projects/{id}/bundle`.

## Double review and adjudication

```bash
leviathan --store pilot.db review line <projectId> <lineId> --reviewer <id> --outcome accept|correct|dispute [--note …] [--text …]
leviathan --store pilot.db review summary <projectId> [--surface <surfaceId>]
leviathan --store pilot.db export adjudication <projectId> adjudication.csv
```

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

```bash
LeviathanText__Auth__ApiKey=… LeviathanText__Auth__ReadOnly=true \
  dotnet run --project src/LeviathanText.Api
```

Every `/api/v1` request must then send `Authorization: Bearer <key>` or `X-Api-Key`.
SSE clients that cannot set headers (browser `EventSource`) may use `?api_key=…`; URLs can
be logged, so use TLS and treat access logs accordingly. Read-only blocks non-GET methods.
**No accounts/roles/audit** — do not treat this as a complete authentication system. Without
a key the API logs a local-only warning. The current workspace client does not prompt for a
key: use it locally, or add a client-side key holder before exposing a UI.

## Known limits to communicate

- TIFF is not supported by the current imaging backend (clean `RejectedArchive`).
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
