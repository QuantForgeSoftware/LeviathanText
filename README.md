<!-- Copyright (c) 2026 QuantForgeSoftware. All rights reserved. -->
# LeviathanText

An independent Greek recognition and scholarly transcription platform — focused first on **Koine and Classical Greek manuscripts, papyri, and inscriptions**. C# backend, CLI, and training; TypeScript/Angular browser workspace. No Kraken, no Python, no forks: interoperability is defined through documented public file formats (PAGE XML, TEI P5/EpiDoc, IIIF) only.

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
dotnet run --project src/LeviathanText.Cli -- --store pilot.db export diplomatic <projectId> <surfaceId>   # loss report on stderr
dotnet run --project src/LeviathanText.Cli -- --store pilot.db export tei <projectId> <surfaceId>
dotnet run --project src/LeviathanText.Cli -- --store pilot.db recognize surface <projectId> <surfaceId> --model <dir> --name <model>
```

## HTTP API

```bash
dotnet run --project src/LeviathanText.Api
# GET  /api/v1/projects, POST /api/v1/projects
# POST /api/v1/projects/{id}/import-page, GET .../diplomatic, GET .../tei
# POST /api/v1/jobs (idempotency key), GET /api/v1/jobs/{id}/events (SSE with cursor replay)
```

## Web workspace

```bash
cd web && npx ng serve --proxy-config proxy.conf.json   # http://localhost:4200
cd web && npx ng test --watch=false --browsers=ChromeHeadless
```

## Measured pilot baselines (honest status)

See [docs/evaluation/pilot-p01-report.md](docs/evaluation/pilot-p01-report.md). Pilot-scale only — single documentary hand (papyri) and single-codex cross-witness probe (manuscripts); labels not yet expert-adjudicated; inscriptions have no labels yet (construction work pending). **These are baselines, not thresholds or pass/fail results.** The combined initial Greek scope is not complete until all three material gates pass.

## Documentation

- [todo.md](todo.md) — the full implementation plan with execution records and acceptance gates
- [docs/contracts/](docs/contracts/) — the versioned contract set (transcription, domain/pipeline, manifest, interchange/API, examples)
- [docs/data/](docs/data/) — per-track source cards, license registers, and acquisition/audit records
- [docs/evaluation/acceptance-plan.md](docs/evaluation/acceptance-plan.md) — evaluation design and acceptance-sheet template
- [docs/handoffs/](docs/handoffs/) — instance handoffs
