<!-- Copyright (c) 2026 John Baima. SPDX-License-Identifier: Apache-2.0 -->
# LeviathanText

An independent Greek recognition and scholarly transcription platform — focused first on **Koine and Classical Greek manuscripts, papyri, and inscriptions**. C# backend, CLI, and training; TypeScript/Angular browser workspace. It is not a fork of Kraken and has no Kraken or Python runtime dependency. Interoperability is defined through documented public file formats (PAGE XML, TEI P5/EpiDoc, IIIF) only.

## License and contributions

Original project software and accompanying project documentation are licensed under the [Apache License, Version 2.0](LICENSE), unless a file or directory identifies separate terms. See [NOTICE](NOTICE) for attribution and scope. Third-party dependencies and assets retain their own licenses. Dataset images, transcriptions, annotations and model weights require separately recorded rights; this software license does not grant blanket rights to them. User documents and outputs are not relicensed by using LeviathanText. Copyright notices, including historical “all rights reserved” wording, do not override the Apache grant for covered original project material.

John Baima leads the official project under [GOVERNANCE.md](GOVERNANCE.md). Code contributions require reproducible evidence of a bug correction, performance improvement or broader text support, with regression checks protecting existing functions, accuracy and performance. Read [CONTRIBUTING.md](CONTRIBUTING.md), certify the [DCO](DCO) with commit sign-offs, and use the [pull-request template](.github/pull_request_template.md). AI-assisted work is welcome under the same requirements. These contribution rules govern the official project and add no restrictions to the software license or independent forks.

## Architecture

| Package | Responsibility |
| --- | --- |
| `src/LeviathanText.Contracts` | Shared contract vocabulary: identifiers, scalar ranges, text layers/revisions, scholarly annotations, closed error codes, stage envelopes, confidence semantics (implements contract set `C0-0.4-draft`) |
| `src/LeviathanText.Core` | Domain behavior: Unicode scalar/grapheme machinery, NFC normalization with annotation offset mapping, revision chains with conflict semantics, coordinate-frame registration, stage validity, idempotent job protocol |
| `src/LeviathanText.Formats` | Native JSON persistence, diplomatic text export with mandatory loss reports, bounded PAGE XML (2013 + 2019), bounded TEI/EpiDoc round trips, bounded IIIF Presentation 3 import |
| `src/LeviathanText.Imaging` | SkiaSharp decode/crop/deskew with recorded transforms and coordinate mapping |
| `src/LeviathanText.Recognition` | CRNN + CTC line recognition (label set, greedy decoder, per-grapheme confidence) |
| `src/LeviathanText.Training` | Variable-width CTC trainer: train / best-checkpoint / resume / reload, synthetic dataset, PAGE-image corpus loader |
| `src/LeviathanText.Evaluation` | Canonical-equivalent scalar/grapheme CER, separate mark-error attribution, uncertainty coverage, page-level scoring (blank-region insertions, missed lines) |
| `src/LeviathanText.AI` | Provider-neutral proposals: schema-validating adapter, scripted test double, acceptance into L2 with provenance (L1 untouched) |
| `src/LeviathanText.Application` | Use cases: project service, interchange service, recognition pipeline |
| `src/LeviathanText.Infrastructure` | SQLite local store (schema-versioned) for projects and durable jobs |
| `src/LeviathanText.Api` | Versioned REST under `/api/v1` + server-sent events for job progress |
| `src/LeviathanText.Worker` | Independently launchable durable job runner (leases, heartbeats, bounded retries) |
| `src/LeviathanText.Cli` | Local CLI — no web server required |
| `web/` | Angular 20 workspace: collection list, surface view (diplomatic + loss report), polytonic rendering checks |

## Build and verify

```bash
dotnet restore
dotnet build LeviathanText.slnx -c Release   # 0 errors, 0 warnings expected
dotnet test LeviathanText.slnx               # all suites green
```

The dataset-backed pilot tests are skipped automatically when the artifact store is absent; nothing else in the suite depends on data.

## CLI (local, offline)

```bash
dotnet run --project src/LeviathanText.Cli -- --store pilot.db project new "Pilot corpus"
dotnet run --project src/LeviathanText.Cli -- --store pilot.db import page <projectId> page.xml
dotnet run --project src/LeviathanText.Cli -- --store pilot.db import iiif <projectId> manifest.json
dotnet run --project src/LeviathanText.Cli -- --store pilot.db layout detect <projectId> <surfaceId>   # auto layout (never overwrites)
dotnet run --project src/LeviathanText.Cli -- --store pilot.db export diplomatic <projectId> <surfaceId>   # loss report on stderr
dotnet run --project src/LeviathanText.Cli -- --store pilot.db export tei <projectId> <surfaceId>
dotnet run --project src/LeviathanText.Cli -- --store pilot.db export training <projectId>   # L1-only labels + annotation status
dotnet run --project src/LeviathanText.Cli -- --store pilot.db recognize surface <projectId> <surfaceId> --package <model-package-dir>
```

Remote captures (IIIF) are fetched once into a checksummed local cache when an analysis stage first needs them, so later runs work offline.

## End-to-end smoke

```bash
scripts/pilot-demo.sh /tmp/lt-demo   # import → train → recognize → exports → backup
```

## HTTP API

```bash
dotnet run --project src/LeviathanText.Api
# projects: GET/POST /api/v1/projects, GET /projects/{id}
# ingestion: POST /projects/{id}/import-page | import-iiif | import-image | import-pdf | import-legacy (ALTO/hOCR + image)
# search/QA: GET /projects/{id}/search?q=&mode=literal|base; GET /projects/{id}/profile-report
# review: GET /projects/{id}/review-queue[?surfaceId=] (persisted L0 confidence order)
# layout: POST /surfaces/{id}/detect-layout; PUT /surfaces/{id}/reading-order; PUT /regions/{id}/line-order
# transcription: GET/POST /lines/{id}/revisions, GET /lines/{id}/annotations, POST /lines/{id}/annotations
# geometry: PUT /lines/{id}/geometry; POST /surfaces/{id}/lines; DELETE /lines/{id}
# exports: GET /projects/{id}/surfaces/{sid}/diplomatic | tei | alto | hocr
#          GET /projects/{id}/training-export | bundle (zip with manifest) | lines.csv
# AI: GET /providers, POST /proposals, POST /proposals/{id}/accept, bulk preview/accept
# models: GET /models (packages under LeviathanText:Models:Directory), POST /surfaces/{id}/recognize
# jobs: POST /jobs (idempotency key; layout.detect / layout.project / recognize.surface / recognize.project / import.dir),
#       GET /jobs (recent, ?status=), GET /jobs/{id}, GET /jobs/{id}/events (SSE with cursor replay)

# Shared deployment (opt-in): set LeviathanText:Auth:ApiKey (Bearer or X-Api-Key required
# under /api/v1); LeviathanText:Auth:ReadOnly=true blocks mutations. Without a key the API
# is local-only (no accounts/roles/audit) and must not be exposed.
```

## Web workspace

```bash
cd web && npx ng serve --proxy-config proxy.conf.json   # http://localhost:4200
cd web && npx ng test --watch=false --browsers=ChromeHeadless
```

The workspace includes the line editor (polytonic input, revisions, uncertainty/gap annotations with retraction, draft recovery), a dual-pane L1/L2 diff, a geometry/reading-order overlay with manual placement for text-first imports, double-review/adjudication, search, jobs, exports, an API-key panel, and an opt-in AI assistance panel.

## Status and operations

- [CHANGELOG.md](CHANGELOG.md) — what changed in the current work block.

- [docs/STATUS.md](docs/STATUS.md) — one-page status: what works, measured numbers, blockers.
- [docs/architecture.md](docs/architecture.md) — module map, data flow, storage, contracts.
- [docs/operations.md](docs/operations.md) — run/backup/restore/batch/auth operations, review and inscription imports.
- [docs/release/parity.md](docs/release/parity.md) — honest reference-capability matrix (not a parity claim).
- [docs/release/security.md](docs/release/security.md) — trust model, controls and known security gaps.
- [docs/handbook/transcription-guide.md](docs/handbook/transcription-guide.md) — annotator workflow.

## Measured pilot baselines (honest status)

See [docs/evaluation/pilot-p01-report.md](docs/evaluation/pilot-p01-report.md). Pilot-scale only — single documentary hand (papyri) and single-codex cross-witness probe (manuscripts); labels not yet expert-adjudicated; inscriptions have no labels yet (construction work pending). **These are baselines, not thresholds or pass/fail results.** The combined initial Greek scope is not complete until all three material gates pass.

## Documentation

- [project-plan.md](project-plan.md) — plan narrative, requirements, source register, decisions, gates and estimates
- `scripts/install-local.sh` — single-user local install with CLI/API/worker wrappers
- [todo.md](todo.md) — actionable checklist (open/in-progress items)
- [docs/contracts/](docs/contracts/) — the versioned contract set (transcription, domain/pipeline, manifest, interchange/API, examples)
- [docs/data/](docs/data/) — per-track source cards, license registers, and acquisition/audit records
- [docs/evaluation/acceptance-plan.md](docs/evaluation/acceptance-plan.md) — evaluation design and acceptance-sheet template
- [docs/handoffs/](docs/handoffs/) — instance handoffs
