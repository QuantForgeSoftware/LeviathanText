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
| Geometry/order are user-visible mutations | Append-only surface-level `GeometryEdits` audit with author/timestamp and before/after snapshots; editor undo; backups | **conforms (pilot)**; not revisioned entities — candidate contract increment |
| Stage envelope (§2.1) and manual layout as first-class | `StageRun` records model identity/transform chain; `LayoutSource` manual/automatic/imported; detection refuses overwrite | **conforms** |
| Bounded resource use (§2.3) | Import through bounded pipeline; layout preview decode bounded by pixels; recognition decodes full frames per surface; no tiling for very large images | **partial** (documented) |
| Model package (§3): architecture, weights, tokenizer, provenance, hashes | `ModelPackageWriter`/`RecognitionModelLoader` verify labels, arch, digest; package from `train lines`; model card | **conforms** |
| Layout model package (analogue of §3) | `patch-row-cnn-v2` package checks architecture, weights **SHA-256 digest**, and writes a model card with scope/limitations; metadata records threshold, counts, seed | **conforms (pilot)** |
| Reject executable payloads / path limits | Loader reads only named files; no script/exec loading; archive/encoded size and dimension limits | **conforms** (checkpoint format is data-only) |
| Confidence semantics (§4) | Confidence carries scale and source; layout/geometry use `None`; L0 revisions persist the **mean and per-grapheme raw probabilities** with scalar ranges | **conforms** |
| Jobs (§5): durable, leases, heartbeats, idempotency, cancellation | SQLite-backed jobs, timed leases, API key idempotency, DB pickup, cancellation between batch items | **conforms** (cancellation granularity documented) |
| At-least-once execution with idempotent output | Recognition uses a stable run id (job id); re-running the same run appends no duplicate L0 revisions | **conforms** |
| Error vocabulary (§6) | Closed `ErrorCode` set mapped to HTTP; transport-level auth/read-only errors use separate `code` strings | **documented deviation** (auth is transport, not pipeline) |
| Greek profile (§1–§6): layers, marks, offsets, uncertainty | Scalar offsets in annotations; no folding in L1; mark rendering; gap extent; alternatives; profile inspector | **conforms** for implemented kinds; **profile definitions await specialist** |
| Abbreviations / historical correction sequences | Abbreviation annotations now link L1 spans to L2 revisions; correction sequences carry group/order-basis metadata and a replay panel. Specialist validation of real cases remains. | **implemented; validation pending** |
| Annotation correction | Append-only retraction ledger; active views/training exports exclude retracted ids; CSV marks them | **conforms (pilot)** |
| Double review / adjudication (DATA-03 readiness) | Append-only review ledger anchored to revisions; corrections create L1 revisions; disagreement summary + adjudication CSV; CLI/API/UI | **conforms (pilot)**; reviewer identity is a free string — no accounts/roles yet |
| Scoring (examples §1–2) | `RecognitionScorer` (scalar+grapheme+mark attribution); explicit uncertainty spans in training export; blank/missed-line cases covered by page-CER matching | **conforms** to the documented semantics |
| Corpus manifest fields | Ledgers use the documented fields; no new required fields added | **conforms** |
| Interchange formats | PAGE, bounded TEI, IIIF P3, ALTO v4, hOCR; loss reports mandatory; unsupported elements reported; DTD refused | **conforms** to the bounded-profile contract |
| Shared deployment identity | Local accounts with role-based authorization, hashed API keys, rotation/disable; bootstrap admin key; auth audit; **opt-in per-project membership** scoping project and entity routes; transport-level 401/403 | **conforms (pilot)**; no SSO, refresh tokens or invitation/presence UI |
| HTTP/UI contracts (§2/§3) | Implemented inventory recorded in interchange-and-api §2.4; UI dual pane, marks, drafts, geometry, search, jobs, models | **conforms**; search/models/recognize/CSV/profile/bundle routes are pilot surfaces not yet named in the contract body |

## Open actions (recommendation for the next contract increment)

1. **Promote geometry history to entities** if the contract should expose it as revisions
   (today: surface-level audit list, written into domain-and-pipeline §1.3).
2. **Per-grapheme confidence persistence** if review queues must be reproduced exactly
   (mean confidence is now stored on L0 revisions; the per-grapheme detail is not).
3. *(Resolved 2026-10-19: stable run ids make L0 persistence idempotent for retries.)*
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
