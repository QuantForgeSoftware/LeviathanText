# Interchange and API contract

Contract: **C0-0.4-draft** (proposal, not accepted). Issued 2026-10-18 by Instance A (A-PLAN). Status: **draft, open for review**. Revision history: 0.2 (initial) → 0.3 (native JSON serialization conventions §2.0 added; conventions implemented in `LeviathanText.Contracts.Serialization.ContractJson`). Depends on the transcription contract (greek-transcription.md) and domain-and-pipeline.md.

Source requirements: project-plan Section 3 (scholarly interchange, bounded TEI/EpiDoc profile), Section 4 (web workspace, AI integration), Section 6 (FMT/UI/AI package inputs), Section 7 (formats, AI, UI verification). This document is **prose and tables only**; it defines no OpenAPI file, no generated client, no schemas.

## 1. Supported formats and bounded mappings

### 1.1 Format inventory

| Format | Direction | Scope for the Greek release | Everything else |
| --- | --- | --- | --- |
| Native JSON | both | Full-fidelity project interchange | — |
| Diplomatic plain text (UTF-8) | export (import: labeled plain text) | L1 text, linearized in reading order | No geometry, layers, or uncertainty; loss report mandatory |
| TEI P5 / EpiDoc (bounded profile) | both | See §1.2; bounded `rend="direction:…;script:…"` orientation mapping | Unsupported markup → attachment + report |
| PAGE XML (supported subset) | both | Regions/lines, polygons, baselines, text, optional `readingDirection`/`primaryScript` | Unsupported elements → report |
| IIIF Presentation 3.0 | import (bounded) | Canvas/image references into workspace | No IIIF hosting; caching rules in §1.3 |
| ALTO (v4 subset) / hOCR (XHTML subset) | both (pilot) | Line boxes + line text; polygons/bbox and baselines; hOCR direction (`dir`, `ocr_vertical`); unsupported elements → report; no word segmentation is synthesized | ABBYY XML and PDF remain unsupported (recorded if offered input); ALTO direction is not mapped |

### 1.2 TEI/EpiDoc bounded profile

Pinned versions are an implementation-time decision; the contract requires pinning + fixture-based round trips before adapters ship (project-plan §3).

| Scholarly distinction | Native representation | EpiDoc/TEI mapping | Round-trip rule |
| --- | --- | --- | --- |
| Surface / image link | Surface entity + capture reference | `facsimile` → `surface xml:id`/`graphic url,n,width,height` | Round-trips; missing link appears in the export loss report |
| Line | Line entity | `lb n=` inside edition div, incl. `break="no"` and nested `textpart` | Round-trips incl. `@n` and `@xml:id` |
| Diplomatic text | L1 revision | Visible `lem`/edition text; supplied and expansion text excluded | Round-trips as visible text |
| Regularized reading | L2 revision + `alternative-reading` | `reg`/`orig` (or `sic`/`corr`) inside `choice` | Both readings round-trip |
| Abbreviation + expansion | L1 span + L2 link (`ExpansionRevisionId`/`ExpansionRange`) | `expan`/`abbr` (or `choice`) | Both halves round-trip |
| Unclear / illegible | `unclear` / `illegible` annotation | `<unclear reason=…>` | Round-trips as annotation; literal underdots never enter L1 |
| Gap (lost text) | `lost` + extent class | `gap` with `extent`/`reason` | Known/estimated/unknown extent is explicit; `unit` is not representable and is reported |
| Supplied text | L2 `supplied` | `supplied` (and `supplied reason`) | Never imported into L1; annotation anchored to L2 |
| Addition / deletion (witness) | L3 correction annotations | `add`/`del` (+ `addSpan`/`delSpan`) | Mapped; sequence preserved only where source orders it |
| Different hand | `different-hand` annotation | `handShift`/`@new`/`@hand` | Unattributed hands round-trip as unattributed |
| Alternative readings | `alternative-reading` set | `app`/`rdg` | All readings preserved; no silent lemma choice |
| Responsibility / provenance | Document metadata + revision metadata | `respStmt`/`change`/`idno`/`licence` | Document metadata round-trips; automatic revision-author mapping remains partial |

Unsupported/unknown elements: preserved in an **original-markup attachment**, reported by name/count in the import report, never silently flattened (project-plan §3). Import reports list every mapping decision that discarded information. Attachments are bounded (currently 2 MiB) and checksummed; larger sources are parsed but not retained, and the caller must keep the original file. Implementation evidence: [interchange-v2](../evaluation/interchange-v2.md).

### 1.3 Loss reporting (mandatory, all exports)

Every lossy export carries a machine-checkable loss report: which distinctions were dropped or flattened, per category (geometry, uncertainty, alternatives, editorial layers, provenance), with counts. "Explains any information lost in plain text or OCR-only formats" is a hard rule (project-plan §3). Export without a loss report for a lossy format is a contract violation.

### 1.4 IIIF import (bounded)

- Import preserves: source/canvas identifiers, attribution, license/rights statement text, and available views (multiple images of one surface are separate captures — never coordinate-merged without registration, project-plan §3).
- Cached projects must remain usable offline; the cache records what was fetched and when.

## 2. HTTP API behavior (prose contract)

Versioned REST under a versioned prefix (e.g. `/api/v1/…`). JSON bodies only; UTF-8; all text fields are Unicode scalar text per greek-transcription.md §4. Resource shape follows the domain model (objects, surfaces, captures, regions, lines, revisions, annotations, jobs, models, exports).

### 2.0 Native JSON serialization conventions (implemented in `LeviathanText.Contracts.Serialization.ContractJson`)

These conventions apply to every contract record on the wire:

| Value kind | JSON form |
| --- | --- |
| Property names | camelCase |
| Enums | camelCase strings (`"revisionConflict"`, `"l1Diplomatic"`, `"notYetTranscribed"`); unknown strings and integer values are hard errors when read (schema-strict) |
| `EntityId` | its string form (`"ln-line17"`) |
| `ScalarRange` | `{"start":n,"end":m}` (half-open, scalar offsets) |
| `Confidence` | `"none"` or `{"value":x,"scale":"…"}` — absent confidence is `"none"`, never `0` |
| `Checksum` | `{"algorithm":"sha256","digest":"…"}` |
| Numbers | strict (no quoted numbers, no NaN/Infinity) |
| Records with dictionary payloads (e.g. `LeviathanError.Details`) | compare by content, not reference (round-trip equality holds) |


### 2.1 Core resource behaviors

| Area | Contract |
| --- | --- |
| Revisions | Reads may address any revision; writes target a parent revision and fail `revision-conflict` on stale parents (domain-and-pipeline.md §6). With `merge: true`, a stale parent is resolved by a bounded three-way merge; overlapping edits still fail with `conflictStart`/`conflictEnd`/`localText`/`remoteText`. |
| Project saves | A loaded project carries a store version; a stale save fails `revision-conflict` instead of overwriting a newer document. |
| Sessions | Server-issued, store-backed coordination records (`POST /sessions`, heartbeat, revoke, list). They bind a user to a node/surface and expire unless heartbeat; they are not authentication tokens or locks. |
| Provenance crosswalk | Read-only CSV row per surface: native object/surface/capture ids, external refs, image hash, line count and line source ids. It never renumbers or merges entities. |
| Annotations | Created/updated against a named revision; scalar offsets in, scalar offsets out; server-side UTF-16 conversion endpoints are pure functions and versioned. |
| Geometry | Always bound to a coordinate frame; cross-frame requests without registration fail `frame-unregistered`. |
| Exports | Synchronous for bounded sizes; otherwise a job. Response includes export version, profile version, and (when lossy) the loss report inline or by reference. |
| IIIF import | A job; result lists imported canvases, captured attribution/rights, and anything skipped. |

### 2.2 Jobs and events

| Concern | Contract |
| --- | --- |
| Job creation | Returns job ID + status URI. Inputs validated up front; unknown model/profile → immediate rejection with the §6 vocabulary codes. |
| Progress | Server-sent events first (project-plan §4): `job.progress` (stage, percent/bounded counters), `job.stage-change`, `job.completed`, `job.failed` (with error object), `heartbeat`. Events are advisory; the job resource is authoritative. |
| Ordering/resume | Event replay from a cursor; missed events degrade to polling the job resource. |
| Idempotency | Job creation accepts a client idempotency key; duplicate creation returns the existing job (domain-and-pipeline.md §5). |
| Cancellation | `cancel` on the job resource; event `job.cancelled`. Cooperative per §5 semantics. |

### 2.3 AI proposals over HTTP

| Field | Contract |
| --- | --- |
| Proposal creation | Client names: target spans (revision + scalar offsets), task type (`candidate-reading`, `word-division`, `expansion`, `accentuation`, `restoration` — visually separate tasks, project-plan §4), provider/model config reference, prompt version. |
| Provider passthrough | Provider identity, model, prompt/config version recorded on every proposal. Credentials are server-side only; never accepted from or returned to the browser. |
| Proposal record | Proposed text + span links + task + provenance + confidence semantics (§4 of domain contract) + acceptance status (`proposed`/`accepted`/`rejected`/`expired`). |
| Acceptance | Accepting a proposal creates an L2 revision with provenance; L1 is untouched. Bulk acceptance requires a dry-run preview first (project-plan §4). |
| Malformed responses | Provider responses failing schema validation become failed proposal attempts with the validation reason recorded; they never surface partial text as a proposal. |

### 2.4 Implemented endpoint inventory (pilot)

This is a summary of the implemented surface, not a new contract. Route names are stable
within the pilot; shapes follow §2.0 and errors use the closed vocabulary.

| Area | Routes |
| --- | --- |
| Projects | `GET/POST /projects`, `GET/DELETE /projects/{id}`, `GET /projects/{id}/stats` |
| Ingestion | `POST /projects/{id}/import-page`, `…/import-iiif`, `…/import-image`, `…/import-legacy` |
| Layout | `POST /surfaces/{id}/detect-layout`, `PUT /surfaces/{id}/reading-order`, `PUT /regions/{id}/line-order`, `POST /surfaces/{id}/lines`, `DELETE /lines/{id}`, `PUT /lines/{id}/geometry` |
| Transcription | `GET/POST /lines/{id}/revisions`, `GET /lines/{id}` (L1 chain plus all-layer `revisions`), `GET/POST /lines/{id}/annotations` |
| Orientation | `PUT /lines/{id}/orientation`, `PUT /regions/{id}/orientation` (direction/script; null = inherit/undeclared) |
| Exports | `GET /projects/{id}/surfaces/{sid}/{diplomatic,tei,alto,hocr}`, `GET /projects/{id}/{training-export,bundle,lines.csv,annotations.csv}` |
| Search/QA | `GET /projects/{id}/search?q=&mode=literal|base`, `GET /projects/{id}/profile-report` |
| Models | `GET /models` (packages under a configured directory), `POST /surfaces/{id}/recognize` (name only, verified package) |
| Jobs | `POST /jobs`, `GET /jobs[?status=]`, `GET /jobs/{id}`, `GET /jobs/{id}/events` (SSE), `POST /jobs/{id}/cancel` |
| AI | `GET /providers`, `POST /proposals`, `POST /proposals/{id}/accept`, bulk preview/accept |
| Auth (opt-in) | API key via `Authorization: Bearer`, `X-Api-Key`, or `?api_key=` (SSE); `Auth:ReadOnly` blocks mutations |

## 3. UI-facing behavior contracts (prose)

| Concern | Contract |
| --- | --- |
| Client generation | The TypeScript client is generated from the versioned HTTP contract during implementation (project-plan §4); this document is its prose source. |
| Text editing | Grapheme-aware selection/editing; editing operates on grapheme boundaries, scalar offsets under the hood. |
| Layers | L0/L1/L2/L3/L4 views are separately addressable; raw-vs-proposed comparisons are first-class views. |
| Undo/revision history | Undo acts through revision creation (new revision superseding), never by deleting history. |
| Rendering checks | Combining-mark rendering checks and the polytonic input palette are required workspace features (project-plan §4). |
| Coordinate fidelity | Overlay accuracy across zoom/rotation is a verification item (project-plan §7); the API must return geometry in stated frames sufficient to support this. |
| Offline | Cached projects usable offline; network calls are explicit user-configurable behaviors (project-plan §2). |

## 4. Open questions for reviewers

1. B/EVAL: does the acceptance plan need export/round-trip fixtures enumerated per material (e.g. an EpiDoc fixture with `gap extent=unknown`), and should those fixture *names* be fixed at contract level now?
2. AI (future): should proposal tasks have a closed or open vocabulary? (Proposed closed, with `other:<text>` escape hatch.)
3. UI (future): is server-sent-events-only progress acceptable through Phase 2, or should the contract reserve WebSocket upgrade semantics now? (Proposed: SSE only, revisit at Phase 3 shared workspaces.)
