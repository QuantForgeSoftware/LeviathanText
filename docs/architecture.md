# Architecture overview (pilot)

A C# backend (ASP.NET Core + CLI + worker) with an Angular workspace, local-first SQLite
persistence, and no Python/Kraken runtime. Contracts are the boundary; the native project JSON
is the record of record.

## Projects and dependencies

| Project | Role | Depends on |
| --- | --- | --- |
| `LeviathanText.Contracts` | entity ids, revisions, annotations, errors, serialization conventions | — |
| `LeviathanText.Core` | Unicode/scalar helpers, Greek text profile | Contracts |
| `LeviathanText.Formats` | native project JSON, PAGE/TEI/EpiDoc/ALTO/hOCR/IIIF, loss reports | Contracts/Core |
| `LeviathanText.Imaging` | decode limits, crops, transforms, line extraction | Contracts/Core |
| `LeviathanText.Layout` | projection detector, learned patch/row CNN, comparison | Contracts/Core/Imaging |
| `LeviathanText.Recognition` | CRNN+CTC, labels, decoding, recognizer | Contracts/Core |
| `LeviathanText.Training` | CTC training, checkpoints, packages, manifests, datasets | Contracts/Core/Recognition/Imaging |
| `LeviathanText.Evaluation` | CER/grapheme scoring, correction effort | Contracts/Core |
| `LeviathanText.AI` | provider protocol/registry, proposals | Contracts/Core |
| `LeviathanText.Application` | project/transcription/geometry/review/search/export services, pipeline, bundle | above (no Infrastructure) |
| `LeviathanText.Infrastructure` | SQLite store, jobs, user keys | Contracts/Formats/Application? (store only) |
| `LeviathanText.Api` | versioned REST, SSE, auth middleware, model/layout registry | Application/Infrastructure |
| `LeviathanText.Worker` | durable job handlers (layout/import/recognition) | Application/Infrastructure |
| `LeviathanText.Cli` | local batch workflows | Application/Infrastructure/Training |
| `web/` | Angular workspace | REST contract |

## Data flow

1. **Ingest** — PAGE/TEI/EpiDoc/ALTO/hOCR/IIIF → native project (regions/lines/captures,
   L1 import or L2 reference for editions). Unsupported markup is reported, not flattened.
2. **Layout** — imported geometry or automatic detection (projection default; learned model
   opt-in per material). Manual corrections produce a surface-level geometry audit.
3. **Extraction/recognition** — line crops with padding → CRNN+CTC → L0 revisions with mean and
   per-grapheme confidence; never merged into L1.
4. **Review** — human L1 revisions, L2 editorial readings/proposals, annotations, retractions,
   double-review decisions and adjudication.
5. **Export** — diplomatic text + loss report, TEI/ALTO/hOCR, training JSON, CSVs, bundle with
   manifest; the bundle includes the native JSON as authoritative.

## Storage

SQLite tables: `projects` (native JSON + contract version), `jobs` (durable state/lease/
idempotency), `users` (hashed keys/roles), `auth_audit` (mutations/denials). Schema version is
enforced (`user_version`); newer stores are refused, older stores migrate in place. Binary
artifacts (images/crops/checkpoints) live on disk or a configured cache with checksums.

## Contracts and versioning

Contract set **C0-0.4-draft** governs ids, revisions, stages, errors, scoring and formats.
Native JSON is version-checked; model packages carry their own architecture/labels/digests;
layout packages carry a data digest and model card. Additive-but-unversioned fields are called
out in the contract self-review.

## Deployment topology (pilot)

- Local: CLI only, or API + worker + Angular on localhost; no auth required.
- Shared: API + worker against one SQLite store, account keys/roles, optional read-only mode.
  SQLite is single-writer: run one worker process; no horizontal scaling.
- Jobs are durable and lease-based; cancellation is cooperative between batch items.
