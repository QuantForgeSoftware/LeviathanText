# Known issues and limits (pilot)

Date: 2026-10-19. Everything here is a recorded limitation, not a hidden one.

## Data and claims

- Labels are **not specialist-adjudicated**; measured CER is pilot-scale and cannot support an
  acceptance claim. The P01 locked split is **spent** — new claims need fresh held-out witnesses.
- M02a is one codex; M01 adds a second book but no hand-level holdout. Inscription labels are
  blocked on image-aligned adjudication.
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
  A patch/row CNN is **opt-in per surface kind** and is strong on the training book (M02a
  held-out P 0.965 / R 0.987) but degrades out-of-domain; a P01-trained variant improves
  aggregate P01 metrics yet fails on some dense pages and is not enabled.
- The learned row model underperforms projection (negative result recorded).
- Detection never overwrites existing layout; manual line add/delete is available. Geometry
  and order edits are recorded in a surface-level append-only audit (before/after snapshots),
  not as revisioned entities; editor undo and backups remain the recovery path.

## Imaging

- TIFF is unsupported by the current SkiaSharp build (clean `RejectedArchive`); PDF/ABBYY are
  not implemented.
- Very large PNGs are resampled after a full decode (memory bounded by the 12000 px limit, not
  by tiling).

## Jobs and operations

- Cancellation is cooperative **between** batch items, not mid-image.
- Job leases/heartbeats exist; there is no scheduler, priority queue or per-tenant quota.
- SQLite single-writer store; the worker runs one job at a time per process.

## Security

- The shared-deployment guard is a single API key plus read-only mode. **No accounts, roles,
  per-user audit or key rotation.** The workspace UI does not prompt for a key.
- Provider credentials are read from environment variables; hosted provider calls are unproven.

## Interchange

- ALTO/hOCR are bounded subsets (line boxes + line text; no word segmentation synthesis).
- TEI/EpiDoc is a bounded profile; unsupported markup is reported, not mapped.
- Plain-text and CSV exports are lossy by design; the native project JSON is authoritative.

## Contract status

See [`docs/contracts/self-review-2026-10-19.md`](../contracts/self-review-2026-10-19.md) for the
conformance matrix and the actions that should precede a contract bump.
