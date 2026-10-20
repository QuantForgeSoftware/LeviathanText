# Domain and pipeline contract

Contract: **C0-0.4-draft** (proposal, not accepted). Issued 2026-10-18 by Instance A (A-PLAN). Status: **draft, open for review**. Revision history: 0.2 (initial) → 0.3 (StageRun input/output lists use `StageArtifact` records; JSON conventions §2.0 added) → 0.4 (Proposal entity kind added to the identity hierarchy, B-CCR-7). Depends on the transcription contract (greek-transcription.md) and corpus-manifest.md, which it extends rather than repeats.

Source requirements: todo Section 4 (architecture, pipeline and domain model, storage and jobs), Section 6 (C0 ownership list, stage contracts, versioning), Section 7 (training/reload parity, idempotency evidence). All names here are **design placeholders** pending implementation assignment; nothing in this document authorizes code.

## 1. Identity model

### 1.1 Entity hierarchy

```
Collection
  └─ PhysicalObject            (a manuscript volume, papyrus, inscribed object)
       └─ Surface              (a leaf/recto or verso, papyrus side, inscribed face)
            └─ Fragment        (physically separate piece sharing a surface record when useful)
            └─ Capture         (one imaging event of a surface/fragment)
                 └─ Derivative (crop, deskew, dewarp, tile — traceable to a capture)
            └─ Region          (layout unit on a surface, in one coordinate frame)
                 └─ Line       (text line, with optional baseline/polygon)
                      └─ GraphemeSpan  (character-level annotation anchor)
            └─ TranscriptionRevision  (per layer L1/L2; see greek-transcription.md)
                 └─ Annotation (anchored to a revision by scalar offsets)
            └─ Proposal      (AI assistance record; accepted → L2 revision, provenance kept)
```

### 1.2 Identifier rules

| Rule | Contract |
| --- | --- |
| Primary keys | Opaque stable IDs minted by the project (e.g. `obj-…`, `surf-…`, `ln-…`). Never derived from mutable source data. |
| External IDs | TM numbers, inventory numbers, shelfmarks, ISicily IDs, IIIF canvas IDs are recorded as **references** on the entity, never used as primary keys. |
| Surface typing | A book page is one surface type, not the mandatory shape (todo §4). `surface_kind` ∈ `codex-page`, `papyrus-side`, `inscription-face`, `other:<text>`. |
| Fragments | A fragment belongs to a surface; its geometry may be in the fragment's own capture frame. Joining fragments is a recorded relationship, never a silent merge. |
| Capture identity | Every capture records: device/imaging profile where known, original file checksum, capture date where known, rights evidence reference (manifest link). |
| Coordinate frames | Every geometry-bearing entity names its **frame**: which capture or derivative it is expressed against. Geometry never transfers between frames without an explicit registration record; unregistered multi-capture geometry stays frame-local (todo §4). |
| Derivative lineage | Each derivative stores its full parent chain and the transform(s) applied, including non-invertible operations (crop bounds, dewarp control points/binarization parameters). Information loss is recorded, not assumed away. |

### 1.3 Immutability and revisions

- Original captures are immutable. Every mutation visible to a user is a new **revision**.
- A `TranscriptionRevision` is per text layer (L0 raw, L1 diplomatic, L2 editorial) and per scope (surface/line). Each revision stores: parent revision, author (human/model ID + model identity/version for proposals), timestamp, and the full text it defines (or a delta — implementation choice, but the *contract* is: any revision can be materialized exactly).
- Annotations store `[scalar_start, scalar_end)` against a **named revision** (todo §3; see greek-transcription.md §4). Stale annotations reference their originating revision and are never silently re-anchored.
- Accepted AI proposals are revisions of L2 with provenance; raw L0 output is preserved alongside regardless of acceptance (todo §2).
- Geometry and reading-order corrections are recorded in an append-only surface-level `GeometryEdits` audit (kind, author, timestamp, bounded before/after snapshots). These are not text revisions; promoting them to revisioned entities is a candidate for a future contract increment.
- Review decisions (`Accept`/`Correct`/`Dispute`) are an append-only surface ledger anchored to the reviewed revision. A correction creates a new L1 revision; the decision records both ids. Disagreement between reviewers is exposed for adjudication and is never resolved automatically.
- Annotation retractions are append-only records; active views (editor, training export, loss reports) exclude retracted ids while the annotation itself remains in the ledger as evidence of the correction.

## 2. Stage pipeline contracts

Pipeline (todo §4): ingest → prepare derivatives → layout → reading order → line/region extraction → recognition → alignment → optional proposals → review/accept → export.

### 2.1 Common stage envelope

Every stage declares, in its contract record:

| Field | Semantics |
| --- | --- |
| `stage_id`, `contract_version` | Which declared I/O contract this run used. |
| `inputs` | Entity IDs + input artifact checksums + coordinate frames consumed. |
| `outputs` | Artifact references (checksum-addressed) + produced entity IDs. |
| `model_ref` | Model package ID + version + input/output contract hash, if a model was used; `manual` otherwise. |
| `config_hash` | Hash of stage configuration. |
| `status` | `pending` \| `running` \| `succeeded` \| `failed:<reason-code>` \| `invalidated`. |
| `resource_use` | Wall time, peak memory; optional, for acceptance reporting. |

### 2.2 Stage-by-stage I/O

| Stage | Consumes | Produces | Contract notes |
| --- | --- | --- | --- |
| Ingest | Files + manifest source record | Capture/derivative entities, checksums | Rejects or flags unsupported formats explicitly; no silent coercion. |
| Prepare | Capture | Derivatives with lineage + transform records | Original remains retrievable (acceptance invariant). |
| Layout | Derivative (+ manual/imported geometry adapter input) | Regions (polygons), optional baselines | Manual layout is a first-class input path, flagged `layout_source: manual \| imported \| automatic` — never indistinguishable from automatic (todo §7). |
| Reading order | Regions | Ordered, validated graph + serializable order view | Cycle detection mandatory; uncertain/alternative orders preserved; human corrections recorded. |
| Extraction | Regions/baselines + derivative | Line images + extraction records | Records crop bounds; preserves marks at boundaries (diacritics, overbars, interlinear). Failures produce explicit `extraction-failed` statuses, not empty lines. |
| Recognition | Line images + model ref | L0 text, per-grapheme confidence, alignment where supported | Model I/O contract in §3. |
| Alignment | L0 + existing L1 | Alignment links (grapheme/span level) | Aligns to the **diplomatic** layer only; editorial text alignment is out of scope for this contract. |
| Proposals (AI) | Selected spans + provider config | Proposal records (see interchange-and-api.md) | Never mutates L1; proposals are separate records until accepted. |
| Export | Selected layer(s) + profile version | Export artifact + loss report | See interchange-and-api.md. |

### 2.3 Checkpointing, invalidation, resources

- Stage outputs are content-addressed; a stage run is **valid** iff its inputs' checksums and `model_ref`/`config_hash` still match. Any input change **invalidates dependent stages transitively**; invalidation is recorded, and re-run is explicit, never implicit mid-collection.
- Resume: a pipeline run can resume from the latest valid checkpoint per item; completed outputs are not recomputed.
- Streaming: page/tile streaming with bounded queues is the contract for collection-scale runs; no stage may assume whole-collection memory residency.

## 3. Model I/O and metadata contract

| Field | Contract |
| --- | --- |
| Model package identity | Architecture + version, tokenizer/label set (+ hash), preprocessing recipe (+ version), runtime requirements, provenance, measured metrics, checksums, license metadata (todo §5 native model package). |
| Input contract | Exact expected image preprocessing (size policy, color, normalization), line-image orientation, and expected coordinate frame. |
| Output contract | Recognized label sequence in profile label space, per-label or per-grapheme confidence, and (where architecture supports) alignment to input time-steps. Output text is **L0**; conversion to Unicode scalar text is part of the recorded decoding recipe. |
| Label policy | Any normalization between profile text and training labels is recorded per model version (greek-transcription.md §6). Unknown/out-of-label-set signs are surfaced as `unknown-label`, never dropped silently. |
| Provenance on use | Every recognition stage run records `model_ref`, so any L0 output can be traced to a model version + config hash. |
| Security | Model archives are non-executable payloads with enforced size/path limits and compatibility checks (todo §5); the contract records rejection reasons (`rejected-archive`, `rejected-path`, `rejected-version`). |

## 4. Confidence semantics

- Confidence is always reported with its **scale and source**: raw model probability, calibrated score, or annotator uncertainty — three different things, never conflated (todo §7).
- Model confidence is **not** scholarly certainty; UI/export surfaces must keep the distinction visible.
- Confidence applies to: graphemes/labels (recognition), detections (layout), alignment links. Absent confidence (e.g. manual layout) is explicit `none`, not a fabricated value.
- Unknown geometry (e.g. a line with no baseline) is represented explicitly (`geometry: absent` with reason), never as a zero/empty polygon (todo §4).

## 5. Jobs: durability, idempotency, cancellation

Assume at-least-once execution (todo §4):

| Concern | Contract |
| --- | --- |
| Job record | ID, type, inputs, owner, lease holder, heartbeat, progress, retries used, status, resource limits. |
| Leases | A worker holds a timed lease; expiry releases the job for retry. Heartbeats renew; expired lease = worker presumed dead. |
| Idempotency | Output publication is idempotent: re-running a completed job's publication must not duplicate accepted revisions or overwrite finalized exports. Finalization of artifacts is atomic (complete-then-visible, or staged-then-promoted). |
| Restart safety | A restarted worker resumes from stage checkpoints; it never re-accepts an already-accepted proposal or re-exports a finalized artifact as a new one. |
| Cancellation | Cooperative, checked at stage boundaries; a cancelled job leaves valid checkpoints, status `cancelled`, no partial artifacts visible. |
| Retries | Bounded, with backoff, recorded; retry of a *failed* stage is a new attempt on the same job record. |
| Failure codes | Shared, closed vocabulary of machine-readable reasons (e.g. `input-missing`, `checksum-mismatch`, `model-incompatible`, `out-of-memory`, `cancelled`, `stage-contract-violation`). |

## 6. Error and version-conflict vocabulary

Shared error shape (for API/SDK; see interchange-and-api.md for HTTP mapping):

| Field | Meaning |
| --- | --- |
| `code` | From the closed vocabulary above plus domain codes (`revision-conflict`, `stale-annotation-anchor`, `frame-unregistered`, `profile-version-unsupported`, `schema-version-unsupported`, `lossy-export-required-report`). |
| `scope` | Entity/job/stage the error binds to. |
| `details` | Structured, bounded diagnostic data (no secrets, no unbounded blobs). |
| `remedy_hint` | Machine-readable suggestion where one exists (`re-run-stage`, `rebase-revision`, `register-captures`), or none. |

Version-conflict rules:

- Writing a revision based on a stale parent revision → `revision-conflict` with the current revision named; the writer rebases deliberately.
- Annotation anchored to a revision that is no longer current → operations that would implicitly re-anchor fail with `stale-annotation-anchor`; re-anchoring is an explicit, recorded act.
- Mixed contract versions across stages → `stage-contract-violation`; the integrator resolves version bumps, per todo §6 (a proposed contract change is a handoff, not permission to alter another worker's files).

## 7. Open questions for reviewers

1. B/EVAL: is per-grapheme confidence sufficient at this contract level, or do acceptance plans need word-level confidence semantics reserved now?
2. APP (future): should job records support job hierarchies (batch → per-item) at contract level, or is a flat job + stage checkpoints sufficient for Phase 1?
3. IMG (future): does layout need to emit per-region `layout_source` plus confidence even when manual, to keep §4 semantics uniform? (Proposed: yes, with confidence `none`.)
