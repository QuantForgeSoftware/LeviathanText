# Known issues and limits (pilot)

Date: 2026-10-19. Everything here is a recorded limitation, not a hidden one.

## Data and claims

- Labels are **not specialist-adjudicated**; measured CER is pilot-scale and cannot support an
  acceptance claim. The P01 locked split is **spent** (including automatic suite reruns that are
  now prevented by the opt-in guard) — new claims need fresh held-out witnesses.
- M02a is one codex; M01 adds a second book but no hand-level holdout. Inscription labels are
  still blocked on image-aligned adjudication, though the EpiDoc text-first bootstrap and manual
  box placement now exist.
- Out-of-profile labels (PAGE markup leakage) exist in imported corpora; use
  `train lines --profile-strict` and `validate text` before training.

## Recognition

- CPU-only; the same seed is **not bit-reproducible** across runs (multi-seed measurements in
  `evaluation/end-to-end-page-cer.md` show the spread, and layout CNN training uses the same
  backend).
- Layout matching is one-to-one greedy by IoU; earlier reports (before 2026-10-19) used a
  non-one-to-one matcher and are superseded by the regenerated records.
- L0 revisions persist the **mean** raw confidence (with scale); per-grapheme confidence stays
  in memory, so review-queue ordering is not exactly reproducible later.
- Retried recognition jobs are idempotent per run id (job id) and do not duplicate L0 sets.
- Recognition decodes full frames; **no tiled region decode** for very large images.

## Layout

- Single-column layouts only; multi-column, curved and rotated pages are unsupported.
  Projection remains the default (combined pilot P 0.403 / R 0.473 under one-to-one matching).
  A patch/row CNN is **opt-in per surface kind**; it is strong on the training book (M02a
  P 0.965/R 0.987) and on a fresh minuscule book (M01 layout P 0.994/R 0.991), but degrades on
  papyri and multi-column material; a P01-trained variant improves aggregate P01 metrics yet
  fails on some dense pages and is not enabled.
- The learned row model underperforms projection (negative result recorded).
- Detection never overwrites existing layout; manual line add/delete is available. Geometry
  and order edits are recorded in a surface-level append-only audit (before/after snapshots),
  not as revisioned entities; editor undo and backups remain the recovery path.

## Imaging

- TIFF is unsupported by the current SkiaSharp build (clean `RejectedArchive`). PDF page
  rasterization is implemented (PDFtoImage/PDFium; bounded DPI/page count/size) but no PDF text
  extraction or layout inference is claimed. ABBYY XML remains unimplemented.
- Very large PNGs are resampled after a full decode (memory bounded by the 12000 px limit, not
  by tiling).

## Jobs and operations

- Cancellation is cooperative **between** batch items, not mid-image.
- Job leases/heartbeats exist; there is no scheduler, priority queue or per-tenant quota.
- SQLite single-writer store; the worker runs one job at a time per process.

## Security

- Shared deployment has **local accounts with roles** (reader/annotator/reviewer/admin), hashed
  keys, rotation/disable and a read-only mode. There are no refresh tokens or SSO. Opt-in
  per-project membership and an admin member panel exist; invitation tokens and presence are not
  implemented. Basic auth audit
  entries exist (mutations/denials). The workspace has an API-key panel
  (localStorage; X-Api-Key + SSE query token); local-only mode remains the default.
- Provider credentials are read from environment variables; hosted provider calls are unproven.
- PDF rendering uses native PDFium against files readable by the server process; size/page limits
  are enforced, but PDFium itself is not sandboxed. Treat PDFs as untrusted input. PDF text is
  imported as **L2 reference**, never as visible-text/L1 labels; line grouping does not infer
  columns or handle rotation.

## Interchange

- PAGE and hOCR carry per-region/line direction and script metadata (PAGE `readingDirection`/`primaryScript`, hOCR `dir`/`ocr_vertical`); native JSON preserves it and TEI uses a bounded `@rend` mapping. ALTO does not yet map direction, and recognition remains LTR.
- ALTO/hOCR are bounded subsets (line boxes + line text; no word segmentation synthesis).
- TEI/EpiDoc is a bounded profile; unsupported markup is reported, not mapped.
- Plain-text and CSV exports are lossy by design; the native project JSON is authoritative.

## Contract status

See [`docs/contracts/self-review-2026-10-19.md`](../contracts/self-review-2026-10-19.md) for the
conformance matrix and the actions that should precede a contract bump.
