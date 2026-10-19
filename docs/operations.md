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
dotnet run --project src/LeviathanText.Worker -- --store pilot.db --once

# Recognition of a whole project needs a verified model package
dotnet run --project src/LeviathanText.Cli -- --store pilot.db train lines /abs/page-xml-dir --out ./packages/model-a --epochs 120
curl -X POST http://127.0.0.1:5000/api/v1/jobs -H 'Content-Type: application/json' -d \
  '{"type":"recognize.project","ownerId":"ops","projectId":"<col-id>","modelPackagePath":"/abs/packages/model-a","idempotencyKey":"rec-1"}'
```

Check jobs with `leviathan jobs list` or `GET /api/v1/jobs`; watch with
`GET /api/v1/jobs/{id}/events` (SSE; events are advisory, the job resource is authoritative).

## Exports

- `export diplomatic` — plain text with a mandatory loss report on stderr.
- `export tei` / `export alto` / `export hocr` — bounded profiles (unsupported fields reported).
- `export training` — L1-only labels, restorations excluded, uncertainty spans listed.
- `export bundle` — zip with native JSON, training export and all per-surface formats + manifest.
- HTTP equivalents: `/projects/{id}/surfaces/{sid}/{diplomatic|tei|alto|hocr}`,
  `/projects/{id}/training-export`, `/projects/{id}/bundle`.

## Backup and restore

```bash
leviathan --store pilot.db backup ./snapshots/2026-10-19
leviathan --store pilot.db restore ./snapshots/2026-10-19 --into pilot-restored.db [--force]
```

Backup uses `VACUUM INTO` (consistent snapshot), copies the image cache and writes a
checksum manifest. Restore verifies the database **and every image** before writing; a
corrupt set leaves no partial target.

## Shared deployment (opt-in)

```bash
LeviathanText__Auth__ApiKey=… LeviathanText__Auth__ReadOnly=true \
  dotnet run --project src/LeviathanText.Api
```

Every `/api/v1` request must then send `Authorization: Bearer <key>` or `X-Api-Key`.
Read-only blocks non-GET methods. **No accounts/roles/audit** — do not treat this as a
complete authentication system. Without a key the API logs a local-only warning.

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
