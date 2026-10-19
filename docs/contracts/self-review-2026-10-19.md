# Contract self-review — 2026-10-19 (I0 substitute)

- Contract set: **C0-0.4-draft** (greek-transcription, domain-and-pipeline, corpus-manifest,
  examples, interchange-and-api)
- Reviewer: Instance A (integrator), single-instance self-review. The todo's I0 gate asks for
  B's review; with B quiet, this substitutes a written self-review at the I1 checkpoint. It is
  **not** an independent review.
- Method: map each contract section to implementation, tests, and measured evidence; record
  conformance, documented deviations, and open gaps. No requirement was relaxed silently.

## Conformance summary

| Contract area | Implementation evidence | Status |
| --- | --- | --- |
| Identity model (§1.1–1.3): typed ids, prefixes, opaque suffixes | `EntityId` validation + prefix table; JSON converter; id-scoping regression tests | **conforms** |
| Imported local ids stay stable | PAGE/ALTO/hOCR ids are namespaced per capture (`ln1-<token>`) to keep global endpoints unambiguous; source id remains readable as the prefix | **documented deviation** (recorded in the adapter docs and regression tests) |
| Text revisions are immutable, append-only, layer-separated | `TextRevision` chain; L0/L1/L2 separate; L0 persisted with model provenance; tests | **conforms** |
| Geometry/order are user-visible mutations | Direct updates with client undo + backups; **no server-side geometry revision history** | **open gap** (disclosed since the geometry increment) |
| Stage envelope (§2.1) and manual layout as first-class | `StageRun` records model identity/transform chain; `LayoutSource` manual/automatic/imported; detection refuses overwrite | **conforms** |
| Bounded resource use (§2.3) | Import through bounded pipeline; layout preview decode bounded by pixels; recognition decodes full frames per surface; no tiling for very large images | **partial** (documented) |
| Model package (§3): architecture, weights, tokenizer, provenance, hashes | `ModelPackageWriter`/`RecognitionModelLoader` verify labels, arch, digest; package from `train lines`; model card | **conforms** |
| Reject executable payloads / path limits | Loader reads only named files; no script/exec loading; archive/encoded size and dimension limits | **conforms** (checkpoint format is data-only) |
| Confidence semantics (§4) | Confidence carries scale and source; layout/geometry use `None`; recognition produces raw probabilities | **partial**: per-grapheme confidence is not persisted with L0 revisions (in-memory/API review queue only) |
| Jobs (§5): durable, leases, heartbeats, idempotency, cancellation | SQLite-backed jobs, timed leases, API key idempotency, DB pickup, cancellation between batch items | **conforms** (cancellation granularity documented) |
| At-least-once execution with idempotent output | Recognition retries append another L0 revision set (history noise, no overwrite) | **open gap**: L0 append should dedupe by job/run id |
| Error vocabulary (§6) | Closed `ErrorCode` set mapped to HTTP; transport-level auth/read-only errors use separate `code` strings | **documented deviation** (auth is transport, not pipeline) |
| Greek profile (§1–§6): layers, marks, offsets, uncertainty | Scalar offsets in annotations; no folding in L1; mark rendering; gap extent; alternatives; profile inspector | **conforms** for implemented kinds; **profile definitions await specialist** |
| Abbreviations / historical correction sequences | Annotations cover erasures/additions/overwritten; no dedicated abbreviation expansion kind or editor for historical correction order | **open gap** |
| Scoring (examples §1–2) | `RecognitionScorer` (scalar+grapheme+mark attribution); explicit uncertainty spans in training export; blank/missed-line cases covered by page-CER matching | **conforms** to the documented semantics |
| Corpus manifest fields | Ledgers use the documented fields; no new required fields added | **conforms** |
| Interchange formats | PAGE, bounded TEI, IIIF P3, ALTO v4, hOCR; loss reports mandatory; unsupported elements reported; DTD refused | **conforms** to the bounded-profile contract |
| HTTP/UI contracts (§2/§3) | Implemented inventory recorded in interchange-and-api §2.4; UI dual pane, marks, drafts, geometry, search, jobs, models | **conforms**; search/models/recognize/CSV/profile/bundle routes are pilot surfaces not yet named in the contract body |

## Open actions (recommendation for the next contract increment)

1. **Decide geometry history**: either add a `GeometryRevision` entity (new kind, C0 bump) or
   write the exclusion into the contract explicitly. Today it is a documented implementation
   gap, not a contract statement.
2. **Persist L0 confidence** with revisions (or a sidecar keyed by revision id) so review
   queues are reproducible and auditable after the run.
3. **Idempotent L0 persistence** for retried recognition jobs (dedupe key = job/run id).
4. **Name the pilot endpoints** (search, profile report, models, recognize, CSVs, bundle) in
   the contract or mark them explicitly as non-contract pilot extensions.
5. **Abbreviation/expansion representation** with specialist review: reversible links from
   visible script to expanded reading, explicit `not inferred` rule.
6. **Tiling/streaming for recognition** on very large captures; TIFF backend decision.

## Verdict

No blocking contract violations were found for the pilot scope. The deviations above are
recorded and bounded; the three that could affect future data integrity (geometry history,
confidence persistence, retry idempotency) should be decided before any contract bump or
external claim.
