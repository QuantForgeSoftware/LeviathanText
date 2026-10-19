# Interchange and API contract

Contract: **C0-0.4-draft** (proposal, not accepted). Issued 2026-10-18 by Instance A (A-PLAN). Status: **draft, open for review**. Revision history: 0.2 (initial) → 0.3 (native JSON serialization conventions §2.0 added; conventions implemented in `LeviathanText.Contracts.Serialization.ContractJson`). Depends on the transcription contract (greek-transcription.md) and domain-and-pipeline.md.

Source requirements: todo Section 3 (scholarly interchange, bounded TEI/EpiDoc profile), Section 4 (web workspace, AI integration), Section 6 (FMT/UI/AI package inputs), Section 7 (formats, AI, UI verification). This document is **prose and tables only**; it defines no OpenAPI file, no generated client, no schemas.

## 1. Supported formats and bounded mappings

### 1.1 Format inventory

| Format | Direction | Scope for the Greek release | Everything else |
| --- | --- | --- | --- |
| Native JSON | both | Full-fidelity project interchange | — |
| Diplomatic plain text (UTF-8) | export (import: labeled plain text) | L1 text, linearized in reading order | No geometry, layers, or uncertainty; loss report mandatory |
| TEI P5 / EpiDoc (bounded profile) | both | See §1.2 | Unsupported markup → attachment + report |
| PAGE XML (supported subset) | both | Regions/lines, polygons, baselines, text | Unsupported elements → report |
| IIIF Presentation 3.0 | import (bounded) | Canvas/image references into workspace | No IIIF hosting; caching rules in §1.3 |
| ALTO / hOCR / ABBYY XML / PDF | — | **Not in the Greek release** (later parity work, todo §3) | Recorded as unsupported if offered input |

### 1.2 TEI/EpiDoc bounded profile

Pinned versions are an implementation-time decision; the contract requires pinning + fixture-based round trips before adapters ship (todo §3).

| Scholarly distinction | Native representation | EpiDoc/TEI mapping | Round-trip rule |
| --- | --- | --- | --- |
| Surface / image link | Surface entity + capture reference | facsimile → graphic/pb | Must round-trip; loss of image link is an error |
| Line | Line entity | `lb n=` inside edition div | Must round-trip incl. line numbers |
| Diplomatic text | L1 revision | `lem`/editoric text within `app` or plain edition text | Must round-trip as visible text |
| Regularized reading | L2 revision, linked | `reg`/`orig` pair (or `choice`) | Expansion links must round-trip or be reported lost |
| Abbreviation + expansion | L1 span + L2 link | `expan`/`abbr` (or `choice`) | Both halves must round-trip |
| Unclear | `unclear` annotation | `<unclear reason=…>` | Round-trips as annotation; literal underdots never enter L1 |
| Gap (lost text) | `lost` + extent class | `gap` with `extent`/`reason`/`unit` | Known/estimated/unknown extent mapping is explicit |
| Supplied text | L2 `supplied` | `supplied` (and `supplied reason`) | Never imported into L1 |
| Addition / deletion (witness) | L3 correction annotations | `add`/`del` (+ `addSpan`/`delSpan`) | Sequence preserved only where source orders it |
| Different hand | `different-hand` annotation | `handShift`/`@hand` | Unattributed hands round-trip as unattributed |
| Alternative readings | `alternative-reading` set | `app`/`rdg` | All readings preserved; no silent lemma choice |
| Responsibility / provenance | Revision metadata | `respStmt`/change log | Round-trips into revision provenance where mappable |

Unsupported/unknown elements: preserved in an **original-markup attachment**, reported by name/count in the import report, never silently flattened (todo §3). Import reports list every mapping decision that discarded information.

### 1.3 Loss reporting (mandatory, all exports)

Every lossy export carries a machine-checkable loss report: which distinctions were dropped or flattened, per category (geometry, uncertainty, alternatives, editorial layers, provenance), with counts. "Explains any information lost in plain text or OCR-only formats" is a hard rule (todo §3). Export without a loss report for a lossy format is a contract violation.

### 1.4 IIIF import (bounded)

- Import preserves: source/canvas identifiers, attribution, license/rights statement text, and available views (multiple images of one surface are separate captures — never coordinate-merged without registration, todo §3).
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
| Revisions | Reads may address any revision; writes target a parent revision and fail `revision-conflict` on stale parents (domain-and-pipeline.md §6). |
| Annotations | Created/updated against a named revision; scalar offsets in, scalar offsets out; server-side UTF-16 conversion endpoints are pure functions and versioned. |
| Geometry | Always bound to a coordinate frame; cross-frame requests without registration fail `frame-unregistered`. |
| Exports | Synchronous for bounded sizes; otherwise a job. Response includes export version, profile version, and (when lossy) the loss report inline or by reference. |
| IIIF import | A job; result lists imported canvases, captured attribution/rights, and anything skipped. |

### 2.2 Jobs and events

| Concern | Contract |
| --- | --- |
| Job creation | Returns job ID + status URI. Inputs validated up front; unknown model/profile → immediate rejection with the §6 vocabulary codes. |
| Progress | Server-sent events first (todo §4): `job.progress` (stage, percent/bounded counters), `job.stage-change`, `job.completed`, `job.failed` (with error object), `heartbeat`. Events are advisory; the job resource is authoritative. |
| Ordering/resume | Event replay from a cursor; missed events degrade to polling the job resource. |
| Idempotency | Job creation accepts a client idempotency key; duplicate creation returns the existing job (domain-and-pipeline.md §5). |
| Cancellation | `cancel` on the job resource; event `job.cancelled`. Cooperative per §5 semantics. |

### 2.3 AI proposals over HTTP

| Field | Contract |
| --- | --- |
| Proposal creation | Client names: target spans (revision + scalar offsets), task type (`candidate-reading`, `word-division`, `expansion`, `accentuation`, `restoration` — visually separate tasks, todo §4), provider/model config reference, prompt version. |
| Provider passthrough | Provider identity, model, prompt/config version recorded on every proposal. Credentials are server-side only; never accepted from or returned to the browser. |
| Proposal record | Proposed text + span links + task + provenance + confidence semantics (§4 of domain contract) + acceptance status (`proposed`/`accepted`/`rejected`/`expired`). |
| Acceptance | Accepting a proposal creates an L2 revision with provenance; L1 is untouched. Bulk acceptance requires a dry-run preview first (todo §4). |
| Malformed responses | Provider responses failing schema validation become failed proposal attempts with the validation reason recorded; they never surface partial text as a proposal. |

## 3. UI-facing behavior contracts (prose)

| Concern | Contract |
| --- | --- |
| Client generation | The TypeScript client is generated from the versioned HTTP contract during implementation (todo §4); this document is its prose source. |
| Text editing | Grapheme-aware selection/editing; editing operates on grapheme boundaries, scalar offsets under the hood. |
| Layers | L0/L1/L2/L3/L4 views are separately addressable; raw-vs-proposed comparisons are first-class views. |
| Undo/revision history | Undo acts through revision creation (new revision superseding), never by deleting history. |
| Rendering checks | Combining-mark rendering checks and the polytonic input palette are required workspace features (todo §4). |
| Coordinate fidelity | Overlay accuracy across zoom/rotation is a verification item (todo §7); the API must return geometry in stated frames sufficient to support this. |
| Offline | Cached projects usable offline; network calls are explicit user-configurable behaviors (todo §2). |

## 4. Open questions for reviewers

1. B/EVAL: does the acceptance plan need export/round-trip fixtures enumerated per material (e.g. an EpiDoc fixture with `gap extent=unknown`), and should those fixture *names* be fixed at contract level now?
2. AI (future): should proposal tasks have a closed or open vocabulary? (Proposed closed, with `other:<text>` escape hatch.)
3. UI (future): is server-sent-events-only progress acceptable through Phase 2, or should the contract reserve WebSocket upgrade semantics now? (Proposed: SSE only, revisit at Phase 3 shared workspaces.)
